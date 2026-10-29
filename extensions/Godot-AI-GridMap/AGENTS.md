# AGENTS.md — Godot-AI-GridMap

A **Godot-MCP extension** for Godot's built-in `GridMap` node (3D tile maps over a `MeshLibrary`), shipped as an
**RQL module**: it adds no MCP tools, only what the core `rql-query` / `rql-mutate` / `rql-schema` tools need to
paint grids (validated settings, cell rows, `SetCell` / `ClearCell` / `Clear`, an agent guide rendered into the
core `godot-rql` skill). A **source-only NuGet package** (`com.IvanMurzak.Godot.MCP.GridMap`) compiled inside the
consumer's Godot project against the consumer's own GodotSharp; the packaging recipe is in
`docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-GridMap/Editor/Rql/` — the module:
  - `GridMapModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the adapter and the guide.
  - `Godot_GridMap_Adapter.cs` — `CellSize` / `CellOctantSize` (validated), `cells`, `cellCount`, the cell calls
    (`#if TOOLS`).
  - Pure-managed, NOT behind `#if TOOLS`: `GridMapRules.cs` (validation), `GridMapGuide.cs` (the guide),
    `GridMapCellRow.cs` (the read-only `cells` row).
- `src/Godot-AI-GridMap/build/com.IvanMurzak.Godot.MCP.GridMap.props` — source-injection props, auto-imported by
  NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-GridMap.Tests/` — xUnit (no Godot binary): rules, guide examples parse, the module's member
  table (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `scripts/extension_e2e.py` from the monorepo suite.
- `testbed/` — consumer project restoring the local-packed package (injection proof) and `e2e-meshlib.tres`, the
  empty MeshLibrary fixture the e2e assigns (copied to the live project root).
- Versions and core pins are set from the monorepo root: `scripts/extension_release.py bump` / `version`,
  `scripts/update_core.py`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `GridMapModule` and documented in `GridMapGuide` (topic
  "GridMap"). Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Validate, never clamp.** Members and calls reject what Godot refuses (cell size below 0.001, octant size 0,
  coordinates beyond ±32767, orientation outside 0..23) or 0.x refused (negative items: erase with `ClearCell`)
  before anything changes, so Godot never logs an error (the headless suite fails on any `ERROR:`).
- Never select or edit nodes (`EditorInterface.EditNode`): the 0.x tool's selection raised a native GridMap gizmo
  error in the headless editor. The calls are on the GridMap itself, so the core undo snapshot covers its cells.
- The namespace ends in `.GridMap`, which shadows `Godot.GridMap`: use the `GdGridMap` alias.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-GridMap/Godot-AI-GridMap.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-GridMap.Tests/Godot-AI-GridMap.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.gridmap/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.GridMap`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` pins (equal to the core's); **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump them with `scripts/update_core.py`.
