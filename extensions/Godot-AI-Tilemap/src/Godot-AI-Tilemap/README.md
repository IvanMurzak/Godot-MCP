# Tilemap Tools

AI MCP tools for Godot TileMapLayer (4.3+): create layers, assign tilesets, set/erase cells, and inspect used cells.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Tilemap` (0.1.3).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Tilemap /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.3` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `tilemap-create` — Create a TileMapLayer node in the currently edited Godot scene.
- `tilemap-set-tileset` — Assign a TileSet resource to an existing TileMapLayer.
- `tilemap-set-cell` — Set a single cell on a TileMapLayer (by map + atlas coords).
- `tilemap-erase-cell` — Erase a single cell on a TileMapLayer (set it back to empty).
- `tilemap-get-used-cells` — List the used (non-empty) cells of a TileMapLayer (read-only).
- `tilemap-clear` — Clear all cells on a TileMapLayer (the assigned TileSet is kept).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Tilemap/LICENSE).
Releasing another module does not rebuild or republish this package.
