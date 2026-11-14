[English](runtime.md) · **中文** · [日本語](runtime.ja.md) · [Español](runtime.es.md) · [فارسی](runtime.fa.md) · [हिन्दी](runtime.hi.md) · [العربية](runtime.ar.md)

# 运行时用法（游戏内）

编辑器插件连接的是 **Godot 编辑器**。Godot-MCP 也可以在**运行中或已导出的游戏**（debug 或 release）内运行，让 LLM 读取并操控实时的游戏状态：例如一个国际象棋游戏，其机器人的走法由 LLM 通过你编写的两个工具给出。

运行时模式有意与编辑器不同：

- **它从不自动连接。** 由你的代码决定是否以及何时调用 `Connect()`。
- **默认不暴露任何内容。** 每个工具（`[AiToolType]`）、提示词（`[AiPromptType]`）和资源（`[AiResourceType]`）都由你自己注册；每一类都是可选的。插件的编辑器工具位于 `#if TOOLS` 之后，不会编译进导出的游戏。

在发布调用 `GodotMcpRuntime.Initialize` 的构建之前，请先阅读 [runtime-security.md](runtime-security.zh-CN.md)。

<a id="set-it-up"></a>
## 配置

在 `SceneTree` 存在后调用一次 `GodotMcpRuntime.Initialize(...)`，例如在某个 autoload 的 `_Ready()` 中：

```csharp
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;   // GodotMcpConnectionMode
using com.IvanMurzak.Godot.MCP.Runtime;      // GodotMcpRuntime
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;   // AuthOption
using Godot;

public partial class GameMcp : Node
{
    private GodotMcpRuntimeHandle? _mcp;

    public override async void _Ready()
    {
        _mcp = GodotMcpRuntime.Initialize(builder =>
        {
            builder.WithConfig(config =>
            {
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // 你自己的服务器
                config.Host           = "http://localhost:8080";           // 优先使用回环地址
                config.AuthOption     = McpServerConsts.AuthOption.token;  // 要求令牌
                // 令牌：优先从环境变量 GODOT_MCP_TOKEN 读取，而不是写在代码中。
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] 类
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] 类
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] 类
        }).Build();

        await _mcp.Connect();   // 显式启用；KeepConnected 为 true 时在后台重试
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| 调用 | 作用 |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | 开始配置；`configure` 可为 `null`，即零工具、由环境变量配置的默认设置。 |
| `WithConfig(Action<GodotMcpConfig>)` | 在代码中设置 `Host`、`Token`、`ConnectionMode`、`AuthOption`。多次调用按顺序叠加。 |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | 注册 `[AiToolType]` 类：整个程序集，或指定类型。 |
| `WithPromptsFromAssembly` / `WithPrompts` | 对 `[AiPromptType]` 类同理。 |
| `WithResourcesFromAssembly` / `WithResources` | 对 `[AiResourceType]` 类同理。 |
| `WithoutMainThreadDispatcher()` | 跳过自动的主线程调度器（仅当你安装了自己的调度器时）。 |
| `ConnectWhenRunFromEditor()` | 在从编辑器启动的游戏中也进行连接（默认关闭，见下文）。 |
| `WithRuntimeErrorCapture()` | 捕获游戏内错误并注册 `runtime-errors-*`。默认关闭。 |
| `WithScriptExecute()` | 在游戏中注册 `script-execute`。默认关闭；需要在 csproj 中启用。 |
| `Build()` | 返回一个未连接的 `GodotMcpRuntimeHandle`。 |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | 打开、关闭或销毁连接。 |

`Build()` 还会向运行中的 `SceneTree` 添加一个主线程调度器节点（除非你选择不添加），以便工具处理函数可以使用 `MainThread.Instance.Run(...)`。

<a id="server-url-and-token"></a>
## 服务器 URL 与令牌

游戏从不加载编辑器保存的配置。它从你的代码（`WithConfig`）以及 `GODOT_MCP_*` 进程环境变量或项目 `.env` 文件中获取设置；后两者实时读取，并优先于代码。变量列表见 [runtime-security.md](runtime-security.zh-CN.md#environment-variables)。对于游戏，推荐的配置是 Custom 模式、回环主机加令牌。

<a id="games-run-from-the-editor"></a>
## 从编辑器运行的游戏

通过 Play（F5）启动的游戏会继承编辑器的环境，包括 `GODOT_MCP_HOST` 和 `GODOT_MCP_TOKEN`。因此默认情况下，在这样的游戏中 `handle.Connect()` 不做任何事并返回 `false`（会记录原因）：编辑器仍是智能体唯一对话的插件。导出的游戏以及在编辑器之外启动的游戏会正常连接。若要让从编辑器运行的游戏也连接，请使用 `builder.ConnectWhenRunFromEditor()` 或 `GODOT_MCP_RUNTIME_FROM_EDITOR=1`。此时，支持插件角色的 GameDev-MCP-Server 会保留两个连接，并在编辑器连接期间将工具调用路由到编辑器。

<a id="a-game-tool"></a>
## 一个游戏工具

运行时工具的写法与编辑器工具相同（[custom-tools.md](custom-tools.zh-CN.md)）。将仅用于游戏的系列标记为 `Target = ToolTarget.Runtime`，这样编辑器不会注册它们，即使编辑器已连接，智能体也能访问游戏。

```csharp
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;              // [AiToolType], [AiTool]
using com.IvanMurzak.McpPlugin.Common;       // ToolTarget
using com.IvanMurzak.ReflectorNet.Utils;     // MainThread
using Godot;

[AiToolType(Target = ToolTarget.Runtime)]
public partial class GameMcpTools
{
    [AiTool("game-scene-tree-summary", Title = "Game Scene-Tree Summary", ReadOnlyHint = true)]
    [Description("Summary of the running game's SceneTree: current scene and root child names.")]
    public SceneTreeSummary GameSceneTreeSummary()
    {
        return MainThread.Instance.Run(() =>
        {
            var summary = new SceneTreeSummary();
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return summary;

            summary.CurrentSceneName = tree.CurrentScene?.Name ?? "<none>";
            foreach (var child in tree.Root.GetChildren())
                summary.RootChildNames.Add(child.Name);
            return summary;
        });
    }
}

public sealed class SceneTreeSummary
{
    public string CurrentSceneName { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> RootChildNames { get; set; } = new();
}
```

提示词和资源的注册方式相同：一个标有 `[AiPromptType]` / `[AiResourceType]` 的 `partial class`，其成员标有 `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`。资源方法返回 `ResponseResourceContent[]`（例如 `ResponseResourceContent.CreateText(uri, json, "application/json")`）。所有这些类型都来自 `com.IvanMurzak.McpPlugin` 包。

<a id="capturing-in-game-runtime-errors"></a>
## 捕获游戏内运行时错误

编辑器工具看不到运行中游戏内产生的错误，因此智能体可能启动游戏、读到一片安静的日志，并误以为一切正常。`builder.WithRuntimeErrorCapture()` 弥补了这一缺口。它安装三个尽力而为的通道，并注册 `runtime-errors-get` / `runtime-errors-clear`：

1. **引擎错误（Godot 4.5+）**，通过 `OS.AddLogger`：GDScript 运行时错误、`push_error` / `push_warning` 和着色器错误，带有 `file`、`line` 和 `function`。在 Godot 4.5+ 上，GDScript 错误还会在 `frames`（最内层在前）和 `stackTrace` 中携带完整调用栈。
2. **C# 未处理异常**（`AppDomain.CurrentDomain.UnhandledException`），带完整的托管堆栈跟踪。
3. **C# 未观察的 `Task` 异常**（`TaskScheduler.UnobservedTaskException`），仅为记录日志而观察。

在低于 4.5 的 Godot 上引擎通道不可用；C# 通道仍然有效。`handle.Dispose()` 会移除这些钩子。捕获到的消息和堆栈跟踪会原样发送给智能体，可能包含路径、用户名或机密信息，因此只应在可信的回环 + 令牌连接上启用捕获。

<a id="running-c-in-the-game"></a>
## 在游戏中运行 C#

`builder.WithScriptExecute()` 让智能体发送 C# 代码，由运行中的游戏用 Roslyn 编译并在其主线程上运行，可使用游戏自身的类型、autoload 和场景树。导出构建不包含 Roslyn，因此游戏的 `.csproj` 需要显式启用：

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

当前版本的 `godot-cli install-plugin` 会写入带有 `Condition` 的 Code-Exec 和 Roslyn 包引用，该条件会遵循此属性；RQL 永远不会随游戏发布。未启用时，`WithScriptExecute()` 会记录一条警告且不注册任何内容。

> **这就是任意代码执行。** 任何能在该连接上调用工具的人，都能在游戏进程中运行任意代码。仅在开发构建中、在带令牌的回环主机上使用。仅限桌面 CoreCLR：NativeAOT 和 iOS 无法运行运行时生成的代码。
