# GridMap Tools

RQL module for Godot GridMap (3D tile maps): validated settings, cell rows and SetCell / ClearCell / Clear for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.GridMap` (1.1.1).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.GridMap /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.1.1` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create GridMaps, assign their
MeshLibrary and paint cells with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide
is part of the core `godot-rql` skill. The module adds, on `GridMap`:

- **Validated settings**: `CellSize` (at least 0.001 on every axis) and `CellOctantSize` (at least 1). An invalid
  value fails the whole mutation and changes nothing (0.x clamped it).
- **Cells**: read-only `cells` rows (`x y z item itemName orientation`) and `cellCount`; calls
  `SetCell(x, y, z, item, orientation = 0)` (item 0 or more, orientation 0 to 23), `ClearCell(x, y, z)` and
  `Clear()`.

```
root | call AddChild("GridMap", "Grid")
nodes | where name == "Grid" | set MeshLibrary = "res://e2e-meshlib.tres", CellSize = "(1, 1, 1)"
nodes | where name == "Grid" | call SetCell(1, 0, 1, 0)
nodes | where name == "Grid" | call SetCell(2, 0, 1, 0, 10)
nodes | where name == "Grid" | get cells | select x y z item orientation
nodes | where name == "Grid" | call ClearCell(1, 0, 1)
nodes | where $ is GridMap | select name cellCount MeshLibrary
nodes | where name == "Grid" | call Clear()
```

Every write is one undoable action and marks the scene unsaved (`editor | call SaveScene()` writes it).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `gridmap-defaults` | removed: a new GridMap starts with Godot's defaults; the guide lists typical values |
| `gridmap-create` | `rql-mutate` `root \| call AddChild("GridMap", "Grid")`, then `… \| set CellSize = "(1, 1, 1)"` |
| `gridmap-set-mesh-library` | `rql-mutate` `nodes \| where name == "Grid" \| set MeshLibrary = "res://tiles.tres"` |
| `gridmap-set-cell` | `rql-mutate` `… \| call SetCell(1, 0, 1, 0)` (orientation as a fifth argument) |
| `gridmap-clear-cell` | `rql-mutate` `… \| call ClearCell(1, 0, 1)` |
| `gridmap-clear` | `rql-mutate` `… \| call Clear()` |
| `gridmap-get` | `rql-query` `nodes \| where $ is GridMap \| select name CellSize CellScale CellOctantSize MeshLibrary cellCount`; `… \| get cells` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-GridMap/LICENSE).
Releasing another module does not rebuild or republish this package.
