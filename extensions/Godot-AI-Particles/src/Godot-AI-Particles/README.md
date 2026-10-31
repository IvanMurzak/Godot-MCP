# Particles Tools

RQL module for Godot GPU particles (2D & 3D): validated emitter settings and Restart() for the core rql-query / rql-mutate tools, plus an agent guide.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Particles` (1.1.1).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Particles /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.1.1` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create, tune and read emitters
with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core
`godot-rql` skill. The module adds:

- **Validated emitter members** on `GPUParticles3D` and `GPUParticles2D`: `Amount` (at least 1), `Lifetime`
  (seconds, above 0), `Explosiveness` and `Randomness` (0 to 1). An out-of-range value fails the whole mutation
  and changes nothing (0.x clamped it).
- **`Restart()`** as an RQL call.

```
root | call AddChild("GPUParticles3D", "FX")
nodes | where name == "FX" | set Amount = 32, Lifetime = 2, Explosiveness = 0.5, Randomness = 0.25, OneShot = false
nodes | where name == "FX" | set ProcessMaterial = {"$type": "ParticleProcessMaterial"}
nodes | where name == "FX" | set ProcessMaterial.props["initial_velocity_min"] = 2, ProcessMaterial.props["initial_velocity_max"] = 4
nodes | where name == "FX" | call Restart()
nodes | where name == "FX" | set Emitting = false
nodes | where $ is GPUParticles3D | select name Amount Lifetime Emitting Explosiveness ProcessMaterial
```

Every write is one undoable action and marks the scene unsaved (`editor | call SaveScene()` writes it).

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `particles-defaults` | removed: a new emitter starts with Godot's defaults; the guide lists typical values |
| `particles-create` | `rql-mutate` `root \| call AddChild("GPUParticles3D", "FX")` (or `GPUParticles2D`), then `set …` |
| `particles-configure` | `rql-mutate` `nodes \| where name == "FX" \| set Amount = 32, Lifetime = 2, …` |
| `particles-set-emitting` | `rql-mutate` `… \| set Emitting = false`; `… \| call Restart()` |
| `particles-get` | `rql-query` `nodes \| where $ is GPUParticles3D \| select name Amount Lifetime Emitting …` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Particles/LICENSE).
Releasing another module does not rebuild or republish this package.
