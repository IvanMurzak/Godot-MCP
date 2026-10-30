# Terrain3D Tools

RQL module for the Terrain3D addon (TokisanGames heightmap terrain): terrain3d source with validated ChangeRegionSize, addon check and material creation for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Terrain3D` (1.0.2).
Requires a Godot C# project (4.4.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Required prerequisite — Terrain3D (install it yourself)

This extension **does NOT include** the Terrain3D addon and does not download or vendor it. You must
install Terrain3D into your own Godot project separately:

- Install **Terrain3D** from the **Godot Asset Library** (search "Terrain3D"), or from
  **https://github.com/TokisanGames/Terrain3D** (tested against **v1.0.2-stable**). Terrain3D ships
  **precompiled binaries** in its release — no C++ build is required.
- Unzip it into **`addons/terrain_3d/`**, **restart Godot** (a GDExtension only loads on editor
  startup), then enable it under **Project Settings → Plugins**.

Without Terrain3D installed and enabled, the `terrain3d` source is empty and `terrain3d.addon | select installed hint`
says how to install it (by design).

> Terrain3D is © its authors and distributed under the **MIT License**. This extension is
> **not affiliated with, endorsed by, or sponsored by** the Terrain3D project — it merely provides AI
> tools that drive it. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Terrain3D /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create, configure and read terrains
with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core
`godot-rql` skill. Terrain3D is a GDExtension class without a C# type (its nodes are `Node3D` in RQL, `class ==
"Terrain3D"`), so the module adds sources and a function rather than adapters:

- **`terrain3d`**: one row per Terrain3D node of the edited scene, with `node`, `name`, `path`, `regionSize`,
  `dataDirectory`, `material`, `regionCount`, `meshLods`, `meshSize`, `vertexSpacing`, `version`, and
  **`ChangeRegionSize(size)`** (64, 128, 256, 512, 1024 or 2048; any other size fails and changes nothing, 0.x
  snapped it; not undoable, the size lives in the region data).
- **`terrain3d.addon`**: `installed` / `hint`, whether the Terrain3D GDExtension is loaded.
- **`terrain3d.newMaterial()`**: a new Terrain3DMaterial (made through ClassDB) to assign to a terrain.

Write a terrain's properties on its node (`get node | set props["…"] = …`): those writes are undoable and mark
the scene unsaved (`editor | call SaveScene()` writes it, and Terrain3D saves its regions to `data_directory`).

```
terrain3d.addon | select installed hint
root | call AddChild("Terrain3D", "Terrain")
terrain3d | select name regionSize dataDirectory meshLods meshSize vertexSpacing version
terrain3d | where name == "Terrain" | call ChangeRegionSize(1024)
terrain3d | where name == "Terrain" | get node | set props["data_directory"] = "res://terrain_data"
terrain3d | where name == "Terrain" | get node | set props["material"] = terrain3d.newMaterial()
terrain3d | where name == "Terrain" | get node | set props["mesh_lods"] = 8, props["vertex_spacing"] = 2
terrain3d | select name regionSize dataDirectory material.class regionCount meshLods vertexSpacing
```

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `terrain3d-defaults` | removed: typical values (region size 256 to 1024, `mesh_lods` 7, `mesh_size` 48, `vertex_spacing` 1, `res://terrain_data`) are in the guide |
| `terrain3d-create` | `rql-mutate` `root \| call AddChild("Terrain3D", "Terrain")`, then `call ChangeRegionSize(…)` / `set props["data_directory"] = …` |
| `terrain3d-set-data-directory` | `rql-mutate` `terrain3d \| where name == "Terrain" \| get node \| set props["data_directory"] = "res://terrain_data"` |
| `terrain3d-set-region-size` | `rql-mutate` `terrain3d \| where name == "Terrain" \| call ChangeRegionSize(512)` (an exact size: no more snapping) |
| `terrain3d-set-material` | `rql-mutate` `terrain3d \| where name == "Terrain" \| get node \| set props["material"] = terrain3d.newMaterial()` |
| `terrain3d-get` | `rql-query` `terrain3d \| select name regionSize dataDirectory material.class regionCount version`; addon presence: `terrain3d.addon \| select installed hint` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Terrain3D/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Terrain3D** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
