<div align="center">

<p><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/cli-hero.svg" alt="godot-cli: Godot MCP アドオンのインストール、サインイン、AI エージェントのセットアップ、ターミナルからの Godot エディター操作" width="100%"/></p>

[![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) [![Node](https://img.shields.io/badge/Node-22.14%2B-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=nodedotjs)](https://nodejs.org/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · **日本語** · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**`godot-cli`** は [Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) のコマンドラインです。Godot-MCP は、AI エージェント(Claude Code、Cursor、Codex、GitHub Copilot など)を [Model Context Protocol](https://modelcontextprotocol.io/) で Godot エディターにつなぐエディターアドオンです。[`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli) と [`unreal-mcp-cli`](https://www.npmjs.com/package/unreal-mcp-cli) の兄弟分で、[ai-game.dev](https://ai-game.dev) が支えています。

<p align="center"><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/terminal.svg" alt="ターミナルセッション: godot-cli がアドオンをインストールし、サインインし、Claude Code を設定してエディターを開く" width="100%"/></p>

<a id="features"></a>
## 特長

✅ **ワンコマンドでインストール** — `install-plugin` がアドオンと NuGet 参照を追加して有効化\
✅ **サインインは 1 回** — `login` は OAuth デバイスフロー。資格情報はエディターが自動で取得\
✅ **エージェント設定** — `setup-mcp` が Claude Code、Cursor、Codex などの MCP 設定を書き込む\
✅ **エディター制御** — プロジェクトのエディターを `open`、`close`、`status`、`wait-for-ready`\
✅ **ツール実行** — `rql`、`exec`、`run-tool` はエディターがなければヘッドレスで起動\
✅ **ヘッドレス CI** — `check`、`import`、`test`、`export`、`run-script` はエディター不要\
✅ **拡張** — `install-extension` でオプション RQL モジュールを追加。`update` で一括更新

<a id="quick-start"></a>
## クイックスタート

[Node.js](https://nodejs.org/) 22.14 以降と、.NET 8 SDK を備えた Godot 4.3+ .NET エディターが必要です。Windows、macOS、Linux で動作します。

```bash
npm install -g godot-cli

godot-cli install-plugin ./MyGodotProject          # アドオン + NuGet 参照 + プラグインを有効化
godot-cli login                                    # ai-game.dev にサインイン(マシンごとに 1 回)
godot-cli setup-mcp claude-code ./MyGodotProject   # エージェントの MCP 設定を書き込む
godot-cli open ./MyGodotProject                    # C# をビルドし、エディターを起動して接続
godot-cli wait-for-ready ./MyGodotProject          # プラグインが応答するまで待つ
```

プロジェクトフォルダーからエディターで C# や RQL を実行できます。エディターが開いていなければヘッドレスエディターが起動します:

```bash
godot-cli exec 'EditorInterface.Singleton.GetEditedSceneRoot()?.Name'
godot-cli rql 'nodes | where name starts-with "Tree" | count'
```

インストールせずに任意のコマンドを実行することもできます: `npx godot-cli install-plugin ./MyGodotProject`。

<a id="commands"></a>
## コマンド

- **`open [path]`** — プロジェクトの C# アセンブリをビルドし(初回オープンでアドオンが読み込まれるように。後述)、Godot エディターのバイナリを解決して、`GODOT_MCP_*` 接続環境変数付きで `--editor --path <project>` を起動します。`--no-build` でビルドを省略します。`--headless` は代わりにウィンドウなしのエディターセッションを起動します([`exec`](#exec) を参照)。
- **`exec [code]`** — `script-execute` を通じてエディターで C# を実行します([`exec`](#exec) を参照)。起動中のエディターを再利用し、なければヘッドレスエディターを起動して待機状態に保ちます。
- **`create-project [path]`** — 新しい Godot プロジェクトの雛形を作ります。`--dotnet` は Godot.NET.Sdk の `.csproj` も書き込み、`--name` でその名前を指定します。
- **`build [path]`** — 次にエディターを開いたときに `godot_mcp` アドオンが読み込まれるよう、プロジェクトの C# アセンブリをビルドします(`dotnet build`)。GDScript のみのプロジェクト(`.csproj` なし)では何もしません。`open` が起動前に行うビルドと同じものです。
- **`rql [query]`** — コマンドラインから JSON なしで RQL クエリを 1 つ実行します: `godot-cli rql 'nodes | where name starts-with "Tree" | count'`。最後のステージが `set` または `call` のクエリは `rql-mutate` へ(`--dry-run`、`--max-targets <n>`)、それ以外は `rql-query` へ送られます。`--schema [type]` は `rql-schema` を実行します。シェルが引用符を取り除いてしまう場合は `-f <file>`(`-f -`: stdin)でクエリを読み込みます。`run-tool` と同様にヘッドレスエディターを起動します。`SaveScene()`、`NewScene(...)`、`CreateResource(...)`、または `godot.resource(...)` の編集のあとは、書き込んだファイルを表示します(`Saved to disk: res://scenes/forest.tscn`)。他の言語の書き方の演算子(`startsWith`、`like`)には RQL での書き方をヒントとして示します。`rql --help` でレシピを一覧表示します。
- **`run-tool <tool> [path]`** — JSON 入力で `<url>/api/tools/<tool>` に POST します。テキストモードでは、テキストの応答をそのテキストとして表示します(`--raw`: 応答の JSON、`--verbose`: エンドポイントも表示)。プロジェクトのローカルサーバーでエディターが応答しない場合、`exec` と同様にヘッドレスエディターを起動します(`--no-start` で無効化、`--once` で呼び出し後に閉じる)。
- **`run-system-tool <tool> [path]`** — `<url>/api/system-tools/<tool>` に POST します(MCP クライアントに公開されないツール)。自動起動は `run-tool` と同じです。
- **`check [path]`** — エディターを起動せずに使う CI ゲート: インポート、`dotnet build`、エディターの起動、GDScript の読み込みチェックを行い、ログから判定します([ヘッドレスコマンド](#headless-commands-check-import-test-export-run-script)を参照)。
- **`import [path]`** — `godot --headless --import`。ログにインポートエラーやスクリプトエラーがあれば失敗します。
- **`test [path]`** — プロジェクトの GUT または gdUnit4 のテストを、厳格なタイムアウト付きでヘッドレス実行します。判定は JUnit レポートとログから行います。
- **`export <preset> <output>`** — プリセットをヘッドレスでエクスポートします(`--debug`、`--pack`)。事前にエクスポートテンプレートを、事後に出力を確認します。
- **`run-script <script> [args...]`** — 単発の `SceneTree` GDScript を実行します(`godot --headless -s`)。`quit(0)` のあとでのみ終了コード 0 になります。
- **`commands`** — 全コマンド、その引数とオプション、終了コードの機械可読なマニフェスト(`--format json`)。
- **`tools [name]`** — エディターの AI ツール(MCP `tools/list`。応答するエディターがなければヘッドレスエディターを起動し、一覧を git で無視される `.ai-game-dev/tools.json` にキャッシュ)。名前を指定すると、その説明、引数、`run-tool` 呼び出しの例を表示します。`--refresh` はエディターに再度問い合わせ、`--no-start` はキャッシュまたは組み込みの一覧を使います。
- **`status [path]`** — プロジェクトで起動中の Godot エディターを検出し、MCP サーバーのヘルスを確認します。
- **`wait-for-ready [path]`** — MCP サーバーが `ping` に応答するまでポーリングします。
- **`login [path]`** — OAuth 2.1 デバイス認可フロー(RFC 8628)で ai-game.dev クラウドに認証します。ブラウザを開き、エディタープラグインが自動で採用するクラウド資格情報を共有マシンストア(`~/.ai-game-dev/credentials.json`)に保存します。`--project` / `--base-url` / `--force` / `--tools-only` / `--yes` フラグは後述の `login` セクションを参照してください。
- **`setup-mcp <agent> [path]`** — プロジェクト固定の `<host>/mcp/p/<pin>` URL を指すエージェントの MCP クライアント設定を書き込みます(エージェントが*この*プロジェクトのエディターにルーティングされるように)。Cloud モードでは設定にこのプロジェクトのキー(`Authorization: Bearer agd_pk_…`)が含まれます。`--no-pin` で素の `<host>/mcp` URL、`--oauth` で URL のみの設定、`--regenerate-key` でキーを置き換えます。
- **`setup-skills <agent> [path]`** — `godot-cli` エージェントスキル(コールドスタートの手順、RQL レシピ、終了コード)と `ai-game-dev-app` を、エージェントのスキルパスにオフラインで書き込みます。`--list` で各エージェントのスキル対応を表示します。
- **`configure [path]`** — プロジェクトローカルの `.godot-mcp/features.json` でツール、プロンプト、リソースを一覧 / 表示 / 非表示にします(`--enable-*` で表示、`--disable-*` で非表示。非表示のツールも名前で呼べば実行されます)。
- **`close [path]`** — プロジェクトで起動中の Godot エディターを正常に終了します(`--force` で強制終了)。そのエディターが起動したローカル MCP サーバーがエディター終了後も残っていれば、それも停止します。`--owned-only` は `godot-cli` が起動したエディター(`exec` / `open --headless`)だけを閉じます。
- **`install-plugin [path]`** — `godot_mcp` アドオンを一通りインストールします: `res://addons/godot_mcp/` を用意し(対応する GitHub リリースをダウンロード、または `--source <path>` でローカルコピー)、インストールしたアドオンが必要とする NuGet の `PackageReference` と拡張カタログの `<EmbeddedResource>` をプロジェクトの `.csproj` に追加し、プラグインを有効化します。冪等です。
- **`install-extension <id> [path]`** — Godot-MCP の**拡張**(オプションの RQL モジュールパッケージ)をプロジェクトにインストールします: 共有カタログから `<id>` を解決し、プロジェクトの `.csproj` にその `<PackageReference>` を追加 / 更新してから、復元のために再ビルドします。冪等で、エディター内のドックと動作は同一です。
- **`remove-plugin [path]`** — `project.godot` の `[editor_plugins]` で `godot_mcp` アドオンを無効化します(アドオンのファイルは削除しません)。
- **`update [path]`** — すべてを更新します: プロジェクトの `godot_mcp` アドオンとインストール済みの拡張を最新バージョンにし、次に `godot-cli` 自体を更新します。プロジェクト外では CLI のみを更新します。`--check` は報告のみ(古いものがあれば終了コード 2)。`--no-self`、`--no-project`、`--no-extensions` で各ステップを省略します。

<a id="build-before-open-open--build"></a>
### 開く前のビルド(`open` / `build`)

`open` は**エディターを起動する前にプロジェクトの C# アセンブリをビルドします**。Godot はエディターの読み込み直後に有効なアドオンの `EditorPlugin` をインスタンス化します。そのため、一度もビルドしていない C# プロジェクトを*初めて*開くとアセンブリがまだ存在せず、Godot は `Unable to load addon script 'res://addons/godot_mcp/Editor/GodotMcpPlugin.cs' … Disabling the addon` で失敗します。先にビルドしておけば、アドオンのインスタンス化時にアセンブリが確実に存在します。

- ビルドは `godot-cli build` と同じもの: `dotnet build <project>.csproj --configuration Debug`。
- **GDScript のみのプロジェクト**(ルートに `.csproj` がない)は自動的にスキップされます。コンパイルするものがないためです。
- C# プロジェクトではビルドは**無条件に**実行されます。`dotnet build` はインクリメンタルなので、最新のプロジェクトならすぐに終わります。省略するには `open` に `--no-build` を渡してください(ビルド済みの場合や、できるだけ早く開きたい場合など)。
- ビルドが**失敗した**場合、`open` はエディターを**起動しません**(起動してもアドオン無効化の失敗が再現するだけです)。代わりにエラーを表示します。

<a id="editor-resolution-open"></a>
### エディターの解決(`open`)

`open` は次の順で Godot エディターのバイナリを探します:

1. `--editor-path <path>`(明示指定)。
2. 環境変数 `GODOT_BIN` / `GODOT4_BIN`。
3. `PATH` 上で最初に見つかった Godot バイナリ(Windows では mono ビルドを優先)。
4. OS ごとの一般的なインストール先 — Windows は mono ビルドを優先、macOS は `.app/Contents/MacOS/Godot` バイナリを解決、Linux は展開済みバイナリの一般的な場所を走査。

<a id="connection-env-vars"></a>
### 接続用の環境変数

`open` はこれらをエディタープロセスに渡します(名前はアドオンの `GodotMcpConfig` と一致):

| フラグ | 環境変数 |
| --- | --- |
| `--url <host>` | `GODOT_MCP_HOST` |
| `--cloud-url <url>` | `GODOT_MCP_CLOUD_URL` |
| `--token <token>` | `GODOT_MCP_TOKEN` |
| `--auth None\|Required` | `GODOT_MCP_AUTH_OPTION` |
| `--mode Cloud\|Custom` | `GODOT_MCP_CONNECTION_MODE` |
| `--log-level <level>` | `GODOT_MCP_LOG_LEVEL` |

アドオンの既定は Cloud(ai-game.dev)です: 開放ポートが不要で、他のプログラムとのポート競合を避けられ、無料です。自分で選んだトークンを要求するローカルサーバーを使う場合(オフラインやエアギャップ環境での作業)は、そのトークンを指定して Custom モードでエディターを開き、ドックで **Start Server** を押します(`Required` で `token` 認可が選択されます):

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

<a id="server-url-resolution-run-tool--status--wait-for-ready"></a>
### サーバー URL の解決(`run-tool` / `status` / `wait-for-ready`)

ツール呼び出しの POST 先となるサーバーのベース URL(`<base>/api/tools/<name>`、システムツールは `<base>/api/system-tools/<name>`。`status` / `wait-for-ready` はシステムツール `ping` を確認)は次の順で解決されます:

1. `--url <url>`(明示的な上書き)。
2. 環境変数 `GODOT_MCP_HOST`(Custom モードのホスト)。
3. 環境変数 `GODOT_MCP_CLOUD_URL` → クラウドの **`/mcp` ハブ** URL(なければ `/mcp` を付加)。
4. `GODOT_MCP_CONNECTION_MODE=Cloud` → 既定のクラウドハブ `https://ai-game.dev/mcp`。
5. **登録済みプロジェクトのマーカー**(`.ai-game-dev/project.json` の `serverTarget`。`install-plugin --enroll` が書き込む): ホスト型のターゲットはその `/mcp` ハブへ、`localhost` のターゲットはそのまま使います。これにより、**登録済みのクラウドプロジェクトには環境設定なしで到達できます** — 保存されたクラウド資格情報(プロジェクトごとのストア `<project>/.ai-game-dev/`、次に旧プロジェクト保存先、次に `~/.ai-game-dev/credentials.json`)が自動的に使われ、期限切れ時には更新されます。
6. ローカルへのフォールバック: `http://localhost:<derived-port>`。エディターアドオンがローカルでバインドする、プロジェクトパスから決定的に導かれる v2 ポートです(マーカーの `portOverride` はハッシュ由来のポートより優先)。

クラウドのパスには `/mcp` ハブのセグメントが含まれるため、リクエストはクラウドバックエンド(`/api/tools/*` を提供しない)ではなくハブに届きます。ローカル / セルフホストのサーバーを明示的に指定するには `--url http://localhost:<port>` を渡してください。

`run-tool`、`run-system-tool`、`exec` は、ステップ 6 の代わりに**プロジェクトの接続モード**を使います。読み取り方はアドオンと同じです(`GODOT_MCP_CONNECTION_MODE`、プロジェクトの `.env`、保存された `user://godot-mcp-config.json`、なければアドオンの既定である Cloud)。Custom モードは上記のローカルサーバーを使います。Cloud モードはマシンのサインインでクラウドハブを使います(エディターは起動しません: 応答するのは ai-game.dev に接続したエディターだけです)。このマシンが ai-game.dev に**一度も**サインインしていない場合は、警告を出してプロジェクトのローカルサーバーを使います(その場で起動したエディターが応答し、何も保存されません)。サインインは存在するが使えない場合(期限切れ、失効、読み取り不能)はエラーです(終了コード 6、`NOT_AUTHENTICATED`): サインイン済みのマシンがローカルサーバーに切り替わることはありません。`open`、`tools`、`open --headless` も同様です: サインイン済みマシンの Cloud プロジェクトでは、`open --headless` はエディターを起動せず終了コード 6 で終了します(ai-game.dev を経由する `open`、`run-tool`、`exec` を使ってください)。明示的な `--url` / `--token` はそのまま使われます。ローカルでない `--url` / `GODOT_MCP_HOST` は `exec` からは直接呼び出され、`open --headless` では拒否されます(ヘッドレスエディターはローカルなため)。


<a id="exec"></a>
## `exec`

プロジェクトの Godot エディターで C# を実行します(アドオンの `script-execute` ツール、[Code-Exec](https://github.com/IvanMurzak/Code-Exec) 上で動作):

```bash
godot-cli exec '1 + 2'                                   # → System.Int32: 3
godot-cli exec -f snippet.cs --using MyGame.Gameplay      # ファイルからコードを読む("-" またはパイプ: stdin)
godot-cli exec 'Helper.Twice(21)' --include helpers       # <project>/.ai-game-dev/snippets 配下の .cs ファイル/フォルダー
```

コードは普通の文で、最後の式が結果になります。出力は値(`Type: value`)と、`logs:` の下にコードが出力した内容です(`Console`。Godot.NET.Sdk 4.5+ では `GD.Print` / push_warning / push_error も)。コンパイルエラーはスニペットの行(`snippet.cs(2,12)`)を示し、終了コード 6 で終了します。

- **プロジェクトの起動中のエディター**(あなたが開いたウィンドウ付きのもの、または `godot-cli` が起動したもの)は再利用され、閉じられることはありません。あなたのエディターはプロジェクトの**ローカル**サーバーを提供している必要があります(ドック: Custom モード、サーバー起動済み)。
- **起動中のものがない場合:** C# アセンブリをビルドしてから、プロジェクトのローカルサーバー(`http://localhost:<derived port>`、または `--url`)に接続する**ヘッドレス**エディター(`--headless --editor --path`、`--window` ならウィンドウ付き)を起動します。サーバーはそのプラグインが自分で起動します。ヘッドレスのものは、ツール呼び出しがないまま `--idle <seconds>`(既定 300、`0` = 無期限)経過すると、ファイルのある開いているシーンを保存して終了します(ウィンドウ付きのものは開いたままです)。並行した呼び出しでもビルドは 1 回、起動するエディターは 1 つです。ローカルサーバーが応答しないユーザーのエディターは最大 60 秒待ち、その後報告します。
- `open --headless [--idle-timeout <s>]` はコードを実行せずに同じセッションを起動します。`close --owned-only` はそれを保存して終了します(プラグインの `session-close` ツール経由 — ユーザーのエディターは決して閉じません)。

セッションは [`@baizor/gamedev-cli-core`](https://www.npmjs.com/package/@baizor/gamedev-cli-core) の `EditorSession` を使います: リース `<project>/.ai-game-dev/session.json` は、エディターが CLI によって起動されたことを示し(閉じられるのはそのようなエディターだけです)、そのエディターが動作するサーバーを記録します。そのエディターが確実に生きている間は、以降の `exec` / `open` / `close` 呼び出しは `--url` の指定にかかわらずそのサーバーを使います。`<project>/.ai-game-dev/session.lock` により、並行した呼び出しでも起動するエディターは 1 つになります: C# のビルドはこのロックの下で、エディターを起動する呼び出しでのみ実行されます。どちらのファイルも `<project>/.ai-game-dev/.gitignore` に記載されています。エディターには `AIGD_SESSION_IDLE_SECONDS`、`AIGD_SESSION_OWNER=cli`、`AIGD_SESSION_NONCE` と、`GODOT_MCP_CONNECTION_MODE=Custom`、`GODOT_MCP_HOST`、`GODOT_MCP_START_SERVER=true` が渡されます。オプション: `--ready-timeout <ms>`、`--timeout <ms>`(ツール呼び出し。既定 310000: サーバーの 5 分の `tool-timeout` をわずかに超える値)、`--no-build`、`--editor-path`、`--token`、`--raw`。

`run-tool` / `run-system-tool` も、Godot プロジェクトの**ローカル** URL で呼び出しが拒否された場合は同じ方針に従います: ヘッドレスエディターを起動し(`--no-build` でなければ先に C# をビルド)、再試行します。起動中のエディターは再利用され、閉じられることはありません。`--once` は、この呼び出しがエディターを起動した場合に限り、呼び出し後にそれを閉じます。`--no-start` でこれを無効にでき、クラウドやリモートの URL では何も起動しません。オプション: `--editor-path`、`--ready-timeout <ms>`。

<a id="exit-codes-and-output-formats"></a>
## 終了コードと出力形式

終了コードは `unity-mcp-cli` および `unreal-mcp-cli` と共通です(`godot-cli commands --format json` で一覧表示):

| コード | 意味 |
| --- | --- |
| 0 | 成功 |
| 1 | 一般的または未分類のエラー。Ctrl+C / SIGTERM で中断された実行も含む(Godot のプロセスツリーは強制終了される) |
| 2 | 使い方のエラー: 不明なコマンドやオプション、不正なフラグ値や引数、不明なツール(`NOT_FOUND`)(`--help` / `--version` は 0) |
| 6 | コマンドは実行されたが失敗: ツールがエラーを返した、スクリプトやコンパイルのエラー、テスト判定なし、資格情報の拒否 |
| 7 | エディターまたは MCP サーバーに到達できない(HTTP 502 も) |
| 8 | テストが実行され、1 つ以上が失敗した(スイートレベルの setup/teardown の失敗も含む) |
| 14 | タイムアウト(HTTP 408 / 504 も) |
| 17 | サーバーがビジー(HTTP 503 / 429)。呼び出しは実行されていない |
| 18 | エディターがモーダルダイアログでブロックされている(予約済み: ヘッドレスの Godot では検出不可) |

`update --check` は、古いものがある場合は引き続き終了コード 2 を返します。

**成功しなかったツール呼び出し**(`run-tool`、`run-system-tool`、`exec`、`rql`、`tools`)は、3 つの CLI すべてで同じ終了コードとエラーコードに対応します(表は `@baizor/gamedev-cli-core` にあり、`commands --format json` が `errorCodes` として一覧表示します)。MCP サーバーが応答した場合はその HTTP ステータスで決まり、エンベロープのエラーにそれが含まれます: `errors[{code, message, httpStatus}]`。

| 状況 | HTTP ステータス | 終了コード | `errors[].code` |
|---|---|---|---|
| 不正な引数: 不正なフラグ、CLI またはサーバーが拒否したツール引数(ここにない 4xx すべて) | 400(422 `[invalid_params]`) | `2` | `USAGE` |
| その名前のツールがない(`tools` で一覧表示)、またはパスにプロジェクトがない | 404 | `2` | `NOT_FOUND` |
| 使える資格情報がない、またはサーバーが拒否した: `login` でサインイン | 401, 403 | `6` | `NOT_AUTHENTICATED` |
| ツールが実行されエラーを報告した(`status: "error"`、ここにない 5xx すべて) | 409, 422, 500 | `6` | `TOOL_FAILED` |
| プロジェクトのコードがビルドできず、エディターを起動しなかった | — | `6` | `COMPILATION_FAILED` |
| 接続の拒否やリセット、サーバーにエンジンプラグインが未接続、呼び出し送信後にプラグインが切断 | 502 | `7` | `UNREACHABLE` |
| 時間内に応答なし: `--timeout`、またはサーバー自身のツールタイムアウト | 408, 504 | `14` | `TIMEOUT` |
| ビジーで呼び出しを実行しなかった(リロード中、レート制限、別の CLI 呼び出しがエディターを使用中)。後で再試行 | 503, 429 | `17` | `BUSY` |
| その他(呼び出しの中断、予期しないエラー) | — | `1` | `ERROR` |

`--format text|json|ndjson`(既定 `text`)は `run-tool`、`run-system-tool`、`exec`、`status`、`wait-for-ready`、`check`、`import`、`test`、`export`、`run-script`、`commands` で使えます。`json` は stdout にちょうど 1 つのエンベロープを出力します:

```json
{ "success": false, "command": "check", "data": { "...": "..." },
  "errors": [{ "code": "SCRIPT_ERRORS", "message": "res://player.gd:12: error: Expected expression." }],
  "warnings": [] }
```

`ndjson` はイベントを stderr に 1 行 1 JSON オブジェクトで出力し(`{"type":"progress","command":…,"message":…}`、それ以外の行はすべて `{"type":"log","command":…,"level":"info|error","message":…}`)、エンベロープを stdout に 1 行で出力します。どちらの場合も、ログ、スピナー、見出しは色なしで stderr に出ます。エラーコード: `USAGE`、`TOOL_FAILED`、`UNREACHABLE`、`TIMEOUT`、`BUSY`、`AWAITING_INPUT`、`TESTS_FAILED`、`TEST_RUN_ERROR`、`COMPILATION_FAILED`、`SCRIPT_ERRORS`、`IMPORT_FAILED`、`PROJECT_OPEN`、`NOT_FOUND`、`EXPORT_FAILED`、`NOT_AUTHENTICATED`、`CLOUD_PROJECT`、`ERROR`。`--raw` はこれまでどおり使えます。

<a id="headless-commands-check-import-test-export-run-script"></a>
## ヘッドレスコマンド(`check`、`import`、`test`、`export`、`run-script`)

これらはエディターセッションなしで Godot を実行し、MCP サーバーも不要です。ヘッドレスモードでは Godot の終了コードは判定材料になりません(GDScript のパースエラーがある `--import` や、読み込みに失敗したアドオンでも 0 で終了します)。そのため各コマンドは、ログ(`SCRIPT ERROR`、`ERROR:`、`Parse Error`)、ステップが実行されたことを示すマーカー、生成されるべきファイルから実行結果を判定します。各プロセスには厳格な `--timeout` があります(Godot のプロセスツリー全体が強制終了され、終了コード 14)。ログと一時スクリプトは `<project>/.godot/godot-cli/` に置かれます。`check`、`import`、`test`、`export` は、エディターで開かれているプロジェクトを拒否します(終了コード 6、`PROJECT_OPEN`: インポートが `.godot/` をめぐってそのエディターと衝突するため)。`--allow-open` で上書きでき、`close` で `godot-cli` のエディターを停止できます。バイナリは `--editor-path`、`GODOT_BIN`、`PATH`、または通常のインストールフォルダーから取得します。

```bash
godot-cli check                     # 0 問題なし、6 SCRIPT_ERRORS / COMPILATION_FAILED、14 タイムアウト
godot-cli check --strict            # GDScript と C# のコンパイラ警告でも失敗
godot-cli test --format github      # ::error 注釈。0 合格、8 失敗あり、6 判定なし / ランナーなし
godot-cli export "Windows Desktop" build/game.exe
godot-cli run-script tools/migrate.gd -- --dry-run
```

- **`check [path] [--strict] [--include-addons]`** は 4 つの独立したプロセスを実行します(コールドの単一 `--build-solutions --quit` は C# の `[Tool]` プラグインでクラッシュするため): `--headless --import`。C# プロジェクトなら `dotnet build`(エラーは `file(line,col): error CS…` 形式)。`--headless --editor --quit`(アドオンが有効なら `[Godot-MCP] plugin loaded` を出力する必要あり)。そして `res://` 配下のすべての `.gd` を読み込み(`res://addons/` は `--include-addons` のときのみ)、パースエラーを `res://file:line` として報告する GDScript パス。`--strict` は GDScript の警告(プロジェクトの Warn レベルの警告をメモリ上でエラーに格上げ。保存はしない)と C# の `CS` 警告でも失敗します。エンジンのメッセージ(たとえばプラグインのランタイム警告)は警告として一覧表示され、チェックを失敗させることはありません。
- **`import [path]`** はスクリプトエラー(`SCRIPT_ERRORS`)と、インポートに失敗したリソース(`IMPORT_FAILED`)で失敗します。
- **`test [path]`** は GUT(`addons/gut`)または gdUnit4(`addons/gdUnit4`)を見つけ、プロジェクトをインポートし(`--no-import` で省略)、JUnit レポート付きでランナーのコマンドラインスクリプトを実行します(`-gjunit_xml_file`、または gdUnit4 の `-rd`。gdUnit4 は `--ignoreHeadlessMode -c` で実行)。すべてのテストが合格すれば 0、失敗があれば 8、レポートがない・テストが 1 つも実行されなかった・テストスクリプトの読み込みに失敗した場合は 6(`TEST_RUN_ERROR`。そのテストがレポートから欠けるので合格にしてはならないため)、ランナーがインストールされていなければインストールのヒント付きで 6(`NOT_FOUND`)。`--runner gut|gdunit4`、`--dir <dir>`(複数指定可。既定は `res://test`、`res://tests`、または GUT の `.gutconfig.json`)、`--filter <text>`(ファイル名にそれを含むテストスクリプト)、`--output <file>`(JUnit XML をコピー)、`--format text|json|ndjson|github`。
- **`export <preset> <output> [--debug|--pack]`** は、`export_presets.cfg` にプリセットがあることと、エディターのバージョン用のエクスポートテンプレートがインストールされていることを確認し(`--pack` の場合やプリセットが独自のテンプレートを指定している場合を除く)、`--export-release|--export-debug|--export-pack` を実行します。出力ファイルが存在し、空でなく、今回の実行で書き込まれ、ログにエクスポートエラーがない場合を除き失敗します(`EXPORT_FAILED`)(Godot は、たとえば `.sln` がないために C# の publish が失敗した場合でも、実行ファイルを書き出して 0 で終了します)。
- **`run-script <script> [args...]`** は `godot --headless --path <project> -s <script> -- <args>` を実行します: スクリプトは `SceneTree` または `MainLoop` を継承し、`quit(code)` で終了します。`quit(0)` なら 0、それ以外のコード(`data.scriptExitCode`)または読み込みに失敗したスクリプトなら 6、`--timeout`(既定 300000 ms)なら 14。インポートを行わないため、エディターがプロジェクトを開いている間も実行できます。

<a id="login"></a>
## `login`

`godot-cli login` は、**OAuth 2.1 デバイス認可フロー**(RFC 8628)を使って、エディタープラグインの [ai-game.dev](https://ai-game.dev) への**クラウド**接続を認証します。短いユーザーコードと確認 URL を表示し、ブラウザを開いて、あなたが承認するまでポーリングします:

```bash
godot-cli login                       # マシンごとに 1 回サインイン(既定)
godot-cli login --project ./MyGame    # 代わりにプロジェクトごとの資格情報を保持
godot-cli login --base-url <url>      # 既定以外のサーバーに認証
godot-cli login --force               # 既存の資格情報があっても再認証
godot-cli login --tools-only         # CI/自動化: ツール用資格情報のみ(App は取得しない)
godot-cli login --yes                # 非対話: 別アカウントの資格情報の置き換えを確認
```

- 既定では、資格情報は共有の**マシンストア** `~/.ai-game-dev/credentials.json`(POSIX では `0600`、Windows では DPAPI)に保存されます。**マシンごとに 1 回**サインインすれば Godot エディタープラグインが自動で採用するので、`godot-cli open --mode Cloud` は **`--token` なしで**接続します。
- 既定のログインはマシン全体をサインインさせます: フローは**エージェントスコープ**の許可を要求し、そこから RFC 8693 トークン交換でより狭い**ツール(プラグイン)用資格情報**を導出します。そのため、マシン上のすべての AI-Game-Dev ツール(エンジンプラグイン、CLI、デスクトップ App)がサインインを利用できます。
- `--tools-only` はツール用資格情報**のみ**を発行します。エージェントスコープのものは何も保存されず、デスクトップ App はサインインを採用できず、ランナーは [ai-game.dev](https://ai-game.dev) 上で個別に失効できるデバイスとして表示されます。CI や共有の自動化ランナーに使ってください。
- マシンがすでに**別アカウントの**資格情報を持っている場合、login は置き換える前に確認します(拒否すると発行したばかりの資格情報を失効させ、ストアには手を付けません)。非対話で確認するには `--yes` を渡します。
- `--project <path>`(または位置引数 `[path]`)は、プロジェクトごとのアカウント用に、プロジェクトごとの資格情報(`<path>/.ai-game-dev/credentials.json`、gitignore 対象)を保持します。旧来のプロジェクトごとの保存先 `<path>/.godot-mcp/credentials.json` には**もう書き込まれません** — 1 リリースの間は引き続き読み込まれます(初回使用時にマシンストアへ移行)。
- フローは資格情報一式(アクセストークン + ローテーションするリフレッシュトークン + 有効期限)を**すべて**保存します — **個人アクセストークン(PAT)は一切発行されません**。失敗時には何も書き込まれないため、既存の資格情報は拒否 / 期限切れ / ネットワークエラーがあってもそのまま残ります。コマンドは期限切れのアクセストークンを自動で更新するので(`@baizor/gamedev-cli-core` 経由)、サインイン済みのマシンは `login` を再実行しなくてもサインインしたままです。

<a id="setup-skills"></a>
## `setup-skills`

`godot-cli setup-skills <agent> [path]` は、選択したエージェントのスキルパス(たとえば Claude Code の `.claude/skills`)に、Godot プロジェクト用の AI エージェント**スキルファイル**を生成します。`--list` ですべてのエージェントとそのスキル対応を確認できます。

```bash
godot-cli setup-skills claude-code            # ./.claude/skills に生成
godot-cli setup-skills cursor ./MyGame        # MyGame/.cursor/skills に生成
godot-cli setup-skills --list                 # エージェントとそのスキルパスを一覧表示
```

オフライン(エディター、サーバー、ネットワークなし)で次のものを書き込みます:

- `godot-cli/SKILL.md`: エージェントがコールドスタートからこの CLI を使う方法: 最初のエディターコマンドでヘッドレスエディターが起動すること、シーン・ノード・リソース用のレシピ付きの `rql`、`check` / `test`、`--format json`、各終了コードの意味。意図的に短くしてあります(エージェントは読んだバイトをすべてコンテキストに保持するため)。詳細は `references/rql.md` と `references/install.md` にあり、必要なときだけ読まれます。レシピは `godot-cli --help` や `rql --help` と同じソースから生成されるため、スキルとヘルプが食い違うことはありません。
- `ai-game-dev-app/SKILL.md`: CLI にないクリエイティブツールのためのコンパニオンアプリ。

**冪等**です: 再実行しても同じバイト列が書き込まれます。以前のバージョンが手書きのカタログから生成した `godot-mcp` / `godot-mcp-<family>` スキルは削除します。ツールごとのスキルはアドオンの役割です: アドオンは、エディターが動作するたびに実際に登録しているツールからそれらを生成します(アドオンの Skills パネルで切り替え)。あわせて `godot-cli-install` ガイドも生成します。

<a id="install-plugin"></a>
## `install-plugin`

`godot-cli install-plugin [path]` は**本格的なインストーラー**です。ターミナルからのゼロからのインストールで、冪等なひとつのコマンドで動作するプロジェクトを作ります:

```bash
godot-cli install-plugin ./MyGodotProject                      # 対応するリリースをダウンロードしてインストール
godot-cli install-plugin --version <x.y.z> ./MyGodotProject   # 特定のアドオンリリースに固定
godot-cli install-plugin --source ./Godot-MCP/addons/godot_mcp ./MyGodotProject   # オフライン / 開発用コピー
```

次の 4 つのステップを実行します:

1. **`res://addons/godot_mcp/` を用意する。** 既定では `godot-mcp-addon-<version>.zip` を HTTPS で **github.com からのみ**ダウンロードします(`IvanMurzak/Godot-MCP` のリリース `v<version>`。バージョンの既定は CLI 自身のバージョン)。`github.com` 以外のホストや素の `http` は拒否されます。`--source <path>` を指定すると、代わりにローカルディレクトリからアドオンをコピーします(ネットワークなし) — `<path>` は `addons/godot_mcp` *そのもの*のディレクトリでも、それを*含む*ディレクトリでも構いません。
2. **アドオンが必要とする NuGet パッケージ**(`com.IvanMurzak.ReflectorNet`、`com.IvanMurzak.McpPlugin`)をプロジェクトの `.csproj` に冪等に追加します — なければ追加し、古いバージョンは調整し、正しい固定はそのままにします。バージョンはインストールするアドオンから取得します: その `nuget-deps.json`(リリースワークフローが `Godot-MCP.csproj` から生成してすべてのリリース zip に含める)、または `--source` が Godot-MCP のチェックアウト内を指す場合はそのチェックアウトの `Godot-MCP.csproj`。マニフェストのない古いアドオン zip では、警告を出して CLI に組み込まれたバージョンにフォールバックします。
3. **拡張カタログを埋め込む**(`<EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />`)— プロジェクトの `.csproj` に冪等に追加します。これがないと、アドオンの拡張レジストリはエディター実行時にカタログを読めず、**Extensions パネルが空**になります。単一ソースで、固定バージョンとあわせてアドオンの csproj とのパリティテストが行われています。`<EnableDynamicLoading>true</EnableDynamicLoading>` も設定します(Godot のテンプレートにはこれがあり、ないとエディターから起動したゲームが C# スクリプトをインスタンス化できません)。
4. **`project.godot` の `[editor_plugins]` でプラグインを有効化**します。

ライブラリとして安全に使え(`{ kind: 'success' | 'failure' }` のユニオンを返し、公開境界の外に例外を投げない)、冪等です — インストール済みのプロジェクトで再実行しても変更なしと報告します。

<a id="install-extension"></a>
## `install-extension`

`godot-cli install-extension <id> [path]` は、オプションの Godot-MCP **拡張**(RQL を拡張するオプションの NuGet パッケージ。独自のツールは追加しない)を Godot C# プロジェクトにインストールします。エディター内の **Extensions** ドックが行うのと同じインストールを、ターミナル / ライブラリから行う手段です:

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject   # PackageReference を追加/更新
godot-cli install-extension "CSG Tools"                                      # 名前で解決。既定は cwd
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG --version <x.y.z>   # カタログの固定バージョンを上書き
```

- `<id>` を**共有拡張カタログ**(`addons/godot_mcp/extensions.catalog.json` — ドック、CLI、アプリがすべて参照する唯一の情報源)に照らして解決します。パッケージ ID(次に名前)で、大文字小文字を区別せずに照合します。
- プロジェクトのルート `.csproj` に `<PackageReference Include="<packageId>" Version="<version>" />` を読み込み・変更・書き込みします: なければ**追加**、カタログ(または `--version`)がより新しいバージョンを指定している場合のみバージョンを**引き上げ**、最新ならば**何もしない** — その後、Godot が新しいパッケージを復元してコンパイルするよう、再ビルド(`godot-cli build`)を促します。
- ドックの `ExtensionInstaller` と動作は同一です(同じ追加 / 更新 / 何もしない、および数値によるバージョン比較のルール。双方で共有のシナリオセットにより検証済み)。

<a id="library-api"></a>
## ライブラリ API

パッケージは副作用のないライブラリ(`.` エントリ)もエクスポートしています:

```ts
import { openProject, runTool, setupMcp, installPlugin, installExtension, execCode, ensureEditor } from 'godot-cli';

// 共有拡張カタログと検索ヘルパーもエクスポートされているので、GUI(アプリ)は
// ドックと CLI がインストールに使うのと同じ一覧を表示できる:
import { EXTENSIONS_CATALOG, findExtension } from 'godot-cli';

// ヘッドレスコマンド、そのログ/JUnit パーサー、終了コード:
import { checkProject, importProject, runTests, exportProject, runScript, parseGodotLog, parseJUnit, ExitCode } from 'godot-cli';
```

すべての関数は判別可能なユニオン(`{ kind: 'success', ... }` / `{ kind: 'failure', error }`)を返し、公開境界の外に例外を投げることはありません。

<a id="development"></a>
## 開発

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```


<a id="supported-ai-agents"></a>
## 対応 AI エージェント

`godot-cli` は、主要な AI コーディングエージェントすべてに対して、すぐに使える MCP クライアント設定を書き込みます — `godot-cli setup-mcp <agent> [path]` を実行して接続してください(スキルファイルの生成は `godot-cli setup-skills <agent> [path]`)。どちらのコマンドでも `--list` で対応エージェントをすべて確認できます。

対応エージェントには、Claude Code、Claude Desktop、Cursor、VS Code と Visual Studio(Copilot)、GitHub Copilot CLI、Rider(Junie)、Codex、Gemini、Antigravity、Cline、Open Code、Kilo Code、そして汎用の `custom` MCP クライアントがあります。

```bash
godot-cli setup-mcp --list                    # 対応するエージェント ID をすべて一覧表示
godot-cli setup-mcp claude-code ./MyGame      # エージェントの MCP クライアント設定を書き込む(このプロジェクトに固定)
godot-cli setup-mcp claude-code ./MyGame --no-pin   # 代わりに固定なしの素の <host>/mcp URL を書き込む
godot-cli setup-mcp claude-code ./MyGame --oauth    # URL のみ: エージェントは自身の OAuth でサインイン
godot-cli setup-mcp claude-code ./MyGame --regenerate-key   # 新しいプロジェクトキー。古いものは失効
```

Cloud モードでは、`setup-mcp` はすべてのエージェントの設定に**プロジェクトキー**を書き込みます: `Authorization: Bearer agd_pk_…`(Codex: `http_headers`)。プロジェクトキーは期限切れにならず、このプロジェクトのピンに紐づき(アカウント内の他のプロジェクトには到達できません)、マシンのサインイン(`godot-cli login`)でプロジェクトごとに 1 回作成され、以降は `~/.ai-game-dev/project-keys.json` から再利用されます。`--regenerate-key` はキーを置き換え、古いものを失効させます。マシンのサインインがない場合(またはサーバーがキーを発行できない間)は、設定は URL のみとなり、エージェントは自身の OAuth で認証します。`--oauth` はその URL のみの設定を明示的に要求します。明示的な `--token` が常に優先されます。ローカルサーバーの設定(`--url http://localhost:…`)にはプロジェクトキーは含まれません。

> Godot-MCP プロジェクトの完全なドキュメントは[メイン README](../../docs/README.ja.md) を参照してください。**[ai-game.dev](https://ai-game.dev)** が支えています。
