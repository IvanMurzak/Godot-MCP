#!/usr/bin/env bash
# Build step for the Glama listing image (see glama/README.md).
#
# Installs, under $GLAMA_PREFIX (default /opt/godot-mcp-glama):
#   - the .NET 8 SDK (Godot 4 C# needs it to build the project and hosts its runtime in the editor),
#   - a headless-capable Godot 4.x .NET (mono) editor from the official GitHub release,
#   - godot-cli from npm,
#   - a sample C# Godot project with the godot_mcp addon installed (godot-cli create-project / install-plugin / build),
#   - the GameDev-MCP-Server release binary the installed addon pins (SHA256-verified).
# Then imports and builds the project once, so the container only has to boot the editor.
#
# Runs as root on Debian (Glama's generated image is debian:trixie-slim with Node.js preinstalled).
set -euo pipefail

PREFIX="${GLAMA_PREFIX:-/opt/godot-mcp-glama}"
GODOT_VERSION="${GODOT_VERSION:-4.5.1}"
GODOT_CLI_VERSION="${GODOT_CLI_VERSION:-latest}"
PROJECT="$PREFIX/project"

log() { echo "[glama-install] $*" >&2; }

case "$(uname -m)" in
  x86_64 | amd64) GODOT_ARCH=x86_64; SERVER_RID=linux-x64 ;;
  aarch64 | arm64) GODOT_ARCH=arm64; SERVER_RID=linux-arm64 ;;
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
rm -rf "$PREFIX/godot"
unzip -q "$TMP/godot.zip" -d "$TMP/godot"
mv "$TMP/godot/${GODOT_NAME}" "$PREFIX/godot"
# The binary is Godot_v<ver>-stable_mono_linux.<arch>; GodotSharp/ must stay beside it.
GODOT_BIN="$(find "$PREFIX/godot" -maxdepth 1 -type f -name 'Godot_v*' | head -n1)"
chmod +x "$GODOT_BIN"
ln -sf "$GODOT_BIN" /usr/local/bin/godot

log "godot-cli@${GODOT_CLI_VERSION}"
npm install -g --no-fund --no-audit "godot-cli@${GODOT_CLI_VERSION}" >&2

log "sample project at ${PROJECT}"
rm -rf "$PROJECT"
godot-cli create-project --dotnet --path "$PROJECT" --name GodotMcpDemo >&2
# Build with the Godot.NET.Sdk of the running editor: a 4.3 build on a newer editor hits changed APIs.
sed -i -E "s#Sdk=\"Godot\.NET\.Sdk/[^\"]+\"#Sdk=\"Godot.NET.Sdk/${GODOT_VERSION}\"#" "$PROJECT"/*.csproj
godot-cli install-plugin --path "$PROJECT" >&2

SERVER_VERSION="$(sed -nE 's/.*const string ServerVersion = "([^"]+)".*/\1/p' \
  "$PROJECT/addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs" | head -n1)"
[ -n "$SERVER_VERSION" ] || { log "could not read ServerVersion from the installed addon"; exit 1; }
log "GameDev-MCP-Server ${SERVER_VERSION} (${SERVER_RID})"
SERVER_URL="https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v${SERVER_VERSION}"
SERVER_ZIP="gamedev-mcp-server-${SERVER_RID}.zip"
curl -fsSL -o "$TMP/$SERVER_ZIP" "$SERVER_URL/$SERVER_ZIP"
curl -fsSL -o "$TMP/SHA256SUMS" "$SERVER_URL/SHA256SUMS"
(cd "$TMP" && grep " \*\?${SERVER_ZIP}\$" SHA256SUMS | sha256sum -c -) >&2
rm -rf "$PREFIX/server"
unzip -q "$TMP/$SERVER_ZIP" -d "$TMP/server"
mv "$TMP/server/${SERVER_RID}" "$PREFIX/server"
chmod +x "$PREFIX/server/gamedev-mcp-server"

# Three separate processes: a cold single-process `--build-solutions --quit` crashes on C# [Tool] plugins.
log "import + build + editor smoke"
timeout 300 godot --headless --path "$PROJECT" --import --quit >&2 || log "import pass exited non-zero (non-gating)"
godot-cli build --path "$PROJECT" >&2
# --no-connect equivalent: Custom mode at a closed port, so the build never reaches the cloud.
SMOKE_LOG="$TMP/editor-smoke.log"
GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST=http://127.0.0.1:9 \
  timeout 300 godot --headless --editor --path "$PROJECT" --quit >"$SMOKE_LOG" 2>&1 || true
cat "$SMOKE_LOG" >&2
grep -q "\[Godot-MCP\] plugin loaded" "$SMOKE_LOG" || { log "the godot_mcp addon did not load"; exit 1; }

log "done"
