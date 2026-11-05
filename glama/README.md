# Glama listing image

[Glama](https://glama.ai/mcp/servers) grades an MCP server by building a Docker image, starting it as a **stdio** MCP
server in its sandbox and calling `initialize` + `tools/list`. GameDev-MCP-Server alone lists no Godot tools: they come
from the Godot editor once the addon connects to the server. This folder builds an image that runs the whole stack in one
container, so Glama sees the real editor tool set on its first `tools/list`.

Nothing here ships with the addon (`.gitattributes` export-ignores it) and nothing here is used by CI or releases.

## What runs in the container

| File | Role |
| --- | --- |
| `install.sh` | Build step: .NET 8 SDK, Godot 4.5.1 .NET (mono), `godot-cli` from npm, and a sample C# project set up the way users do it (`create-project --dotnet`, `install-plugin --with-server`, `build`). It ends by probing the result like Glama. |
| `entrypoint.sh` | The stdio server (Glama's CMD). It runs GameDev-MCP-Server in stdio mode next to the headless editor (Custom mode), and holds stdin back until `godot-cli wait-for-ready` sees the editor. |
| `probe.mjs` | Probes a stdio command the way Glama does, optionally with tool calls. |
| `Dockerfile` | Local reproduction of the image Glama generates from its admin form (below). It is not given to Glama. |

Each script's header has the details. stdout carries only JSON-RPC; every log goes to stderr.

The editor registers **14 tools**: `rql-*` (3), `screenshot-*` (3) and `script-*` (8), each with a title and annotations.
The README's "24 built-in tools" also counts the `console-*`, `reflection-*` and `runtime-errors-*` families (they serve a
running game, so the editor never registers them) and the system tools (`ping`, `godot-skill-*`, `session-close`), which
are not part of `tools/list`.

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
| Build | about 2 min (the install step takes about 65 s, including the probe) |
| Image size | 2.32 GB: mostly the .NET SDK (`script-build` compiles at run time), the NuGet packages the editor loads the addon from, Godot and Node.js (Glama's image also carries Python) |
| First full `tools/list` | 10–12 s after `docker run` (the editor connects about 9 s after start). Through mcp-proxy, `initialize` answers at 8.7–9.8 s and `tools/list` right after. |
| Shutdown | within 1 s of stdin closing or `docker stop`; a server that fails to start ends the container at once |
| Offline | works with `--network none`; the network is only needed at build time |

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
   The log should show `[probe] first tools/list after …: 14 tools`.
4. Click **Make Release** and publish it. A GitHub release does not count as a Glama release.

Optional run-time overrides, set through the env schema: `GLAMA_READY_TIMEOUT` (seconds, default 50, below mcp-proxy's
60 s connection timeout) and `GLAMA_MCP_PORT` (fixed plugin port).
`install.sh` also reads `GODOT_VERSION` (default `4.5.1`) and `GODOT_CLI_VERSION` (default: `cli/package.json`, or `latest` if unpublished), but Glama's
form has no build-time environment, so changing them means editing the defaults in `install.sh`.

## Sandbox risks

- **Build network.** The build downloads from dot.net, github.com (Godot, addon and server releases), npmjs.org and
  nuget.org.
- **Size and build time.** If Glama caps either, Deploy fails.
- **CPU architecture.** x86_64 and arm64 are supported; any other architecture fails the build with a clear message.
- **/dev/shm and GPU.** Not needed: the editor runs `--headless`, with no renderer or display.
- **One session at a time.** Each session (each copy of `entrypoint.sh`) gets its own server port, but all sessions
  share the one sample project, so concurrent sessions would run two editors on it.
- **Slow sandbox.** If the editor needs more than `GLAMA_READY_TIMEOUT` seconds, the server still answers but has no
  Godot tools. The stderr log then says `did not connect`.
