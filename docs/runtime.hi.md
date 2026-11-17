[English](runtime.md) · [中文](runtime.zh-CN.md) · [日本語](runtime.ja.md) · [Español](runtime.es.md) · [فارسی](runtime.fa.md) · **हिन्दी** · [العربية](runtime.ar.md)

# Runtime इस्तेमाल (in-game)

Editor plugin **Godot editor** को connect करता है। Godot-MCP एक **चलते हुए या export किए game** (debug या release) के अंदर भी चल सकता है, ताकि LLM live game state पढ़ और चला सके: उदाहरण के लिए एक chess game जिसकी bot चालें आपके लिखे दो tools के ज़रिए LLM से आती हैं।

Runtime mode जानबूझकर editor से अलग है:

- **यह कभी अपने आप connect नहीं होता।** आपका code तय करता है कि `Connect()` को call करना है या नहीं, और कब।
- **Default रूप से यह कुछ भी expose नहीं करता।** हर tool (`[AiToolType]`), prompt (`[AiPromptType]`) और resource (`[AiResourceType]`) आप खुद register करते हैं; हर प्रकार वैकल्पिक है। Addon के editor tools `#if TOOLS` के पीछे हैं और export किए game में compile नहीं होते।

`GodotMcpRuntime.Initialize` call करने वाला build ship करने से पहले [runtime-security.md](runtime-security.hi.md) पढ़ें।

<a id="set-it-up"></a>
## Setup करें

`SceneTree` मौजूद होते ही `GodotMcpRuntime.Initialize(...)` एक बार call करें, उदाहरण के लिए किसी autoload के `_Ready()` से:

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
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // आपका अपना server
                config.Host           = "http://localhost:8080";           // loopback बेहतर है
                config.AuthOption     = McpServerConsts.AuthOption.token;  // token ज़रूरी करें
                // Token: code में value की बजाय environment से GODOT_MCP_TOKEN बेहतर है।
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] classes
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] classes
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] classes
        }).Build();

        await _mcp.Connect();   // explicit opt-in; KeepConnected true रहने तक background में retry करता है
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| Call | क्या करता है |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | Configuration शुरू करता है; zero-tool, environment से configured default के लिए `configure` `null` हो सकता है। |
| `WithConfig(Action<GodotMcpConfig>)` | Code में `Host`, `Token`, `ConnectionMode`, `AuthOption` सेट करता है। Calls क्रम से जुड़ती हैं। |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | `[AiToolType]` classes register करता है: पूरी assembly, या ख़ास types। |
| `WithPromptsFromAssembly` / `WithPrompts` | `[AiPromptType]` classes के लिए वही। |
| `WithResourcesFromAssembly` / `WithResources` | `[AiResourceType]` classes के लिए वही। |
| `WithoutMainThreadDispatcher()` | Automatic main-thread dispatcher छोड़ देता है (सिर्फ़ तब जब आप अपना install करें)। |
| `ConnectWhenRunFromEditor()` | Editor से शुरू किए game में भी connect करता है (default रूप से बंद, नीचे देखें)। |
| `WithRuntimeErrorCapture()` | In-game errors capture करता है और `runtime-errors-*` register करता है। Default रूप से बंद। |
| `WithScriptExecute()` | Game में `script-execute` register करता है। Default रूप से बंद; csproj opt-in चाहिए। |
| `Build()` | एक `GodotMcpRuntimeHandle` लौटाता है जो connected नहीं है। |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | Connection खोलना, बंद करना, या पूरी तरह ख़त्म करना। |

`Build()` चल रहे `SceneTree` में एक main-thread dispatcher node भी जोड़ता है (जब तक आप opt out न करें), ताकि tool handlers `MainThread.Instance.Run(...)` इस्तेमाल कर सकें।

<a id="server-url-and-token"></a>
## Server URL और token

Game कभी editor का saved config load नहीं करता। यह settings आपके code (`WithConfig`) से और `GODOT_MCP_*` process environment variables या प्रोजेक्ट की `.env` file से लेता है, जो live पढ़ी जाती हैं और code पर भारी पड़ती हैं। Variables [runtime-security.md](runtime-security.hi.md#environment-variables) में listed हैं। Game के लिए token के साथ loopback host पर Custom mode recommended setup है।

<a id="games-run-from-the-editor"></a>
## Editor से चलाए गए games

Play (F5) से शुरू हुआ game editor का environment inherit करता है, `GODOT_MCP_HOST` और `GODOT_MCP_TOKEN` समेत। इसलिए default रूप से ऐसे game में `handle.Connect()` कुछ नहीं करता और `false` लौटाता है (वह वजह log करता है): एजेंट सिर्फ़ editor वाले plugin से ही बात करता है। Export किए games और editor के बाहर शुरू किए games सामान्य रूप से connect होते हैं। Editor से चलाए game को भी connect करने के लिए `builder.ConnectWhenRunFromEditor()` या `GODOT_MCP_RUNTIME_FROM_EDITOR=1` इस्तेमाल करें। तब plugin roles वाला GameDev-MCP-Server दोनों connections रखता है और editor जुड़ा रहने तक tool calls उसी को route करता है।

<a id="a-game-tool"></a>
## Game tool

Runtime tool editor tool की तरह ही लिखा जाता है ([custom-tools.md](custom-tools.hi.md))। सिर्फ़ game वाली families पर `Target = ToolTarget.Runtime` लगाएँ ताकि editor उन्हें register न करे और editor जुड़े होने पर भी एजेंट game तक पहुँचे।

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

Prompts और resources भी इसी तरह register होते हैं: `[AiPromptType]` / `[AiResourceType]` वाली एक `partial class`, जिसके members पर `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]` लगा हो। Resource method `ResponseResourceContent[]` लौटाता है (उदाहरण के लिए `ResponseResourceContent.CreateText(uri, json, "application/json")`)। ये सभी types `com.IvanMurzak.McpPlugin` package से आते हैं।

<a id="capturing-in-game-runtime-errors"></a>
## In-game runtime errors capture करना

Editor tools चलते हुए game के अंदर हुए errors नहीं देखते, इसलिए एजेंट game launch करके, ख़ामोश logs पढ़कर गलती से मान सकता है कि सब ठीक है। `builder.WithRuntimeErrorCapture()` यह कमी दूर करता है। यह तीन best-effort channels install करता है और `runtime-errors-get` / `runtime-errors-clear` register करता है:

1. **Engine errors (Godot 4.5+)** `OS.AddLogger` के ज़रिए: GDScript runtime errors, `push_error` / `push_warning` और shader errors, `file`, `line` और `function` के साथ। Godot 4.5+ पर GDScript error पूरा call stack भी `frames` (सबसे अंदर वाला पहले) और `stackTrace` में देता है।
2. **C# unhandled exceptions** (`AppDomain.CurrentDomain.UnhandledException`) पूरे managed stack trace के साथ।
3. **C# unobserved `Task` exceptions** (`TaskScheduler.UnobservedTaskException`), सिर्फ़ logging के लिए observe किए जाते हैं।

Godot 4.5 से नीचे engine channel उपलब्ध नहीं है; C# channels फिर भी काम करते हैं। `handle.Dispose()` hooks हटा देता है। Capture किए messages और stack traces एजेंट तक जस के तस जाते हैं और उनमें paths, user names या secrets हो सकते हैं, इसलिए capture सिर्फ़ भरोसेमंद loopback + token connection पर enable करें।

<a id="running-c-in-the-game"></a>
## Game में C# चलाना

`builder.WithScriptExecute()` एजेंट को C# भेजने देता है जिसे चलता हुआ game Roslyn से compile करके अपने main thread पर चलाता है, game के अपने types, autoloads और scene tree के साथ। Export builds Roslyn को बाहर रखते हैं, इसलिए game की `.csproj` को opt in करना होता है:

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

मौजूदा `godot-cli install-plugin` Code-Exec और Roslyn package references को एक ऐसी `Condition` के साथ लिखता है जो इस property का पालन करती है; RQL कभी ship नहीं होता। Opt-in के बिना `WithScriptExecute()` warning log करता है और कुछ register नहीं करता।

> **यह arbitrary code execution है।** Connection पर tools call कर सकने वाला कोई भी व्यक्ति game process में कोई भी code चला सकता है। इसे सिर्फ़ dev builds में, token के साथ loopback host पर इस्तेमाल करें। सिर्फ़ Desktop CoreCLR: NativeAOT और iOS run time पर generate किया code नहीं चला सकते।
