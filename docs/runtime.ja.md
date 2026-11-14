[English](runtime.md) · [中文](runtime.zh-CN.md) · **日本語** · [Español](runtime.es.md) · [فارسی](runtime.fa.md) · [हिन्दी](runtime.hi.md) · [العربية](runtime.ar.md)

# ランタイムでの利用(ゲーム内)

エディタープラグインは **Godot エディター**を接続します。Godot-MCP は**実行中またはエクスポート済みのゲーム**(debug または release)の中でも動作できるため、LLM がゲームのライブな状態を読み取り、操作できます。たとえば、あなたが書いた 2 つのツールを通じて、ボットの指し手を LLM が決めるチェスゲームなどです。

ランタイムモードは意図的にエディターと異なります:

- **自分から接続することはありません。** `Connect()` を呼ぶかどうか、いつ呼ぶかはあなたのコードが決めます。
- **既定では何も公開しません。** すべてのツール(`[AiToolType]`)、プロンプト(`[AiPromptType]`)、リソース(`[AiResourceType]`)はあなた自身が登録します。どの種類も任意です。アドオンのエディターツールは `#if TOOLS` の内側にあり、エクスポートしたゲームにはコンパイルされません。

`GodotMcpRuntime.Initialize` を呼ぶビルドを配布する前に、[runtime-security.md](runtime-security.ja.md) を読んでください。

<a id="set-it-up"></a>
## セットアップ

`SceneTree` が存在するようになったら、たとえばオートロードの `_Ready()` から、`GodotMcpRuntime.Initialize(...)` を 1 回呼びます:

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
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // 独自のサーバー
                config.Host           = "http://localhost:8080";           // ループバックを推奨
                config.AuthOption     = McpServerConsts.AuthOption.token;  // トークンを必須にする
                // トークン: コード内の値より、環境変数 GODOT_MCP_TOKEN を推奨。
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] クラス
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] クラス
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] クラス
        }).Build();

        await _mcp.Connect();   // 明示的なオプトイン。KeepConnected が true の間はバックグラウンドで再試行
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| 呼び出し | 機能 |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | 設定を開始する。ツールなしで環境変数から設定する既定構成なら、`configure` は `null` でもよい。 |
| `WithConfig(Action<GodotMcpConfig>)` | `Host`、`Token`、`ConnectionMode`、`AuthOption` をコードで設定する。呼び出しは順に合成される。 |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | `[AiToolType]` クラスを登録する: アセンブリ全体、または特定の型。 |
| `WithPromptsFromAssembly` / `WithPrompts` | `[AiPromptType]` クラスについて同様。 |
| `WithResourcesFromAssembly` / `WithResources` | `[AiResourceType]` クラスについて同様。 |
| `WithoutMainThreadDispatcher()` | 自動のメインスレッドディスパッチャーを省略する(独自のものを導入する場合のみ)。 |
| `ConnectWhenRunFromEditor()` | エディターから起動したゲームでも接続する(既定はオフ。後述)。 |
| `WithRuntimeErrorCapture()` | ゲーム内のエラーを捕捉し、`runtime-errors-*` を登録する。既定はオフ。 |
| `WithScriptExecute()` | ゲームに `script-execute` を登録する。既定はオフ。csproj でのオプトインが必要。 |
| `Build()` | 未接続の `GodotMcpRuntimeHandle` を返す。 |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | 接続を開く、閉じる、または破棄する。 |

`Build()` は、実行中の `SceneTree` にメインスレッドディスパッチャーのノードも追加するため(オプトアウトしない限り)、ツールハンドラーは `MainThread.Instance.Run(...)` を使えます。

<a id="server-url-and-token"></a>
## サーバー URL とトークン

ゲームがエディターの保存済み設定を読み込むことはありません。設定は、あなたのコード(`WithConfig`)と、`GODOT_MCP_*` のプロセス環境変数またはプロジェクトの `.env` ファイルから取得します。後者はライブに読まれ、コードより優先されます。変数の一覧は [runtime-security.md](runtime-security.ja.md#environment-variables) にあります。ゲームでは、ループバックホストとトークンを使った Custom モードが推奨構成です。

<a id="games-run-from-the-editor"></a>
## エディターから起動したゲーム

Play(F5)で起動したゲームは、`GODOT_MCP_HOST` と `GODOT_MCP_TOKEN` を含め、エディターの環境を引き継ぎます。そのため既定では、そのようなゲームでの `handle.Connect()` は何もせず `false` を返します(理由はログに出ます): エージェントが話す相手のプラグインはエディターだけのままです。エクスポートしたゲームや、エディター外で起動したゲームは通常どおり接続します。エディターから起動したゲームも接続させるには、`builder.ConnectWhenRunFromEditor()` または `GODOT_MCP_RUNTIME_FROM_EDITOR=1` を使います。プラグインロールを持つ GameDev-MCP-Server は両方の接続を保持し、エディターが接続している間はツール呼び出しをエディターにルーティングします。

<a id="a-game-tool"></a>
## ゲーム用ツール

ランタイムツールはエディターツールと同じように書きます([custom-tools.md](custom-tools.ja.md))。ゲーム専用のファミリーには `Target = ToolTarget.Runtime` を付けてください。そうすればエディターはそれを登録せず、エディターが接続している間もエージェントはゲームに届きます。

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

プロンプトとリソースも同じ方法で登録します: `[AiPromptType]` / `[AiResourceType]` を付けた `partial class` に、`[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]` を付けたメンバーを置きます。リソースメソッドは `ResponseResourceContent[]` を返します(例: `ResponseResourceContent.CreateText(uri, json, "application/json")`)。これらの型はすべて `com.IvanMurzak.McpPlugin` パッケージにあります。

<a id="capturing-in-game-runtime-errors"></a>
## ゲーム内ランタイムエラーの捕捉

エディターツールには実行中のゲーム内で発生したエラーが見えないため、エージェントがゲームを起動し、何も出ていないログを読んで、正常だと誤って判断するおそれがあります。`builder.WithRuntimeErrorCapture()` がその隙間を埋めます。ベストエフォートの 3 つのチャネルを導入し、`runtime-errors-get` / `runtime-errors-clear` を登録します:

1. **エンジンのエラー(Godot 4.5+)**: `OS.AddLogger` 経由で、GDScript のランタイムエラー、`push_error` / `push_warning`、シェーダーエラーを、`file`、`line`、`function` 付きで捕捉。Godot 4.5+ では、GDScript のエラーは `frames`(最も内側が先頭)と `stackTrace` に完全なコールスタックも持ちます。
2. **C# の未処理例外**(`AppDomain.CurrentDomain.UnhandledException`): 完全なマネージドスタックトレース付き。
3. **C# の未監視 `Task` 例外**(`TaskScheduler.UnobservedTaskException`): ログ記録のためだけに監視済みにします。

Godot 4.5 未満ではエンジンのチャネルは使えませんが、C# のチャネルは動作します。`handle.Dispose()` でフックを取り除きます。捕捉したメッセージとスタックトレースはそのままエージェントに送られ、パス、ユーザー名、秘密情報を含むことがあるため、捕捉は信頼できるループバック + トークンの接続でのみ有効にしてください。

<a id="running-c-in-the-game"></a>
## ゲーム内での C# の実行

`builder.WithScriptExecute()` を使うと、エージェントが送った C# を、実行中のゲームが Roslyn でコンパイルし、ゲーム自身の型、オートロード、シーンツリーを使ってメインスレッドで実行できます。エクスポートビルドには Roslyn が含まれないため、ゲームの `.csproj` でオプトインします:

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

現行の `godot-cli install-plugin` は、このプロパティを尊重する `Condition` 付きで Code-Exec と Roslyn のパッケージ参照を書き込みます。RQL は決して同梱されません。オプトインがなければ、`WithScriptExecute()` は警告をログに出し、何も登録しません。

> **これは任意コード実行です。** 接続上でツールを呼び出せる人は誰でも、ゲームプロセスで任意のコードを実行できます。開発ビルドで、ループバックホストとトークンを使う場合にのみ使用してください。デスクトップの CoreCLR のみ: NativeAOT と iOS は実行時に生成したコードを実行できません。
