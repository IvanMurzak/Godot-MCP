# PhantomCamera Tools

AI MCP tools for the Godot Phantom Camera addon (Cinemachine-style virtual cameras): create hosts and cameras, set follow/look-at targets and priority, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.PhantomCamera` (0.1.2).
Requires a Godot C# project (4.4.0+) with the core `godot_mcp` addon installed.

Also install the third-party **Phantom Camera** addon. It is not bundled with this package.

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
