<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP：AI 智能体通过 godot_mcp 插件在 Godot 编辑器中工作" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · **中文** · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** 将 AI 智能体（Claude Code、Cursor、Codex、GitHub Copilot、Gemini 或任何其他 [MCP](https://modelcontextprotocol.io/) 客户端）连接到 **Godot 编辑器**。智能体可以读取和修改你的场景、节点、资源和脚本，在编辑器中运行 C#，并查看截图。它是一个面向 Godot 4.3+（.NET）的 C# 编辑器插件，配套 CLI 为 [`godot-cli`](../cli/docs/README.zh-CN.md)，也是 [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) 的 Godot 姊妹项目。

<p align="center">
  <img src="img/terminal.svg" alt="终端会话：godot-cli 安装插件、登录、配置 Claude Code 并打开编辑器" width="100%"/>
</p>

<a id="how-it-works"></a>
## 工作原理

<p align="center">
  <img src="img/architecture.svg" alt="AI 智能体通过 MCP 与服务器（ai-game.dev 或本地 gamedev-mcp-server）通信；服务器通过 SignalR 连接 godot_mcp 编辑器插件；插件在 Godot 主线程上运行工具" width="100%"/>
</p>

插件连接到一个 MCP 服务器：默认是托管的 [ai-game.dev](https://ai-game.dev)，也可以是你自己的 [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)，插件可以替你下载并运行它。你的智能体连接到同一个服务器并调用插件的工具。

<a id="features"></a>
## 功能

✅ **统一查询语言** — [RQL](rql.md) 读取和编辑节点、场景、资源与设置\
✅ **可撤销的编辑** — RQL 修改要么全部生效、要么全不生效，可用 Ctrl+Z 撤销\
✅ **C# 与 GDScript** — 创建、更新、校验并挂载 `.cs` / `.gd` 脚本\
✅ **在编辑器中运行 C#** — `script-execute` 针对你游戏的类型运行代码片段\
✅ **可视化反馈** — 视口、某个相机或单个节点的截图\
✅ **云端或自托管** — 登录 ai-game.dev，或使用插件运行的本地服务器\
✅ **扩展** — 可选的 RQL 模块，涵盖动画、导航、瓦片地图和第三方插件\
✅ **游戏内运行时** — 从运行中的游戏暴露你自己的工具（默认关闭）\
✅ **自定义工具** — 每个工具对应一个带特性标注的 C# 方法（[指南](custom-tools.zh-CN.md)）

<a id="quick-start"></a>
## 快速开始

要求：Godot 4.3 或更高版本的 **.NET 版**（仅支持 GDScript 的版本无法编译该插件）、.NET 8 SDK，以及供 `godot-cli` 使用的 Node.js 22.14+。

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # 可选：新建一个 Godot C# 项目
godot-cli install-plugin ./MyGodotProject             # 插件 + NuGet 引用 + 启用插件
godot-cli login                                       # 登录 ai-game.dev（浏览器，每台机器一次）
godot-cli setup-mcp claude-code ./MyGodotProject      # 写入智能体的 MCP 配置
godot-cli open ./MyGodotProject                       # 构建 C#、启动编辑器、建立连接
```

然后对你的智能体说：*"Create 3 cubes in a circle with radius 2."*

`godot-cli setup-mcp --list` 列出所有支持的智能体。如需通过编辑器的 **AssetLib** 选项卡、[发布版 zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest) 或手动安装，请参阅 [install.zh-CN.md](install.zh-CN.md)：此时项目的 `.csproj` 需要以下 NuGet 引用。

| 包 | 版本 |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0`（仅编辑器） |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1`（仅编辑器） |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0`（仅编辑器） |

<a id="usage"></a>
## 使用

用自然语言与你的智能体对话，它会自行选择工具。在项目文件夹中，你也可以在终端里调用相同的工具：

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # 列出编辑器的工具
```

如果没有正在运行的编辑器，这些命令会为该项目启动一个无界面（headless）编辑器。所有命令见 [CLI 参考](../cli/docs/README.zh-CN.md)。

<a id="tools"></a>
### 工具

Godot-MCP 内置 **24 个工具**，分为 **9 个系列**：

| 系列 | 工具 |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | 运行中游戏内的日志、C# 反射和错误捕获 |
| **ping**, **session**, **skills** | 供 `godot-cli` 和配置使用的系统工具，不向智能体展示 |

所有工具及其参数：[tools.zh-CN.md](tools.zh-CN.md)。

<a id="extensions"></a>
### 扩展

扩展是可选的 NuGet 包，用于扩展 RQL；它们本身不添加任何工具。

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

| 扩展 | 新增内容 | 依赖 |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | AnimationPlayer 动画库、轨道和关键帧 | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | 行为树及其节点 | [Beehave](https://github.com/bitbrain/beehave) 插件 |
| [CSG](../extensions/Godot-AI-CSG/README.md) | 带尺寸校验的 CSG 形状 | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | 时间线和角色 | [Dialogic](https://github.com/dialogic-godot/dialogic) 插件，Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | GridMap 设置、单元格行和单元格编辑 | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | 导航代理、区域、链接、烘焙 | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | 2D 和 3D 的 GPU 粒子 | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | 虚拟相机、跟随和注视 | [Phantom Camera](https://github.com/ramokz/phantom-camera) 插件，Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | 地形数据源、区域大小和材质 | [Terrain3D](https://github.com/TokisanGames/Terrain3D) 插件，Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | TileMapLayer 单元格行和单元格编辑 | |

所需的第三方插件需由你自行安装到项目中；停靠面板的 **Extensions** 区域可以一次性全部安装。

<a id="documentation"></a>
## 文档

| 页面 | 内容 |
| --- | --- |
| [手动安装](install.zh-CN.md) | AssetLib、发布版 zip、NuGet 引用、智能体配置 |
| [连接与服务器](connection.zh-CN.md) | Cloud 与 Custom 模式、环境变量、服务器 |
| [工具参考](tools.zh-CN.md) | 所有工具及其参数、截图、扩展 |
| [Godot-MCP 中的 RQL](rql.md) | 数据源、函数、写入、撤销与保存 |
| [自定义工具](custom-tools.zh-CN.md) | 编写工具、C# 程序集重载、.NET 10 已知问题 |
| [运行时用法](runtime.zh-CN.md) | 运行中游戏内的 MCP：配置、错误捕获、游戏内 C# |
| [运行时安全](runtime-security.zh-CN.md) | 游戏内运行时提供的保证以及你必须做的事 |
| [CLI 参考](../cli/docs/README.zh-CN.md) | 所有 `godot-cli` 命令、退出码和输出格式 |
| [Architecture](ARCHITECTURE.md) · [Releasing](RELEASING.md) · [Extensions](EXTENSION-MONOREPO.md) | 面向贡献者 |

<a id="contributing"></a>
## 参与贡献

构建和运行单元测试无需 Godot 可执行文件：

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

代码约定、无界面编辑器冒烟测试和主要陷阱见 [AGENTS.md](../AGENTS.md)。Fork 本仓库并向 `main` 提交 pull request。问题与作品展示：[Discord](https://discord.gg/cfbdMZX99G)。

相关项目：[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## 许可证

[Apache-2.0](../LICENSE) © Ivan Murzak。插件还使用了 [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) 和 Code-Exec，它们有各自的许可证。

该许可证为 [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/)：可免费使用，包括用于商业产品，但不得用于构建与许可方产品相竞争的产品。
