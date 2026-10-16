# CSG Tools

AI MCP tools for Godot CSG (Constructive Solid Geometry): create box/sphere/cylinder primitives and combiners, set boolean operations, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.CSG` (0.1.2).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `csg-defaults` — Return the recommended starter config (size / radius / height / segments) for a CSG node of the requested kind.
- `csg-box-create` — Create a CsgBox3D primitive in the currently edited Godot scene.
- `csg-sphere-create` — Create a CsgSphere3D primitive in the currently edited Godot scene.
- `csg-cylinder-create` — Create a CsgCylinder3D primitive in the currently edited Godot scene.
- `csg-combiner-create` — Create a CsgCombiner3D container that groups child CSG shapes for boolean ops.
- `csg-set-operation` — Set an existing CSG node's boolean operation (Union / Intersection / Subtraction).
- `csg-get` — Read a CSG node's scalar config (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-CSG/LICENSE).
Releasing another module does not rebuild or republish this package.
