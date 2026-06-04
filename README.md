# Godot-MCP

**Model Context Protocol (MCP) integration for the [Godot Engine](https://godotengine.org/).**
The Godot counterpart of [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) — it lets AI
assistants drive the Godot Editor through MCP tools written in C#.

> **Status:** early scaffold. The addon boots in the editor and logs a startup line; the MCP
> connection and tools are landing in follow-up tasks.

## Requirements

- **Godot 4.3+** (C# / .NET edition, Godot.NET.Sdk `4.3.0`)
- **.NET 8 SDK**

## What it is

Godot-MCP is a Godot **editor addon** (`addons/godot_mcp/`) backed by a `Godot.NET.Sdk` C#
project. On editor load, a `[Tool]` `EditorPlugin` boots the plugin. Once complete it will
expose Godot Editor operations as MCP tools and connect to **[ai-game.dev](https://ai-game.dev)**
— the same cloud backend Unity-MCP uses — so an AI agent can build and edit Godot games.

## Reused libraries

It does **not** fork the MCP/reflection stack — those are shared with Unity-MCP and pulled
from [nuget.org](https://www.nuget.org/) as `PackageReference`s:

| Package | Version | Role |
| --- | --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | `5.3.1` | Reflection / serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | `6.5.5` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`) |

## Building

`Godot.NET.Sdk` is a NuGet SDK, so a Godot binary is **not** required to compile:

```bash
dotnet restore
dotnet build
```

To run the addon, open the project in Godot 4.3+, then enable **Godot-MCP** under
*Project → Project Settings → Plugins*.

## License

[Apache-2.0](LICENSE) © Ivan Murzak
