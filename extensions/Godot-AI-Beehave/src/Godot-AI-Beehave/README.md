# Beehave Tools

AI MCP tools for the Godot Beehave addon (behaviour-tree AI): scaffold a tree root, add composites, decorators, and leaf placeholders, and inspect them.

Optional Godot-MCP extension, independently versioned as NuGet package `com.IvanMurzak.Godot.MCP.Beehave` (0.1.2).
Requires a Godot C# project (4.3.0+) with the core `godot_mcp` addon installed.

Also install the third-party **Beehave** addon. It is not bundled with this package.

## Install

```sh
godot-cli install-plugin /path/to/project
godot-cli install-extension com.IvanMurzak.Godot.MCP.Beehave /path/to/project
godot-cli build /path/to/project
```

The installer resolves the latest stable version from NuGet; use `--version 0.1.2` to request an exact version.
Packages contain C# source and build props, compiled against the project's own GodotSharp.
Previous NuGet versions remain installable after the repository migration.

## Tools

- `beehave-defaults` — Return the recommended starter skeleton (root, tick rate, a composite, and leaf placeholders) for a Beehave behaviour tree.
- `beehave-tree-create` — Create a BeehaveTree behaviour-tree root in the currently edited Godot scene.
- `beehave-add-composite` — Add a Selector/Sequence composite under an existing tree or composite.
- `beehave-add-decorator` — Add an Inverter/Limiter decorator under an existing tree or composite.
- `beehave-add-leaf` — Add an Action/Condition leaf placeholder under a composite or decorator.
- `beehave-get` — Read a BeehaveTree's scalar config and dump its tree structure (read-only).

## Development and releases

See the [monorepo test and release guide](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/EXTENSION-MONOREPO.md).
This module retains its own package, version, tests and Apache-2.0 [license](https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/Godot-AI-Beehave/LICENSE).
Releasing another module does not rebuild or republish this package.
