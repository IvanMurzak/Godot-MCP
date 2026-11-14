<div align="center">

<p><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/cli-hero.svg" alt="godot-cli：安装 Godot MCP 插件、登录、配置你的 AI 智能体，并在终端中操控 Godot 编辑器" width="100%"/></p>

[![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) [![Node](https://img.shields.io/badge/Node-22.14%2B-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=nodedotjs)](https://nodejs.org/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · **中文** · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**`godot-cli`** 是 [Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) 的命令行工具。Godot-MCP 是一个 Godot 编辑器插件，通过 [Model Context Protocol](https://modelcontextprotocol.io/) 将 AI 智能体（Claude Code、Cursor、Codex、GitHub Copilot 等）连接到 Godot 编辑器。它是 [`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli) 和 [`unreal-mcp-cli`](https://www.npmjs.com/package/unreal-mcp-cli) 的姊妹工具，由 [ai-game.dev](https://ai-game.dev) 提供支持。

<p align="center"><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/terminal.svg" alt="终端会话：godot-cli 安装插件、登录、配置 Claude Code 并打开编辑器" width="100%"/></p>

<a id="features"></a>
## 功能

✅ **一条命令安装** — `install-plugin` 添加插件和 NuGet 引用并启用插件\
✅ **登录一次即可** — `login` 使用 OAuth 设备授权流程；编辑器会自动读取凭据\
✅ **智能体配置** — `setup-mcp` 为 Claude Code、Cursor、Codex 等写入 MCP 配置\
✅ **编辑器控制** — 用 `open`、`close`、`status` 和 `wait-for-ready` 管理项目的编辑器\
✅ **运行工具** — `rql`、`exec`、`run-tool` 在没有编辑器运行时启动无界面编辑器\
✅ **无界面 CI** — 无需打开编辑器即可运行 `check`、`import`、`test`、`export` 和 `run-script`\
✅ **扩展** — `install-extension` 添加可选的 RQL 模块；`update` 更新所有内容

<a id="quick-start"></a>
## 快速开始

需要 [Node.js](https://nodejs.org/) 22.14 或更高版本，以及带 .NET 8 SDK 的 Godot 4.3+ .NET 编辑器。支持 Windows、macOS 和 Linux。

```bash
npm install -g godot-cli

godot-cli install-plugin ./MyGodotProject          # 插件 + NuGet 引用 + 启用插件
godot-cli login                                    # 登录 ai-game.dev，每台机器一次
godot-cli setup-mcp claude-code ./MyGodotProject   # 写入智能体的 MCP 配置
godot-cli open ./MyGodotProject                    # 构建 C#、启动编辑器、建立连接
godot-cli wait-for-ready ./MyGodotProject          # 等待插件响应
```

在项目文件夹中于编辑器内运行 C# 或 RQL；没有打开的编辑器时会启动一个无界面编辑器：

```bash
godot-cli exec 'EditorInterface.Singleton.GetEditedSceneRoot()?.Name'
godot-cli rql 'nodes | where name starts-with "Tree" | count'
```

也可以不安装直接运行任意命令：`npx godot-cli install-plugin ./MyGodotProject`。

<a id="commands"></a>
## 命令

- **`open [path]`** — 构建项目的 C# 程序集（以便首次打开时插件能加载——见下文），定位 Godot 编辑器可执行文件，并带着 `GODOT_MCP_*` 连接环境变量启动 `--editor --path <project>`。`--no-build` 跳过构建。`--headless` 则启动一个无窗口的编辑器会话（见 [`exec`](#exec)）。
- **`exec [code]`** — 通过 `script-execute` 在编辑器中运行 C#（见 [`exec`](#exec)）：复用正在运行的编辑器，否则启动一个无界面编辑器并保持其常驻。
- **`create-project [path]`** — 创建一个新的 Godot 项目骨架；`--dotnet` 还会写入一个 Godot.NET.Sdk `.csproj`，`--name` 设置其名称。
- **`build [path]`** — 构建项目的 C# 程序集（`dotnet build`），以便下次打开编辑器时加载 `godot_mcp` 插件。仅 GDScript 的项目（没有 `.csproj`）不做任何事。这与 `open` 在启动前运行的构建相同。
- **`rql [query]`** — 从命令行执行一条 RQL 查询，无需 JSON：`godot-cli rql 'nodes | where name starts-with "Tree" | count'`。最后一个阶段为 `set` 或 `call` 的查询发往 `rql-mutate`（`--dry-run`、`--max-targets <n>`），其他的发往 `rql-query`；`--schema [type]` 运行 `rql-schema`；当 shell 去掉查询的引号时，`-f <file>`（`-f -`：stdin）从文件读取查询。与 `run-tool` 一样会启动无界面编辑器。在 `SaveScene()`、`NewScene(...)`、`CreateResource(...)` 或 `godot.resource(...)` 编辑之后，它会输出写入的文件（`Saved to disk: res://scenes/forest.tscn`）；按其他语言习惯拼写的运算符（`startsWith`、`like`）会得到 RQL 正确拼写的提示。`rql --help` 列出常用示例。
- **`run-tool <tool> [path]`** — 以 JSON 输入 POST 到 `<url>/api/tools/<tool>`。文本模式下，文本回答直接输出其文本（`--raw`：响应 JSON；`--verbose`：同时输出端点）。当项目的本地服务器上没有编辑器响应时，它会像 `exec` 一样启动一个无界面编辑器（`--no-start` 禁用；`--once` 在调用后关闭它）。
- **`run-system-tool <tool> [path]`** — POST 到 `<url>/api/system-tools/<tool>`（不向 MCP 客户端暴露的工具）。自动启动行为与 `run-tool` 相同。
- **`check [path]`** — 无需运行编辑器的 CI 关卡：导入、`dotnet build`、编辑器启动和 GDScript 加载检查，根据日志判定结果（见[无界面命令](#headless-commands-check-import-test-export-run-script)）。
- **`import [path]`** — `godot --headless --import`，日志中出现导入错误或脚本错误时失败。
- **`test [path]`** — 以无界面方式运行项目的 GUT 或 gdUnit4 测试，带硬性超时；根据 JUnit 报告和日志判定结果。
- **`export <preset> <output>`** — 以无界面方式导出一个预设（`--debug`、`--pack`）；先检查导出模板，之后检查输出。
- **`run-script <script> [args...]`** — 运行一次性的 `SceneTree` GDScript（`godot --headless -s`）；仅在 `quit(0)` 后以 0 退出。
- **`commands`** — 所有命令及其参数、选项和退出码的机器可读清单（`--format json`）。
- **`tools [name]`** — 编辑器的 AI 工具（MCP `tools/list`；没有编辑器响应时启动一个无界面编辑器，并将列表缓存到 `.ai-game-dev/tools.json`，已被 git 忽略）。带名称时：输出其描述、参数和一个 `run-tool` 调用示例。`--refresh` 重新向编辑器查询；`--no-start` 使用缓存或内置列表。
- **`status [path]`** — 检测项目是否有正在运行的 Godot 编辑器，并探测 MCP 服务器的健康状态。
- **`wait-for-ready [path]`** — 轮询 MCP 服务器，直到它响应 `ping`。
- **`login [path]`** — 通过 OAuth 2.1 设备授权流程（RFC 8628）在 ai-game.dev 云端进行认证——打开浏览器，然后将云端凭据保存到共享的本机存储（`~/.ai-game-dev/credentials.json`），编辑器插件会自动采用。其 `--project` / `--base-url` / `--force` / `--tools-only` / `--yes` 参数见下方 `login` 一节。
- **`setup-mcp <agent> [path]`** — 写入智能体的 MCP 客户端配置，指向绑定到项目的 `<host>/mcp/p/<pin>` URL（使智能体路由到*本*项目的编辑器）。在 Cloud 模式下，配置中带有本项目的密钥（`Authorization: Bearer agd_pk_…`）。添加 `--no-pin` 使用不带绑定的 `<host>/mcp` URL，`--oauth` 生成只含 URL 的配置，`--regenerate-key` 替换密钥。
- **`setup-skills <agent> [path]`** — 离线地在智能体的 skills 路径下写入 `godot-cli` 智能体 skill（冷启动工作流、RQL 示例、退出码）和 `ai-game-dev-app`。`--list` 显示每个智能体的 skills 支持情况。
- **`configure [path]`** — 在项目本地的 `.godot-mcp/features.json` 中列出 / 显示 / 隐藏工具、提示词和资源（`--enable-*` 显示，`--disable-*` 隐藏；被隐藏的工具按名称调用时仍会运行）。
- **`close [path]`** — 优雅地停止为项目运行的 Godot 编辑器（`--force` 强制终止）。如果该编辑器运行的本地 MCP 服务器比编辑器存活得更久，也会将其停止。`--owned-only` 只关闭由 `godot-cli` 启动的编辑器（`exec` / `open --headless`）。
- **`install-plugin [path]`** — 端到端安装 `godot_mcp` 插件：生成 `res://addons/godot_mcp/`（下载匹配的 GitHub 发布版，或用 `--source <path>` 指定本地副本），向项目 `.csproj` 添加已安装插件所需的 NuGet `PackageReference` 以及扩展目录的 `<EmbeddedResource>`，并启用插件。幂等。
- **`install-extension <id> [path]`** — 将 Godot-MCP **扩展**（可选的 RQL 模块包）安装到项目中：从共享目录解析 `<id>`，在项目 `.csproj` 中添加/更新其 `<PackageReference>`，然后重新构建以还原。幂等——行为与编辑器内的停靠面板完全一致。
- **`remove-plugin [path]`** — 在 `project.godot` 的 `[editor_plugins]` 中禁用 `godot_mcp` 插件（不删除插件文件）。
- **`update [path]`** — 更新所有内容：将项目的 `godot_mcp` 插件和已安装的扩展更新到最新版本，然后更新 `godot-cli` 自身。在项目之外只更新 CLI。`--check` 只报告（有过期内容时退出码为 2）；`--no-self`、`--no-project`、`--no-extensions` 跳过对应步骤。

<a id="build-before-open-open--build"></a>
### 打开前构建（`open` / `build`）

`open` **在启动编辑器之前构建项目的 C# 程序集**。编辑器一加载，Godot 就会实例化已启用插件的 `EditorPlugin`——因此在*全新*首次打开一个从未构建过的 C# 项目时，程序集尚不存在，Godot 会报错 `Unable to load addon script 'res://addons/godot_mcp/Editor/GodotMcpPlugin.cs' … Disabling the addon`。先构建可确保插件实例化时程序集已就绪。

- 构建与 `godot-cli build` 运行的相同：`dotnet build <project>.csproj --configuration Debug`。
- **仅 GDScript 的项目**（根目录没有 `.csproj`）会被自动跳过——没有需要编译的内容。
- 对 C# 项目，构建**无条件**运行；`dotnet build` 是增量的，所以已是最新的项目几乎瞬间完成。向 `open` 传入 `--no-build` 可跳过（例如你已经构建过，或想尽快打开）。
- 如果构建**失败**，`open` **不会**启动编辑器（启动只会重现插件被禁用的失败）；而是直接报告错误。

<a id="editor-resolution-open"></a>
### 编辑器定位（`open`）

`open` 按以下顺序定位 Godot 编辑器可执行文件：

1. `--editor-path <path>`（显式指定）。
2. `GODOT_BIN` / `GODOT4_BIN` 环境变量。
3. `PATH` 上第一个匹配的 Godot 可执行文件（在 Windows 上优先 mono 版本）。
4. 各操作系统的常见安装目录——Windows 优先 mono 版本；macOS 解析 `.app/Contents/MacOS/Godot` 可执行文件；Linux 扫描常见的解压位置。

<a id="connection-env-vars"></a>
### 连接环境变量

`open` 会将以下变量传给编辑器进程（名称与插件的 `GodotMcpConfig` 一致）：

| 参数 | 环境变量 |
| --- | --- |
| `--url <host>` | `GODOT_MCP_HOST` |
| `--cloud-url <url>` | `GODOT_MCP_CLOUD_URL` |
| `--token <token>` | `GODOT_MCP_TOKEN` |
| `--auth None\|Required` | `GODOT_MCP_AUTH_OPTION` |
| `--mode Cloud\|Custom` | `GODOT_MCP_CONNECTION_MODE` |
| `--log-level <level>` | `GODOT_MCP_LOG_LEVEL` |

插件的默认模式是 Cloud（ai-game.dev）：它无需开放端口，能避免与其他程序发生端口冲突，而且免费。对于要求使用你自选令牌的本地服务器（离线或物理隔离环境），请用该令牌以 Custom 模式打开编辑器，然后在停靠面板中点击 **Start Server**（`Required` 选择 `token` 授权）：

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

<a id="server-url-resolution-run-tool--status--wait-for-ready"></a>
### 服务器 URL 解析（`run-tool` / `status` / `wait-for-ready`）

工具调用所 POST 的服务器基础 URL（`<base>/api/tools/<name>`，系统工具则为 `<base>/api/system-tools/<name>`——`status` / `wait-for-ready` 探测的是系统工具 `ping`）按以下顺序解析：

1. `--url <url>`（显式覆盖）。
2. `GODOT_MCP_HOST` 环境变量（Custom 模式主机）。
3. `GODOT_MCP_CLOUD_URL` 环境变量 → 云端 **`/mcp` hub** URL（缺少时会追加 `/mcp`）。
4. `GODOT_MCP_CONNECTION_MODE=Cloud` → 默认云端 hub `https://ai-game.dev/mcp`。
5. **已注册项目标记**（`.ai-game-dev/project.json` 的 `serverTarget`，由 `install-plugin --enroll` 写入）：托管目标路由到其 `/mcp` hub，`localhost` 目标原样使用。这使得**已注册的云端项目无需任何环境配置即可访问**——持久化的云端凭据（按顺序：项目级存储 `<project>/.ai-game-dev/`、旧版项目级存储、`~/.ai-game-dev/credentials.json`）会被自动使用，并在过期时刷新。
6. 本地回退：`http://localhost:<derived-port>`，即编辑器插件在本地绑定的、由项目路径确定性推导的 v2 端口（标记中的 `portOverride` 优先于哈希推导的端口）。

云端路径带有 `/mcp` hub 段，以便请求到达 hub 而不是云端后端（后端不提供 `/api/tools/*`）。传入 `--url http://localhost:<port>` 可显式指定本地/自托管服务器。

`run-tool`、`run-system-tool` 和 `exec` 会用**项目的连接模式**替代第 6 步，读取方式与插件相同（`GODOT_MCP_CONNECTION_MODE`、项目的 `.env`、持久化的 `user://godot-mcp-config.json`，否则为插件默认的 Cloud）。Custom 模式使用上述本地服务器。Cloud 模式使用云端 hub 和本机的登录状态（不会启动编辑器：只有连接到 ai-game.dev 的编辑器才会响应）。如果本机**从未**登录过 ai-game.dev，则改用项目的本地服务器并给出警告（此时启动的编辑器会为其提供服务；不持久化任何内容）。登录状态存在但无法使用（过期、已吊销、无法读取）时报错（退出码 6，`NOT_AUTHENTICATED`）：已登录的机器绝不会被切换到本地服务器。`open`、`tools` 和 `open --headless` 同样如此：在已登录机器上的 Cloud 项目中，`open --headless` 不启动编辑器并以 6 退出（请使用经由 ai-game.dev 的 `open`、`run-tool` 和 `exec`）。显式的 `--url` / `--token` 按原样使用；非本地的 `--url` / `GODOT_MCP_HOST` 会被 `exec` 直接调用，而被 `open --headless` 拒绝（无界面编辑器是本地的）。


<a id="exec"></a>
## `exec`

在项目的 Godot 编辑器中运行 C#（插件的 `script-execute` 工具，基于 [Code-Exec](https://github.com/IvanMurzak/Code-Exec)）：

```bash
godot-cli exec '1 + 2'                                   # → System.Int32: 3
godot-cli exec -f snippet.cs --using MyGame.Gameplay      # 代码来自文件（"-" 或管道：stdin）
godot-cli exec 'Helper.Twice(21)' --include helpers       # <project>/.ai-game-dev/snippets 下的 .cs 文件/文件夹
```

代码是普通语句；最后一个表达式即为结果。输出为该值（`Type: value`），以及在 `logs:` 下代码打印的内容（`Console`；使用 Godot.NET.Sdk 4.5+ 时还有 `GD.Print` / push_warning / push_error）。编译错误会指向代码片段中的行（`snippet.cs(2,12)`），并以 6 退出。

- **项目已有正在运行的编辑器**（你自己的带窗口编辑器，或由 `godot-cli` 启动的编辑器）时会复用它，且从不关闭。你自己的编辑器必须为项目的**本地**服务器提供服务（停靠面板：Custom 模式，服务器已启动）。
- **没有正在运行的编辑器：** 先构建 C# 程序集，然后启动一个**无界面**编辑器（`--headless --editor --path`，或用 `--window` 启动带窗口的），连接到项目的本地服务器（`http://localhost:<derived port>`，或 `--url`），该服务器由其插件自行启动。无界面编辑器在 `--idle <seconds>`（默认 300，`0` = 永不）内没有工具调用时退出，并保存有文件的已打开场景（带窗口的编辑器保持打开）。并行调用只构建一次、只启动一个编辑器；如果用户的编辑器其本地服务器没有响应，会最多等待 60 秒，然后报告。
- `open --headless [--idle-timeout <s>]` 启动同样的会话但不运行代码；`close --owned-only` 保存并退出它（通过插件的 `session-close` 工具——绝不会关闭用户的编辑器）。

会话使用 [`@baizor/gamedev-cli-core`](https://www.npmjs.com/package/@baizor/gamedev-cli-core) 的 `EditorSession`：租约文件 `<project>/.ai-game-dev/session.json` 标记某个编辑器由 CLI 启动（只有这样的编辑器才会被关闭），并记录其运行的服务器：只要该编辑器确实存活，之后的 `exec` / `open` / `close` 调用都会使用那个服务器，无论 `--url` 如何设置。`<project>/.ai-game-dev/session.lock` 使并发调用只启动一个编辑器：C# 构建在该锁下运行，且只在启动编辑器的那次调用中运行。这两个文件都列在 `<project>/.ai-game-dev/.gitignore` 中。编辑器会收到 `AIGD_SESSION_IDLE_SECONDS`、`AIGD_SESSION_OWNER=cli` 和 `AIGD_SESSION_NONCE`，以及 `GODOT_MCP_CONNECTION_MODE=Custom`、`GODOT_MCP_HOST` 和 `GODOT_MCP_START_SERVER=true`。选项：`--ready-timeout <ms>`、`--timeout <ms>`（工具调用，默认 310000：略长于服务器 5 分钟的 `tool-timeout`）、`--no-build`、`--editor-path`、`--token`、`--raw`。

当调用在 Godot 项目的**本地** URL 上被拒绝时，`run-tool` / `run-system-tool` 遵循同样的策略：启动一个无界面编辑器（除非 `--no-build`，否则先构建 C#），然后重试。正在运行的编辑器会被复用且从不关闭；`--once` 仅在本次调用启动了编辑器时于调用后将其关闭。`--no-start` 关闭此行为，云端或远程 URL 从不启动任何东西。选项：`--editor-path`、`--ready-timeout <ms>`。

<a id="exit-codes-and-output-formats"></a>
## 退出码与输出格式

退出码与 `unity-mcp-cli` 和 `unreal-mcp-cli` 共用（`godot-cli commands --format json` 会列出它们）：

| 代码 | 含义 |
| --- | --- |
| 0 | 成功 |
| 1 | 通用或未分类的错误；也包括被 Ctrl+C / SIGTERM 中断的运行（Godot 进程树会被终止） |
| 2 | 用法错误：未知命令或选项、错误的参数值或参数、未知工具（`NOT_FOUND`）（`--help` / `--version` 以 0 退出） |
| 6 | 命令已运行但失败：工具返回错误、脚本或编译错误、没有测试结论、凭据被拒绝 |
| 7 | 无法访问编辑器或 MCP 服务器（也包括 HTTP 502） |
| 8 | 测试已运行且至少一个失败（套件级 setup/teardown 失败也计入） |
| 14 | 超时（也包括 HTTP 408 / 504） |
| 17 | 服务器繁忙（HTTP 503 / 429）；调用未执行 |
| 18 | 编辑器被模态对话框阻塞（保留：在无界面 Godot 中无法检测） |

`update --check` 在有过期内容时仍以 2 退出。

**未成功的工具调用**（`run-tool`、`run-system-tool`、`exec`、`rql`、`tools`）在三个 CLI 中映射到相同的退出码和错误码（该表位于 `@baizor/gamedev-cli-core` 中；`commands --format json` 将其列为 `errorCodes`）。当 MCP 服务器有响应时，由其 HTTP 状态决定，信封中的错误会携带它：`errors[{code, message, httpStatus}]`。

| 情况 | HTTP 状态 | 退出码 | `errors[].code` |
|---|---|---|---|
| 参数错误：错误的参数、被 CLI 或服务器拒绝的工具参数（此处未列出的任何 4xx） | 400（422 `[invalid_params]`） | `2` | `USAGE` |
| 没有该名称的工具（用 `tools` 列出），或该路径下没有项目 | 404 | `2` | `NOT_FOUND` |
| 没有可用凭据，或服务器拒绝了凭据：用 `login` 登录 | 401, 403 | `6` | `NOT_AUTHENTICATED` |
| 工具已运行并报告错误（`status: "error"`，此处未列出的任何 5xx） | 409, 422, 500 | `6` | `TOOL_FAILED` |
| 项目代码未能构建，因此没有启动编辑器 | — | `6` | `COMPILATION_FAILED` |
| 连接被拒绝或重置、没有引擎插件连接到服务器、调用发出后插件丢失 | 502 | `7` | `UNREACHABLE` |
| 未及时响应：`--timeout`，或服务器自身的工具超时 | 408, 504 | `14` | `TIMEOUT` |
| 繁忙且未执行调用（正在重载、被限流、另一个 CLI 调用占用着编辑器）；请稍后重试 | 503, 429 | `17` | `BUSY` |
| 其他任何情况（调用被中止、意外错误） | — | `1` | `ERROR` |

`--format text|json|ndjson`（默认 `text`）适用于 `run-tool`、`run-system-tool`、`exec`、`status`、`wait-for-ready`、`check`、`import`、`test`、`export`、`run-script` 和 `commands`。`json` 在 stdout 上恰好输出一个信封：

```json
{ "success": false, "command": "check", "data": { "...": "..." },
  "errors": [{ "code": "SCRIPT_ERRORS", "message": "res://player.gd:12: error: Expected expression." }],
  "warnings": [] }
```

`ndjson` 在 stderr 上输出事件，每行一个 JSON 对象（`{"type":"progress","command":…,"message":…}`，其余每行为 `{"type":"log","command":…,"level":"info|error","message":…}`），并在 stdout 上以一行输出信封。两种格式下，日志、加载动画和标题都输出到 stderr 且不带颜色。错误码：`USAGE`、`TOOL_FAILED`、`UNREACHABLE`、`TIMEOUT`、`BUSY`、`AWAITING_INPUT`、`TESTS_FAILED`、`TEST_RUN_ERROR`、`COMPILATION_FAILED`、`SCRIPT_ERRORS`、`IMPORT_FAILED`、`PROJECT_OPEN`、`NOT_FOUND`、`EXPORT_FAILED`、`NOT_AUTHENTICATED`、`CLOUD_PROJECT`、`ERROR`。`--raw` 的行为保持不变。

<a id="headless-commands-check-import-test-export-run-script"></a>
## 无界面命令（`check`、`import`、`test`、`export`、`run-script`）

这些命令在没有编辑器会话的情况下运行 Godot，不需要 MCP 服务器。在无界面模式下，Godot 的退出码不能作为结论（带 GDScript 解析错误的 `--import`，或加载失败的插件，仍会以 0 退出），因此每个命令都根据其日志（`SCRIPT ERROR`、`ERROR:`、`Parse Error`）、证明步骤已运行的标记，以及必须生成的文件来判定结果。每个进程都有硬性的 `--timeout`（整个 Godot 进程树会被终止：退出码 14）。日志和临时脚本写入 `<project>/.godot/godot-cli/`。`check`、`import`、`test` 和 `export` 会拒绝已在编辑器中打开的项目（退出码 6，`PROJECT_OPEN`：导入会与该编辑器争夺 `.godot/`）；`--allow-open` 可覆盖，`close` 可停止 `godot-cli` 启动的编辑器。可执行文件来自 `--editor-path`、`GODOT_BIN`、`PATH` 或常见安装文件夹。

```bash
godot-cli check                     # 0 无问题，6 SCRIPT_ERRORS / COMPILATION_FAILED，14 超时
godot-cli check --strict            # GDScript 和 C# 编译器警告也视为失败
godot-cli test --format github      # ::error 注解；0 通过，8 有失败，6 无结论 / 无测试运行器
godot-cli export "Windows Desktop" build/game.exe
godot-cli run-script tools/migrate.gd -- --dry-run
```

- **`check [path] [--strict] [--include-addons]`** 运行四个独立的进程（单个冷启动的 `--build-solutions --quit` 会在 C# `[Tool]` 插件上崩溃）：`--headless --import`；C# 项目的 `dotnet build`（错误格式为 `file(line,col): error CS…`）；`--headless --editor --quit`，当插件启用时必须输出 `[Godot-MCP] plugin loaded`；以及一次 GDScript 检查，加载 `res://` 下的每个 `.gd`（仅在 `--include-addons` 时包含 `res://addons/`），并以 `res://file:line` 报告解析错误。`--strict` 还会在 GDScript 警告（项目中 Warn 级别的警告在内存中提升为错误，从不保存）和 C# `CS` 警告时失败。引擎消息（例如某个插件的运行时警告）作为警告列出，绝不会导致检查失败。
- **`import [path]`** 在脚本错误（`SCRIPT_ERRORS`）和资源导入失败（`IMPORT_FAILED`）时失败。
- **`test [path]`** 查找 GUT（`addons/gut`）或 gdUnit4（`addons/gdUnit4`），导入项目（`--no-import` 跳过），并以 JUnit 报告（`-gjunit_xml_file`，或 gdUnit4 的 `-rd`；gdUnit4 以 `--ignoreHeadlessMode -c` 运行）运行测试运行器的命令行脚本。所有测试通过时以 0 退出，有失败时以 8 退出；没有报告、没有运行任何测试，或某个测试脚本加载失败（其测试不会出现在报告中，因此不能算通过）时以 6（`TEST_RUN_ERROR`）退出；未安装测试运行器时以 6（`NOT_FOUND`）退出并给出安装提示。`--runner gut|gdunit4`、`--dir <dir>`（可重复；默认 `res://test`、`res://tests` 或 GUT 的 `.gutconfig.json`）、`--filter <text>`（文件名包含该文本的测试脚本）、`--output <file>`（复制 JUnit XML）、`--format text|json|ndjson|github`。
- **`export <preset> <output> [--debug|--pack]`** 检查 `export_presets.cfg` 中是否有该预设、编辑器版本对应的导出模板是否已安装（除非使用 `--pack` 或预设指定了自己的模板），运行 `--export-release|--export-debug|--export-pack`，并且除非输出文件存在、非空、由本次运行写入，且日志中没有导出错误，否则以失败（`EXPORT_FAILED`）告终（即使 C# 发布因缺少 `.sln` 而失败，Godot 也会写出可执行文件并以 0 退出）。
- **`run-script <script> [args...]`** 运行 `godot --headless --path <project> -s <script> -- <args>`：脚本继承 `SceneTree` 或 `MainLoop`，并以 `quit(code)` 结束。`quit(0)` 时以 0 退出；其他代码（`data.scriptExitCode`）或脚本加载失败时以 6 退出；`--timeout`（默认 300000 毫秒）时以 14 退出。它不执行导入，因此在编辑器打开该项目时也能运行。

<a id="login"></a>
## `login`

`godot-cli login` 使用 **OAuth 2.1 设备授权流程**（RFC 8628）为编辑器插件到 [ai-game.dev](https://ai-game.dev) 的**云端**连接进行认证——它会打印一个简短的用户代码和验证 URL，打开浏览器，并轮询直到你批准：

```bash
godot-cli login                       # 每台机器登录一次（默认）
godot-cli login --project ./MyGame    # 改为保存项目级凭据
godot-cli login --base-url <url>      # 针对非默认服务器进行认证
godot-cli login --force               # 覆盖现有凭据重新认证
godot-cli login --tools-only         # CI/自动化：仅工具凭据（App 不会采用）
godot-cli login --yes                # 非交互：确认替换另一个账户的凭据
```

- 默认情况下，凭据保存到共享的**本机存储** `~/.ai-game-dev/credentials.json`（POSIX 上为 `0600` / Windows 上为 DPAPI）——**每台机器登录一次**，Godot 编辑器插件就会自动采用它，因此 `godot-cli open --mode Cloud` **无需 `--token`** 即可连接。
- 默认登录会让整台机器登录：该流程请求一个**智能体范围（agent-scope）**的授权，并通过 RFC 8693 令牌交换从中派生出范围更窄的**工具（插件）凭据**，因此机器上的每个 AI-Game-Dev 工具（引擎插件、CLI、桌面 App）都能获得该登录状态。
- `--tools-only` **只**签发工具凭据——不存储任何智能体范围的凭据，桌面 App 无法采用该登录，运行器在 [ai-game.dev](https://ai-game.dev) 上显示为一个可单独吊销的设备。适用于 CI 和共享的自动化运行器。
- 如果机器上已有**另一个账户**的凭据，login 会在替换前询问（拒绝会吊销刚签发的凭据，并保持存储不变）。传入 `--yes` 可非交互地确认。
- `--project <path>`（或位置参数 `[path]`）为按项目区分账户的场景保存项目级凭据（`<path>/.ai-game-dev/credentials.json`，已被 git 忽略）。旧的项目级存储 `<path>/.godot-mcp/credentials.json` **不再写入**——在一个发布版本内仍会读取它（并在首次使用时迁移到本机存储）。
- 该流程持久化**完整**的凭据集（访问令牌 + 轮换的刷新令牌 + 过期时间）——**从不签发个人访问令牌（PAT）**。任何失败都不会写入任何内容，因此现有凭据在被拒绝 / 过期 / 网络错误时完好无损。命令会自动刷新过期的访问令牌（通过 `@baizor/gamedev-cli-core`），因此已登录的机器无需重新运行 `login` 即可保持登录状态。

<a id="setup-skills"></a>
## `setup-skills`

`godot-cli setup-skills <agent> [path]` 在所选智能体的 skills 路径下（例如 Claude Code 的 `.claude/skills`）为 Godot 项目生成 AI 智能体 **skill 文件**。使用 `--list` 查看每个智能体及其 skills 支持情况。

```bash
godot-cli setup-skills claude-code            # 生成到 ./.claude/skills
godot-cli setup-skills cursor ./MyGame        # 生成到 MyGame/.cursor/skills
godot-cli setup-skills --list                 # 列出智能体及其 skills 路径
```

它离线写入以下内容（不需要编辑器、服务器或网络）：

- `godot-cli/SKILL.md`：智能体如何从冷启动开始使用此 CLI：第一条编辑器命令会启动无界面编辑器，`rql` 及其针对场景、节点和资源的示例，`check` / `test`，`--format json`，以及每个退出码的含义。它有意保持简短（智能体读到的每个字节都会留在上下文中）；`references/rql.md` 和 `references/install.md` 包含深入内容，仅在需要时读取。示例与 `godot-cli --help` 和 `rql --help` 来自同一来源，因此 skill 与帮助信息不会出现偏差。
- `ai-game-dev-app/SKILL.md`：配套应用，用于 CLI 不具备的创意类工具。

它是**幂等**的：重新运行会写出完全相同的字节。它会移除早期版本根据手写目录生成的 `godot-mcp` / `godot-mcp-<family>` skills。按工具划分的 skills 由插件负责：每当编辑器运行时，插件都会根据其实际注册的工具生成它们（可在插件的 Skills 面板中开关），同时生成其 `godot-cli-install` 指南。

<a id="install-plugin"></a>
## `install-plugin`

`godot-cli install-plugin [path]` 是一个**真正的安装器**——它用一条幂等命令让从零开始的终端安装得到一个可用的项目：

```bash
godot-cli install-plugin ./MyGodotProject                      # 下载匹配的发布版并安装
godot-cli install-plugin --version <x.y.z> ./MyGodotProject   # 固定某个插件发布版
godot-cli install-plugin --source ./Godot-MCP/addons/godot_mcp ./MyGodotProject   # 离线 / 开发副本
```

它执行四个步骤：

1. **生成 `res://addons/godot_mcp/`。** 默认情况下，它通过 HTTPS **仅从 github.com** 下载 `godot-mcp-addon-<version>.zip`（`IvanMurzak/Godot-MCP` 的发布版 `v<version>`；版本默认为 CLI 自身的版本）。非 `github.com` 的主机和普通 `http` 会被拒绝。使用 `--source <path>` 时，它改为从本地目录复制插件（不联网）——`<path>` 可以*就是* `addons/godot_mcp` 目录，也可以是*包含*它的目录。
2. **添加插件所需的 NuGet 包**（`com.IvanMurzak.ReflectorNet`、`com.IvanMurzak.McpPlugin`）到项目的 `.csproj`，且是幂等的——缺失时添加，版本过旧时校正，版本正确时保持不变。版本来自正在安装的插件：其 `nuget-deps.json`（由发布工作流从 `Godot-MCP.csproj` 生成到每个发布版 zip 中），或者当 `--source` 指向 Godot-MCP 检出目录时，来自该检出的 `Godot-MCP.csproj`。没有该清单的旧版插件 zip 会回退到 CLI 内置的版本，并给出警告。
3. **嵌入扩展目录**（`<EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />`）到项目的 `.csproj`，且是幂等的。没有它，插件的扩展注册表在编辑器运行时读不到任何目录，**Extensions 面板为空**。它与版本固定一样只有单一来源，并与插件的 csproj 做一致性测试。它还会设置 `<EnableDynamicLoading>true</EnableDynamicLoading>`（Godot 的模板自带；没有它，从编辑器运行的游戏无法实例化 C# 脚本）。
4. 在 `project.godot` 的 `[editor_plugins]` 中**启用插件**。

它对库调用是安全的（返回 `{ kind: 'success' | 'failure' }` 联合类型；从不在公共边界之外抛出异常）且是幂等的——在已安装的项目上重新运行会报告没有变化。

<a id="install-extension"></a>
## `install-extension`

`godot-cli install-extension <id> [path]` 将一个可选的 Godot-MCP **扩展**（扩展 RQL 的可选 NuGet 包；它本身不添加工具）安装到 Godot C# 项目中——这是编辑器内 **Extensions** 停靠面板所执行的相同安装在终端/库中的通道：

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject   # 添加/更新 PackageReference
godot-cli install-extension "CSG Tools"                                      # 按名称解析；默认当前目录
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG --version <x.y.z>   # 覆盖目录中固定的版本
```

- 根据**共享扩展目录**（`addons/godot_mcp/extensions.catalog.json`——停靠面板、CLI 和应用共同使用的唯一事实来源）解析 `<id>`，先按包 id 匹配（再按名称），不区分大小写。
- 以“读取-修改-写入”的方式将 `<PackageReference Include="<packageId>" Version="<version>" />` 写入项目根目录的 `.csproj`：缺失时**添加**，仅当目录（或 `--version`）固定了更新的版本时才**升级**版本，已是最新时**不做任何事**——然后提示你重新构建（`godot-cli build`），以便 Godot 还原并编译新包。
- 行为与停靠面板的 `ExtensionInstaller` 完全一致（相同的添加 / 更新 / 不变规则以及数字版本比较规则；由双方共享的一组场景验证）。

<a id="library-api"></a>
## 库 API

该包还导出一个无副作用的库（`.` 入口）：

```ts
import { openProject, runTool, setupMcp, installPlugin, installExtension, execCode, ensureEditor } from 'godot-cli';

// 共享扩展目录及其查找辅助函数也会导出，因此 GUI（应用）
// 可以渲染与停靠面板和 CLI 安装时相同的列表：
import { EXTENSIONS_CATALOG, findExtension } from 'godot-cli';

// 无界面命令、它们的日志/JUnit 解析器以及退出码：
import { checkProject, importProject, runTests, exportProject, runScript, parseGodotLog, parseJUnit, ExitCode } from 'godot-cli';
```

每个函数都返回一个可辨识联合类型（`{ kind: 'success', ... }` / `{ kind: 'failure', error }`），从不在公共边界之外抛出异常。

<a id="development"></a>
## 开发

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```


<a id="supported-ai-agents"></a>
## 支持的 AI 智能体

`godot-cli` 为所有主流 AI 编程智能体写入开箱即用的 MCP 客户端配置——运行 `godot-cli setup-mcp <agent> [path]` 即可接入（运行 `godot-cli setup-skills <agent> [path]` 生成其 skill 文件）。在任一命令上使用 `--list` 查看所有支持的智能体。

支持的智能体包括 Claude Code、Claude Desktop、Cursor、VS Code 和 Visual Studio（Copilot）、GitHub Copilot CLI、Rider（Junie）、Codex、Gemini、Antigravity、Cline、Open Code、Kilo Code，以及通用的 `custom` MCP 客户端。

```bash
godot-cli setup-mcp --list                    # 列出所有支持的智能体 id
godot-cli setup-mcp claude-code ./MyGame      # 写入智能体的 MCP 客户端配置（绑定到本项目）
godot-cli setup-mcp claude-code ./MyGame --no-pin   # 改为写入不带绑定的 <host>/mcp URL
godot-cli setup-mcp claude-code ./MyGame --oauth    # 只含 URL：智能体通过自己的 OAuth 登录
godot-cli setup-mcp claude-code ./MyGame --regenerate-key   # 新的项目密钥；旧密钥被吊销
```

在 Cloud 模式下，`setup-mcp` 会为每个智能体在其配置中写入一个**项目密钥**：`Authorization: Bearer agd_pk_…`（Codex：`http_headers`）。项目密钥永不过期，绑定到本项目的 pin（无法访问你账户下的任何其他项目），每个项目用你的本机登录状态（`godot-cli login`）创建一次，之后从 `~/.ai-game-dev/project-keys.json` 中复用。`--regenerate-key` 替换它并吊销旧密钥。没有本机登录（或服务器无法签发密钥）时，配置只含 URL，智能体通过自己的 OAuth 认证；`--oauth` 显式要求生成这种只含 URL 的配置。显式的 `--token` 始终优先。本地服务器配置（`--url http://localhost:…`）从不携带项目密钥。

> 完整的 Godot-MCP 项目文档请参阅[主 README](../../docs/README.zh-CN.md)。由 **[ai-game.dev](https://ai-game.dev)** 提供支持。
