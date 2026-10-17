# Navigation Tools

AI MCP tools for Godot navigation (2D & 3D): create regions, agents, and links, set region meshes, configure agents, and inspect navigation nodes.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Navigation` (0.1.3).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Navigation /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.3` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `navigation-defaults` — Return the recommended starter config (radius, distances, max speed) for a 2D/3D NavigationAgent.
- `navigation-region-create` — Create a NavigationRegion2D/NavigationRegion3D node (a navigable area) in the edited scene.
- `navigation-region-set-mesh` — Assign a region's navigation resource (NavigationPolygon in 2D, NavigationMesh in 3D).
- `navigation-agent-create` — Create a NavigationAgent2D/NavigationAgent3D node (pathfinding + avoidance) in the edited scene.
- `navigation-agent-configure` — Update a NavigationAgent's scalar properties (clamped to valid ranges).
- `navigation-link-create` — Create a NavigationLink2D/NavigationLink3D node (an off-mesh connection between two points).
- `navigation-get` — Read a navigation node's scalar config (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Navigation/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Navigation** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
