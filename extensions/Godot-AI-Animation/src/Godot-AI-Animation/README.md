# Animation Tools

RQL module for Godot AnimationPlayer: libraries, animations, tracks and keys for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Animation` (1.0.0).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Animation /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.0` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create players, libraries,
animations, tracks and keys with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide
is part of the core `godot-rql` skill. The module adds:

- **AnimationPlayer / AnimationTree** (any `AnimationMixer`): the read-only maps `libraries["name"]` (`""` = the
  global library) and `animations["library/name"]`; calls `AddLibrary(name)` and
  `CreateAnimation(name, length = 1, loopMode = "none", library = "")` (adds the library when it is missing).
- **AnimationLibrary**: `animations["name"]` and `CreateAnimation(name, length = 1, loopMode = "none")`.
- **Animation**: a validated `Length` (at least 0.001 s), read-only `tracks` rows (`index kind path keyCount`) and
  `trackCount`; calls `AddTrack(kind, path)` (`value`, `position-3d`, `rotation-3d`, `scale-3d`; returns the
  index), `InsertKey(track, time, value)` for value tracks and `InsertTransformKey(track, time, "(x, y, z)")` for
  the 3D transform tracks (rotation as Euler degrees).

```
root | call AddChild("Node3D", "Mover")
root | call AddChild("AnimationPlayer", "Player")
nodes | where name == "Player" | call AddLibrary("")
nodes | where name == "Player" | get libraries[""] | call CreateAnimation("Idle", 2, "linear")
nodes | where name == "Player" | call CreateAnimation("Walk", 1, "none", "Extra")
nodes | where name == "Player" | get animations["Idle"] | call AddTrack("value", "Mover:position:x")
nodes | where name == "Player" | get animations["Idle"] | call InsertKey(0, 0.5, 1.5)
nodes | where name == "Player" | get animations["Idle"] | call AddTrack("rotation-3d", "Mover")
nodes | where name == "Player" | get animations["Idle"] | call InsertTransformKey(1, 1, "(0, 90, 0)")
nodes | where name == "Player" | get animations["Idle"] | set Length = 1.5, LoopMode = "Pingpong"
nodes | where name == "Player" | get animations["Idle"].tracks | select index kind path keyCount
nodes | where $ is AnimationPlayer | select name animations.keys() libraries.keys()
```

Every write is one undoable action (it changes the animation, library or player it is called on) and marks the
scene unsaved (`editor | call SaveScene()` writes it). One exception: `CreateAnimation` on the player into a
library that already exists changes only that library, so it is not undoable; call it on the library
(`get libraries["…"] | call CreateAnimation(…)`) instead. Invalid values (a length below 0.001, a negative key
time, a missing track, a name with `/`, `:`, `,` or `[`, an existing name) fail the call and change nothing
(0.x clamped lengths and key times).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `animation-defaults` | removed: typical values (length 1 s, loop mode none) are the call defaults and in the guide |
| `animation-player-create` | `rql-mutate` `root \| call AddChild("AnimationPlayer", "Player")` |
| `animation-library-add` | `rql-mutate` `nodes \| where name == "Player" \| call AddLibrary("Extra")` |
| `animation-create` | `rql-mutate` `… \| get libraries[""] \| call CreateAnimation("Idle", 2, "linear")` or `… \| call CreateAnimation("Idle", 2, "linear", "")` on the player |
| `animation-add-track` | `rql-mutate` `… \| get animations["Idle"] \| call AddTrack("value", "Mover:position:x")` |
| `animation-insert-key` | `rql-mutate` `… \| get animations["Idle"] \| call InsertKey(0, 0.5, 1.5)`; transform tracks: `call InsertTransformKey(1, 1, "(0, 90, 0)")` |
| `animation-get` | `rql-query` `nodes \| where $ is AnimationPlayer \| select name animations.keys() libraries.keys()`; `… \| get animations["Idle"] \| select Length LoopMode tracks` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Animation/LICENSE).
Releasing another module does not rebuild or republish this package.
