#!/usr/bin/env bash
# Build step for the Glama listing image (see glama/README.md).
#
# Installs, under $GLAMA_PREFIX (default /opt/godot-mcp-glama):
#   - the .NET 8 SDK (Godot 4 C# needs it to build the project and hosts its runtime in the editor),
#   - a headless-capable Godot 4.x .NET (mono) editor from the official GitHub release,
#   - godot-cli from npm,
#   - a sample C# Godot project with the godot_mcp addon and the server it pins installed
#     (godot-cli create-project / install-plugin --with-server / build).
# Then imports and builds the project, and probes the result exactly like Glama (glama/probe.mjs), so a broken
# image fails the build instead of Glama's grading run.
#
# Runs as root on Debian (Glama's generated image is debian:trixie-slim with Node.js preinstalled).
set -euo pipefail

PREFIX="${GLAMA_PREFIX:-/opt/godot-mcp-glama}"
GODOT_VERSION="${GODOT_VERSION:-4.5.1}"
# godot-cli (and, through install-plugin's default, the addon) at the version of this checkout, the one Glama grades;
# `latest` when that version is not on npm yet (a bump that is not released).
GODOT_CLI_VERSION="${GODOT_CLI_VERSION:-$(node -p 'JSON.parse(require("fs").readFileSync(process.argv[1])).version' "$(dirname "$0")/../cli/package.json")}"
PROJECT="$PREFIX/project"

log() { echo "[glama-install] $*" >&2; }

case "$(uname -m)" in
  x86_64 | amd64) GODOT_ARCH=x86_64 ;;
  aarch64 | arm64) GODOT_ARCH=arm64 ;;
  *) log "unsupported CPU architecture: $(uname -m)"; exit 1 ;;
esac

log "system packages"
export DEBIAN_FRONTEND=noninteractive
apt-get update
ICU_PKG="$(apt-cache search --names-only '^libicu[0-9]+$' | awk '{print $1}' | sort -V | tail -n1)"
apt-get install -y --no-install-recommends ca-certificates curl unzip libfontconfig1 ${ICU_PKG}
rm -rf /var/lib/apt/lists/*

mkdir -p "$PREFIX"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

log ".NET 8 SDK"
curl -fsSL https://dot.net/v1/dotnet-install.sh -o "$TMP/dotnet-install.sh"
bash "$TMP/dotnet-install.sh" --channel 8.0 --install-dir /usr/share/dotnet >&2
ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet --version >&2

log "Godot ${GODOT_VERSION} mono (${GODOT_ARCH})"
GODOT_NAME="Godot_v${GODOT_VERSION}-stable_mono_linux_${GODOT_ARCH}"
curl -fsSL -o "$TMP/godot.zip" \
  "https://github.com/godotengine/godot/releases/download/${GODOT_VERSION}-stable/${GODOT_NAME}.zip"
unzip -q "$TMP/godot.zip" -d "$TMP/godot"
mv "$TMP/godot/${GODOT_NAME}" "$PREFIX/godot"
# The binary is Godot_v<ver>-stable_mono_linux.<arch>; GodotSharp/ must stay beside it.
GODOT_BIN="$(find "$PREFIX/godot" -maxdepth 1 -type f -name 'Godot_v*' | head -n1)"
chmod +x "$GODOT_BIN"
ln -sf "$GODOT_BIN" /usr/local/bin/godot

npm view "godot-cli@${GODOT_CLI_VERSION}" version >/dev/null 2>&1 || GODOT_CLI_VERSION=latest
log "godot-cli@${GODOT_CLI_VERSION}"
npm install -g --no-fund --no-audit "godot-cli@${GODOT_CLI_VERSION}" >&2

log "sample project at ${PROJECT}"
godot-cli create-project --dotnet --path "$PROJECT" --name GodotMcpDemo >&2
# Build with the Godot.NET.Sdk of the running editor: a 4.3 build on a newer editor hits changed APIs.
sed -i -E "s#Sdk=\"Godot\.NET\.Sdk/[^\"]+\"#Sdk=\"Godot.NET.Sdk/${GODOT_VERSION}\"#" "$PROJECT"/*.csproj
godot-cli install-plugin --path "$PROJECT" --with-server >&2 # + the server the addon pins, SHA256-verified
# A stable path for entrypoint.sh: the CLI extracts the release zip under .ai-game-dev/server/ (with its <rid>/ folder).
SERVER_BIN="$(find "$PROJECT/.ai-game-dev/server" -type f -name gamedev-mcp-server | head -n1)"
[ -n "$SERVER_BIN" ] || { log "install-plugin --with-server installed no gamedev-mcp-server"; exit 1; }
ln -sfn "$(dirname "$SERVER_BIN")" "$PREFIX/server"

# Three separate processes: a cold single-process `--build-solutions --quit` crashes on C# [Tool] plugins.
log "import + build"
timeout 300 godot --headless --path "$PROJECT" --import --quit >&2 || log "import pass exited non-zero (non-gating)"
godot-cli build --path "$PROJECT" >&2

log "cleanup (the editor loads the addon's NuGet packages from ~/.nuget/packages at run time: they stay)"
dotnet nuget locals http-cache --clear >&2
dotnet nuget locals temp --clear >&2
npm cache clean --force 2>/dev/null

# The exact check Glama runs: the first tools/list over stdio must carry the editor's tools.
log "runtime probe"
node "$(dirname "$0")/probe.mjs" -- bash "$(dirname "$0")/entrypoint.sh"

log "done"
