# YOUR_DISPLAY_NAME

YOUR_DESCRIPTION

A **source-only** MCP tool extension for [Godot-MCP / AI Game Developer](https://github.com/IvanMurzak/Godot-MCP).
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

After a rebuild, the extension's `[AiToolType]` tool families are auto-discovered — no registry edit.

## Tools

| Tool | Description |
| --- | --- |
| `YOUR_TOOL_PREFIX-echo` | Pure-managed readiness probe — echoes a message. |
| `YOUR_TOOL_PREFIX-editor-info` | Editor-only — returns the running Godot editor version + UI scale. |

License: Apache-2.0.
