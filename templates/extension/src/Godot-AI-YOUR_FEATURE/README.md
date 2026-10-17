# YOUR_DISPLAY_NAME

YOUR_DESCRIPTION

A **source-only** RQL-module extension for [Godot-MCP / AI Game Developer](https://github.com/IvanMurzak/Godot-MCP).
The package ships C# source (no compiled DLL, no bundled Godot) that compiles inside your Godot project
against your own GodotSharp, so it never locks you to a Godot version.

## Install

Requires the core [`godot_mcp`](https://github.com/IvanMurzak/Godot-MCP) addon in your Godot C# project.

```bash
# via the godot-cli (resolves from the shared catalog, edits your .csproj, rebuilds)
godot-cli install-extension com.IvanMurzak.Godot.MCP.YOUR_FEATURE

# …or add the reference manually and rebuild:
#   <PackageReference Include="com.IvanMurzak.Godot.MCP.YOUR_FEATURE" Version="0.1.0" />
```

…or pick it from the **Extensions** dock inside the Godot editor.

After a rebuild, the core discovers the extension's RQL module (`IRqlModule`) in the project assembly — no
registry edit. The extension adds no MCP tools: agents use it through the core `rql-query` / `rql-mutate` /
`rql-schema` tools, and its guide is part of the core `godot-rql` skill.

## RQL

```
YOUR_SOURCE | select godotVersion editorScale
YOUR_SOURCE | call Echo("ci-roundtrip")
```

License: Apache-2.0.
