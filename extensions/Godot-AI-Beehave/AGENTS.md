# AGENTS.md — Godot-AI-Beehave

A **Godot-MCP extension** for the third-party [**Beehave**](https://github.com/bitbrain/beehave) addon (behaviour
trees), shipped as an **RQL module**: it adds no MCP tools, only what the core `rql-query` / `rql-mutate` /
`rql-schema` tools need to work with Beehave nodes (the `beehave` source and an agent guide rendered into the core
`godot-rql` skill). A **source-only NuGet package** (`com.IvanMurzak.Godot.MCP.Beehave`) compiled inside the
consumer's Godot project against the consumer's own GodotSharp; the packaging recipe is in
`docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Beehave/Editor/Rql/` — the module:
  - `BeehaveModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the `beehave` source and the guide. Its class
    name is load-bearing: `scripts/test_extensions.py`'s isolation canary references
    `com.IvanMurzak.Godot.MCP.Beehave.BeehaveModule`.
  - `BeehaveApi.cs` (the source: `installed`, `hint`, `trees`, `nodes`), `BeehaveTreeRow.cs`, `BeehaveNodeRow.cs`
    (read-only rows holding `node`), `BeehaveScripts.cs` (global script classes of a node) — `#if TOOLS`.
  - `BeehaveNames.cs` (addon class / property names, kinds, install hint) and `BeehaveGuide.cs` (the guide) —
    pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-Beehave/build/com.IvanMurzak.Godot.MCP.Beehave.props` — source-injection props, auto-imported by
  NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Beehave.Tests/` — xUnit (no Godot binary): names / kinds, guide examples parse, the module's view
  types (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `scripts/extension_e2e.py` from the monorepo suite, which
  installs the pinned addon from `extension.json` `testAddon`.
- `testbed/Beehave-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- Versions and core pins are set from the monorepo root: `scripts/extension_release.py bump` / `version`,
  `scripts/update_core.py`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **No compile-time dependency on Beehave.** Its classes are GDScript `class_name`s: no C# type (so no adapter can
  target them), not in ClassDB, `GetClass()` returns the native base `Node`. They are named by string only
  (`BeehaveNames`) and recognized through the node's script chain against `ProjectSettings.GetGlobalClassList()`.
  `dotnet build -c Debug` must pass with the addon absent, and configuring the module never touches Godot.
- **Rows are read-only views; writes go through `node`.** `get node | set props["…"]` is the core's write, so it is
  undoable and marks the scene unsaved. Creation is the core `AddChild("BeehaveTree", …)` (global script classes).
- **Absent addon:** `installed` false, `hint` = the install hint, empty lists; never throw.
- Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted). The module uses only Godot,
  `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has no core).

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-Beehave/Godot-AI-Beehave.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Beehave.Tests/Godot-AI-Beehave.Tests.csproj
```

The live suite (one headless editor for every module, the pinned addon installed) is
`python scripts/test_extensions.py` from the monorepo root. `dotnet pack` reuses the **global NuGet cache** for an
already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.beehave/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Beehave`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` pins (equal to the core's); **GodotSharp and the
  Beehave addon must never become package dependencies** (CI asserts the nuspec). Bump them with
  `scripts/update_core.py`.
