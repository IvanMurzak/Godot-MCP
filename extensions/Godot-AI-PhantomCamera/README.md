# PhantomCamera Tools

AI MCP tools for the Godot Phantom Camera addon (Cinemachine-style virtual cameras): create hosts and cameras, set follow/look-at targets and priority, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.PhantomCamera` (0.1.2).
Requires a Godot C# project (4.4.0+) with the core `godot_mcp` addon installed.

## Required prerequisite — Phantom Camera (install it yourself)

This extension **does NOT include** the Phantom Camera addon and does not download or vendor it. You must
install Phantom Camera into your own Godot project separately:

- Install **Phantom Camera** from the **Godot Asset Library**, or from
  **https://github.com/ramokz/phantom-camera** (tested against **v0.11.0.2**).
- Enable it under **Project Settings → Plugins**.

Without Phantom Camera installed and enabled, the editor tools below report `installed: false` and take no
action (by design).

> Phantom Camera is © its authors and distributed under the **MIT License**. This extension is **not
> affiliated with, endorsed by, or sponsored by** the Phantom Camera project — it merely provides AI tools
> that drive it. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.PhantomCamera /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `phantomcamera-defaults` — Return the recommended starter config (priority, follow/look-at mode, damping) for a Phantom Camera.
- `phantomcamera-host-create` — Ensure a PhantomCameraHost exists under a Camera3D in the edited scene (required by the addon).
- `phantomcamera-create` — Create a PhantomCamera3D node (a virtual camera) in the currently edited Godot scene.
- `phantomcamera-set-follow` — Set an existing PhantomCamera3D's follow mode and/or follow target.
- `phantomcamera-set-look-at` — Set an existing PhantomCamera3D's look-at mode and/or look-at target.
- `phantomcamera-set-priority` — Set an existing PhantomCamera3D's priority (higher wins; raising it switches the active camera).
- `phantomcamera-get` — Read an existing PhantomCamera3D's scalar config (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-PhantomCamera/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - PhantomCamera** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
