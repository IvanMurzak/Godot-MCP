[English](tools.md) · [中文](tools.zh-CN.md) · **日本語** · [Español](tools.es.md) · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

# ツールリファレンス

Godot-MCP の組み込みツールは、Godot エディターでできることのほぼすべてをカバーし、以下のファミリーに分類されています。ツールはコンパクトなので、エージェントに必要な呼び出しもトークンも少なくて済みます([RQL のエージェント評価で測定](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md))。エディターのデータ層は [RQL](rql.md)(Reflection Query Language)です: `rql-query`、`rql-mutate`、`rql-schema` は、操作ごとに 1 つのツールを使う代わりに、ひとつのクエリでシーンのノード、Godot のプロパティ、リソース、ファイル、シーン、エディターの状態を読み取り、変更します。

既定の `toolFormat`(`Rql`)では、他のツールは RQL の値を引数に取り、コンパクトなテキストを返します。`Json` では ReflectorNet の JSON スキーマと結果のままになります。スクリーンショットは PNG 画像を返します。すべてのエディターツールは、アドオンを有効にした時点で使えます。

**console**、**reflection**、**runtime-errors** ファミリーは*実行中のゲーム*向けです([ランタイムでの利用](runtime.ja.md)を参照)。エディターでは、代わりに RQL の `console` ソースと RQL の `call` / `CallMethod` を使ってください。

| ファミリー | ツール | 機能 |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | 実行中のエディターを照会・変更。すべてか無しかで、元に戻せる。[rql.md](rql.md) を参照。 |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | C#(`.cs`)と GDScript(`.gd`)のファイル、ノードへのスクリプトのアタッチ、GDScript の検証、プロジェクトの C# のビルド、エディターでの C# の実行。 |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | エディターのビューポート、カメラ、単独でレンダリングした 1 つのノードを、LLM が見られる PNG としてキャプチャ。 |
| **console** | `console-get-logs`, `console-clear-logs` | 実行中のゲームで: 収集したログの読み取りとクリア。 |
| **reflection** | `reflection-method-find`, `reflection-method-call` | 実行中のゲームで: ReflectorNet を使い、読み込まれた任意のアセンブリの C# メソッドを検索・呼び出し。 |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | 実行中のゲームで: GDScript のランタイムエラー、`push_error` / `push_warning`、シェーダーエラー、C# 例外をポーリング。**既定はオフ。** |
| **ping** | `ping` | 準備状態の確認。システムツール。 |
| **session** | `session-close` | `godot-cli` が起動したエディターを保存して終了。システムツール。 |
| **skills** | `godot-skill-create`, `godot-skill-generate` | 新しい C# ツールファイルをプロジェクトに書き込む。`SKILL.md` ファイルを再生成する。システムツール。 |

**システムツール**は MCP サーバーの `/api/system-tools/` HTTP エンドポイントで提供されます(例: `godot-cli run-system-tool ping`)。`tools/list` では AI エージェントに表示されません。

<a id="rql"></a>
## rql

- `rql-query`: 読み取り専用のクエリ。コンパクトな行として返されます。`nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`: `set` または `call` で終わるクエリ。すべてか無しかで、元に戻せ(Ctrl+Z)、`dry_run` でプレビューできます。シーンの編集はシーンを未保存状態にします。編集したリソースファイルと `project.godot` は保存されます。
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`: 編集中のシーンのソース、関数、ノードクラス。型名を指定すると、そのメンバー、メソッド、Godot のプロパティ。`rql-schema "<extension>"` はインストール済みの拡張のガイドを表示します。

ソースには `nodes`、`root`、`selection`、`scenes`、`files`、`folders`、`classes`、`console`、`editor`、`project`、`settings`、`inputActions`、`autoloads`、`groups`、`layers`、`tools` があります。TileSet、Theme、ShaderMaterial のパラメーター、MeshLibrary を含む言語の全容は、[rql.md](rql.md) と `godot-rql` スキルにあります。

<a id="script"></a>
## script

- `script-read`、`script-create`、`script-update`、`script-delete`: `.cs` と `.gd` ファイルの CRUD。
- `script-attach-to-node`: ノードにスクリプトをアタッチ。
- `script-validate`: GDScript ファイルをパース・コンパイルし、構造化された診断を返す。
- `script-build`: 新しいスクリプトが読み込まれるよう、プロジェクトの C# をビルド(`dotnet build`、Debug)。ビルドはバックグラウンドで実行されます。成功すると Godot が C# アセンブリをリロードし、プラグインが再接続し、結果(警告またはエラー)はリロード後に届きます。[C# アセンブリのリロード](custom-tools.ja.md#c-assembly-reloads)を参照。
- `script-execute`: [Code-Exec](https://github.com/IvanMurzak/Code-Exec) を通じ、Roslyn でエディター内の C# をコンパイル・実行。最後の式を結果とする普通の文(独自の `using`、ローカルクラス、`await` が使える)、または静的メソッドを持つクラス。ゲームのコンパイル済みの型が見えます。`include` で `<project>/.ai-game-dev/snippets` からヘルパーの `.cs` ファイルを追加します。値と、コードが出力した内容(`Console`。プロジェクトが Godot.NET.Sdk 4.5+ でビルドされていれば `GD.Print`、push_warning、push_error も)を返します。ターミナルからは: `godot-cli exec '1 + 2'`。

<a id="screenshot"></a>
## screenshot

- `screenshot-viewport`: エディターのビューポート。
- `screenshot-camera`: 特定のカメラからの視点。
- `screenshot-isolated`: 選んだ角度から、単独でレンダリングした 1 つのノード。

3 つとも会話に画像を返します。`savePath` を渡すとファイルにも書き込みます: `res://Screenshots/shot.png`、`Screenshots/shot.png`(プロジェクトルートからの相対パス)、またはプロジェクト内の絶対パス。存在しないフォルダーは作成され、拡張子がなければ `.png` が付加されます。`savePath` は、プロジェクトの外を指す(`..`、`user://`)、シンボリックリンクやジャンクションを経由する、`res://.godot/` やこのアドオンの中を指す、`.png` でない、または既存のファイルを指す場合に拒否されます(画像もファイルもなし): 上書きは決してしません。Godot はこの PNG を他のプロジェクトファイルと同様にインポートします。`.gdignore` ファイルのあるフォルダーならそれを避けられます。

<a id="console-reflection-runtime-errors-running-game"></a>
## console、reflection、runtime-errors(実行中のゲーム)

- `console-get-logs` / `console-clear-logs`: プラグインが収集したログ(プラグイン自身の接続診断を含む)を読み取り(フィルタリング可)、クリアします。
- `reflection-method-find` / `reflection-method-call`: 読み込まれたすべてのアセンブリから C# メソッド(private も含む)を検索し、引数を付けて呼び出します。
- `runtime-errors-get`: 捕捉したゲーム内エラー。古い順で、新しいものが保持されます。前回の `highestSequence` を `sinceSequence` として渡すと、新しいエラーだけを取得します。捕捉が一度も有効化されていなければ `available:false` を返します。
- `runtime-errors-clear`: バッファをクリアします。シーケンスカウンターはカウントを続けます。

ランタイムエラーの捕捉は、ゲームが `builder.WithRuntimeErrorCapture()` を呼ぶまでオフです。詳細: [ゲーム内ランタイムエラーの捕捉](runtime.ja.md#capturing-in-game-runtime-errors)。

<a id="ping-session-skills-system-tools"></a>
## ping、session、skills(システムツール)

- `ping`: メッセージをそのまま返すか、`pong` を返します。`curl -X POST <host>/api/system-tools/ping -d '{}'`。
- `session-close`: `godot-cli exec` / `open --headless` が起動したエディターを保存して終了します(ファイルのある開いているシーンと、それらが変更したリソース)。`.ai-game-dev/session.json` にあるそのセッションの nonce が必要なため、あなた自身のエディターを閉じることはありません。`godot-cli close --owned-only` がこれを呼び出します。
- `godot-skill-create`: 新しい C# ツールファイルをプロジェクトに書き込みます。プロジェクトを再ビルドすると呼び出せるようになります。
- `godot-skill-generate`: エディターが登録しているツールから、すべての `SKILL.md` を選択した AI エージェントのスキルフォルダーに再生成します。

<a id="visibility"></a>
## 表示設定

各ツール、プロンプト、リソースには、ドックの Tools / Prompts / Resources ウィンドウに **Visible** スイッチがあります(ターミナルからは `godot-cli configure` で同じことができます)。これは MCP クライアントが `tools/list` でその項目を見るかどうかだけを決め、クライアントのコンテキストを小さく保ちます。アクセス制御ではありません: 非表示のツールも、クライアントが名前で呼べば実行されます。

<a id="extensions"></a>
## 拡張

拡張は、`rql-*` ツールと `godot-rql` スキルに RQL のソース、アダプター、エージェント向けガイドを追加します。独自の MCP ツールは追加しません。一覧は [README](README.ja.md#extensions) にあります。

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

インストーラーは拡張の NuGet パッケージだけを追加します。必要なサードパーティ製アドオンはご自身でプロジェクトにインストールしてください。ドックの **Extensions** セクションには **Install All** があります。`godot-cli update` はアドオンを更新し、浮動バージョンの拡張(`Version="*"`)を最新バージョンに再解決します。バージョンを固定した拡張はそのまま維持されます。拡張がアドオンの固定より新しい McpPlugin を必要とする場合、アドオンを更新するか拡張のバージョンを固定するまで、復元は `NU1605` で失敗します。
