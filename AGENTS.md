# AGENTS.md — Godot-MCP

Godot-MCP connects LLM agents to the [Godot](https://godotengine.org/) editor over the
[Model Context Protocol](https://modelcontextprotocol.io/) — the Godot sibling of Unity-MCP. The MCP
server is NOT in this repo: the addon consumes the shared
[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server).

The addon is a `[Tool]` `EditorPlugin` (`GodotMcpPlugin`) that installs the main-thread dispatcher,
builds a `Reflector` with Godot type converters, and connects to an MCP server (cloud `ai-game.dev` by
default, or a custom local one) through the McpPlugin SignalR client. The editor tools' data layer is
[RQL](docs/rql.md) (`rql-query` / `rql-mutate` / `rql-schema`, `Editor/Rql/`). The MCP/reflection/query stack
(`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`, `com.IvanMurzak.Rql` — PolyForm Shield 1.0.0) is
**not forked**: it is consumed from nuget.org as `PackageReference`s whose pins are owned by the upstream release
pipelines. **Never bump them here.** The current pins are in `Godot-MCP.csproj` (mirrored by `Godot-MCP.Tests/`,
`Godot-Tests/` and `cli/src/utils/addon-deps.ts`; keep them in lockstep) and restated in `README.md` / `docs/`.

## Layout

- `addons/godot_mcp/` — the shipped addon (C#, `Godot.NET.Sdk`). `Runtime/` is un-gated and compiles
  into game builds; `Editor/` is the editor-only surface (`#if TOOLS`). Map and the Editor → Runtime
  dependency rule: [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Do NOT put agent/dev files here:
  everything in it ships to consumers (release zip and Asset Library).
- `addons/godot_mcp/{Runtime,Editor}/Tools/` — runtime-safe / editor-driving tool families: one
  `[AiToolType] partial class Tool_<Family>` per family, one partial-class file per
  `[AiTool("<name>")]` + `[Description]` method. Names mirror Unity-MCP where sensible; full list in
  README "Tools Reference". New editor data access goes into RQL (an adapter, source, function or
  `editor` call under `Editor/Rql/`, see [`docs/rql.md`](docs/rql.md)), not a new tool.
- `Godot-MCP.Tests/` — xUnit CI suite; one `<Subject>Tests.cs` per area.
- `Godot-Tests/` — headless Godot CI testbed + runtime harness ([`Godot-Tests/README.md`](Godot-Tests/README.md)).
- `cli/` — the `godot-cli` npm package; see [`cli/AGENTS.md`](cli/AGENTS.md).
- `scripts/` — CI/release helpers (`bump-version.mjs`, `check-runtime-boundary.py`, harness asserts).
- `extensions/` — independent source-only NuGet modules; read its AGENTS.md before editing.
- `templates/extension/` — extension scaffolding; never included in core or consumer assemblies.
- `docs/` — `ARCHITECTURE.md`, `RELEASING.md`, `runtime-security.md`, Asset Library material.
- `tests/chain-fixtures/`, `.github/scripts/chain_*.py` — release-train fixtures; byte-compared, LF-pinned.

## Build / test

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore            # 0 errors (CI gate)
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

No Godot binary is needed for these. The xUnit project compiles a CI-friendly subset of the addon
sources directly (no `ProjectReference`; the game `.csproj` excludes `Godot-MCP.Tests/**`). Only
**pure-managed** Godot types are unit-testable: native-backed ones (`NodePath`, `Node`, `Resource`,
`SceneTree`) crash the test host with `AccessViolationException`; verify those with the headless smoke.

## Headless live-editor smoke (operator check, not a CI gate)

Use the `software` workspace testbed `engines/godot/test-project/` (it junctions `addons/godot_mcp`
to this repo) and a Godot mono `_console` binary. Run **three separate processes**; a cold single-process
`--headless --build-solutions --quit` SIGSEGVs on the first C# `[Tool]` plugin (upstream Godot bug,
godot #81790 / #100805). CI uses the same pattern (`.github/workflows/test_godot_plugin.yml`).

```bash
"$GODOT" --headless --path <testbed> --import --quit      # materialize .godot/ (non-gating)
dotnet build <testbed>/<testbed>.csproj -c Debug          # compile C#
"$GODOT" --headless --path <testbed> --editor --quit      # expect "[Godot-MCP] plugin loaded", no FileNotFoundException
```

Stale assembly → wipe `.godot/mono/temp/{obj,bin}` and rebuild. Live connect: start a local MCP
server, set `GODOT_MCP_CONNECTION_MODE=Custom` and `GODOT_MCP_HOST=http://localhost:<port>` (plus `GODOT_MCP_TOKEN` if the server requires auth), boot with
`--quit-after 600000`, then `curl -X POST <host>/api/system-tools/ping -d '{}'` → `pong`. `ping` is a
**System** tool: `/api/system-tools/`, not `/api/tools/`. With that connection up,
`python scripts/smoke_rql.py --host <host> --project <testbed>` checks `rql-query` / `rql-mutate` / `rql-schema` by
reading back the `.tres`/`.tscn` Godot wrote and runs every agent-guide example (CI runs it in
`test_godot_runtime_harness.yml`; it writes fixed `res://scenes/` / `res://materials/` paths, so use a disposable
project). Two traps: build
the testbed with the `Godot.NET.Sdk` version of the running editor (CI rewrites the `Sdk=` pin; a 4.3
build on a 4.4+ editor throws `MissingMethodException` from changed APIs), and keep exactly ONE editor
connected (a background launch must record Godot's own PID — `(cd tb && exec godot …) &` — or stale
editors keep answering tool calls with an old build).

## Load-bearing gotchas

- **Editor-runtime assembly loading.** Godot does not resolve a project's NuGet deps in the editor.
  `Runtime/Connection/GodotMcpAssemblyResolver.cs` hooks `AssemblyLoadContext.Default.Resolving`
  (probes beside the assembly, then `*.deps.json` + NuGet global packages). It is installed from a
  `[ModuleInitializer]` (not `_EnterTree`, which is too late) and references only BCL types. Read its
  doc comment before touching NuGet-dependency code that runs in the editor.
- **Consumer install.** The addon ships as source, so a consumer `.csproj` must declare the same
  `PackageReference`s AND embed `addons/godot_mcp/extensions.catalog.json` with
  `LogicalName="Godot-MCP.extensions.catalog.json"` (no fallback: missing = empty extension list).
  `godot-cli install-plugin` writes both. It takes the NuGet versions from the INSTALLED addon:
  `addons/godot_mcp/nuget-deps.json`, generated into the release zip from `Godot-MCP.csproj` by
  `scripts/write-nuget-deps.mjs` (never committed), or the checkout's csproj for `--source`; the
  constants in `cli/src/utils/addon-deps.ts` (parity-tested against `Godot-MCP.csproj`) are only the
  fallback for older zips. User-facing steps: README.
- **Server pin.** The consumed server version is `ServerVersion` in
  `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`; that release (all 7 RID zips) must exist
  on GameDev-MCP-Server before an addon release pins it. Details: `docs/RELEASING.md`.
- **Tool families are mirrored.** Adding/removing a tool or family: update `cli/src/utils/skills.ts`
  (`SKILL_FAMILIES`) and the README counts/table; `cli/tests/skills-addon-parity.test.ts` fails on drift.
- **RQL guide examples run live.** `Editor/Rql/RqlGuide.cs` examples are mirrored by
  `Godot-Tests/rql-guide-examples.json` (xUnit parity) and executed in order by `scripts/smoke_rql.py`; keep them
  runnable top to bottom against its fixture scene.
- **RQL and hot reload.** RQL lives in the default load context; its Type-keyed caches are cleared on teardown
  (`RqlStaticCaches`). The editor tool surface is shaped by the `configurePlugin` callback the editor passes to the
  `GodotMcpConnection` constructor (Runtime/ never names editor types); console-* / reflection-* stay in the in-game runtime only.

## Conventions

- Namespaces are `com.IvanMurzak.Godot.MCP.*` and do NOT follow folders; match neighbouring files.
- Every `.cs` starts with the ASCII-art Apache-2.0 header (copy from a neighbouring file).
- Editor-only code goes behind `#if TOOLS`; pure-managed logic (models, helpers, config) stays outside
  it so it is unit-testable. `Runtime/` must not use editor-only APIs (`scripts/check-runtime-boundary.py`).
- Godot API calls from tool handlers go through the main-thread dispatcher; never touch
  `Node`/`Resource`/`EditorInterface` off-thread.
- Commits: `<type>(<scope>): <description>`, `Closes #N`. Never `git add -A`; never commit
  `bin/`/`obj/`/`.godot/`/`*.uid`.
- Releases are a manual `release.yml` dispatch; see `docs/RELEASING.md`.
