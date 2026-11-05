# Glama listing image

[Glama](https://glama.ai/mcp/servers) grades an MCP server by building a Docker image, starting it as a **stdio** MCP
server in its sandbox and calling `initialize` + `tools/list`. GameDev-MCP-Server alone lists no Godot tools: they come
from the Godot editor once the addon connects to the server. This folder builds an image that runs the whole stack in one
container, so Glama sees the real editor tool set on its first `tools/list`.

Nothing here ships with the addon (`.gitattributes` export-ignores it) and nothing here is used by CI or releases.

## What runs in the container

| File | Role |
| --- | --- |
| `install.sh` | Build step. Installs the .NET 8 SDK, Godot 4.5.1 .NET (mono) from the official GitHub release, and `godot-cli` from npm. Scaffolds a sample C# project the way users do (`godot-cli create-project --dotnet`, `install-plugin`, `build`), and downloads the GameDev-MCP-Server release that the installed addon pins (`ServerVersion`, SHA256-verified). It then imports, builds and boots the project once and fails the build if `[Godot-MCP] plugin loaded` is missing. |
| `entrypoint.sh` | The stdio server (Glama's CMD). Starts the server (`--client-transport streamableHttp --auth none`, loopback, free port) and the headless editor in Custom mode (`GODOT_MCP_CONNECTION_MODE=Custom`, `GODOT_MCP_HOST`). It waits until the editor's tools are listed, then bridges stdin/stdout to the server. When stdin closes or on SIGTERM/SIGINT, it kills the editor and the server. |
| `wait-for-tools.mjs` | Readiness gate: polls `initialize` + `tools/list` until the tool count is non-zero and stable. It gives up after `GLAMA_READY_TIMEOUT` seconds (default 50, below mcp-proxy's 60 s connection timeout) and the bridge starts anyway. |
| `stdio-bridge.mjs` | stdio ↔ streamable-HTTP bridge. Sends `initialize` first and holds every other message until the session id exists, so an eager client never races the session. It writes JSON or SSE replies as one JSON-RPC line each. |
| `Dockerfile` | Local reproduction of the image Glama generates from its admin form (see below). It is not given to Glama. |
| `probe.mjs` | Probes an image the way Glama does and optionally runs tool calls. |

stdout carries only JSON-RPC. The server, editor and scripts all log to stderr.

The editor registers **14 tools**: `rql-*` (3), `screenshot-*` (3) and `script-*` (8), each with a title and annotations.
The README's "24 built-in tools" also counts the `console-*`, `reflection-*` and `runtime-errors-*` families. Those serve
a running game and are never registered by the editor.

## Build, run, probe

From the repository root (x86_64 or arm64 Linux Docker; the scripts pick the matching Godot and server builds):

```bash
docker build -f glama/Dockerfile -t godot-mcp-glama .
node glama/probe.mjs rql-schema '{}' rql-query '{"query":"editor | select isPlaying editedScene selection"}' \
  -- docker run -i --rm godot-mcp-glama
# Through mcp-proxy, exactly as Glama runs it (HTTP on :8080, /mcp and /sse):
docker run --rm -p 8080:8080 godot-mcp-glama mcp-proxy -- bash glama/entrypoint.sh
```

The probe writes `initialize`, `notifications/initialized` and `tools/list` at once. It fails if the first
`tools/list` is empty, a tool has no title or annotations, a call errors, or stdout carries a non-JSON line.

Measured on a 4 vCPU / 7 GB x86_64 VPS (Docker 29):

| | |
| --- | --- |
| Build | 2 min 42 s cold, about 2 min with cached base layers (the install step takes about 70 s) |
| Image size | 2.40 GB (mostly the .NET SDK, the NuGet cache the editor loads the addon's packages from, Godot, Node.js and Python from Glama's base) |
| First full `tools/list` | 11.3 s after `docker run` (the editor connects about 9 s after start). Through mcp-proxy, `initialize` answers at 9.8 s and `tools/list` right after. |
| Shutdown | the container exits within 1 s of stdin closing |
| Offline | works with `--network none`: the network is only needed at build time |

Godot may print engine teardown errors and a segmentation fault when it is killed at shutdown. That is harmless: the
container still exits 0.

## Glama admin settings (owner)

Glama no longer accepts a free-form Dockerfile. Its admin page is a form that generates one: a `debian:trixie-slim`
base with Node.js, `mcp-proxy` and Python (uv), a `git clone` of this repository into `/app`, then `RUN` of the build
steps and `CMD ["mcp-proxy","--",<CMD arguments>]`. Glama builds from the GitHub repository, so this folder must be
merged into `main` first.

1. Make sure the root `glama.json` (`"maintainers": ["IvanMurzak"]`) is on `main`. Open
   <https://glama.ai/mcp/servers/IvanMurzak/Godot-MCP/score> signed in as GitHub `IvanMurzak` and **Claim** the
   server. If it is already claimed, run the claim flow again so Glama re-reads `glama.json`.
2. Open the server's **Admin → Dockerfile** page (`…/IvanMurzak/Godot-MCP/admin/dockerfile`) and fill in the form.
   JSON fields take a **single-line** JSON literal; pretty-printed JSON fails validation.

   | Field | Value |
   | --- | --- |
   | Base image / Node.js version / Python version | leave the defaults (locked on most listings) |
   | Build steps | `["bash glama/install.sh"]` |
   | CMD arguments | `["bash","glama/entrypoint.sh"]` |
   | Environment variables JSON schema | `{"type":"object","properties":{},"required":[]}` |
   | Placeholder parameters | `{}` |
   | Pinned commit SHA | leave empty or as Glama manages it (latest `main`) |

3. Click **Deploy** and wait for the build test to pass. The build log should end with `[glama-install] done`.
   The test log should show `[glama-wait] 14 tools listed`.
4. Click **Make Release** and publish it. A GitHub release does not count as a Glama release.

Optional overrides go in the env schema: `GLAMA_READY_TIMEOUT` (seconds) and `GLAMA_MCP_PORT` (fixed server port).
`install.sh` also reads `GODOT_VERSION` (default `4.5.1`) and `GODOT_CLI_VERSION` (default `latest`), but Glama's
form has no build-time environment, so changing them means editing the defaults in `install.sh`.

## Sandbox risks

- **Build network.** The build downloads from dot.net, github.com (Godot, addon and server releases), npmjs.org and
  nuget.org. At run time the image needs no network.
- **Image size and build time.** 2.4 GB and about 2 minutes on 4 vCPUs. A build-time cap in Glama would show up as a
  failed Deploy.
- **CPU architecture.** x86_64 and arm64 are both handled; any other architecture fails the build with a clear message.
- **/dev/shm and GPU.** Not needed: the editor runs `--headless`, with no renderer or display.
- **Slow sandbox.** If the editor needs more than 50 s to connect, the server still answers but lists no Godot tools. The
  stderr log then says `did not list tools`.
