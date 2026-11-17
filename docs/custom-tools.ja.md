[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · **日本語** · [Español](custom-tools.es.md) · [فارسی](custom-tools.fa.md) · [हिन्दी](custom-tools.hi.md) · [العربية](custom-tools.ar.md)

# 独自ツール

プロジェクトの C# コードに独自の MCP ツールを追加できます。ツールファミリーは `[AiToolType]` を付けた `partial class` で、各ツールは `[AiTool("tool-name", …)]` を付けたメソッドです。LLM がいつどのように呼び出すべきかわかるよう、メソッドと各パラメーターに `[Description]` を付けます。

Godot API の呼び出し(`Node`、`Resource`、`EditorInterface` など)は、**必ず**メインスレッドで実行してください。`MainThread.Instance.Run(...)` で囲みます。プラグインは ReflectorNet の `MainThread` を Godot のメインスレッドディスパッチャーで実装しています。

```csharp
[AiToolType]
public partial class Tool_MyFeature
{
    [AiTool("my-custom-task", Title = "Do a custom task")]
    [Description("Explain to the LLM what this does and when to call it.")]
    public string CustomTask
    (
        [Description("Explain to the LLM what this parameter is.")]
        string inputData
    )
    {
        // Godot API に触れない処理は、ここのバックグラウンドスレッドで実行できる。

        return MainThread.Instance.Run(() =>
        {
            // EditorInterface / Node / Resource にはここ、メインスレッドで触れる。
            return "[Success] Operation completed.";
        });
    }
}
```

- 手作業で整形した文字列ではなく、構造化されたモデル(ReflectorNet がシリアライズします)または `void` を返してください。
- パラメーターを省略可能にするには、null 許容型と既定値を使います: `string? optional = null`。
- `[AiToolType(Target = ToolTarget.Runtime)]` はファミリーをゲーム専用に、`ToolTarget.Editor` はエディター専用にします。既定の `Both` はどこにでも登録します。ゲーム用ツール: [runtime.md](runtime.ja.md)。
- `godot-skill-create` システムツールがこのようなファイルを書いてくれます。次のビルド後に呼び出せるようになります。

<a id="c-assembly-reloads"></a>
## C# アセンブリのリロード

Godot は、ビルドされた DLL が変わってから約 0.5 秒後に、プロジェクトの C# アセンブリをリロードします(`script-build`、エディターの Build ボタン、またはターミナルでの `dotnet build`)。プラグインはそのアセンブリの一部なので、アンロードされ、再構築され、数秒以内に再接続します。その時点でまだ実行中のツール呼び出しはアンロードをブロックしてしまうため、プラグインはリロードの前後で呼び出しを制御します:

- 新しい DLL がディスクに置かれると、新しい呼び出しは再試行可能なエラーで拒否されます("Godot is about to reload the project's C# assembly … Retry in a few seconds")。すでに実行中の呼び出しは先に完了します(Godot は最大 10 秒待ちます)。
- アンロードが始まると、プラグインは呼び出しの受け付けを止め、呼び出しのためにまだキューにあるメインスレッドの処理を破棄します。サーバーはそのような呼び出しを接続とともに失われたものとして報告するので、それも再試行してください。
- リロード後、プラグインは自動的に再接続し、`script-build` が結果を返します。

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## 既知の問題: .NET 10 でリロード後にクラッシュまたはハングする

.NET 10 では、C# アセンブリのリロード直後にエディターがクラッシュまたはハングすることがあります。これは .NET ランタイムのバグ [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441) で、ランタイムの main ブランチで修正済みです([#134983](https://github.com/dotnet/runtime/pull/134983))。.NET 10 のサービシング修正は [#136481](https://github.com/dotnet/runtime/pull/136481) です。

ランタイムに修正が入るまでは、エディタープロセスにこの変数を設定してください:

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

`godot-cli` が起動するエディター(`open`、`open --headless`、`exec`)には自動的に設定されます。Godot を自分で起動する場合は、システム全体に設定し(Windows: `setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`、macOS / Linux: Godot を起動するシェルやランチャーで export)、エディターを再起動してください。.NET 10 ランタイムに修正が含まれたら削除してください。
