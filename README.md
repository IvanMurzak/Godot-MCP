# Godot-MCP

**Model Context Protocol (MCP) integration for the [Godot Engine](https://godotengine.org/).**

[![License](https://img.shields.io/badge/license-Apache--2.0-blue.svg)](LICENSE)

Godot-MCP is the Godot counterpart of [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP):
a C# editor addon that exposes Godot Editor operations as **AI Tools** and connects them to an
MCP server, so an AI assistant can inspect and drive your Godot project — create nodes, edit
scenes, manage resources and scripts, capture screenshots, and more — through the same cloud
backend ([ai-game.dev](https://ai-game.dev)) that powers Unity-MCP.

## Requirements

- **Godot 4.3+** — the C# / .NET (mono) edition. The addon csproj pins `Godot.NET.Sdk/4.3.0`
  as its minimum floor; newer 4.x editors work.
- **.NET 8 SDK** (`net8.0`).

## What it is

Godot-MCP is a Godot **editor addon** (`addons/godot_mcp/`) backed by a `Godot.NET.Sdk` C#
project. On editor load, a `[Tool]` `EditorPlugin` (`GodotMcpPlugin`) boots the plugin: it
installs a main-thread dispatcher, builds a [ReflectorNet](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet)
`Reflector` with Godot type converters, and opens a SignalR connection to an MCP server over the
reused [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin)
client. The AI tools it registers are then callable by any MCP-aware AI agent.

The MCP / reflection stack is **not forked** — it is shared with Unity-MCP and consumed from
[nuget.org](https://www.nuget.org/) as `PackageReference`s. The pins are owned by the upstream
release pipelines; this repo never bumps or vendors them.

## Install

There are two things to install: the **addon** (the plugin files) and the two **NuGet packages**
the addon's C# depends on. Godot compiles *every* `.cs` under your project into one assembly, so
your project's `.csproj` must declare the same NuGet references the addon needs — otherwise the
addon's C# will not compile.

### 1. Add the addon

Either install **Godot-MCP** from the Godot Asset Library, or copy the `addons/godot_mcp/` folder
from this repository into your Godot C# project's `addons/` directory.

Then enable it: **Project → Project Settings → Plugins → Godot-MCP → Enable**. On a successful
load the editor Output panel prints:

```
[Godot-MCP] plugin loaded
```

### 2. Add the NuGet packages

Add both `PackageReference`s to your project's `.csproj` (use these exact pinned versions — they
must match the addon's `Godot-MCP.csproj`):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.3.1" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="6.5.5" />
</ItemGroup>
```

| Package | Version | Role |
| --- | --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | `5.3.1` | Reflection / serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | `6.5.5` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`) |

Run `dotnet restore` so the packages land in your NuGet cache, then build. **No manual DLL copying
is required** — at editor runtime the addon's assembly resolver locates the DLLs in your NuGet
global-packages folder by reading the build's `*.deps.json`. (If you prefer self-contained output,
set `<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` so the DLLs are copied beside
your project assembly instead.)

## Connect

The plugin connects to an MCP server in one of two modes. The mode and its URL / token can be set
in the serialized config or overridden at process start with environment variables (handy for CI,
headless runs, and local dev). All variable names are the Godot analog of Unity-MCP's `UNITY_MCP_*`.

### Cloud mode (default) — ai-game.dev

In **Cloud** mode the plugin connects to the hosted backend at `https://ai-game.dev` (the `/mcp`
hub path is appended automatically). This is the default `connectionMode`.

| Environment variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Force the mode: `Cloud` or `Custom` (case-insensitive). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | Override the cloud base URL. A trailing `/mcp` is stripped if present; a non-http(s) value falls back to the default. | `https://ai-game.dev` |
| `GODOT_MCP_TOKEN` | Bearer token, routed to the active mode's token. Surrounding quotes are trimmed. | (none) |

### Custom mode — your own server

In **Custom** mode the plugin connects to a server URL you supply (a local dev server, a
self-hosted instance, etc.).

| Environment variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Set to `Custom` to select this mode. | `Cloud` |
| `GODOT_MCP_HOST` | The custom server URL. Must be an absolute http(s) URL or it falls back to the default. | `http://localhost:8080` |
| `GODOT_MCP_TOKEN` | Bearer token (only needed if the server requires authorization). | (none) |

Example — boot the editor pointed at a local server:

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:5300
# export GODOT_MCP_TOKEN=...   # only if the server enforces auth
```

The active mode always recomputes from the environment, so a process-level override wins over the
serialized config without editing any file.

## Tools

Godot-MCP groups its AI tools into **10 families**. Tool names mirror Unity-MCP where sensible
(`scene-*`, `node-*`, …). Every tool returns a structured, ReflectorNet-serialized result (or a
PNG image for screenshots).

| Family | Tools | What it does |
| --- | --- | --- |
| **ping** | `ping` | Lightweight readiness probe — echoes a message back, or returns `pong`. Verifies the end-to-end MCP path (editor → SignalR → tool dispatch). |
| **node** | `node-find`, `node-create`, `node-modify`, `node-set-parent`, `node-duplicate`, `node-delete` | Inspect and edit the active scene tree (the Godot analog of Unity GameObjects), driving `EditorInterface` on the main thread. |
| **scene** | `scene-open`, `scene-save`, `scene-create`, `scene-list-opened`, `scene-get-data` | Open, save, create, and inspect Godot scenes (`res://*.tscn` PackedScenes) in the editor. |
| **resource** | `resource-find`, `resource-get-data`, `resource-modify`, `resource-create`, `resource-move`, `resource-delete` | Find and mutate Godot resources (`.tres`/`.res`) through `ResourceLoader`/`ResourceSaver`/`EditorFileSystem`, keeping `.import` sidecars consistent. |
| **filesystem** | `filesystem-list`, `filesystem-reimport` | Browse and reimport the project's `res://` tree via the editor `EditorFileSystem` index (file types + uids without loading resources). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node` | CRUD on C# (`.cs`) and GDScript (`.gd`) files, plus attaching a script to a node. (No dynamic code execution — that is intentionally out of scope.) |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Capture the editor viewport, a specific camera, or an isolated node render, returned as a PNG image the LLM can inspect. |
| **editor** | `editor-application-get-state`, `editor-application-set-state`, `editor-selection-get`, `editor-selection-set` | Read/drive the editor run-and-play lifecycle (Godot launches the game in a separate process) and the current selection. |
| **console** | `console-get-logs`, `console-clear-logs` | Read and clear the plugin's editor log collector (`GD.Print`/`GD.PushWarning`/`GD.PushError`). |
| **reflection** | `reflection-method-find`, `reflection-method-call` | Find and call C# methods (static/instance, public/private) across every loaded assembly via ReflectorNet — the engine-agnostic escape hatch. |

## Building & contributing

`Godot.NET.Sdk` is a NuGet SDK, so **no Godot binary is required to compile or unit-test**:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore   # 0 errors required (CI gate)
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

A Godot 4.3+ editor is only needed for live behavioral verification of the engine-driving tools.
See [`CLAUDE.md`](CLAUDE.md) for the full build/test/run runbook, the editor-runtime assembly-load
fix, conventions, and the headless testbed smoke.

## License

[Apache-2.0](LICENSE) © Ivan Murzak
