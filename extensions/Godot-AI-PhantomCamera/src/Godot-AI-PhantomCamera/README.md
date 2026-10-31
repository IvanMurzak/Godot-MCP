# PhantomCamera Tools

RQL module for the Godot Phantom Camera addon: a phantomcamera source that lists virtual cameras and hosts with their modes by name, and validated SetFollow / SetLookAt calls.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.PhantomCamera` (1.1.1).
Requires a Godot C# project (4.4.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Required prerequisite — Phantom Camera (install it yourself)

This extension **does NOT include** the Phantom Camera addon and does not download or vendor it. You must
install Phantom Camera into your own Godot project separately:

- Install **Phantom Camera** from the **Godot Asset Library**, or from
  **https://github.com/ramokz/phantom-camera** (tested against **v0.11.0.2**).
- Enable it under **Project Settings → Plugins**.

Without Phantom Camera installed and enabled, `phantomcamera | select installed hint` reports `false` with an install
hint and the `phantomcamera` lists are empty (by design).

> Phantom Camera is © its authors and distributed under the **MIT License**. This extension is **not
> affiliated with, endorsed by, or sponsored by** the Phantom Camera project — it merely provides an RQL module
> that drives it. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.PhantomCamera /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.1.1` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents set up and read virtual cameras with the
core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core `godot-rql` skill.
Phantom Camera classes are GDScript `class_name`s (no C# type; `class` shows the native base `Node3D` / `Node2D`), so
the module adds:

- **The `phantomcamera` source**: `installed`, `hint` (how to install the addon when it is missing), and the addon's
  nodes in the edited scene as **read-only rows**: `phantomcamera.cameras` (`name path scriptClass priority followMode
  followTarget lookAtMode lookAtTarget followDamping lookAtDamping node`, modes by name) and `phantomcamera.hosts`
  (`name path camera node`).
- **Writes go through the row's `node`** (`get node | set props["priority"] = 50`): the core's writes, undoable, mark
  the scene unsaved. Cameras, hosts and the Camera3D are created with the core `AddChild`.
- **Validated mode calls** on a camera row: `SetFollow(mode, target?)` and `SetLookAt(mode, target?)` take the mode by
  name and set the target after it (the addon silently ignores a target set while the mode is None). An unknown
  mode, ThirdPerson / look-at on a PhantomCamera2D, a single target for None / Group, or a target of the wrong
  dimension fails and changes nothing. They are calls on the row, not on the node, so they are **not undoable** (they
  mark the scene unsaved themselves); the undoable alternative is `get node | set props["follow_mode"] = 2, …`.

```
phantomcamera | select installed hint
root | call AddChild("Camera3D", "Camera3D")
godot.find("Camera3D") | call AddChild("PhantomCameraHost", "PhantomCameraHost")
root | call AddChild("Node3D", "Player")
root | call AddChild("PhantomCamera3D", "PCam")
phantomcamera.cameras | where name == "PCam" | call SetFollow("Simple", "Player")
phantomcamera.cameras | where name == "PCam" | call SetLookAt("Simple", "Player")
phantomcamera.cameras | where name == "PCam" | get node | set props["priority"] = 50, props["follow_damping"] = true
phantomcamera.cameras | select name scriptClass priority followMode followTarget lookAtMode lookAtTarget followDamping
phantomcamera.hosts | select name path camera
```

Without the addon the module still loads: `phantomcamera | select installed hint` returns `false` and the install
hint, and the lists are empty. Save the scene with `editor | call SaveScene()`.

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `phantomcamera-defaults` | removed: the guide lists typical values (priority 10, follow / look-at mode Simple, damping on) |
| `phantomcamera-host-create` | `rql-mutate` `root \| call AddChild("Camera3D", "Camera3D")`, then `godot.find("Camera3D") \| call AddChild("PhantomCameraHost", "PhantomCameraHost")` |
| `phantomcamera-create` | `rql-mutate` `root \| call AddChild("PhantomCamera3D", "PCam")`, then `… \| get node \| set props["priority"] = 20` |
| `phantomcamera-set-follow` | `rql-mutate` `phantomcamera.cameras \| where name == "PCam" \| call SetFollow("Simple", "Player")` |
| `phantomcamera-set-look-at` | `rql-mutate` `phantomcamera.cameras \| where name == "PCam" \| call SetLookAt("Simple", "Player")` |
| `phantomcamera-set-priority` | `rql-mutate` `phantomcamera.cameras \| where name == "PCam" \| get node \| set props["priority"] = 50` |
| `phantomcamera-get` | `rql-query` `phantomcamera.cameras \| select name priority followMode followTarget lookAtMode lookAtTarget` |
| `Installed: false` results | `phantomcamera \| select installed hint` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-PhantomCamera/LICENSE).
Releasing another module does not rebuild or republish this package.
