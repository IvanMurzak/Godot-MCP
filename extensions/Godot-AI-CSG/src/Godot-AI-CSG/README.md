# CSG Tools

RQL module for Godot CSG (constructive solid geometry): validated box / sphere / cylinder / torus dimensions and rootShape for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.CSG` (1.0.0).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.0` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create, combine and read CSG shapes
with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core
`godot-rql` skill. The module adds:

- **Validated dimensions** on the CSG primitives: CSGBox3D `Size` (every axis above 0), CSGSphere3D `Radius`
  (above 0), `RadialSegments` (at least 4), `Rings` (at least 1), CSGCylinder3D `Radius` / `Height` (above 0),
  `Sides` (at least 3), CSGTorus3D `Sides` / `RingSides` (at least 3). An invalid value fails the whole mutation
  and changes nothing (0.x clamped it).
- **`rootShape`** on every CSG shape: true for the shape that builds the combined mesh.

```
root | call AddChild("CSGCombiner3D", "Combo")
nodes | where name == "Combo" | call AddChild("CSGBox3D", "Box")
nodes | where name == "Box" | set Size = "(3, 3, 3)"
nodes | where name == "Combo" | call AddChild("CSGSphere3D", "Ball")
nodes | where name == "Ball" | set Radius = 1.8, RadialSegments = 16, Rings = 8, Operation = "Subtraction"
nodes | where name == "Combo" | call AddChild("CSGCylinder3D", "Pipe")
nodes | where name == "Pipe" | set Radius = 0.5, Height = 4, Sides = 12, Operation = "Union"
nodes | where $ is CSGShape3D | select name class Operation rootShape
```

Every write is one undoable action and marks the scene unsaved (`editor | call SaveScene()` writes it).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `csg-defaults` | removed: a new shape starts with Godot's defaults (box 1×1×1, sphere radius 0.5 / 12 segments, cylinder radius 0.5 / height 2); the guide lists them |
| `csg-box-create` | `rql-mutate` `root \| call AddChild("CSGBox3D", "Box")`, then `set Size = "(3, 3, 3)"` |
| `csg-sphere-create` | `rql-mutate` `… \| call AddChild("CSGSphere3D", "Ball")`, then `set Radius = 1.5, RadialSegments = 16` |
| `csg-cylinder-create` | `rql-mutate` `… \| call AddChild("CSGCylinder3D", "Pipe")`, then `set Radius = 1, Height = 4` |
| `csg-combiner-create` | `rql-mutate` `root \| call AddChild("CSGCombiner3D", "Combo")` (shapes go under it with `AddChild` / `Reparent`) |
| `csg-set-operation` | `rql-mutate` `nodes \| where name == "Ball" \| set Operation = "Subtraction"` |
| `csg-get` | `rql-query` `nodes \| where $ is CSGShape3D \| select name class Operation Size Radius Height RadialSegments` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-CSG/LICENSE).
Releasing another module does not rebuild or republish this package.
