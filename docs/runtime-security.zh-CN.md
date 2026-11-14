[English](runtime-security.md) · **中文** · [日本語](runtime-security.ja.md) · [Español](runtime-security.es.md) · [فارسی](runtime-security.fa.md) · [हिन्दी](runtime-security.hi.md) · [العربية](runtime-security.ar.md)

# 运行时（游戏内）安全约定

Godot-MCP 不仅能在编辑器中运行，还能**在运行中 / 已导出的游戏构建内**（debug 或 release）运行——完整用法见[运行时用法（游戏内）](runtime.zh-CN.md)。在已发布的游戏中运行 MCP 连接会打开一个**远程控制面**：你注册的 `[AiToolType]` 工具能做的任何事，已连接的 MCP 客户端都能驱动。本文档是该模式的安全约定。在发布调用 `GodotMcpRuntime.Initialize(...)` 的构建之前，请先阅读本文档。

<a id="guarantees-the-runtime-gives-you"></a>
## 运行时为你提供的保证

- **仅限显式启用——从不自动连接。** 编辑器插件在启动时连接；游戏构建**不会**。`GodotMcpRuntime.Initialize(...).Build()` 返回一个**默认关闭**的 `GodotMcpRuntimeHandle`。在*你的*代码显式调用 `handle.Connect()` 之前，不会建立任何连接。非编辑器构建中不存在自动连接的代码路径。
- **默认零工具——完全手动。** 新构建的运行时**不注册任何** MCP 工具。攻击面恰好是你通过 `builder.WithToolsFromAssembly(...)` / `builder.WithTools(...)` 启用的 `[AiToolType]` 类集合——仅此而已。若一个也没注册，连接仍会以空工具集构建并连接（与 Unity-MCP 的模型完全一致）。
- **编辑器工具系列不会泄漏进来。** 插件的编辑器工具系列（`Tool_Rql`、`Tool_Script`、`Tool_Screenshot`、`Tool_Session`）受 `#if TOOLS` 控制，**不会编译**进导出的游戏构建。即使对插件自身的程序集调用 `WithToolsFromAssembly(...)` 也无法注册它们。
- **不自动加载持久化配置。** 游戏构建从不会悄悄读取已保存的（`user://`）配置文件——那是仅限编辑器的便利功能。主机和令牌只来自你的代码（`WithConfig`），或来自 `GODOT_MCP_*` 进程环境变量 / 项目 `.env`，由 `GodotMcpConfig` 实时读取。

<a id="what-you-must-do-when-enabling-it"></a>
## 启用时你必须做的事

- **有意识地连接。** 仅在你确实打算暴露工具时（开发构建、QA 测试工具、专门设计的游戏内智能体功能）才调用 `handle.Connect()`。调用 `handle.Disconnect()` / `handle.Dispose()` 停止暴露。
- **只注册最少的工具集。** 只需要少数几个工具时，优先使用 `WithTools(typeof(MyToolFamily))`，而不是宽泛的 `WithToolsFromAssembly(...)`。每个已注册的工具都能被已连接的客户端访问。
- **优先使用回环地址 + 令牌。** 对于本地工具，将 Custom 模式绑定到回环主机（`http://localhost:…` / `127.0.0.1`），并设置 `AuthOption = Consts.MCP.Server.AuthOption.token` 以及真实的 `Token`：

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // 回环地址
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // 不推荐：见下方的环境变量形式——不要发布硬编码的机密
  });
  ```

  优先使用下面的带外形式，让构建中不嵌入任何机密：

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **不要在 release 构建中于公共网络接口上暴露未认证的 MCP 接口**，除非你已专门设计并保护了该接口。任何非回环主机都应是经过审查的有意决定。
- **仅在断开连接时更改主机或模式。** 除非你指定了自己的 `CredentialProvider`，否则每个连接的 bearer 令牌都绑定到它所拨号的模式和主机：更改之后，现有链路的重连不会发送令牌，因此请调用 `handle.Disconnect()` 和 `handle.Connect()` 来切换服务器。
- **不要在发布的二进制文件中嵌入长期有效的机密。** 优先从环境变量读取 `GODOT_MCP_TOKEN`，而不是硬编码 `config.Token`，这样令牌就无法从构建中提取出来。
- **仅在可信连接上启用运行时错误捕获。** `builder.WithRuntimeErrorCapture()` **默认关闭**。启用后，捕获的错误会通过 `runtime-errors-get` 工具把**完整**消息以及（对于 C# 故障）**完整的托管堆栈跟踪**转发给已连接的智能体。这些字符串可能包含敏感的运行时数据——绝对文件系统路径、机器名/用户名、查询字符串，或者出现在异常消息或参数中的机密/令牌。这正是其预期的诊断价值，但也扩大了经由连接暴露的数据范围。仅在回环主机上、使用 `AuthOption = Consts.MCP.Server.AuthOption.token` 和真实令牌时启用——绝不要在 release 构建中于未认证的公共接口上启用。

<a id="in-game-code-execution-withscriptexecute"></a>
## 游戏内代码执行（`WithScriptExecute`）

`builder.WithScriptExecute()` 在游戏中注册 `script-execute`：已连接的客户端发送 C#，游戏用 Roslyn 编译并在其主线程上运行。这是**在游戏进程中的任意代码执行**，拥有玩家的文件系统和网络访问权限。它默认关闭，且需要在构建时启用：只有当导出游戏的 `.csproj` 设置了 `GodotMcpCodeExecInGame=true` 并定义了 `GODOT_MCP_CODE_EXEC_IN_GAME` 时，导出的游戏才包含 Code-Exec 和 Roslyn（[在游戏中运行 C#](runtime.zh-CN.md#running-c-in-the-game)）。未启用时，该调用会记录一条警告且不注册任何内容。

> **从编辑器启动的游戏。** 它们的构建就是编辑器构建（`TOOLS`），因此插件的编辑器工具系列也会编译进游戏程序集。此时对该程序集调用 `WithToolsFromAssembly(...)` 就会注册它们，包括编辑器的 `script-execute`，无论是否调用了 `WithScriptExecute()`。在导出构建中它们不存在。如果从编辑器运行的游戏不得提供它们，请注册具体类型（`WithTools(...)`）。

- **仅限 debug / 开发构建。** 用你自己的开发开关控制该调用（以及 csproj 中的启用项）。绝不要在公开的 release 构建中启用它。
- **始终使用回环地址 + 令牌。** 使用回环主机和 `AuthOption = token`，并从环境变量获取真实令牌。任何能在该连接上调用工具的人都等于掌控了该进程。
- **仅限桌面 CoreCLR。** NativeAOT 和 iOS 无法运行运行时生成的代码；移动平台未经测试。
- **体积。** 启用后会在导出中加入 Roslyn（`Microsoft.CodeAnalysis*.dll`，数 MB）。RQL 永远不会随游戏发布。

<a id="environment-variables"></a>
## 环境变量

运行时配置（`GodotMcpConfig`）实时读取以下 `GODOT_MCP_*` 变量（进程环境或项目 `.env`）。在解析时它们会覆盖代码中设置的值。

| 变量 | 取值 | 说明 |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | 连接模式（回环主机隐含 `Custom`）。 |
| `GODOT_MCP_CLOUD_URL` | URL | 覆盖云端基础 URL（默认 `https://ai-game.dev`）。 |
| `GODOT_MCP_HOST` | URL | Custom 模式的服务器主机（默认 `http://localhost:8080`）。 |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Custom 模式认证：匿名 / 账户（oauth）/ 离线 bearer（`token`）。未设置时，`GODOT_MCP_TOKEN` 会选择 `token`。 |
| `GODOT_MCP_TOKEN` | string | Bearer 令牌（按当前模式发往 Cloud 或 Custom）。 |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | 日志详细程度阈值。 |
| `GODOT_MCP_EDITOR_PID` | 进程 id | 由编辑器插件在编辑器进程中设置，因此编辑器启动的每个进程都会继承它。在该编辑器运行期间，带有此变量的进程中运行时的 `Connect()` 保持离线（除非按下一行启用）。 |
| `GODOT_MCP_RUNTIME_FROM_EDITOR` | `1` / `true` | 仅限进程环境。允许运行时在从 Godot 编辑器启动的游戏中连接（默认在那里保持离线，因为游戏继承了编辑器的 `GODOT_MCP_*` 设置）；等同于 `builder.ConnectWhenRunFromEditor()`。 |

<a id="editor-side-surfaces-accepted-posture"></a>
## 编辑器端接口（已接受的安全姿态）

上述约定针对的是**游戏构建**。这里为完整起见记录两个编辑器端接口；二者目前都是**有意为之**（尚未实现加密 / 共享密钥——这些是推迟的、受一致性约束的决策，需要在 Unity / Godot / Unreal 姊妹项目之间协调）。

<a id="editor-token-storage-is-plaintext-at-rest"></a>
### 编辑器令牌以明文形式静态存储

当你连接**编辑器插件**（Cloud 设备认证，或 Custom 模式令牌）时，它会将你的连接配置持久化到 **`user://godot-mcp-config.json`**（Godot 通过 `ProjectSettings.GlobalizePath` 将 `user://` 解析为各平台的用户数据目录）。Bearer 令牌（`token`）和 Cloud 令牌（`cloudToken`）以**明文 JSON** 写入——**没有**加密，也**没有**存入操作系统密钥库。

- **信任假设：本地用户账户。** 任何能读取你用户数据目录的人都能读取该令牌。请把 `user://godot-mcp-config.json` 当作机密——不要提交它、同步到共享盘或粘贴它。
- **轮换。** 在停靠面板中清除已保存的令牌（或删除该文件）后重新连接。
- **优先级。** `GODOT_MCP_TOKEN` 进程环境变量的值，或 Custom 模式下 `.env` 中的值，会在解析时遮蔽已持久化的令牌，且**不会**写回此文件——因此 CI / 共享机器可以在从不将令牌持久化到磁盘的情况下运行。在 Cloud 模式下从 `.env` 读取的令牌会在停靠面板保存时写回。
- **设置了的 `GODOT_MCP_TOKEN` 会发送给 Custom 主机。** 在没有 `GODOT_MCP_AUTH_OPTION` 时，它会覆盖已配置的 `AuthOption = none`，包括在代码中设置的。设置 `GODOT_MCP_AUTH_OPTION=none` 可使连接保持匿名。

<a id="the-dev-control-bridge-is-unauthenticated-but-gated-off"></a>
### dev-control 桥接无认证，但默认被关闭

存在一个仅用于开发的注入/控制 HTTP 桥接（`DevControlServer`），用于从测试 / 终端驱动编辑器停靠面板。它**没有认证**，但其安全边界有三重：

1. **仅限编辑器。** 它在 `#if TOOLS` 之后编译，因此不存在于导出的游戏构建中。
2. **仅限回环。** 它只绑定 **`127.0.0.1`**——绝不绑定可路由的网络接口。
3. **由环境变量控制，默认关闭。** **仅当 `GODOT_MCP_DEV_CONTROL=1`**（必须恰好是 `1`）时才会构造并启动它。未设置 / 任何其他值都意味着桥接从不监听——已发布的插件以及任何正常的编辑器会话都不会打开该接口。

这个环境变量开关是**关键所在**：它是"开发脚手架"与"实时停靠面板上的无认证控制接口"之间唯一的屏障。它由一个纯托管的判定函数（`DevControlGate.IsEnabled`）配合单元测试（`DevControlGateTests`）**以及**在构造处的启动时断言（`DevControlGate.AssertEnabledOrThrow`）共同保证，因此任何在未设置该环境变量时就启用桥接的回归都会在 CI 和运行时快速失败，而不会悄无声息地以启用状态发布。
