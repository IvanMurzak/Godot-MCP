# Godot-MCP architecture — Editor vs Runtime

This document explains how the `addons/godot_mcp/` addon is split into an **editor-only**
surface and a **runtime (in-game)** surface, why the split is done the way it is, and the
rules that keep it honest. It is the conceptual companion to the folder layout
(`addons/godot_mcp/Runtime/` vs `addons/godot_mcp/Editor/`) and the CI boundary guard
(`scripts/check-runtime-boundary.py`).

> TL;DR — Godot compiles the **whole** addon into **one** assembly. The editor-only code is
> kept out of an exported game build by the `TOOLS` compilation symbol (`#if TOOLS`), which
> Godot defines **only** for the editor build. The `Runtime/` vs `Editor/` folders mirror
> that split so a human can see the boundary at a glance, and the CI guard fails the build
> if any `Runtime/` file ever references an editor-only API in shipping code.

## The Godot one-assembly constraint

Unlike Unity (which compiles `Editor/` and `Runtime/` asmdefs into *separate* assemblies),
**Godot compiles every `.cs` under a C# project into a single project assembly.** There is
no per-folder assembly boundary. So you cannot rely on assembly separation to keep editor
code out of a shipped game — folders are organizational only.

What Godot *does* give you is a build-configuration-scoped compilation symbol:

| Build configuration | Defines `TOOLS`? | Used for |
| --- | --- | --- |
| `Debug`         | **yes** | the editor (the addon running inside the Godot editor) |
| `ExportDebug`   | no      | an exported game build (debug) |
| `ExportRelease` | no      | an exported game build (release) |

Anything wrapped in `#if TOOLS … #endif` is compiled **only** into the editor build and is
**stripped** from `ExportDebug`/`ExportRelease`. That is the mechanism that lets the addon
carry a full editor dock + editor tool families *and* a lean runtime entry point in one
source tree without the editor code bloating (or even compiling into) a shipped game.

## The Runtime vs Editor module split

The addon is organised so that **what each folder contains matches what ships**:

```
addons/godot_mcp/
├── plugin.cfg                 # addon manifest; script="Editor/GodotMcpPlugin.cs"
├── Icons/                     # dock icons (res:// loaded; editor-only assets)
├── Runtime/                   # un-gated — COMPILES INTO A GAME BUILD
│   ├── GodotMcpRuntime.cs            # in-game entry point: GodotMcpRuntime.Initialize(...)
│   ├── GodotMcpRuntimeBuilder.cs     # fluent builder (WithConfig / WithTools / WithRuntimeErrorCapture / …)
│   ├── GodotMcpRuntimeHandle.cs      # disposable handle over a running connection
│   ├── RuntimeErrorCapture.cs        # installs in-game error capture (OS.AddLogger 4.5+ + C# fault hooks)
│   ├── GodotMcpEnv.cs
│   ├── Connection/                   # connection core, config, env/store, logger, resolver,
│   │                                 #   token gen, server-view (version parse), STJ cache
│   ├── MainThread/                   # MainThreadDispatcher + GodotMainThread
│   ├── Reflection/                   # reflector factory + Godot value-type / Variant / Resource-ref /
│   │                                 #   Node-ref converters + GodotAssemblyUtils
│   ├── Data/                         # pure-managed result/ref models (NodeRef, ResourceRef, …)
│   ├── Extensions/                   # extension registry/installer planning logic (pure-managed)
│   └── Tools/                        # RUNTIME-SAFE tools: Tool_Ping, Tool_Console*, Tool_Reflection*,
│                                     #   Tool_RuntimeErrors* (in-game runtime-error capture, opt-in),
│                                     #   Tool_GameScript + ScriptExecutor (script-execute, editor + in-game opt-in)
│                                     #   + the engine-error logger bridge (GodotScriptErrorLogger*, 4.5+,
│                                     #   shared by the editor AND the in-game runtime) + pure helpers
│                                     #   (NodePathNormalizer, ResPathNormalizer, RuntimeErrorCollector, …)
└── Editor/                    # #if TOOLS — STRIPPED FROM A GAME BUILD (editor-only)
    ├── GodotMcpPlugin.cs             # the [Tool] EditorPlugin entry (boots the dock + connection)
    ├── Connection/                   # editor server lifecycle (GodotMcpServerManager), device-auth
    │   └── DevControl/               #   the dev-only HttpListener control bridge
    ├── Extensions/                   # ConsumerProjectFile (editor file IO)
    ├── Reload/                       # the C# reload gate (AssemblyReloadGate + ReloadGateDriver, GatedRunTool)
    │                                 #   and script-build's detached build (ProjectBuildTracker)
    ├── Rql/                          # the RQL data layer (docs/rql.md): engine (RqlGodot), editor source,
    │                                 #   adapters, modules, schema, guide, tool binding (ToolFormat/)
    ├── Tools/                        # the 3 EDITOR tool families: Tool_Rql* (rql-query / -mutate / -schema),
    │                                 #   Tool_Script*, Tool_Screenshot* (+ scene-tree / resource helpers)
    └── UI/                           # the entire dock UI (panels, rows, windows, view-models)
```

**Namespaces are unchanged by this layout.** All types keep their existing
`com.IvanMurzak.Godot.MCP.*` namespaces (e.g. `…Runtime`, `…Connection`, `…Tools`, `…UI`)
regardless of which top-level folder they now live in. In C#, folder ≠ namespace, and
game-developer code + the infra testbeds depend on the existing namespaces — so the move was
folders-only. Do not "fix" a namespace to match its new folder.

> **Note — `Editor/` is the *intended* editor surface, not a hard compile gate.** A handful
> of pure-managed view-model / helper types under `Editor/UI/` and `Editor/Connection/`
> (e.g. `ConnectionPanelView`, `DockTheme`, `GodotDeviceAuthService`, `DevControlRouter`) are
> not themselves wrapped in `#if TOOLS` — only the Godot-`Control`-touching code that
> *consumes* them is. Those helpers therefore still technically compile into a game build,
> exactly as they did before this reorg. The folder reflects their conceptual role (they
> exist to serve the editor dock); the `#if TOOLS` guards on their call sites are what keep
> the editor's `Control`/`EditorInterface` surface out of the game. The boundary the CI guard
> enforces is the one that matters for shipping correctness: **no `Runtime/` file leaks an
> editor-only API into shipping code** (see below).

## The two developer paths

The same addon serves two distinct audiences:

### 1. Dev-only (the default): MCP in the Godot editor

A plugin author installs `addons/godot_mcp/`, enables it in
*Project → Project Settings → Plugins*, and the `GodotMcpPlugin` `EditorPlugin`
(`Editor/GodotMcpPlugin.cs`, `#if TOOLS`) boots on editor load: it installs the assembly
resolver + main-thread dispatcher, builds a `Reflector` with Godot type converters, shows the
"AI Game Developer" dock, and connects to an MCP server (cloud `ai-game.dev` by default, or a
custom local server). The editor tool families (`rql-*` — RQL over scene nodes, resources, files, scenes and
editor state —, `script-*`, `screenshot-*`) drive the live editor. In the editor, the runtime-safe `console-*` /
`reflection-*` tools are replaced by RQL (the `console` source, `call` / `CallMethod`, `rql-schema`): the editor
removes them from the plugin it builds (`RqlInstaller`), the in-game runtime keeps them.

When the developer **exports their game**, `TOOLS` is undefined, so the `EditorPlugin`, the
dock, the device-auth flow, the RQL engine and the editor tool families are **stripped** — none of it
ships, and **no MCP server connection is started in the game**. This is the zero-config path:
do nothing special and the editor tooling simply isn't in your build.

### 2. Runtime (in-game): MCP inside a running game

A developer who wants an MCP endpoint **inside a running game** (e.g. to let an agent inspect
or drive a live build) opts in explicitly from game code:

```csharp
using com.IvanMurzak.Godot.MCP.Runtime;

// Somewhere during game startup (e.g. an autoload _Ready):
var handle = GodotMcpRuntime
    .Initialize(b => b
        .WithConfig(cfg => { /* host, token, connection mode … */ })
        .WithTools(typeof(MyGameTool)))   // register your own runtime-safe [AiToolType]s
    .Build();                              // returns a GodotMcpRuntimeHandle (dispose to stop)
```

Everything this path needs lives under `Runtime/` and compiles into the game build: the
entry point + builder + handle, the connection core, the main-thread dispatcher, the
reflector, the assembly resolver, and the runtime-safe tools (`ping`, `console-*`,
`reflection-*`, and the opt-in `runtime-errors-*`). The `godot-skill-*` family also compiles
in — it declares outside `#if TOOLS` so its `[AiTool]` attributes stay visible to the CI test
host — but it is **not** runtime-safe: its editor-only host is null in a game build, so every
call refuses with an actionable error. The editor tool families are **not**
available here — they are `#if TOOLS` and don't exist in the game build by design (they
require `EditorInterface` and a live editor).

#### In-game runtime error capture (issue #160)

A subtlety worth calling out: the engine-error logger bridge
(`Runtime/Tools/GodotScriptErrorLogger.cs`) is gated by `#if GODOT4_5_OR_GREATER` **but not**
by `#if TOOLS` — because `OS.AddLogger` / `Godot.Logger` are **engine (runtime) APIs**, not
editor APIs. That is what lets the *same* bridge serve two callers:

- the **editor** (`Editor/GodotMcpPlugin.cs`) installs it to capture engine script errors
  raised in-editor (feeding the RQL `console` source / `script-validate`), and
- the **in-game runtime** (`Runtime/RuntimeErrorCapture.cs`, opt-in via
  `GodotMcpRuntime.Initialize(b => b.WithRuntimeErrorCapture())`) installs it to capture
  errors raised in the **running game** — GDScript runtime errors, `push_error`/`push_warning`,
  shader errors — plus C# unhandled / unobserved-`Task` exceptions (with full managed stack
  traces) via `AppDomain.UnhandledException` / `TaskScheduler.UnobservedTaskException`. The
  captured rows are ring-buffered (`RuntimeErrorCollector`) and surfaced via the
  `runtime-errors-*` tool with monotonic-sequence since-poll / clear semantics.

This is why those files live under `Runtime/` and pass the boundary guard: they reference no
editor-only API (only `OS` + `Godot.Logger`), so they ship into a game build. On Godot < 4.5
the bridge's `#else` stub compiles in (no `OS.AddLogger`), so the engine channel degrades
gracefully to a no-op while the C# fault channels still work.

## The Editor → Runtime dependency rule

> **Editor code may reference Runtime code. Runtime code may NEVER reference Editor code.**

This is the single load-bearing invariant. `Editor/` is stripped from the game; `Runtime/`
ships. If a `Runtime/` type referenced an editor-only type (`EditorInterface`,
`EditorPlugin`, `EditorFileSystem`, `EditorScript`) in shipping code, the exported game build
would fail to compile (the symbol is undefined) — or, worse, drag editor-only behaviour into
a context where no editor exists.

A narrow, **documented** exception is allowed: a `Runtime/` file may contain a small
`#if TOOLS … #endif` shim around an editor-only call, because that body is stripped from the
game build. The addon uses exactly one such shim today —
`Runtime/Connection/GodotMcpConnection.cs` guards a single
`Tool_Resource.InstallReflectionResolver()` call behind `#if TOOLS` so the runtime-agnostic
connection compiles with `TOOLS` undefined while still wiring the editor resolver when run in
the editor. The CI guard reports this shim as a *warning*, not a failure.

Editor code that must shape what the connection builds passes a **callback** instead of a shim: the
`GodotMcpConnection` constructor takes `configurePlugin` (`Action<IMcpPlugin, GodotMcpConfig>`), invoked right after
each plugin is built. `GodotMcpPlugin` passes the one `Editor/Rql/RqlInstaller.Install` returns (install the RQL tool
data layer, remove the editor-replaced runtime tools), so `Runtime/` never names an editor type.

### CI boundary guard

`scripts/check-runtime-boundary.py` enforces the rule on every PR (wired into
`.github/workflows/ci.yml` as the fast `runtime/editor boundary` job, which runs before the
.NET build). It scans `addons/godot_mcp/Runtime/**/*.cs` and **exits non-zero** if any file
references `EditorInterface` / `EditorPlugin` / `EditorFileSystem` / `EditorScript` in real
code that is **not** inside a `#if TOOLS` guard and **not** inside a comment or string
literal. It is comment- and string-aware on purpose: the runtime files mention editor type
names heavily in doc-comments and `[Description("…")]` strings to *explain* the boundary, and
those are not violations — only un-guarded code is. It also scans **every** addon `.cs` file and
fails if one uses an editor-only NuGet package (`Rql.*`, `com.IvanMurzak.CodeExec*`, `Microsoft.CodeAnalysis*`)
outside its guard (see the next section: export builds do not reference those packages unless the game opts in).

Run it locally any time:

```bash
python scripts/check-runtime-boundary.py            # 0 = boundary holds, 1 = violation
python scripts/check-runtime-boundary.py --verbose  # also lists the allowed #if TOOLS shims
```

This guard is a fast pre-filter; the authoritative proof that the boundary holds is that the
`ExportRelease` (TOOLS-undefined) configuration **compiles** and the resulting assembly
contains the runtime types and none of the editor types.

## Editor-only NuGet packages (no Roslyn in an exported game)

The `#if TOOLS` strip removes editor *code*; the NuGet *packages* only that code uses are kept out of
exports by an MSBuild condition, the Godot counterpart of Unity-MCP importing the Roslyn DLLs Editor-only.
Four packages are editor-only: `com.IvanMurzak.Rql` (the RQL data layer), `com.IvanMurzak.CodeExec`,
the source-only `com.IvanMurzak.CodeExec.Roslyn` and `Microsoft.CodeAnalysis.CSharp` (`script-execute`).
`Godot-MCP.csproj` declares each with

```xml
<PackageReference Include="com.IvanMurzak.Rql" Version="0.3.0-dev.2877579" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
```

(Code-Exec and Roslyn carry a wider condition, see "In-game code execution" below.)
`ExportDebug` / `ExportRelease` are exactly the Godot.NET.Sdk configurations without `TOOLS` (`Debug`, the editor
build, defines it); the SDK conditions its own `GodotSharpEditor` reference the same way. Godot restores per build,
so the editor build (`Debug`) gets the packages and an export (`dotnet publish -c ExportRelease`) does not.
`godot-cli install-plugin` writes the same `Condition` into the consumer's `.csproj` (it reads the editor-only flag
from the addon's `nuget-deps.json` / `Godot-MCP.csproj`, and adds the condition to an unconditioned entry an older
install wrote; a `Condition` the project already set is kept). `com.IvanMurzak.McpPlugin` and
`com.IvanMurzak.ReflectorNet` stay unconditional: `Runtime/` (the in-game path) uses them.

What makes this compile: every addon file that uses one of those packages is inside `#if TOOLS` (or
`#if TOOLS || GODOT_MCP_UNIT_TESTS` for the pure sources the xUnit suite compiles), which the CI boundary guard
above enforces. A Godot 4.5.1 headless `--export-release` of the testbed ships `McpPlugin.dll`,
`McpPlugin.Common.dll` and `ReflectorNet.dll` and none of `Rql.*`, `com.IvanMurzak.CodeExec.dll` or
`Microsoft.CodeAnalysis*.dll`. An extension package that depends on RQL (e.g. `com.IvanMurzak.Godot.MCP.Tilemap`)
still brings RQL into an export transitively; that is the extension's dependency, not the core addon's.
CI checks the package set of both export configurations without Godot (`scripts/check-export-assemblies.py`).

### In-game code execution (opt-in)

A game can keep Code-Exec and Roslyn to offer `script-execute` itself (`GodotMcpRuntimeBuilder.WithScriptExecute()`,
security: [`runtime-security.md`](runtime-security.md)). Its `.csproj` sets the MSBuild property
`GodotMcpCodeExecInGame=true` and defines `GODOT_MCP_CODE_EXEC_IN_GAME` (README "Running C# in the game"). The three
Code-Exec packages carry

```xml
Condition="'$(GodotMcpCodeExecInGame)' == 'true' or ('$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease')"
```

(`inGameOptIn` in the CLI and `nuget-deps.json`; RQL keeps the editor-only condition). The shared executor
(`Runtime/Tools/ScriptExecutor.cs`, `ScriptExecuteSource.cs`, `GodotSnippetLogger.cs`) and the in-game tool
(`Runtime/Tools/Tool_GameScript.cs`) compile under `#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME`; the editor
`Tool_Script.Execute` calls the same executor. `Tool_GameScript` is not an `[AiToolType]`, so the editor's assembly
scan never registers it; in an editor build a game's own `WithToolsFromAssembly` scan of the addon would register the
editor `script-execute`, so `GodotMcpRuntime.Build` puts the in-game one back in its place. The boundary guard allows
Code-Exec / Roslyn under a guard that includes `GODOT_MCP_CODE_EXEC_IN_GAME`; editor APIs and RQL stay `#if TOOLS`.

## (Optional) Strictly-zero-bytes export exclude for dev-only users

For the common dev-only case (path 1 above), the `#if TOOLS` strip already removes the
editor code from the game. The `Runtime/` code does still compile in (it is small and inert
unless `GodotMcpRuntime.Initialize()` is called). A weight-sensitive developer who uses the
addon **only** as an editor tool and never calls the runtime entry point can exclude the
addon from their export build entirely — shipping literally zero addon bytes — with a
condition in their **own game `.csproj`** (not the addon's):

```xml
<!-- In the consumer game project's .csproj — exclude the addon from EXPORT builds only,
     so it still compiles for the editor (Debug) but ships nothing in ExportRelease. -->
<PropertyGroup Condition="'$(Configuration)' == 'ExportRelease'">
  <DefaultItemExcludes>$(DefaultItemExcludes);addons/godot_mcp/**/*.cs</DefaultItemExcludes>
</PropertyGroup>
```

If you also want to drop the two remaining NuGet packages (`com.IvanMurzak.McpPlugin` /
`com.IvanMurzak.ReflectorNet`, which the `Runtime/` code needs) from the published game, give their
`<PackageReference>`s the same `Condition` the editor-only packages carry (previous section).

> **Only do this if you never use the runtime (in-game) path.** Excluding the addon sources
> from `ExportRelease` removes `GodotMcpRuntime` too, so a game that calls
> `GodotMcpRuntime.Initialize()` would fail to compile in that configuration. This snippet is
> a deliberate opt-in for editor-only users who want the absolute minimum shipped footprint;
> it is **not** required for correctness — the `#if TOOLS` strip already keeps the editor
> surface out of every export.
