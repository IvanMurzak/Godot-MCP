<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP：AI 智能体通过 godot_mcp 插件在 Godot 编辑器中工作" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · **中文** · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** 将 AI 智能体（Claude Code、Cursor、Codex、GitHub Copilot、Gemini 或任何其他 [MCP](https://modelcontextprotocol.io/) 客户端）连接到 **Godot 编辑器**。智能体可以读取和修改你的场景、节点、资源和脚本，构建并运行 C#，并查看截图。它是一个编辑器插件，配套 CLI 为 [`godot-cli`](../cli/docs/README.zh-CN.md)，也是 [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) 的 Godot 姊妹项目。

**要求：** Godot 4.3+ 编辑器的 **.NET 版**，以及 **.NET 8 SDK**（只需安装一次）。你的脚本可以继续使用 GDScript。可选的 `godot-cli` 需要 Node.js 22.14+。

<a id="why"></a>
## 为什么选择 Godot-MCP

✅ **Godot 4.3 至 4.7** — 每个发布版都会在 CI 中针对 4.3、4.4、4.5、4.6 和 4.7 进行测试\
✅ **真实的 C# 构建** — `script-build` 运行 `dotnet build` 并返回编译器错误\
✅ **GDScript 检查** — `script-validate` 返回 `.gd` 文件的解析与编译诊断\
✅ **可撤销的原子编辑** — 一次查询的修改要么全部生效、要么全不生效，每次可用一次 Ctrl+Z 撤销\
✅ **截图** — 视口、某个相机或单独渲染的某个节点，返回给智能体\
✅ **运行时工具** — 来自运行中游戏的日志、C# 反射和运行时错误（需主动开启）\
✅ **免费云端或完全本地** — 默认使用 ai-game.dev（无需开放端口），或无需账户的本地服务器\
✅ **无遥测** — 插件和 `godot-cli` 不发送任何使用数据或分析数据

<a id="gdscript"></a>
## 支持 GDScript 项目

你的游戏代码仍然是 GDScript。插件本身是 C#，因此项目需要几个一次性步骤：

✅ **Godot .NET 编辑器** — 用 Godot 4.3+ 的 .NET 版打开项目（同一个项目，同样的场景）\
✅ **.NET 8 SDK** — 每台机器安装一次\
✅ **C# 解决方案** — 现有项目：**Project → Tools → C# → Create C# solution**；新项目：`create-project --dotnet`\
✅ **插件** — `godot-cli install-plugin` 会添加插件及其 NuGet 引用，并启用它

**纯 GDScript 项目需要 C# 解决方案吗？** 需要，创建一次即可：插件是 C#，会编译进项目的 C# 程序集。**Create C# solution** 会在项目根目录添加一个 `.csproj` 和一个 `.sln` 文件。你的 `.gd` 脚本、场景和资源不会被修改，运行方式与以前相同；智能体同样可以读取、编写和校验 GDScript。之后导出会使用 Godot 的 .NET 导出模板，并包含插件的小型运行时程序集；除非你的代码调用 `GodotMcpRuntime.Initialize()`，否则它们保持不活动状态（[如何将其排除](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users)）。

<p align="center">
  <img src="img/terminal.svg" alt="终端会话：godot-cli 安装插件、登录、配置 Claude Code 并打开编辑器" width="100%"/>
</p>

<a id="how-it-works"></a>
## 工作原理

<p align="center">
  <img src="img/architecture.svg" alt="AI 智能体通过 MCP 与服务器（ai-game.dev 或本地 gamedev-mcp-server）通信；服务器通过 SignalR 连接 godot_mcp 编辑器插件；插件在 Godot 主线程上运行工具" width="100%"/>
</p>

插件主动连接到一个 MCP 服务器，你的智能体连接到同一个服务器并调用插件的工具。服务器可以是插件替你下载并运行的本地 [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)，也可以是托管的 [ai-game.dev](https://ai-game.dev) 中继。插件自身不开放任何端口。

Cloud 是默认模式，因为它无需开放端口，并能避免与其他程序发生端口冲突；而且它是免费的。本地模式是完整的替代方案，适用于离线或物理隔离（air-gapped）环境。

<a id="stability"></a>
### 稳定性

- **版本：** 插件和 `godot-cli` 始终以相同的版本号发布；更新前请阅读[发布说明](https://github.com/IvanMurzak/Godot-MCP/releases/latest)。
- **版本之间的变化：** 一个发布版可能会改变插件所需的 NuGet 版本以及它下载的 GameDev-MCP-Server 版本。`godot-cli update`（或 `install-plugin`）会写入与你安装的插件相匹配的 NuGet 版本，插件也会自行获取与之匹配的服务器。
- **发布关卡：** 只有在 .NET 测试、CLI 测试以及 Godot 4.3–4.7 编辑器测试全部通过后，才会发布新版本。
- **变更历史：** 每个 [GitHub 发布版](https://github.com/IvanMurzak/Godot-MCP/releases)都列出了其合并的变更；`godot-cli` 还维护一份[更新日志](../cli/CHANGELOG.md)。

<a id="quick-start"></a>
## 快速开始

安装 `godot-cli`（Node.js 22.14+）并将插件添加到你的项目：

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # 插件 + NuGet 引用 + 启用插件
```

然后选择智能体连接编辑器的方式。两种都是完整的配置方案。Cloud 是插件的默认模式：无需开放端口，不会与其他程序发生端口冲突，而且免费。本地模式让一切都留在你的机器上，适用于离线或物理隔离环境。

**方案 A：本地，无需账户。** 工具调用留在你的机器上。

1. **设置令牌。** 在 Godot .NET 编辑器中打开项目。在 **AI Game Developer** 停靠面板中选择 **Custom**，设置 **Authorization → token**，点击 **Generate**，然后点击 **Start Server**。记下 **Server URL**（`http://localhost:<port>`，端口由项目路径推导得出）。
   或者自己选一个令牌，在终端中用它打开编辑器，然后点击 **Start Server**：
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. 用同一个令牌**连接你的智能体**：

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

> **不设令牌时**，本地服务器使用 `none` 授权。它只监听回环地址，因此只有你自己电脑上的程序才能访问它。
> 这种情况下，`godot-cli open ./MyGodotProject` 会一步启动编辑器及其本地服务器，并打印供 `setup-mcp --url` 使用的 URL。

**方案 B：ai-game.dev 云端。** 每台机器登录一次；之后智能体配置会携带一个绑定到本项目的密钥。

```bash
godot-cli login                                       # 在浏览器中进行 OAuth 设备登录
godot-cli setup-mcp claude-code ./MyGodotProject      # 写入智能体的 MCP 配置
godot-cli open ./MyGodotProject                       # 构建 C#、启动编辑器、建立连接
```

然后对你的智能体说：*"在半径为 2 的圆上创建 3 个立方体。"*

如需通过编辑器的 **AssetLib** 选项卡、[发布版 zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest) 或手动安装，请参阅 [install.zh-CN.md](install.zh-CN.md)：此时项目的 `.csproj` 需要插件的 NuGet 引用（`com.IvanMurzak.ReflectorNet`、`com.IvanMurzak.McpPlugin`，以及仅编辑器使用的 `com.IvanMurzak.Rql`、`com.IvanMurzak.CodeExec`、`com.IvanMurzak.CodeExec.Roslyn` 和 `Microsoft.CodeAnalysis.CSharp`），版本需与你安装的发布版一致。

<a id="relay"></a>
### 哪些数据经过 ai-game.dev

仅限 Cloud 模式。中继在你的智能体和编辑器之间转发工具调用。它不存储你发送的内容：不存储请求或响应内容，不存储对话、源代码、项目文件或资源。它只保留元数据，保存 90 天：IP 地址、时间和时长、工具名称和调用次数、请求和响应大小，以及 MCP 事件（类型、状态、工具名称、错误）。在本地模式下，编辑器和服务器只通过 localhost 通信。

<a id="clients"></a>
## 连接你的 AI 客户端

`godot-cli setup-mcp <agent> ./MyGodotProject` 会替你写入配置（`godot-cli setup-mcp --list` 列出所有智能体 id）。服务器条目名为 `ai-game-developer`；`<url>` 是 `setup-mcp` 打印的 URL。在 Cloud 模式下，该条目还会携带 `Authorization: Bearer agd_pk_…`，这是一个只能访问本项目的密钥。

<details>
<summary><b>Claude Code</b> — 项目中的 <code>.mcp.json</code></summary>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Cursor</b> — <code>.cursor/mcp.json</code></summary>

```bash
godot-cli setup-mcp cursor ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>VS Code (Copilot)</b> — <code>.vscode/mcp.json</code></summary>

```bash
godot-cli setup-mcp vscode-copilot ./MyGodotProject
```

```json
{ "servers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Codex</b> — <code>.codex/config.toml</code></summary>

```bash
godot-cli setup-mcp codex ./MyGodotProject
```

```toml
[mcp_servers.ai-game-developer]
enabled = true
url = "<url>"
tool_timeout_sec = 300
startup_timeout_sec = 30
```

在 Cloud 模式下，密钥写入 `http_headers`。
</details>

<details>
<summary><b>Gemini CLI</b> — <code>.gemini/settings.json</code></summary>

```bash
godot-cli setup-mcp gemini ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

同样支持：Claude Desktop、Visual Studio (Copilot)、GitHub Copilot CLI、Rider (Junie)、Antigravity、Cline、Open Code、Kilo Code，以及通用的 `custom` 客户端（[CLI 参考](../cli/docs/README.zh-CN.md#supported-ai-agents)）。

<a id="what-the-agent-can-do"></a>
## 智能体能做什么

Godot-MCP 内置 **24 个工具**，分为 **9 个系列**。你无需编写查询：智能体会阅读工具自带的 RQL 指南（`rql-schema`、`godot-rql` 技能）并自行编写查询。

| 能力 | 工具 | 对智能体说 |
| --- | --- | --- |
| 场景和节点 | `rql-query`, `rql-mutate` | *"在主场景中添加一个 DirectionalLight3D 并保存。"* |
| 批量编辑 | `rql-mutate` | *"隐藏所有名称以 Enemy 开头的节点。"* |
| 资源和项目设置 | `rql-query`, `rql-mutate` | *"添加一个绑定到空格键的 jump 输入动作。"* |
| 查询 API | `rql-schema` | *"CharacterBody3D 有哪些 Godot 属性？"* |
| 脚本（GDScript 和 C#） | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *"写一个让这个节点旋转的 GDScript 并挂载上去。"* |
| 构建 C# | `script-build` | *"构建 C# 项目并修复错误。"* |
| 在编辑器中运行 C# | `script-execute` | *"统计当前打开场景中 MeshInstance3D 节点的数量。"* |
| 查看结果 | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *"截取视口截图并检查光照。"* |
| 运行中的游戏（需主动开启） | `console-*`, `reflection-*`, `runtime-errors-*` | *"检查运行中的游戏是否有新的运行时错误。"* |

`ping`、`session-close`、`godot-skill-create` 和 `godot-skill-generate` 是供 `godot-cli` 和配置使用的系统工具；智能体看不到它们。所有工具及其参数：[tools.zh-CN.md](tools.zh-CN.md)。

在项目文件夹中，同样的工具也可以在终端里调用（如果没有正在运行的编辑器，会启动一个无界面编辑器）：

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # 列出编辑器的工具
```

<a id="security"></a>
## 安全

| 方面 | 适用规则 |
| --- | --- |
| **本地服务器** | 只监听回环地址。其默认授权为 `none`，因此你机器上的任何进程都可以调用它。在停靠面板中选择 `token`、设置 `GODOT_MCP_TOKEN`，或用 `godot-cli open <path> --mode Custom --auth Required --token <token>` 打开编辑器，即可要求 Bearer 令牌。 |
| **Origin 检查** | 在任何授权模式下，服务器都会对带有不允许的 `Origin` 头的请求返回 `403`（防御 DNS 重绑定）。 |
| **编辑器插件** | 不开放端口；它主动连接到服务器。仅用于开发的控制桥接位于 `127.0.0.1`，只有在 `GODOT_MCP_DEV_CONTROL=1` 时才会启动。 |
| **云端** | 智能体配置携带一个绑定到单个项目的密钥；编辑器使用你在本机的登录。你的账户令牌只会发送给 ai-game.dev 或 localhost 服务器。中继不存储请求或响应内容，只保留元数据，保存 90 天。 |
| **已保存的令牌** | 编辑器将其令牌以普通 JSON 保存在 `user://godot-mcp-config.json` 中；请把该文件当作机密。 |
| **导出的游戏** | 除非你的代码调用 `GodotMcpRuntime.Initialize(...)` 和 `Connect()`，否则游戏中不会运行任何东西，且只运行你注册的工具。错误捕获和游戏内 C# 需分别主动开启（[运行时安全](runtime-security.zh-CN.md)）。 |
| **不受保护的部分** | 工具调用会修改你的项目并在编辑器中运行 C#：请使用版本控制，并且永远不要将授权为 `none` 的服务器暴露到网络上。 |

详情：[连接与服务器](connection.zh-CN.md)以及 [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)。

<a id="extensions"></a>
## 扩展

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

**游戏开发者可以自由使用全部内容，包括商业游戏。** Godot-MCP 采用 [Apache-2.0](../LICENSE) 许可。其编辑器端依赖 RQL 和 Code-Exec 使用 [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/)，该许可证只限制构建与之竞争的游戏开发 AI 工具的公司。
