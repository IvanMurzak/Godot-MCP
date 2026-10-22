# Tilemap Tools

RQL module for Godot TileMapLayer (4.3+): used-cell rows and SetCell / EraseCell / Clear for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Tilemap` (1.0.1).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Tilemap /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.1` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create TileMapLayers, assign their
TileSet and paint cells with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is
part of the core `godot-rql` skill. The module adds, on `TileMapLayer`:

- **Cells**: read-only `usedCells` rows (`x y sourceId atlasX atlasY alternative`) and `usedCellCount`.
- **Calls**: `SetCell(x, y, sourceId, atlasX, atlasY, alternative = 0)` (source id, atlas coordinates and
  alternative validated: 0 or more), `EraseCell(x, y)` and `Clear()`. A rejected value changes nothing.

```
root | call AddChild("TileMapLayer", "Layer")
nodes | where name == "Layer" | set TileSet = "res://e2e-tileset.tres"
nodes | where name == "Layer" | call SetCell(0, 0, 0, 0, 0)
nodes | where name == "Layer" | call SetCell(1, 0, 0, 2, 1, 1)
nodes | where name == "Layer" | get usedCells | select x y sourceId atlasX atlasY alternative
nodes | where name == "Layer" | call EraseCell(0, 0)
nodes | where $ is TileMapLayer | select name usedCellCount TileSet
nodes | where name == "Layer" | call Clear()
```

Every write is one undoable action and marks the scene unsaved (`editor | call SaveScene()` writes it).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `tilemap-create` | `rql-mutate` `root \| call AddChild("TileMapLayer", "Layer")` |
| `tilemap-set-tileset` | `rql-mutate` `nodes \| where name == "Layer" \| set TileSet = "res://tiles.tres"` |
| `tilemap-set-cell` | `rql-mutate` `… \| call SetCell(0, 0, 0, 0, 0)` (x, y, sourceId, atlasX, atlasY[, alternative]) |
| `tilemap-erase-cell` | `rql-mutate` `… \| call EraseCell(0, 0)` |
| `tilemap-get-used-cells` | `rql-query` `… \| get usedCells \| select x y sourceId atlasX atlasY`; `… \| select usedCellCount TileSet` |
| `tilemap-clear` | `rql-mutate` `… \| call Clear()` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Tilemap/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Tilemap** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
