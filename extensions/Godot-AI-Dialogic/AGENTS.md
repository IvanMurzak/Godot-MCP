# AGENTS.md — Godot-AI-Dialogic

A **Godot-MCP extension** for the third-party [**Dialogic**](https://github.com/dialogic-godot/dialogic) 2.0 addon
(dialogue timelines and characters), shipped as an **RQL module**: it adds no MCP tools, only what the core
`rql-query` / `rql-mutate` / `rql-schema` tools need to work with Dialogic files (the `dialogic` source with its
creation calls and an agent guide rendered into the core `godot-rql` skill). A **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.Dialogic`) compiled inside the consumer's Godot project against the consumer's own
GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Dialogic/Editor/Rql/` — the module:
  - `DialogicModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the `dialogic` source and the guide.
  - `DialogicApi.cs` (the source: `installed`, `hint`, `timelines`, `characters`, `CreateTimeline`,
    `CreateCharacter`), `DialogicTimelineRow.cs` (`AddText`), `DialogicCharacterRow.cs` (holds `resource`),
    `DialogicScripts.cs` (global script classes: presence, `New`) — `#if TOOLS`.
  - `DialogicNames.cs` (addon class / property names, install hint, starter text), `DialogicRules.cs` (paths,
    display name, color), `DialogicTimelineText.cs` (the `.dtl` text format) and `DialogicGuide.cs` (the guide) —
    pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-Dialogic/build/com.IvanMurzak.Godot.MCP.Dialogic.props` — source-injection props, auto-imported by
  NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Dialogic.Tests/` — xUnit (no Godot binary): rules, timeline text, guide examples parse, the
  module's view types (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `scripts/extension_e2e.py` from the monorepo suite, which
  installs the pinned addon (`extension.json` `testAddon`, a newer tag from Godot 4.5) and registers its `Dialogic`
  autoload.
- `testbed/Dialogic-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **No compile-time dependency on Dialogic.** `DialogicTimeline` / `DialogicCharacter` are GDScript `class_name`
  resources: no C# type (so no adapter can target them), not in ClassDB (so the core `CreateResource` cannot make
  them). They are named by string only (`DialogicNames`) and created from their global-class script.
  `dotnet build -c Debug` must pass with the addon absent, and configuring the module never touches Godot.
- **Files, not nodes.** Rows are read-only views of `res://` files (`res://addons` and hidden folders skipped).
  Character writes go through `resource` (`get resource | set props["…"]`): the core saves the `.dch` with
  Dialogic's own saver (`var_to_str(inst_to_dict(…))`, verified live). A timeline is its text: `SaveTimeline` builds
  a `DialogicTimeline` with `from_text`, sets the `timeline_not_saved` meta (Dialogic's saver writes nothing without
  it), saves and `TakeOverPath`s so the cache holds the new resource.
- **Creating and appending are not undoable** (direct file writes, like the core `CreateResource`); creating fails on
  an existing file. Validation (`DialogicRules`) runs before any write.
- **Absent addon:** `installed` false, `hint` = the install hint, empty lists, the calls fail with the hint.
- Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted). The module uses only Godot,
  `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has no core).

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-Dialogic/Godot-AI-Dialogic.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Dialogic.Tests/Godot-AI-Dialogic.Tests.csproj
```

The live suite (one headless editor for every module, the pinned addon installed) is
`python scripts/test_extensions.py` from the monorepo root (Godot 4.4+: `minGodotVersion`). `dotnet pack` reuses
the **global NuGet cache** for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.dialogic/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Dialogic`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` floors; **GodotSharp and the
  Dialogic addon must never become package dependencies** (CI asserts the nuspec). Bump floors with
  `commands/update-core.ps1`.
