# CLAUDE.md — Godot-MCP

Godot-MCP bridges LLMs (Claude, Cursor, Copilot, …) with the [Godot](https://godotengine.org/) editor
via the [Model Context Protocol](https://modelcontextprotocol.io/) — the Godot-engine sibling of Unity-MCP.
Sub-projects: `addons/godot_mcp/` (the Godot **editor addon**, a `Godot.NET.Sdk` C# project), `cli/` (the
`godot-cli` npm package — the Godot analog of `unity-mcp-cli`), and `Godot-MCP.Tests/` (the CI xUnit
suite). The MCP server is NOT in this repo — the addon consumes the shared
[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server). See **CLI (godot-cli)** and
**Server (shared GameDev-MCP-Server)** below.

The addon is the heart of the repo. A `[Tool]` `EditorPlugin` (`GodotMcpPlugin`) boots on editor load,
installs the main-thread dispatcher, builds a `Reflector` with Godot type converters, and connects to an
MCP server (cloud `ai-game.dev` by default, or a custom local server) over the reused
`com.IvanMurzak.McpPlugin` SignalR client. The MCP/reflection stack is **not forked** — it is consumed
from nuget.org as `PackageReference`s and the pins are owned by the upstream release pipelines (never bump
them here). The reused pins are frozen at `com.IvanMurzak.ReflectorNet` **5.3.1** and
`com.IvanMurzak.McpPlugin` **6.10.0** (in `Godot-MCP.csproj`, mirrored by `Godot-MCP.Tests/` and the infra
testbed) — keep all three in lockstep; never bump here. (6.10.0 carries the shared engine-agnostic
`com.IvanMurzak.McpPlugin.AgentConfig` module the addon consumes for AI-agent configurators — registry +
`AiAgentConfig` + `AgentConfiguratorDescription` — replacing the addon's retired local copy; it also carries
the `WaitForImmediateTeardown` API + opt-in bounded-reconnect this addon uses for the godot#78513 reload fix.)

## Tool families

Tools live in `addons/godot_mcp/Runtime/Tools/` (pure-managed families) and `addons/godot_mcp/Editor/Tools/` (editor-only families) — one `[AiToolType]` `partial class Tool_<Family>` per family,
with each tool method (`[AiTool("<name>", ...)]` + `[Description]`) in its own partial-class file. Tool
names mirror Unity-MCP where sensible. 39 built-in tools across 11 families — the 11 families:

| Family (class) | Tools | `#if TOOLS`? |
| --- | --- | --- |
| `Tool_Ping` | `ping` | no (pure-managed) |
| `Tool_Node` | `node-find`/`-create`/`-modify`/`-set-parent`/`-duplicate`/`-delete` | yes (editor) |
| `Tool_Scene` | `scene-open`/`-save`/`-create`/`-list-opened`/`-get-data` | yes (editor) |
| `Tool_Resource` | `resource-find`/`-get-data`/`-modify`/`-create`/`-move`/`-delete` | yes (editor) |
| `Tool_FileSystem` | `filesystem-list`/`-reimport` | yes (editor) |
| `Tool_Script` | `script-read`/`-create`/`-update`/`-delete`/`-attach-to-node`/`-validate` | yes (editor) |
| `Tool_Screenshot` | `screenshot-viewport`/`-camera`/`-isolated` | yes (editor) |
| `Tool_Editor` | `editor-application-get-state`/`-set-state`, `editor-selection-get`/`-set` | yes (editor) |
| `Tool_Console` | `console-get-logs`/`-clear-logs` | no (pure-managed collector) |
| `Tool_Reflection` | `reflection-method-find`/`-call` | no (engine-agnostic ReflectorNet) |
| `Tool_RuntimeErrors` | `runtime-errors-get`/`-clear` | no (pure-managed; in-game, OFF by default — see `WithRuntimeErrorCapture()`) |

Editor-driving families live behind `#if TOOLS` (they touch `EditorInterface`/live `Node`/`Resource`);
their pure-managed result models + helpers live OUTSIDE the guard so they are CI-unit-testable. The
ping/console/reflection families have no editor-API surface and stay fully outside `#if TOOLS`.

## Build / test

Three suites (mirrors the `godot-mcp` implement-task profile `test.md`):

```bash
# Suite 1 — build (CI gate; 0 errors required)
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore

# Suite 2 — unit tests (xUnit; 0 failures required, also runs in CI)
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build

# Suite 3 — headless live-editor smoke (operator/reviewer check, NOT a CI gate; see testbed runbook below)
```

`Godot.NET.Sdk` is a NuGet SDK, so **no Godot binary is needed for Suite 1 or 2**. The xUnit
project compiles a CI-friendly subset of the addon sources directly (it never `ProjectReference`s the
game project; the game `.csproj` excludes `Godot-MCP.Tests/**` so its xUnit files don't leak into the
game assembly). Only **pure-managed** Godot types are unit-testable here — Godot types that wrap a native
object (`NodePath`, `Node`, `Resource`, `SceneTree`) call `godotsharp_*` P/Invoke on construction and
crash the test host with `AccessViolationException` when no Godot native lib is loaded; verify those via
the headless Godot smoke (Suite 3) instead.

## CLI (godot-cli)

`cli/` publishes the **`godot-cli`** npm package — the Godot analog of `unity-mcp-cli`. It is a Node
(`^20.19.0 || >=22.12.0`) TypeScript CLI that resolves and launches the Godot editor with the right
`GODOT_MCP_*` connection env vars, runs MCP/system tools over the server's HTTP API (`POST
<url>/api/tools/<tool>`), probes server health, writes AI-agent MCP-client config, and enables/disables the
`godot_mcp` addon in a project. It also exports a side-effect-free library (`import { openProject, runTool,
… } from 'godot-cli'`) whose functions return a `{ kind: 'success' | 'failure' }` discriminated union and
never throw past the public boundary. It also ships a `setup-skills <agent> [path]` command that generates
AI-agent skill files (a `SKILL.md`-per-tool-family) under the agent's skills path. Unlike the Unity CLI
(which POSTs to a running editor's `/api/system-tools/unity-skill-generate` endpoint), the Godot CLI
generates the files **locally** from a built-in catalog of the addon's tool families — the Godot server
exposes no skill-generate HTTP endpoint, so no server or live editor is required. (The addon *additionally*
auto-generates skills addon-side on plugin boot via `GenerateSkillFilesIfNeeded`; the CLI command is the
server-less, scriptable path.) That catalog (`cli/src/utils/skills.ts` § `SKILL_FAMILIES`) is
hand-maintained, so a CI cross-check (`cli/tests/skills-addon-parity.test.ts`) diffs its family set against
the addon's actual `[AiToolType] Tool_<Family>` classes and **fails on divergence** — keep the catalog, the
§ Tool families table above, and the README in lockstep when adding/removing a family.

```bash
cd cli
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```

**Find detail in** [`cli/README.md`](cli/README.md) — full command table, editor-resolution order,
connection env vars, and server-URL resolution.

## Server (shared GameDev-MCP-Server)

The MCP server lives in its own shared repo: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)
(binary `gamedev-mcp-server`, release assets `gamedev-mcp-server-<rid>.zip`, Docker
`aigamedeveloper/mcp-server`) — one engine-agnostic server consumed by Unity-MCP, Godot-MCP, and
Unreal-MCP. The addon downloads the release pinned by the `ServerVersion` constant in
`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`. **Server version pin:** bumping the consumed server =
changing that constant; the pinned `v<ServerVersion>` release (with all 7 RID zips) must already exist on
GameDev-MCP-Server BEFORE cutting an addon release that pins it.

## Editor-runtime assembly loading (the dependency-resolution fix)

> If you are adding tool families or any code that touches a NuGet dependency at editor runtime, read
> this — it is why the plugin can load `ReflectorNet`/`McpPlugin` in the editor at all.

**The Godot gotcha.** Godot loads the project's compiled assembly into the **default**
`AssemblyLoadContext`, but does **not** teach that context how to find the project's NuGet dependency
graph. The CLR default probing looks only beside the host binary (Godot's own folder) and in the shared
framework; it does **not** read the project's `*.deps.json`, and — unlike a normal `dotnet` app launch —
Godot does **not** emit a `*.runtimeconfig.dev.json` (which would carry the NuGet global-packages probing
paths) next to the built assembly. So the first time editor-plugin code touches a type from a dependency
that was not copied beside the project assembly, the runtime throws:

```
System.IO.FileNotFoundException: Could not load file or assembly 'ReflectorNet, Version=5.3.1.0, ...'
```

This is a known, long-standing Godot limitation for C# addons with external dependencies
(godotengine/godot-proposals#9074, godotengine/godot#112701).

**The fix** lives in `addons/godot_mcp/Runtime/Connection/GodotMcpAssemblyResolver.cs`. It hooks
`AssemblyLoadContext.Default.Resolving` and answers the misses itself, per assembly name, in order:

1. **Same-directory probe** — `<name>.dll` next to the project assembly (covers consumers who copy deps
   into the output dir via `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>`).
2. **`*.deps.json` + NuGet global-packages probe** — parse the project's `*.deps.json` to learn each
   library's `{package}/{version}` and runtime-asset relative path (`lib/net8.0/ReflectorNet.dll`), then
   locate the file under the NuGet global packages folder (`NUGET_PACKAGES` env, else
   `~/.nuget/packages`). **This is the strategy that actually carries in the Godot editor**, where no
   dev.json probing config exists.
3. **`AssemblyDependencyResolver`** — best-effort last try; it resolves RID-specific assets when a
   sibling dev.json/runtimeconfig is present (a plain `dotnet` host), and returns empty in the editor.

Two non-obvious constraints make it work:

- **It is installed from a `[ModuleInitializer]`, not from `_EnterTree`.** Godot instantiates the
  `EditorPlugin` *type* (whose fields/usings reference the NuGet-dependency types) before any plugin
  method body runs, so installing the hook inside `_EnterTree` is too late — type load already triggers
  the failing resolution. The module initializer runs when the assembly's module is first loaded, before
  any type is constructed, and references only BCL types so it cannot itself fault.
- **In the editor, `Assembly.Location` is empty** (Godot byte-loads the project assembly). The resolver
  reconstructs the anchor path from `AppContext.BaseDirectory + <AssemblyName>.dll` (Godot points
  `BaseDirectory` at `.godot/mono/temp/bin/<cfg>/`, where the `*.deps.json` sits).

The resolver is pure-BCL and unit-tested (`Godot-MCP.Tests/AssemblyResolverTests.cs`): the test project
itself references the same packages, so its sibling `*.deps.json` exercises the same resolution path with
no Godot binary.

## Consumer-install story (how an end user's Godot project gets the deps)

A consumer installs the addon by placing `addons/godot_mcp/` in their Godot C# project and enabling it in
*Project → Project Settings → Plugins*. Because Godot compiles **every** `.cs` under the project into one
assembly, the addon's C# only compiles if the **consumer's `.csproj` declares the same NuGet
`PackageReference`s** the addon depends on:

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.3.1" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="6.10.0" />
</ItemGroup>
```

With those references present, `dotnet restore` populates the consumer's NuGet cache and the build emits a
`*.deps.json` describing the graph — which is exactly what `GodotMcpAssemblyResolver` reads at editor
runtime to locate the DLLs. **No manual DLL copying is required**; the resolver finds them in the NuGet
global packages folder. (A consumer who prefers self-contained output can instead set
`<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` so the DLLs land beside the project
assembly — strategy 1 above then covers them.) These reference instructions should be surfaced in the
addon's user-facing README / install docs as the family grows.

## Testbed runbook (headless live-verification)

The infra repo's `Godot-Test-Project/` (plain folder, analog of `Unity-Test-Project/`) wires the addon via
a directory junction `addons/godot_mcp → <Godot-MCP>/addons/godot_mcp` and pins the same NuGet packages.
Binary: `Godot_v4.5.1-stable_mono_win64_console.exe` (the `_console` variant surfaces `GD.Print`).

**Clean editor-boot smoke** (proves the assembly-load fix):

```bash
# (worktree verification) repoint the junction to the worktree addon, then restore it afterwards:
#   New-Item -ItemType Junction -Path addons\godot_mcp -Target <worktree>\Godot-MCP\addons\godot_mcp
GODOT=.../Godot_v4.5.1-stable_mono_win64_console.exe
"$GODOT" --headless --path <infra>/Godot-Test-Project --build-solutions --quit   # builds C#
"$GODOT" --headless --path <infra>/Godot-Test-Project --editor --quit            # boots editor
# Expect: "[Godot-MCP] plugin loaded", a stream of "[Godot-MCP] resolved '<dep>' -> <nuget-cache-path>",
#         and NO FileNotFoundException. If the testbed shows a stale assembly, wipe
#         .godot/mono/temp/{obj,bin} and rebuild (Godot's incremental build can miss junction edits).
```

**Live connect + ping** (proves the end-to-end MCP path) against a local server:

```bash
# 1) Start a local MCP server (MCP-Plugin-dotnet DemoWebApp). Default Authorization=none (no token).
cd <infra>/MCP-Plugin-dotnet/DemoWebApp
dotnet run -c Release -- port=5300 client-transport=streamableHttp        # /help returns 200 when up

# 2) Boot the editor in Custom mode pointed at it (the plugin connects to <host>/hub/mcp-server):
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:5300
# (GODOT_MCP_TOKEN only needed if the server is started with authorization=required)
"$GODOT" --headless --path <infra>/Godot-Test-Project --editor --quit-after 600000   # hold connected
# Expect: "[Godot-MCP] connecting (mode=Custom, host=http://localhost:5300) ..." then
#         "[Godot-MCP] connected.". Server log shows "Version handshake successful. Plugin: 0.1.0".

# 3) While connected, invoke the ping tool via the server's direct-tool-call API:
curl -s -X POST http://localhost:5300/api/tools/ping -H "Content-Type: application/json" -d '{}'
#   -> {"status":"success","structured":{"result":"pong"}}
curl -s -X POST http://localhost:5300/api/tools/ping -H "Content-Type: application/json" \
     -d '{"message":"hello-godot"}'
#   -> {"status":"success","structured":{"result":"hello-godot"}}
```

This exact runbook was executed for issue #6 and observed green (clean boot + `pong`).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP` (reverse-domain, matches Unity-MCP / McpPlugin). New types
  nest under folder-matching namespaces.
- Every `.cs` starts with the ASCII-art Apache-2.0 header (copy from a neighbouring file).
- Editor-only code lives behind `#if TOOLS`. Engine-runtime logic that is pure-managed (config, tools,
  the assembly resolver) stays outside `#if TOOLS` so it is CI-unit-testable.
- All Godot API calls from tool handlers marshal onto the editor main thread via the dispatcher; never
  touch `Node`/`Resource`/`EditorInterface` off-thread.
- Commits: `<type>(<scope>): <description>`; reference issues with `Closes #N`. Never `git add -A`; never
  bump the reused NuGet pins; never commit `bin/`/`obj/`/`.godot/`/`*.uid`.
