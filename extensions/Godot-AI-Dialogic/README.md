# Dialogic Tools

AI MCP tools for the Godot Dialogic addon (dialogue / visual novels): create timeline and character resources, append text events, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Dialogic` (0.1.3).
Requires a Godot C# project (4.4.0+) with the core `godot_mcp` addon installed.

## Required prerequisite — Dialogic (install it yourself)

This extension **does NOT include** the Dialogic addon and does not download or vendor it. You must
install Dialogic into your own Godot project separately:

- Install **Dialogic** from the **Godot Asset Library**, or from
  **https://github.com/dialogic-godot/dialogic** (tested against **2.0-alpha-18 on Godot 4.4** and **2.0-alpha-20 on Godot 4.5+**).
- Enable it under **Project Settings → Plugins** (this also registers the `Dialogic` autoload).

Without Dialogic installed and enabled, the editor tools below report `installed: false` and take no
action (by design).

> Dialogic is © Emilio Coppola and contributors, distributed under the **MIT License**. This extension is
> **not affiliated with, endorsed by, or sponsored by** the Dialogic project — it merely provides AI tools
> that author its resources. See the addon's own repository for its licence and terms.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Dialogic /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.3` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `dialogic-defaults` — Return the recommended starter timeline and character config for Dialogic authoring.
- `dialogic-timeline-create` — Create a Dialogic timeline resource (.dtl), seeded with a first text event.
- `dialogic-character-create` — Create a Dialogic character resource (.dch) with a display name and color.
- `dialogic-timeline-add-text` — Append a text event (optionally with a speaker) to an existing Dialogic timeline (.dtl).
- `dialogic-get` — Read a Dialogic timeline (.dtl) or character (.dch) resource's config (read-only).

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
