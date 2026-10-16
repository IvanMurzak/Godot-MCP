# Terrain3D Tools

AI MCP tools for the Godot Terrain3D addon (TokisanGames heightmap terrain): create terrains, set the data directory, region size, and material, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Terrain3D` (0.1.2).
Requires a Godot C# project (4.4.0+) with the core `godot_mcp` addon installed.

## Required prerequisite — Terrain3D (install it yourself)

This extension **does NOT include** the Terrain3D addon and does not download or vendor it. You must
install Terrain3D into your own Godot project separately:

- Install **Terrain3D** from the **Godot Asset Library** (search "Terrain3D"), or from
  **https://github.com/TokisanGames/Terrain3D** (tested against **v1.0.2-stable**). Terrain3D ships
  **precompiled binaries** in its release — no C++ build is required.
- Unzip it into **`addons/terrain_3d/`**, **restart Godot** (a GDExtension only loads on editor
  startup), then enable it under **Project Settings → Plugins**.

Without Terrain3D installed and enabled, the editor tools below report `installed: false` and take no
action (by design).

> Terrain3D is © its authors and distributed under the **MIT License**. This extension is
> **not affiliated with, endorsed by, or sponsored by** the Terrain3D project — it merely provides AI
> tools that drive it. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Terrain3D /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `terrain3d-defaults` — Return the recommended starter config (region size, mesh LODs, mesh size, vertex spacing, data directory) for a Terrain3D node.
- `terrain3d-create` — Create a Terrain3D node in the currently edited Godot scene (requires the Terrain3D addon).
- `terrain3d-set-data-directory` — Set an existing Terrain3D node's res:// data directory (where region data persists).
- `terrain3d-set-region-size` — Set an existing Terrain3D node's region size (snapped to a valid Terrain3D size).
- `terrain3d-set-material` — Assign a Terrain3DMaterial to an existing Terrain3D node (created when missing).
- `terrain3d-get` — Read a Terrain3D node's scalar config (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Terrain3D/LICENSE).
Releasing another module does not rebuild or republish this package.
