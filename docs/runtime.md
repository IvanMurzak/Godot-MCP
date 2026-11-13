# Runtime usage (in-game)

The editor plugin connects the **Godot editor**. Godot-MCP can also run inside a **running or exported game**
(debug or release), so an LLM can read and drive live game state: for example a chess game whose bot moves come
from an LLM through two tools you write.

Runtime mode differs from the editor on purpose:

- **It never connects by itself.** Your code decides if and when to call `Connect()`.
- **It exposes nothing by default.** You register every tool (`[AiToolType]`), prompt (`[AiPromptType]`) and
  resource (`[AiResourceType]`) yourself; each kind is optional. The addon's editor tools are behind `#if TOOLS` and
  do not compile into an exported game.

Read [runtime-security.md](runtime-security.md) before you ship a build that calls `GodotMcpRuntime.Initialize`.

## Set it up

Call `GodotMcpRuntime.Initialize(...)` once a `SceneTree` exists, for example from an autoload's `_Ready()`:

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
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // your own server
                config.Host           = "http://localhost:8080";           // prefer loopback
                config.AuthOption     = McpServerConsts.AuthOption.token;  // require a token
                // Token: prefer GODOT_MCP_TOKEN from the environment over a value in code.
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] classes
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] classes
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] classes
        }).Build();

        await _mcp.Connect();   // explicit opt-in; retries in the background while KeepConnected is true
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| Call | What it does |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | Start configuring; `configure` may be `null` for the zero-tool, environment-configured default. |
| `WithConfig(Action<GodotMcpConfig>)` | Set `Host`, `Token`, `ConnectionMode`, `AuthOption` in code. Calls compose in order. |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | Register `[AiToolType]` classes: a whole assembly, or specific types. |
| `WithPromptsFromAssembly` / `WithPrompts` | The same for `[AiPromptType]` classes. |
| `WithResourcesFromAssembly` / `WithResources` | The same for `[AiResourceType]` classes. |
| `WithoutMainThreadDispatcher()` | Skip the automatic main-thread dispatcher (only if you install your own). |
| `ConnectWhenRunFromEditor()` | Also connect in a game started from the editor (off by default, see below). |
| `WithRuntimeErrorCapture()` | Capture in-game errors and register `runtime-errors-*`. Off by default. |
| `WithScriptExecute()` | Register `script-execute` in the game. Off by default; needs the csproj opt-in. |
| `Build()` | Returns a `GodotMcpRuntimeHandle` that is not connected. |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | Open, close, or tear down the connection. |

`Build()` also adds a main-thread dispatcher node to the running `SceneTree` (unless you opt out), so tool handlers
can use `MainThread.Instance.Run(...)`.

## Server URL and token

A game never loads the editor's saved config. It takes the settings from your code (`WithConfig`) and from
`GODOT_MCP_*` process environment variables or a project `.env` file, which are read live and win over code.
The variables are listed in [runtime-security.md](runtime-security.md#environment-variables). Custom mode on a
loopback host with a token is the recommended setup for a game.

## Games run from the editor

A game started with Play (F5) inherits the editor's environment, `GODOT_MCP_HOST` and `GODOT_MCP_TOKEN` included.
So by default `handle.Connect()` does nothing in such a game and returns `false` (it logs why): the editor stays the
one plugin the agent talks to. Exported games and games started outside the editor connect normally. To connect a
game run from the editor too, use `builder.ConnectWhenRunFromEditor()` or `GODOT_MCP_RUNTIME_FROM_EDITOR=1`. A
GameDev-MCP-Server with plugin roles then keeps both connections and routes tool calls to the editor while it is
connected.

## A game tool

A runtime tool is written like an editor tool ([custom-tools.md](custom-tools.md)). Mark game-only families
`Target = ToolTarget.Runtime` so the editor does not register them and the agent reaches the game even while the
editor is connected.

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

Prompts and resources register the same way: a `partial class` marked `[AiPromptType]` / `[AiResourceType]` with
members marked `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`. A
resource method returns `ResponseResourceContent[]` (for example `ResponseResourceContent.CreateText(uri, json,
"application/json")`). All of these types come from the `com.IvanMurzak.McpPlugin` package.

## Capturing in-game runtime errors

Editor tools do not see errors raised inside a running game, so an agent could launch the game, read silent logs
and wrongly decide it is healthy. `builder.WithRuntimeErrorCapture()` closes that gap. It installs three
best-effort channels and registers `runtime-errors-get` / `runtime-errors-clear`:

1. **Engine errors (Godot 4.5+)** through `OS.AddLogger`: GDScript runtime errors, `push_error` / `push_warning`
   and shader errors, with `file`, `line` and `function`. On Godot 4.5+ a GDScript error also carries the full
   call stack in `frames` (innermost first) and `stackTrace`.
2. **C# unhandled exceptions** (`AppDomain.CurrentDomain.UnhandledException`) with the full managed stack trace.
3. **C# unobserved `Task` exceptions** (`TaskScheduler.UnobservedTaskException`), observed for logging only.

On Godot below 4.5 the engine channel is unavailable; the C# channels still work. `handle.Dispose()` removes the
hooks. Captured messages and stack traces go to the agent verbatim and can contain paths, user names or secrets,
so enable capture only on a trusted loopback + token connection.

## Running C# in the game

`builder.WithScriptExecute()` lets the agent send C# that the running game compiles with Roslyn and runs on its main
thread, with the game's own types, autoloads and scene tree. Export builds leave Roslyn out, so the game's
`.csproj` opts in:

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

A current `godot-cli install-plugin` writes the Code-Exec and Roslyn package references with a `Condition` that
honours this property; RQL is never shipped. Without the opt-in, `WithScriptExecute()` logs a warning and registers
nothing.

> **This is arbitrary code execution.** Anyone who can call tools on the connection can run any code in the game
> process. Use it only in dev builds, on a loopback host with a token. Desktop CoreCLR only: NativeAOT and iOS
> cannot run code generated at run time.
