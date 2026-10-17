# GridMap Tools

AI MCP tools for Godot GridMap (3D tile-based maps): create, set/clear cells, assign a MeshLibrary, and inspect.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.GridMap` (0.1.3).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.GridMap /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.3` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `gridmap-defaults` — Return the recommended starter configuration for a new GridMap node.
- `gridmap-create` — Create a GridMap node in the currently edited Godot scene.
- `gridmap-set-mesh-library` — Assign a MeshLibrary resource to an existing GridMap node.
- `gridmap-set-cell` — Set a single cell of a GridMap to a MeshLibrary item.
- `gridmap-clear-cell` — Clear a single cell of a GridMap.
- `gridmap-clear` — Clear all cells of a GridMap.
- `gridmap-get` — Read a GridMap's scalar config (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-GridMap/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - GridMap** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
