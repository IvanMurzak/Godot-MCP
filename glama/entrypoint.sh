#!/usr/bin/env bash
# stdio MCP front for the Glama listing image (see glama/README.md).
#
# 1. starts GameDev-MCP-Server (streamable HTTP, no auth, loopback) in the background,
# 2. starts the headless Godot .NET editor on the sample project, connected to it in Custom mode,
# 3. waits until the server lists the editor's tools,
# 4. bridges this process's stdin/stdout to the server (stdio-bridge.mjs),
# and kills the editor and the server when the bridge exits (stdin closed) or on SIGTERM/SIGINT.
# stdout carries only the MCP JSON-RPC stream; every log goes to stderr.
set -uo pipefail

PREFIX="${GLAMA_PREFIX:-/opt/godot-mcp-glama}"
PROJECT="$PREFIX/project"
# Below mcp-proxy's 60 s --connectionTimeout (Glama wraps this script in mcp-proxy): a slow editor then still
# yields an answering server instead of a proxy timeout.
READY_TIMEOUT="${GLAMA_READY_TIMEOUT:-50}"

ulimit -c 0 # the editor may segfault while being killed at shutdown; never leave core files behind
exec 3>&1 1>&2 # fd 3 = the real stdout (MCP stream); everything else defaults to stderr
log() { echo "[glama-entrypoint] $*" >&2; }

# A free loopback port unless pinned: a second container instance (or a respawn) must not collide.
PORT="${GLAMA_MCP_PORT:-$(node -e 'const s=require("net").createServer();s.listen(0,"127.0.0.1",()=>{console.log(s.address().port);s.close()})')}"
URL="http://127.0.0.1:${PORT}"

PIDS=()
cleanup() {
  trap - EXIT INT TERM
  for pid in "${PIDS[@]}"; do kill "$pid" 2>/dev/null; done
  for pid in "${PIDS[@]}"; do
    for _ in $(seq 50); do kill -0 "$pid" 2>/dev/null || break; sleep 0.1; done
    kill -9 "$pid" 2>/dev/null
  done
}
trap cleanup EXIT
trap 'exit 143' INT TERM

log "starting GameDev-MCP-Server on ${URL}"
(cd "$PREFIX/server" && exec ./gamedev-mcp-server --port "$PORT" --client-transport streamableHttp --auth none) </dev/null &
PIDS+=("$!")

log "starting the headless Godot editor"
GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST="$URL" GODOT_MCP_AUTH_OPTION=none \
  godot --headless --editor --path "$PROJECT" </dev/null &
PIDS+=("$!")

START=$SECONDS
if node "$(dirname "$0")/wait-for-tools.mjs" "${URL}/mcp" "$READY_TIMEOUT"; then
  log "editor connected in $((SECONDS - START))s; bridging stdio"
else
  log "WARNING: the editor did not list tools within ${READY_TIMEOUT}s; serving what the server has"
fi

# Backgrounded (stdin passed explicitly) so `wait` stays interruptible by SIGTERM/SIGINT.
node "$(dirname "$0")/stdio-bridge.mjs" "${URL}/mcp" <&0 >&3 &
BRIDGE=$!
PIDS=("$BRIDGE" "${PIDS[@]}")
wait "$BRIDGE"
