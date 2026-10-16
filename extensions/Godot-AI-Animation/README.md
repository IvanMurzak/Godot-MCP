# Animation Tools

AI MCP tools for Godot AnimationPlayer: create players, libraries, and animations, add tracks, insert keyframes, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Animation` (0.1.2).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Animation /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `animation-defaults` — Return the recommended starter config (length, loop mode) for a new animation.
- `animation-player-create` — Create an AnimationPlayer node in the currently edited Godot scene.
- `animation-library-add` — Add a new empty AnimationLibrary to an existing AnimationPlayer.
- `animation-create` — Create an Animation in an AnimationPlayer's library (auto-created when missing).
- `animation-add-track` — Add a value or 3D transform track to an existing Animation.
- `animation-insert-key` — Insert a keyframe on an animation track.
- `animation-get` — Read an AnimationPlayer's libraries, animations, and a clip's tracks (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Animation/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Animation** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
