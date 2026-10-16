# Dialogic Tools

AI MCP tools for the Godot Dialogic addon (dialogue / visual novels): create timeline and character resources, append text events, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Dialogic` (0.1.2).
Requires a Godot C# project (4.4.0+) with the core `godot_mcp` addon installed.

Also install the third-party **Dialogic** addon. It is not bundled with this package.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Dialogic /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
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
