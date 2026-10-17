# AGENTS.md — Godot-AI-Tilemap

A **Godot-MCP extension** for Godot's built-in `TileMapLayer` node (Godot 4.3+, the replacement for the
deprecated `TileMap`) and its `TileSet`, shipped as an **RQL module**: it adds no MCP tools, only what the core
`rql-query` / `rql-mutate` / `rql-schema` tools need to paint layers (used-cell rows, `SetCell` / `EraseCell` /
`Clear`, validation, an agent guide rendered into the core `godot-rql` skill). A **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.Tilemap`) compiled inside the consumer's Godot project against the consumer's own
GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Tilemap/Editor/Rql/` — the module:
  - `TilemapModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the adapter and the guide.
  - `Godot_TileMapLayer_Adapter.cs` — `usedCells`, `usedCellCount` and the cell calls (`#if TOOLS`).
  - Pure-managed, NOT behind `#if TOOLS`: `TilemapRules.cs` (validation), `TilemapGuide.cs` (the guide),
    `TilemapCellRow.cs` (the read-only `usedCells` row).
- `src/Godot-AI-Tilemap/build/com.IvanMurzak.Godot.MCP.Tilemap.props` — source-injection props, auto-imported by
  NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Tilemap.Tests/` — xUnit (no Godot binary): rules, guide examples parse, the module's member
  table (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `run_tool_e2e.py` from the monorepo suite.
- `testbed/` — consumer project restoring the local-packed package (injection proof) and `e2e-tileset.tres`, the
  empty TileSet fixture the e2e assigns (copied to the live project root).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `TilemapModule` and documented in `TilemapGuide` (topic
  "Tilemap"). Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Validate, never clamp.** `SetCell` rejects a negative source id (erase with `EraseCell`), atlas coordinate or
  alternative before anything changes. The calls are on the layer itself, so the core undo snapshot covers its
  cells (`tile_map_data`).
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-Tilemap/Godot-AI-Tilemap.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Tilemap.Tests/Godot-AI-Tilemap.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.tilemap/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Tilemap`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` floors; **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump floors with `commands/update-core.ps1`.
