#!/usr/bin/env bash
# stdio MCP server for the Glama listing image (see glama/README.md).
#
# Runs GameDev-MCP-Server in its stdio mode and the headless Godot .NET editor, which connects to the server's
# plugin port in Custom mode. The client's stdin reaches the server only after `godot-cli wait-for-ready` sees the
# editor answer a ping, so even a client that sends initialize + tools/list at once gets the editor's full tool set.
# When stdin closes the server exits and the editor is killed; SIGTERM/SIGINT stop both.
# stdout carries only the MCP JSON-RPC stream; every log goes to stderr.
set -uo pipefail

PREFIX="${GLAMA_PREFIX:-/opt/godot-mcp-glama}"
PROJECT="$PREFIX/project"
SERVER_DIR="$PREFIX/server" # linked by install.sh to the server `godot-cli install-plugin --with-server` installed
# Below mcp-proxy's 60 s --connectionTimeout (Glama wraps this script in mcp-proxy): a slow editor then still
# yields an answering server instead of a proxy timeout.
READY_TIMEOUT="${GLAMA_READY_TIMEOUT:-50}"

ulimit -c 0 # the editor may segfault while being killed at shutdown; never leave core files behind
exec 3>&1 1>&2 # fd 3 = the real stdout (MCP stream); everything else defaults to stderr
log() { echo "[glama-entrypoint] $*" >&2; }

# A free loopback port unless pinned: mcp-proxy may run several sessions (several copies of this script) at once.
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

# Holds the client's stdin back (it waits in the pipe) until the editor is connected, then forwards it.
gate() {
  local start=$SECONDS
  if godot-cli wait-for-ready --path "$PROJECT" --url "$URL" --timeout "$((READY_TIMEOUT * 1000))" --interval 500; then
    log "editor connected in $((SECONDS - start))s"
  else
    log "WARNING: the editor did not connect within ${READY_TIMEOUT}s; serving without its tools"
  fi
  exec cat
}

log "starting GameDev-MCP-Server (stdio, plugin port ${PORT}) and the headless Godot editor"
# Backgrounded (stdin passed explicitly) so `wait` stays interruptible by SIGTERM/SIGINT; $! is the server.
gate <&0 | (cd "$SERVER_DIR" && exec ./gamedev-mcp-server --port "$PORT" --client-transport stdio) >&3 &
SERVER=$!
PIDS+=("$SERVER")

GODOT_MCP_CONNECTION_MODE=Custom GODOT_MCP_HOST="$URL" GODOT_MCP_AUTH_OPTION=none \
  godot --headless --editor --path "$PROJECT" </dev/null &
PIDS+=("$!")

wait "$SERVER"
