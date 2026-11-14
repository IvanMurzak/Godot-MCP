<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: AI エージェントが godot_mcp アドオンを通じて Godot エディターで作業する" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · **日本語** · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** は、AI エージェント(Claude Code、Cursor、Codex、GitHub Copilot、Gemini、その他任意の [MCP](https://modelcontextprotocol.io/) クライアント)を **Godot エディター**につなぎます。エージェントはシーン、ノード、リソース、スクリプトを読み取って変更し、C# をビルドして実行し、スクリーンショットを確認できます。コンパニオン CLI [`godot-cli`](../cli/docs/README.ja.md) が付属するエディターアドオンで、[Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) の Godot 版です。

**必要なもの:** Godot 4.3+ エディターの **.NET 版**と **.NET 8 SDK**(一度インストールするだけ)。スクリプトは GDScript のままでかまいません。オプションの `godot-cli` には Node.js 22.14+ が必要です。Windows、macOS、Linux で動作します。

<a id="why"></a>
## Godot-MCP を選ぶ理由

✅ **Godot 4.3 から 4.7** — すべてのリリースを CI で 4.3、4.4、4.5、4.6、4.7 に対してテスト\
✅ **本物の C# ビルド** — `script-build` が `dotnet build` を実行し、コンパイラエラーを返す\
✅ **GDScript のチェック** — `script-validate` が `.gd` ファイルの構文・コンパイル診断を返す\
✅ **元に戻せるアトミックな編集** — クエリの変更はすべてか無しかで適用され、Ctrl+Z 1 回で元に戻せる\
✅ **スクリーンショット** — ビューポート、カメラ、または単一ノードだけをレンダリングしてエージェントに返す\
✅ **ランタイムツール** — 実行中のゲームからログ、C# リフレクション、ランタイムエラーを取得(オプトイン)\
✅ **無料のクラウド、または完全ローカル** — 既定は ai-game.dev(開放ポート不要)、またはアカウント不要のローカルサーバー\
✅ **テレメトリなし** — アドオンと `godot-cli` は利用状況や分析データを一切送信しない

<a id="gdscript"></a>
## GDScript プロジェクトでも使える

ゲームのコードは GDScript のままです。アドオン自体は C# なので、プロジェクトには一度だけ次の手順が必要です:

✅ **Godot .NET エディター** — プロジェクトを Godot 4.3+ の .NET 版で開く(同じプロジェクト、同じシーン)\
✅ **.NET 8 SDK** — マシンごとに一度インストール\
✅ **C# ソリューション** — 既存のプロジェクト: **Project → Tools → C# → Create C# solution**、新規: `create-project --dotnet`\
✅ **アドオン** — `godot-cli install-plugin` がアドオンと NuGet 参照を追加し、有効化する

**GDScript だけのプロジェクトにも C# ソリューションは必要ですか?** はい、一度だけ必要です: アドオンは C# で、プロジェクトの C# アセンブリにコンパイルされます。**Create C# solution** はプロジェクトのルートに `.csproj` と `.sln` ファイルを追加します。`.gd` スクリプト、シーン、リソースは変更されず、これまでどおり動作します。エージェントは GDScript の読み書きと検証も行います。エクスポートでは Godot の .NET エクスポートテンプレートが使われ、アドオンの小さなランタイムアセンブリが含まれますが、コードが `GodotMcpRuntime.Initialize()` を呼ばない限り無効のままです([除外する方法](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users))。

<p align="center">
  <img src="img/terminal.svg" alt="ターミナルセッション: godot-cli がアドオンをインストールし、サインインし、Claude Code を設定してエディターを開く" width="100%"/>
</p>

<a id="how-it-works"></a>
## 仕組み

<p align="center">
  <img src="img/architecture.svg" alt="AI エージェントが MCP でサーバー(ai-game.dev、またはローカルの gamedev-mcp-server)と通信し、サーバーが SignalR で godot_mcp エディターアドオンに届き、アドオンが Godot のメインスレッドでツールを実行する" width="100%"/>
</p>

アドオンは外向きに MCP サーバーへ接続し、エージェントも同じサーバーに接続してアドオンのツールを呼び出します。サーバーは、アドオンがダウンロードして起動してくれるローカルの [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) か、ホスト型の [ai-game.dev](https://ai-game.dev) リレーのどちらかです。アドオン自身はポートを開きません。

Cloud が既定なのは、開放ポートが不要で、他のプログラムとのポート競合を避けられるからです。無料で使えます。ローカルモードは、オフラインやエアギャップ環境での作業のための完全な代替手段です。

<a id="stability"></a>
### 安定性

- **バージョン管理:** アドオンと `godot-cli` は常に同じバージョン番号でリリースされます。更新する前に[リリースノート](https://github.com/IvanMurzak/Godot-MCP/releases/latest)を読んでください。
- **バージョン間で変わるもの:** リリースによって、アドオンが必要とする NuGet のバージョンや、ダウンロードする GameDev-MCP-Server のバージョンが変わることがあります。`godot-cli update`(または `install-plugin`)はインストールするアドオンに合った NuGet のバージョンを書き込み、アドオンは対応するサーバーを自分で取得します。
- **リリースの関門:** .NET テスト、CLI テスト、Godot 4.3–4.7 のエディターテストに合格してはじめてリリースされます。
- **変更履歴:** 各 [GitHub リリース](https://github.com/IvanMurzak/Godot-MCP/releases)にマージされた変更が一覧され、`godot-cli` には[変更履歴](../cli/CHANGELOG.md)もあります。

<a id="quick-start"></a>
## クイックスタート

`godot-cli`(Node.js 22.14+)をインストールし、プロジェクトにアドオンを追加します:

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # アドオン + NuGet 参照 + プラグインを有効化
```

次に、エージェントがエディターに届く方法を選びます。どちらも完全なセットアップです。Cloud がアドオンの既定です: 開放ポート不要、他のプログラムとのポート競合なし、無料。Local はすべてを自分のマシンに留め、オフラインやエアギャップ環境での作業に向いています。

**オプション A: ローカル、アカウント不要。** ツール呼び出しは自分のマシンの中で完結します。

1. **トークンを設定する。** プロジェクトを Godot .NET エディターで開きます。**AI Game Developer** ドックで **Custom** を選び、**Authorization → token** を設定して **Generate** を押し、続いて **Start Server** を押します。**Server URL**(`http://localhost:<port>`、ポートはプロジェクトのパスから決まる)を控えておきます。
   または自分でトークンを決め、ターミナルからそのトークンを指定してエディターを開き、**Start Server** を押します:
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. **エージェントを接続する**(同じトークンを使用):

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

> **トークンなし**の場合、ローカルサーバーは認可 `none` を使います。ループバックでのみ待ち受けるため、自分のコンピューター上のプログラムからしか届きません。
> その場合、`godot-cli open ./MyGodotProject` がエディターとローカルサーバーを一度に起動し、`setup-mcp --url` に使う URL を表示します。

**オプション B: ai-game.dev クラウド。** マシンごとに一度サインインします。以降、エージェントの設定にはこのプロジェクトに紐づいたキーが含まれます。

```bash
godot-cli login                                       # ブラウザでの OAuth デバイスログイン
godot-cli setup-mcp claude-code ./MyGodotProject      # エージェントの MCP 設定を書き込む
godot-cli open ./MyGodotProject                       # C# をビルドし、エディターを起動して接続
```

あとはエージェントに頼むだけです: *「半径 2 の円周上にキューブを 3 つ作って。」*

エディターの **AssetLib** タブ、[リリース zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest)、または手作業でインストールする場合は [install.ja.md](install.ja.md) に従ってください: その場合、プロジェクトの `.csproj` に、インストールするリリースのバージョンでアドオンの NuGet 参照(`com.IvanMurzak.ReflectorNet`、`com.IvanMurzak.McpPlugin`、およびエディター専用の `com.IvanMurzak.Rql`、`com.IvanMurzak.CodeExec`、`com.IvanMurzak.CodeExec.Roslyn`、`Microsoft.CodeAnalysis.CSharp`)が必要です。

<a id="relay"></a>
### ai-game.dev を通るもの

Cloud モードのときだけです。リレーはステートレスです: エージェントとエディターの間でツール呼び出しを中継しますが、その内容を検査も永続化もせず、会話、ソースコード、プロジェクトファイル、アセット、シーンデータも収集しません。記録するのは、リクエストのメタデータ(IP アドレス、時刻と所要時間、ツール名と呼び出し回数、リクエストとレスポンスのサイズ)と MCP イベントのメタデータ(イベントの種類、タイミング、ステータス、セッション識別子、ツール・プロンプト・リソースの名前、エラーの詳細)です。これらの記録は、直近 90 日間のローリングウィンドウ内で利用されます。詳細: [プライバシーポリシー](https://ai-game.dev/legal/privacy)。ローカルモードでは、エディターとサーバーは localhost 上でのみ通信します。

<a id="clients"></a>
## AI クライアントを接続する

`godot-cli setup-mcp <agent> ./MyGodotProject` が設定を書き込んでくれます(`godot-cli setup-mcp --list` で全エージェント ID を表示)。サーバーのエントリー名は `ai-game-developer` で、`<url>` は `setup-mcp` が表示する URL です。Cloud モードでは、エントリーにこのプロジェクトにしか届かないキー `Authorization: Bearer agd_pk_…` も含まれます。

<details>
<summary><b>Claude Code</b> — プロジェクト内の <code>.mcp.json</code></summary>

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

Cloud モードでは、キーは `http_headers` に入ります。
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

ほかにも対応: Claude Desktop、Visual Studio (Copilot)、GitHub Copilot CLI、Rider (Junie)、Antigravity、Cline、Open Code、Kilo Code、汎用の `custom` クライアント([CLI リファレンス](../cli/docs/README.ja.md#supported-ai-agents))。

<a id="what-the-agent-can-do"></a>
## エージェントができること

**カバー範囲:** Godot エディターでできることは、ほぼすべてエージェントにもできます: シーン、ノード、リソース、プロジェクト設定、スクリプト、C# のビルド、スクリーンショット、実行中のゲーム。\
**効率:** ツールはコンパクトなので、エージェントに必要な呼び出しもトークンも少なくて済み、より速く、より安く作業できます。これはテストで測定されています: サンドボックスのゲームワールドを使った [RQL のエージェント評価](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md)では、あるタスクが RQL では 2–3 回のツール呼び出しと約 8k トークンで済んだのに対し、従来の find / get / set リフレクションツールでは約 37 回の呼び出しと 169k トークンを要しました。

クエリを書く必要はありません: エージェントがツールに付属する RQL ガイド(`rql-schema`、`godot-rql` スキル)を読み、自分でクエリを書きます。

| 機能 | ツール | エージェントへの頼み方 |
| --- | --- | --- |
| シーンとノード | `rql-query`, `rql-mutate` | *「メインシーンに DirectionalLight3D を追加して保存して。」* |
| 一括編集 | `rql-mutate` | *「名前が Enemy で始まるノードをすべて非表示にして。」* |
| リソースとプロジェクト設定 | `rql-query`, `rql-mutate` | *「Space キーに割り当てた jump 入力アクションを追加して。」* |
| API を調べる | `rql-schema` | *「CharacterBody3D にはどんな Godot プロパティがある?」* |
| スクリプト(GDScript と C#) | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *「このノードを回転させる GDScript を書いてアタッチして。」* |
| C# のビルド | `script-build` | *「C# プロジェクトをビルドしてエラーを直して。」* |
| エディターで C# を実行 | `script-execute` | *「開いているシーンの MeshInstance3D ノードを数えて。」* |
| 結果を見る | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *「ビューポートのスクリーンショットを撮ってライティングを確認して。」* |
| 実行中のゲーム(オプトイン) | `console-*`, `reflection-*`, `runtime-errors-*` | *「実行中のゲームに新しいランタイムエラーがないか確認して。」* |

`ping`、`session-close`、`godot-skill-create`、`godot-skill-generate` は `godot-cli` とセットアップ用のシステムツールで、エージェントには表示されません。すべてのツールと引数: [tools.ja.md](tools.ja.md)。

同じツールはターミナルからも、プロジェクトフォルダーで使えます(エディターが起動していなければヘッドレスエディターが起動します):

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # エディターのツール一覧
```

<a id="security"></a>
## セキュリティ

| 領域 | 適用されること |
| --- | --- |
| **ローカルサーバー** | ループバックでのみ待ち受けます。既定の認可は `none` で、マシン上のどのプロセスからも呼び出せます。Bearer トークンを必須にするには、ドックで `token` を選ぶか、`GODOT_MCP_TOKEN` を設定するか、`godot-cli open <path> --mode Custom --auth Required --token <token>` でエディターを開きます。 |
| **Origin チェック** | どの認可モードでも、許可されていない `Origin` ヘッダーを持つリクエストにはサーバーが `403` を返します(DNS リバインディング対策)。 |
| **エディターアドオン** | ポートを開かず、外向きにサーバーへ接続します。`127.0.0.1` 上の開発専用の制御ブリッジは、`GODOT_MCP_DEV_CONTROL=1` のときだけ起動します。 |
| **クラウド** | エージェントの設定にはひとつのプロジェクトに紐づいたキーが含まれ、エディターはマシンのサインインを使います。アカウントのトークンは ai-game.dev か localhost のサーバーにしか送られません。リレーはステートレスです: ツール呼び出しの内容を検査も永続化もしません([プライバシーポリシー](https://ai-game.dev/legal/privacy))。 |
| **保存されるトークン** | エディターはトークンを `user://godot-mcp-config.json` にプレーンな JSON として保存します。このファイルは秘密情報として扱ってください。 |
| **エクスポートしたゲーム** | コードが `GodotMcpRuntime.Initialize(...)` と `Connect()` を呼ばない限り、ゲーム内では何も動きません。動くのは登録したツールだけです。エラー収集とゲーム内 C# はそれぞれ別のオプトインです([ランタイムのセキュリティ](runtime-security.ja.md))。 |
| **保護されないもの** | ツール呼び出しはプロジェクトを変更し、エディターで C# を実行します: バージョン管理を使い、認可 `none` のサーバーを決してネットワークに公開しないでください。 |

詳細: [接続とサーバー](connection.ja.md)、[GameDev-MCP-Server の README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)。

<a id="extensions"></a>
## 拡張

拡張は RQL を拡張するオプションの NuGet パッケージで、独自のツールは追加しません。

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

| 拡張 | 追加するもの | 必要なもの |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | AnimationPlayer のライブラリ、トラック、キー | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | ビヘイビアツリーとそのノード | [Beehave](https://github.com/bitbrain/beehave) アドオン |
| [CSG](../extensions/Godot-AI-CSG/README.md) | 寸法を検証する CSG シェイプ | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | タイムラインとキャラクター | [Dialogic](https://github.com/dialogic-godot/dialogic) アドオン、Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | GridMap の設定、セル行、セル編集 | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | ナビゲーションエージェント、リージョン、リンク、ベイク | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | 2D / 3D の GPU パーティクル | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | バーチャルカメラ、追従と注視 | [Phantom Camera](https://github.com/ramokz/phantom-camera) アドオン、Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | 地形ソース、リージョンサイズ、マテリアル | [Terrain3D](https://github.com/TokisanGames/Terrain3D) アドオン、Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | TileMapLayer のセル行とセル編集 | |

必要なサードパーティ製アドオンはご自身でプロジェクトにインストールしてください。ドックの **Extensions** セクションからまとめてインストールすることもできます。

<a id="documentation"></a>
## ドキュメント

| ページ | 内容 |
| --- | --- |
| [手動インストール](install.ja.md) | AssetLib、リリース zip、NuGet 参照、エージェントのセットアップ |
| [接続とサーバー](connection.ja.md) | Cloud モードと Custom モード、変数、サーバー |
| [ツールリファレンス](tools.ja.md) | 全ツールとその引数、スクリーンショット、拡張 |
| [Godot-MCP の RQL](rql.md) | ソース、関数、書き込み、元に戻す操作と保存 |
| [独自ツール](custom-tools.ja.md) | ツールの書き方、C# アセンブリのリロード、.NET 10 の既知の問題 |
| [ランタイムでの利用](runtime.ja.md) | 実行中のゲーム内の MCP: セットアップ、エラー収集、ゲーム内の C# |
| [ランタイムのセキュリティ](runtime-security.ja.md) | ゲーム内ランタイムが保証することと、あなたがすべきこと |
| [CLI リファレンス](../cli/docs/README.ja.md) | `godot-cli` の全コマンド、終了コード、出力形式 |
| [Architecture](ARCHITECTURE.md) · [Releasing](RELEASING.md) · [Extensions](EXTENSION-MONOREPO.md) | コントリビューター向け |

<a id="contributing"></a>
## コントリビュート

ビルドとユニットテストに Godot バイナリは不要です:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

規約、ヘッドレスエディターのスモークテスト、主な落とし穴は [AGENTS.md](../AGENTS.md) にあります。リポジトリをフォークし、`main` に対してプルリクエストを作成してください。質問や作品紹介は [Discord](https://discord.gg/cfbdMZX99G) へ。

関連: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## ライセンス

**ゲーム開発者は、商用ゲームを含め、すべてを自由に使えます。** Godot-MCP は [Apache-2.0](../LICENSE) です。エディター側の依存関係である RQL と Code-Exec は [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) を使用しており、これが制限するのはゲーム開発向けの競合 AI ツールを作る企業だけです。
