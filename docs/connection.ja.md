[English](connection.md) · [中文](connection.zh-CN.md) · **日本語** · [Español](connection.es.md) · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

# 接続とサーバー

エディタープラグインは、2 つのモードのいずれかで MCP サーバーに接続します。モードはエディタードックで設定するか、プロセス起動時に `GODOT_MCP_*` 環境変数で上書きします(CI、ヘッドレス実行、ローカル開発に便利です)。環境変数は常に保存された設定より優先されます。`godot-cli open` は `--mode`、`--url`、`--cloud-url`、`--token` を通じてこれらを渡してくれます。

<a id="cloud-mode-default-ai-gamedev"></a>
## Cloud モード(既定): ai-game.dev

プラグインは `https://ai-game.dev` に接続します。サーバーを動かす必要はありません。Cloud が既定なのは、開放ポートが不要で、他のプログラムとのポート競合を避けられるからです。無料で使えます。リレーはリクエストやレスポンスの内容を保存せず、メタデータのみを 90 日間保持します。

`godot-cli login` で 1 回サインインします。OAuth 2.1 デバイスフロー(RFC 8628)を使い、短いコードを表示してブラウザを開き、承認後に資格情報をマシンストア(`~/.ai-game-dev/credentials.json`)に保存します。エディタープラグインがそれを取得するので、トークンをコピーする必要はありません。

| 変数 | 用途 | 既定値 |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` または `Custom`(大文字小文字を区別しない)。 | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | クラウドのベース URL。末尾の `/mcp` は取り除かれる。 | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | 資格情報ストアの絶対パスのディレクトリ。分離したテスト用。プロジェクトの `.env` からも読まれる。 | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | 有効なモードの Bearer トークン。CI やヘッドレス実行のための手動上書き。 | なし |

<a id="custom-mode-your-own-server"></a>
## Custom モード: 独自のサーバー

プラグインは、あなたが選んだサーバー URL に接続します: ローカルサーバー、またはセルフホストのサーバーです。オフラインやエアギャップ環境での作業に使います。

| 変数 | 用途 | 既定値 |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Custom` に設定。 | `Cloud` |
| `GODOT_MCP_HOST` | サーバー URL。絶対 http(s) URL。 | `http://localhost:<port>`(ポートはプロジェクトのパスから決まる) |
| `GODOT_MCP_TOKEN` | そのサーバーに送る Bearer トークン。設定すると `token` 認可が選択される。 | なし |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`。上記のルールを上書きする。 | 保存された選択、なければ `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # サーバーが認証を要求する場合のみ
```

`godot-cli open` がこれらを設定してくれます。自分で選んだトークンを要求するローカルサーバーを使うには、そのトークンを指定してエディターを開き、ドックで **Start Server** を押します(`--auth Required` で `token` 認可が選択されます):

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

- 環境変数(または Custom モードではプロジェクトの `.env`)で設定したトークンは、エディターが起動するローカルサーバーや、**Configure** で書き込むエージェント設定にも適用されます。設定ファイルには保存されません。
- `oauth` では、ai-game.dev アカウントのトークンは **localhost のサーバーにのみ**送られます(`localhost`、`127.0.0.0/8`、`::1`)。それ以外のホストには `GODOT_MCP_TOKEN` を設定してください。
- `token` モードのサーバーはトークンのない接続を拒否します。プラグインは何を設定すべきかをログに出力します。

<a id="the-server"></a>
## サーバー

サーバーは共有の [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)(`gamedev-mcp-server`)です。Unity-MCP、Godot-MCP、Unreal-MCP で共通のひとつのバイナリで、独自のリポジトリからリリースされます。アドオンは使用するバージョンを固定しています(`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs` の `ServerVersion`)。

<a id="let-the-addon-run-it"></a>
### アドオンに起動させる

Custom モードで、ドックの **Server** カードを開き、**Start Server** を押します。アドオンは次のことを行います:

- 固定されたバージョンの `gamedev-mcp-server-<rid>.zip` を、GameDev-MCP-Server の GitHub リリースから HTTPS でダウンロードする(`win-x64`、`win-x86`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`)。
- `.godot/mcp-server/<rid>/` にキャッシュし、固定バージョンが変わったときだけ再ダウンロードする。
- **Server URL** のポート(既定ではプロジェクトのパスから決まるポート)で起動し、接続する。

サーバーはループバックでのみ待ち受け、許可されていない `Origin` ヘッダーを持つリクエストには `403` を返します(DNS リバインディング対策)。ドックで選んだ認可を使います: `none` ではマシン上のどのプロセスからも呼び出せ、`token` では Bearer トークンが必要です。バインド、認可、Origin のルール: [GameDev-MCP-Server の README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)。

**Stop Server** で停止します。エディターを閉じても停止します。ダウンロード URL は固定バージョンとプラットフォームだけから決まります。オフラインの場合、アドオンは警告をログに出し、サーバーは起動しません。CI(`CI` / `GITHUB_ACTIONS`)ではダウンロードはスキップされます。

ツール呼び出しは最大 5 分かかることがあります。`script-execute` は、スニペットの `timeout`(既定 30 秒)が切れると自らスニペットを停止します。

開発用には、`GODOT_MCP_SERVER_PATH=<absolute path>` を設定すると、エディターは固定されたリリースの代わりに既存の `gamedev-mcp-server` バイナリを起動します(ダウンロード、バージョン確認、孤立プロセスの後始末なし)。プラグインのアセンブリ読み込みごとに 1 回、プロセス環境またはプロジェクトの `.env` から読まれます。ファイルを指さないパスは警告付きで無視されます。Linux と macOS では、ファイルはあらかじめ実行可能になっている必要があります。

<a id="run-it-yourself"></a>
### 自分で起動する

[GameDev-MCP-Server のリリース](https://github.com/IvanMurzak/GameDev-MCP-Server/releases)をダウンロードするか、Docker イメージ [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) を使います:

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # MCP クライアントが直接起動する
```

次に、Custom モードでプラグインをそのサーバーに向けます(`GODOT_MCP_HOST=http://localhost:8080`)。すべての引数と環境変数は [GameDev-MCP-Server の README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme) にあります。

<a id="editor-side-security-notes"></a>
## エディター側のセキュリティに関する注意

- **エディターのトークンは平文で保存されます。** プラグインは、`token` と `cloudToken` を含む接続設定を、`user://godot-mcp-config.json` にプレーンな JSON として保存します。このファイルは秘密情報として扱い、コミットや共有をしないでください。ローテーションするには、ドックでトークンをクリアして(またはファイルを削除して)再接続します。プロセス環境、または Custom モードの `.env` から得たトークンは、このファイルには書き込まれません。
- **dev-control ブリッジは `GODOT_MCP_DEV_CONTROL=1` でない限りオフです。** テストでドックを操作するための開発専用の HTTP ブリッジです: エディター専用で、`127.0.0.1` にバインドし、認証はなく、この変数なしには起動しません。

詳細: [runtime-security.md](runtime-security.ja.md#editor-side-surfaces-accepted-posture)。
