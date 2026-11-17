[English](runtime-security.md) · [中文](runtime-security.zh-CN.md) · **日本語** · [Español](runtime-security.es.md) · [فارسی](runtime-security.fa.md) · [हिन्दी](runtime-security.hi.md) · [العربية](runtime-security.ar.md)

# ランタイム(ゲーム内)のセキュリティ契約

Godot-MCP は、エディターだけでなく**実行中 / エクスポート済みのゲームビルド**(debug または release)の**中でも**動作できます — 詳しい手順は[ランタイムでの利用(ゲーム内)](runtime.ja.md)を参照してください。配布したゲームの中で MCP 接続を動かすと、**リモート操作の窓口**が開きます: 登録した `[AiToolType]` ツールにできることは、接続した MCP クライアントが何でも操作できます。このドキュメントは、そのモードのセキュリティ契約です。 `GodotMcpRuntime.Initialize(...)` を呼ぶビルドを配布する前に読んでください。

<a id="guarantees-the-runtime-gives-you"></a>
## ランタイムが保証すること

- **オプトインのみ — 自動接続しない。** エディタープラグインは起動時に接続しますが、ゲームビルドは接続**しません**。 `GodotMcpRuntime.Initialize(...).Build()` は**既定でオフ**の `GodotMcpRuntimeHandle` を返します。*あなたの* コードが明示的に `handle.Connect()` を呼ぶまで、何も接続しません。エディター以外のビルドに自動接続の コードパスはありません。
- **既定ではツールゼロ — 完全に手動。** ビルドしたばかりのランタイムは MCP ツールを**ひとつも**登録しません。 攻撃対象領域は、`builder.WithToolsFromAssembly(...)` / `builder.WithTools(...)` でオプトインした `[AiToolType]` クラスの集合とまったく同じで、それ以上はありません。何も登録しなくても、接続は空のツール セットでビルド・接続されます(Unity-MCP のモデルとまったく同じです)。
- **エディターのツールファミリーは紛れ込まない。** アドオンのエディターツールファミリー(`Tool_Rql`、`Tool_Script`、 `Tool_Screenshot`、`Tool_Session`)は `#if TOOLS` で制御されており、エクスポートしたゲームビルドには**コンパイルされません**。 アドオン自身のアセンブリに対する `WithToolsFromAssembly(...)` でも、それらを登録することはできません。
- **保存済み設定の自動読み込みなし。** ゲームビルドが保存済み(`user://`)の設定ファイルを黙って読むことは ありません — それはエディター専用の便宜機能です。ホストとトークンは、あなたのコード(`WithConfig`)か、 `GodotMcpConfig` がライブに読む `GODOT_MCP_*` のプロセス環境 / プロジェクトの `.env` からのみ取得されます。

<a id="what-you-must-do-when-enabling-it"></a>
## 有効にするときにすべきこと

- **意図して接続する。** 実際にツールを公開するつもりのとき(開発ビルド、QA ハーネス、明示的に設計した ゲーム内エージェント機能)にだけ `handle.Connect()` を呼んでください。公開をやめるには `handle.Disconnect()` / `handle.Dispose()` を呼びます。
- **ツールは最小限だけ登録する。** 少数のツールしか必要ないなら、広範な `WithToolsFromAssembly(...)` より `WithTools(typeof(MyToolFamily))` を推奨します。登録したツールはすべて、接続したクライアントから到達可能です。
- **ループバック + トークンを推奨。** ローカルのツール用途では、Custom モードをループバックホスト (`http://localhost:…` / `127.0.0.1`)に向け、`AuthOption = Consts.MCP.Server.AuthOption.token` と実際の `Token` を設定します:

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // ループバック
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // 非推奨: 下の環境変数の形を参照 — ハードコードした秘密情報を配布しないこと
  });
  ```

  ビルドに秘密情報が埋め込まれないよう、下のアウトオブバンドの形を推奨します:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **リリースビルドで、認証のない MCP の窓口を公開インターフェースにさらさないでください**。ただし、その窓口を 明示的に設計し保護した場合は除きます。ループバック以外のホストは、意図してレビューを経た判断として扱ってください。
- **ホストやモードの変更は切断中にのみ行う。** 独自の `CredentialProvider` を割り当てない限り、各接続の Bearer はダイヤルするモードとホストに紐づきます: 変更後に既存のリンクが再接続してもトークンは送られないため、 サーバーを切り替えるには `handle.Disconnect()` と `handle.Connect()` を呼んでください。
- **配布するバイナリに長期間有効な秘密情報を埋め込まない。** ビルドからトークンを抽出されないよう、 ハードコードした `config.Token` より、環境変数の `GODOT_MCP_TOKEN` を推奨します。
- **ランタイムエラーの捕捉は信頼できる接続でのみ有効にする。** `builder.WithRuntimeErrorCapture()` は **既定でオフ**です。有効にすると、捕捉したエラーは**完全な**メッセージと(C# の障害では)**完全なマネージド スタックトレース**を、`runtime-errors-get` ツールを通じて接続中のエージェントに転送します。これらの文字列には 機密性のあるランタイムデータ — ファイルシステムの絶対パス、マシン名やユーザー名、クエリ文字列、例外メッセージや 引数に現れた秘密情報やトークン — が含まれることがあります。それが意図した診断上の価値ですが、接続を通じて 公開されるデータの範囲は広がります。有効にするのは、ループバックホストで `AuthOption = Consts.MCP.Server.AuthOption.token` と実際のトークンを使う場合だけにしてください — リリースビルドの 認証のない公開インターフェースでは決して有効にしないでください。

<a id="in-game-code-execution-withscriptexecute"></a>
## ゲーム内のコード実行(`WithScriptExecute`)

`builder.WithScriptExecute()` はゲームに `script-execute` を登録します: 接続したクライアントが C# を送ると、ゲームは それを Roslyn でコンパイルし、メインスレッドで実行します。これは、プレイヤーのファイルシステムとネットワークへの アクセス権を持つ、**ゲームプロセス内での任意コード実行**です。既定ではオフで、ビルドでのオプトインが必要です: エクスポートしたゲームが Code-Exec と Roslyn を含むのは、その `.csproj` が `GodotMcpCodeExecInGame=true` を設定し、 `GODOT_MCP_CODE_EXEC_IN_GAME` を定義した場合だけです([ゲーム内での C# の実行](runtime.ja.md#running-c-in-the-game))。 オプトインがなければ、この呼び出しは警告をログに出し、何も登録しません。

> **エディターから起動したゲーム。** そのビルドはエディタービルド(`TOOLS`)なので、アドオンのエディターツール ファミリーもゲームのアセンブリにコンパイルされます。そのアセンブリに対する `WithToolsFromAssembly(...)` は、 `WithScriptExecute()` の有無にかかわらず、エディターの `script-execute` も含めてそれらを登録します。エクスポート ビルドにはそれらは存在しません。エディターから起動したゲームでそれらを提供してはならない場合は、特定の型を 登録してください(`WithTools(...)`)。

- **debug / 開発ビルドのみ。** 呼び出し(と csproj のオプトイン)は、あなた自身の開発用スイッチで制御してください。 公開リリースビルドで有効なまま配布しないでください。
- **常にループバック + トークン。** ループバックホストと、環境変数から取得した実際のトークンで `AuthOption = token` を 使ってください。接続上でツールを呼び出せる人は、プロセスを掌握できます。
- **デスクトップの CoreCLR のみ。** NativeAOT と iOS は実行時に生成したコードを実行できません。モバイルは未テストです。
- **サイズ。** オプトインするとエクスポートに Roslyn(`Microsoft.CodeAnalysis*.dll`、数 MB)が追加されます。RQL は 決して同梱されません。

<a id="environment-variables"></a>
## 環境変数

ランタイムの設定(`GodotMcpConfig`)は、これらの `GODOT_MCP_*` 変数をライブに読みます(プロセス環境またはプロジェクトの `.env`)。解決時にコードで設定した値を上書きします。

| 変数 | 値 | 説明 |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | 接続モード(ループバックホストは `Custom` を意味する)。 |
| `GODOT_MCP_CLOUD_URL` | URL | クラウドのベース URL を上書き(既定 `https://ai-game.dev`)。 |
| `GODOT_MCP_HOST` | URL | Custom モードのサーバーホスト(既定 `http://localhost:8080`)。 |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Custom モードの認証: 匿名 / アカウント(oauth)/ オフライン Bearer(`token`)。未設定なら、`GODOT_MCP_TOKEN` があれば `token` が選択される。 |
| `GODOT_MCP_TOKEN` | 文字列 | Bearer トークン(有効なモードに応じて Cloud または Custom に送られる)。 |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | ログの詳細度のしきい値。 |
| `GODOT_MCP_EDITOR_PID` | プロセス ID | エディタープラグインがエディタープロセスに設定するため、エディターが起動するすべてのプロセスに引き継がれる。そのエディターが動作している間、これを持つプロセスではランタイムの `Connect()` はオフラインのまま(下記でオプトインしない限り)。 |
| `GODOT_MCP_RUNTIME_FROM_EDITOR` | `1` / `true` | プロセス環境のみ。Godot エディターから起動したゲームでもランタイムの接続を許可する(既定ではそこではオフラインのまま。ゲームがエディターの `GODOT_MCP_*` 設定を引き継ぐため)。`builder.ConnectWhenRunFromEditor()` と同じ。 |

<a id="editor-side-surfaces-accepted-posture"></a>
## エディター側の窓口(受け入れ済みの方針)

上記の契約は**ゲームビルド**向けです。エディター側の 2 つの窓口について、網羅性のためにここに記載します。 どちらも現時点では**設計どおり**です(暗号化や共有シークレットはまだ実装されていません — これらは Unity / Godot / Unreal の兄弟プロジェクト間で足並みをそろえて決める、後回しにされたパリティ制約のある判断です)。

<a id="editor-token-storage-is-plaintext-at-rest"></a>
### エディターのトークンは平文で保存される

**エディタープラグイン**を接続すると(Cloud のデバイス認証、または Custom モードのトークン)、プラグインは接続設定を **`user://godot-mcp-config.json`** に保存します(Godot は `ProjectSettings.GlobalizePath` で `user://` をプラットフォーム ごとのユーザーデータディレクトリに解決します)。Bearer トークン(`token`)と Cloud トークン(`cloudToken`)は **平文の JSON** として書き込まれます — 暗号化**されず**、OS のキーストアにも保存**されません**。

- **信頼の前提: ローカルのユーザーアカウント。** あなたのユーザーデータディレクトリを読める人は誰でも トークンを読めます。`user://godot-mcp-config.json` は秘密情報として扱ってください — コミットしたり、共有ドライブに 同期したり、貼り付けたりしないでください。
- **ローテーション。** ドックで保存済みのトークンをクリアして(またはファイルを削除して)再接続します。
- **優先順位。** `GODOT_MCP_TOKEN` のプロセス環境の値、または Custom モードの `.env` の値は、解決時に保存済みの トークンを覆い隠し、このファイルには書き戻され**ません** — そのため CI や共有マシンは、トークンを一度もディスクに 保存せずに動作できます。Cloud モードで読まれた `.env` のトークンは、ドックで保存すると書き戻されます。
- **設定された `GODOT_MCP_TOKEN` は Custom ホストに送られる。** `GODOT_MCP_AUTH_OPTION` がなければ、コードで設定したものも 含め、構成された `AuthOption = none` を上書きします。接続を匿名に保つには `GODOT_MCP_AUTH_OPTION=none` を設定してください。

<a id="the-dev-control-bridge-is-unauthenticated-but-gated-off"></a>
### dev-control ブリッジは認証なしだが、既定でオフに制御されている

テストやターミナルからエディタードックを操作するための、開発専用の注入 / 制御用 HTTP ブリッジ(`DevControlServer`)が あります。**認証はありません**が、そのセキュリティ境界は三重です:

1. **エディター専用。** `#if TOOLS` の内側でコンパイルされるため、エクスポートしたゲームビルドには存在しません。
2. **ループバック専用。** **`127.0.0.1`** だけにバインドします — ルーティング可能なインターフェースには決してバインドしません。
3. **環境変数で既定オフ。** **`GODOT_MCP_DEV_CONTROL=1`**(正確に `1`)**のときだけ**構築・起動されます。 未設定やそれ以外の値ではブリッジは待ち受けません — 配布されたアドオンや通常のエディターセッションが この窓口を開くことはありません。

この環境変数による制御は**要**です: 「開発用の足場」と「ライブのドック上の認証なしの制御窓口」を隔てる唯一のものです。 これは純粋なマネージドの述語(`DevControlGate.IsEnabled`)とそのユニットテスト(`DevControlGateTests`)、**および** 構築箇所での起動時アサーション(`DevControlGate.AssertEnabledOrThrow`)によって強制されています。そのため、環境変数なしで ブリッジを有効にしてしまうリグレッションは、黙って有効なまま配布されるのではなく、CI と実行時にすぐ失敗します。
