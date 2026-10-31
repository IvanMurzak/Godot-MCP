# Dialogic Tools

RQL module for the Godot Dialogic addon: a dialogic source that lists timelines and characters, creates them and appends timeline text.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Dialogic` (1.1.1).
Requires a Godot C# project (4.4.0+) with a core `godot_mcp` addon that has the RQL tools
(`rql-query` / `rql-mutate` / `rql-schema`, the release after 0.28.0).

## Required prerequisite — Dialogic (install it yourself)

This extension **does NOT include** the Dialogic addon and does not download or vendor it. You must
install Dialogic into your own Godot project separately:

- Install **Dialogic** from the **Godot Asset Library**, or from
  **https://github.com/dialogic-godot/dialogic** (tested against **2.0-alpha-18 on Godot 4.4** and **2.0-alpha-20 on Godot 4.5+**).
- Enable it under **Project Settings → Plugins** (this also registers the `Dialogic` autoload).

Without Dialogic installed and enabled, `dialogic | select installed hint` reports `false` with an install hint,
the `dialogic` lists are empty and its calls fail with that hint (by design).

> Dialogic is © Emilio Coppola and contributors, distributed under the **MIT License**. This extension is
> **not affiliated with, endorsed by, or sponsored by** the Dialogic project — it merely provides an RQL module
> that authors its resources. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Dialogic /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 1.1.1` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## RQL

Since 1.0.0 the extension is an **RQL module**: it adds no MCP tools. Agents create and read Dialogic timelines and
characters with the core `rql-query` / `rql-mutate` / `rql-schema` tools, and the module's guide is part of the core
`godot-rql` skill. Dialogic stores dialogue in resource files whose classes are GDScript `class_name`s (the core
`editor | call CreateResource` cannot make them), so the module adds:

- **The `dialogic` source**: `installed`, `hint` (how to install the addon when it is missing), the project's files as
  **read-only rows** (`dialogic.timelines`: `path name eventCount text resource`; `dialogic.characters`: `path name
  displayName color description resource`; `res://addons` and hidden folders are skipped), and the calls
  `CreateTimeline(path, text?, speaker?)` and `CreateCharacter(path, displayName, color?, description?)` (validated:
  `.dtl` / `.dch` paths, an existing file, a hex color, a display name).
- **`AddText(text, speaker?)`** on a timeline row appends a text line (a timeline is its text).
- **Character writes go through the row's `resource`** (`get resource | set props["…"]`): the core writes the
  property and saves the `.dch` with Dialogic's own saver.

Creating and appending write the files directly and are **not undoable** (like `CreateResource`).

```
dialogic | select installed hint
dialogic | call CreateTimeline("res://e2e_intro.dtl", "Hello from CI", "Narrator")
dialogic | call CreateCharacter("res://e2e_hero.dch", "Hero", "#e91e63", "The brave protagonist.")
dialogic.timelines | where name == "e2e_intro" | call AddText("A second line", "Hero")
dialogic.characters | where name == "e2e_hero" | get resource | set props["description"] = "The bravest hero."
dialogic.timelines | select path eventCount text
dialogic.characters | select path displayName color description
```

Without the addon the module still loads: `dialogic | select installed hint` returns `false` and the install hint,
the lists are empty and the calls fail with the hint.

### Migrating from 0.x

| 0.x tool | RQL |
| --- | --- |
| `dialogic-defaults` | removed: `CreateTimeline(path)` without text writes the two-line starter; the guide lists typical values ("Narrator", color "#3aa0ff") |
| `dialogic-timeline-create` | `rql-mutate` `dialogic \| call CreateTimeline("res://intro.dtl", "Hello", "Narrator")` (now fails if the file exists) |
| `dialogic-character-create` | `rql-mutate` `dialogic \| call CreateCharacter("res://hero.dch", "Hero", "#e91e63", "…")` (an invalid color now fails instead of falling back) |
| `dialogic-timeline-add-text` | `rql-mutate` `dialogic.timelines \| where name == "intro" \| call AddText("A second line", "Hero")` |
| `dialogic-get` | `rql-query` `dialogic.timelines \| select path eventCount text` / `dialogic.characters \| select path displayName color description` |
| `Installed: false` results | `dialogic \| select installed hint` |

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Dialogic/LICENSE).
Releasing another module does not rebuild or republish this package.

## Asset Library installation

The independent Asset Library entry is named **Godot MCP - Dialogic** and becomes public after moderator approval.
It downloads only this extension from its versioned GitHub release. Install Godot MCP core
and configure its NuGet dependencies first, then install the archive into the project and build C#.
The archive includes its own license, README and installation instructions. Third-party addon
prerequisites listed above still apply. Use either this source installation or this extension
NuGet package in a project; installing both creates duplicate C# types.

See the [Asset Library release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md#godot-asset-library).
