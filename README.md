<div align="center">

<p><img src="docs/img/hero.svg" alt="Godot MCP: AI agents work in the Godot editor through the godot_mcp addon" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=githubactions)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) <a href="https://stand-with-ukraine.pp.ua"><img src="https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-square.svg" height="28" alt="Stand with Ukraine" title="Stand with Ukraine"></a>

</div>

**Godot-MCP** connects AI agents (Claude Code, Cursor, Codex, GitHub Copilot, Gemini or any other [MCP](https://modelcontextprotocol.io/) client) to the **Godot editor**. The agent can read and change your scenes, nodes, resources and scripts, run C# in the editor, and look at screenshots. It is a C# editor addon for Godot 4.3+ (.NET) with a companion CLI, [`godot-cli`](cli/README.md), and the Godot sibling of [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

<p align="center">
  <img src="docs/img/terminal.svg" alt="A terminal session: godot-cli installs the addon, signs in, configures Claude Code and opens the editor" width="100%"/>
</p>

## How it works

<p align="center">
  <img src="docs/img/architecture.svg" alt="An AI agent talks MCP to a server (ai-game.dev, or a local gamedev-mcp-server); the server reaches the godot_mcp editor addon over SignalR; the addon runs tools on the Godot main thread" width="100%"/>
</p>

The addon connects to an MCP server: the hosted [ai-game.dev](https://ai-game.dev) by default, or your own [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server), which the addon can download and run for you. Your agent connects to the same server and calls the addon's tools.

## Features

✅ **One query language** — [RQL](docs/rql.md) reads and edits nodes, scenes, resources and settings\
✅ **Undoable edits** — RQL changes are all-or-nothing and undo with Ctrl+Z\
✅ **C# and GDScript** — create, update, validate and attach `.cs` / `.gd` scripts\
✅ **Run C# in the editor** — `script-execute` runs a snippet against your game's types\
✅ **Visual feedback** — screenshots of the viewport, a camera or one node\
✅ **Cloud or self-hosted** — ai-game.dev sign-in, or a local server the addon runs\
✅ **Extensions** — optional RQL modules for animation, navigation, tilemaps and addons\
✅ **In-game runtime** — expose your own tools from a running game (off by default)\
✅ **Your own tools** — one attributed C# method per tool ([guide](docs/custom-tools.md))

## Quick start

Requirements: Godot 4.3 or later, **.NET edition** (the GDScript-only build cannot compile the addon), the .NET 8 SDK, and Node.js 22.14+ for `godot-cli`.

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # optional: a new Godot C# project
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + enable the plugin
godot-cli login                                       # sign in to ai-game.dev (browser, once per machine)
godot-cli setup-mcp claude-code ./MyGodotProject      # write the agent's MCP config
godot-cli open ./MyGodotProject                       # build C#, start the editor, connect
```

Then ask your agent: *"Create 3 cubes in a circle with radius 2."*

`godot-cli setup-mcp --list` shows every supported agent. To install from the editor's **AssetLib** tab, from a [release zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest), or by hand, follow [docs/install.md](docs/install.md): the project's `.csproj` then needs these NuGet references.

| Package | Version |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0` (editor only) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1` (editor only) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` (editor only) |

## Usage

Talk to your agent in plain language; it picks the tools. From the project folder you can call the same tools in a terminal:

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # list the editor's tools
```

When no editor is running, these commands start a headless one for the project. Every command is in the [CLI reference](cli/README.md).

### Tools

Godot-MCP ships **24 built-in tools** in **9 families**:

| Family | Tools |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | logs, C# reflection and error capture inside a running game |
| **ping**, **session**, **skills** | system tools for `godot-cli` and setup, not shown to agents |

Every tool and argument: [docs/tools.md](docs/tools.md).

### Extensions

Extensions are optional NuGet packages that extend RQL; they add no tools of their own.

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

| Extension | Adds | Requires |
| --- | --- | --- |
| [Animation](extensions/Godot-AI-Animation/README.md) | AnimationPlayer libraries, tracks and keys | |
| [Beehave](extensions/Godot-AI-Beehave/README.md) | Behaviour trees and their nodes | [Beehave](https://github.com/bitbrain/beehave) addon |
| [CSG](extensions/Godot-AI-CSG/README.md) | CSG shapes with validated dimensions | |
| [Dialogic](extensions/Godot-AI-Dialogic/README.md) | Timelines and characters | [Dialogic](https://github.com/dialogic-godot/dialogic) addon, Godot 4.4+ |
| [GridMap](extensions/Godot-AI-GridMap/README.md) | GridMap settings, cell rows and cell editing | |
| [Navigation](extensions/Godot-AI-Navigation/README.md) | Navigation agents, regions, links, baking | |
| [Particles](extensions/Godot-AI-Particles/README.md) | GPU particles in 2D and 3D | |
| [PhantomCamera](extensions/Godot-AI-PhantomCamera/README.md) | Virtual cameras, follow and look-at | [Phantom Camera](https://github.com/ramokz/phantom-camera) addon, Godot 4.4+ |
| [Terrain3D](extensions/Godot-AI-Terrain3D/README.md) | Terrain source, region size and materials | [Terrain3D](https://github.com/TokisanGames/Terrain3D) addon, Godot 4.4+ |
| [Tilemap](extensions/Godot-AI-Tilemap/README.md) | TileMapLayer cell rows and cell editing | |

Install a required third-party addon into the project yourself; the dock's **Extensions** section can install them all.

## Documentation

| Page | What is in it |
| --- | --- |
| [Manual installation](docs/install.md) | AssetLib, release zip, NuGet references, agent setup |
| [Connection and server](docs/connection.md) | Cloud and Custom modes, variables, the server |
| [Tools reference](docs/tools.md) | Every tool and its arguments, screenshots, extensions |
| [RQL in Godot-MCP](docs/rql.md) | Sources, functions, writes, undo and saving |
| [Custom tools](docs/custom-tools.md) | Writing tools, C# assembly reloads, the .NET 10 known issue |
| [Runtime usage](docs/runtime.md) | MCP inside a running game: setup, error capture, C# in the game |
| [Runtime security](docs/runtime-security.md) | What the in-game runtime guarantees and what you must do |
| [CLI reference](cli/README.md) | Every `godot-cli` command, exit codes and output formats |
| [Architecture](docs/ARCHITECTURE.md) · [Releasing](docs/RELEASING.md) · [Extensions](docs/EXTENSION-MONOREPO.md) | For contributors |

## Contributing

No Godot binary is needed to build and unit-test:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

Conventions, the headless editor smoke test and the main pitfalls are in [AGENTS.md](AGENTS.md). Fork the repo and open a pull request against `main`. Questions and showcases: [Discord](https://discord.gg/cfbdMZX99G).

Related: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

## License

[Apache-2.0](LICENSE) © Ivan Murzak. The addon also uses [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) and Code-Exec, which have their own license.

That license is the [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/): free to use, including in commercial products, except to build a product that competes with the licensor's.
