# Beehave Tools

RQL module for the Godot Beehave addon (behaviour trees): a beehave source that lists trees and their nodes with their Beehave classes; build trees with the core AddChild.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Beehave` (1.0.0).
Requires a Godot C# project (4.3.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Required prerequisite — install the Beehave addon yourself

This extension drives the **Beehave** addon but does **not** ship it. Before the module can do
anything, install Beehave into your own project:

- Install **Beehave** from the **Godot Asset Library**, or download the pinned release `v2.9.2` from the
  upstream repo [`bitbrain/beehave`](https://github.com/bitbrain/beehave/releases/tag/v2.9.2) and unzip it
  so the addon lives at `addons/beehave/`.
- **Enable it** under **Project Settings → Plugins**. Beehave registers GDScript `class_name` types
  (e.g. `BeehaveTree`) in the global script-class list; `beehave | select installed hint` reports whether it is there.

> **Attribution & disclaimer.** Beehave is © its authors under the **MIT** license; this extension is
> **not affiliated** with or endorsed by it. The addon is your project's own runtime dependency — this
> package never vendors, downloads, or redistributes it.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Beehave /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.0.0` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents build and read behaviour trees with the
core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core `godot-rql` skill.
Beehave classes are GDScript `class_name`s (no C# type; `class` shows the native base `Node`), so the module adds:

- **The `beehave` source**: `installed`, `hint` (how to install the addon when it is missing), and the Beehave nodes of
  the edited scene as **read-only rows**: `beehave.trees` (`name path scriptClass enabled tickRate processThread actor
  childCount nodeCount node`) and `beehave.nodes` (`name path scriptClass kind tree depth parent childCount node`).
- **Writes go through the row's `node`** (`get node | set props["…"]`, `call AddChild(…)`, `Free()` …): they are the
  core's writes, so they are undoable and mark the scene unsaved. Nodes are created with the core `AddChild`, which
  instantiates global script classes by name.

```
beehave | select installed hint
root | call AddChild("BeehaveTree", "BT")
godot.find("BT") | call AddChild("SelectorComposite", "Sel")
godot.find("BT/Sel") | call AddChild("InverterDecorator", "Inv")
godot.find("BT/Sel/Inv") | call AddChild("ActionLeaf", "Act")
beehave.trees | where name == "BT" | get node | set props["tick_rate"] = 2, props["enabled"] = true
beehave.trees | select name enabled tickRate processThread actor nodeCount
beehave.nodes | where tree == "BT" | select name scriptClass kind depth path
```

Without the addon the module still loads: `beehave | select installed hint` returns `false` and the install hint,
and the lists are empty. Save the scene with `editor | call SaveScene()`.

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `beehave-defaults` | removed: the guide lists the classes and a typical tree (BeehaveTree, SelectorComposite, condition + action leaves, `tick_rate` 1) |
| `beehave-tree-create` | `rql-mutate` `root \| call AddChild("BeehaveTree", "BT")`, then `… \| get node \| set props["tick_rate"] = 2, props["enabled"] = true, props["actor"] = "Player"` |
| `beehave-add-composite` | `rql-mutate` `godot.find("BT") \| call AddChild("SelectorComposite", "Sel")` (or `SequenceComposite` …) |
| `beehave-add-decorator` | `rql-mutate` `godot.find("BT/Sel") \| call AddChild("InverterDecorator", "Inv")` (or `LimiterDecorator` …) |
| `beehave-add-leaf` | `rql-mutate` `godot.find("BT/Sel/Inv") \| call AddChild("ActionLeaf", "Act")` (or `ConditionLeaf`) |
| `beehave-get` | `rql-query` `beehave.trees \| select name enabled tickRate actor nodeCount` and `beehave.nodes \| where tree == "BT" \| select name scriptClass kind depth path` |
| `Installed: false` results | `beehave \| select installed hint` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Beehave/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Beehave** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
