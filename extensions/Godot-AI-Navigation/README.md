# Navigation Tools

RQL module for Godot navigation (2D & 3D): validated agent, region and link settings, region Bake() and polygon outlines for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Navigation` (1.0.0).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Navigation /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.0` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create, configure, bake and read
navigation nodes with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of
the core `godot-rql` skill. The module adds, for the 2D and 3D nodes:

- **Validated members** on NavigationAgent2D/3D: `Radius` (above 0), `PathDesiredDistance`,
  `TargetDesiredDistance`, `MaxSpeed`, `Height` (3D), `TimeHorizonAgents`, `TimeHorizonObstacles` (0 or more),
  `AvoidancePriority` (0 to 1); on NavigationRegion2D/3D and NavigationLink2D/3D: `EnterCost` / `TravelCost`
  (0 or more). An invalid value fails the whole mutation and changes nothing (0.x clamped it).
- **`Bake()`** on the regions (a synchronous bake of the NavigationMesh / NavigationPolygon, returning the
  polygon count) and **`polygonCount`**; **`AddOutline(points)`** / **`ClearOutlines()`** and `outlineCount` on
  NavigationPolygon.

```
root | call AddChild("NavigationRegion3D", "NavRegion")
nodes | where name == "NavRegion" | set NavigationMesh = {"$type": "NavigationMesh"}
nodes | where name == "NavRegion" | set NavigationMesh.GeometryParsedGeometryType = "StaticColliders", NavigationMesh.AgentRadius = 0.5
nodes | where name == "NavRegion" | call AddChild("StaticBody3D", "Floor")
nodes | where name == "Floor" | call AddChild("CollisionShape3D", "FloorShape")
nodes | where name == "FloorShape" | set Shape = {"$type": "BoxShape3D", "Size": "(10, 1, 10)"}
nodes | where name == "NavRegion" | call Bake()
nodes | where $ is NavigationRegion3D | select name Enabled polygonCount NavigationMesh
root | call AddChild("NavigationAgent3D", "NavAgent")
nodes | where name == "NavAgent" | set Radius = 0.75, MaxSpeed = 8, PathDesiredDistance = 1, TargetDesiredDistance = 1, AvoidanceEnabled = true
root | call AddChild("NavigationLink3D", "NavLink")
nodes | where name == "NavLink" | set StartPosition = "(0, 0, 0)", EndPosition = "(5, 0, 0)", Bidirectional = true
nodes | where $ is NavigationAgent3D | select name Radius MaxSpeed PathDesiredDistance AvoidanceEnabled
```

Every write is one undoable action and marks the scene unsaved (`editor | call SaveScene()` writes it); the
polygons a `Bake()` produces are not undoable (bake again).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `navigation-defaults` | removed: a new agent starts with Godot's defaults (3D radius 0.5 m, max speed 10 m/s; 2D radius 10 px, max speed 100 px/s); the guide lists them |
| `navigation-region-create` | `rql-mutate` `root \| call AddChild("NavigationRegion3D", "NavRegion")` (or `NavigationRegion2D`) |
| `navigation-region-set-mesh` | `rql-mutate` `… \| set NavigationMesh = {"$type": "NavigationMesh"}` (2D: `NavigationPolygon = {"$type": "NavigationPolygon"}`), or a `"res://…"` path; new: `call Bake()` |
| `navigation-agent-create` | `rql-mutate` `root \| call AddChild("NavigationAgent3D", "NavAgent")` (or `NavigationAgent2D`) |
| `navigation-agent-configure` | `rql-mutate` `nodes \| where name == "NavAgent" \| set Radius = 0.75, MaxSpeed = 8, AvoidanceEnabled = true` |
| `navigation-link-create` | `rql-mutate` `… \| call AddChild("NavigationLink3D", "NavLink")`, then `set StartPosition = "(0, 0, 0)", EndPosition = "(5, 0, 0)"` |
| `navigation-get` | `rql-query` `nodes \| where $ is NavigationAgent3D \| select name Radius MaxSpeed …` (regions: `Enabled polygonCount NavigationMesh`; links: `StartPosition EndPosition Bidirectional`) |

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
