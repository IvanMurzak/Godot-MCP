<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: AI エージェントが godot_mcp アドオンを通じて Godot エディターで作業する" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · **日本語** · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** は、AI エージェント(Claude Code、Cursor、Codex、GitHub Copilot、Gemini、その他任意の [MCP](https://modelcontextprotocol.io/) クライアント)を **Godot エディター**につなぎます。エージェントはシーン、ノード、リソース、スクリプトを読み取って変更し、エディターで C# を実行し、スクリーンショットを確認できます。Godot 4.3+(.NET)向けの C# エディターアドオンで、コンパニオン CLI [`godot-cli`](../cli/docs/README.ja.md) が付属します。[Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) の Godot 版です。

<p align="center">
  <img src="img/terminal.svg" alt="ターミナルセッション: godot-cli がアドオンをインストールし、サインインし、Claude Code を設定してエディターを開く" width="100%"/>
</p>

<a id="how-it-works"></a>
## 仕組み

<p align="center">
  <img src="img/architecture.svg" alt="AI エージェントが MCP でサーバー(ai-game.dev、またはローカルの gamedev-mcp-server)と通信し、サーバーが SignalR で godot_mcp エディターアドオンに届き、アドオンが Godot のメインスレッドでツールを実行する" width="100%"/>
</p>

アドオンは MCP サーバーに接続します。既定はホスト型の [ai-game.dev](https://ai-game.dev) で、独自の [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) も使えます(アドオンがダウンロードして起動することもできます)。エージェントも同じサーバーに接続し、アドオンのツールを呼び出します。

<a id="features"></a>
## 特長

✅ **ひとつのクエリ言語** — [RQL](rql.md) でノード、シーン、リソース、設定を読み書き\
✅ **元に戻せる編集** — RQL の変更はすべてか無しかで、Ctrl+Z で元に戻せる\
✅ **C# と GDScript** — `.cs` / `.gd` スクリプトの作成、更新、検証、アタッチ\
✅ **エディターで C# を実行** — `script-execute` がゲームの型を使ってコードを実行\
✅ **視覚フィードバック** — ビューポート、カメラ、単一ノードのスクリーンショット\
✅ **クラウドまたはセルフホスト** — ai-game.dev にサインイン、またはアドオンが起動するローカルサーバー\
✅ **拡張** — アニメーション、ナビゲーション、タイルマップ、アドオン向けのオプション RQL モジュール\
✅ **ゲーム内ランタイム** — 実行中のゲームから独自ツールを公開(既定はオフ)\
✅ **独自ツール** — 属性を付けた C# メソッドひとつで 1 ツール([ガイド](custom-tools.ja.md))

<a id="quick-start"></a>
## クイックスタート

必要なもの: Godot 4.3 以降の **.NET 版**(GDScript 専用ビルドではアドオンをコンパイルできません)、.NET 8 SDK、`godot-cli` 用の Node.js 22.14+。

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # 任意: 新しい Godot C# プロジェクト
godot-cli install-plugin ./MyGodotProject             # アドオン + NuGet 参照 + プラグインを有効化
godot-cli login                                       # ai-game.dev にサインイン(ブラウザ、マシンごとに 1 回)
godot-cli setup-mcp claude-code ./MyGodotProject      # エージェントの MCP 設定を書き込む
godot-cli open ./MyGodotProject                       # C# をビルドし、エディターを起動して接続
```

あとはエージェントに頼むだけです: *「半径 2 の円周上にキューブを 3 つ作って。」*

`godot-cli setup-mcp --list` で対応エージェントをすべて表示します。エディターの **AssetLib** タブ、[リリース zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest)、または手作業でインストールする場合は [install.ja.md](install.ja.md) に従ってください。その場合、プロジェクトの `.csproj` に次の NuGet 参照が必要です。

| パッケージ | バージョン |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0`(エディターのみ) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1`(エディターのみ) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0`(エディターのみ) |

<a id="usage"></a>
## 使い方

エージェントには普段の言葉で話しかけてください。ツールはエージェントが選びます。プロジェクトフォルダーからは、同じツールをターミナルでも呼び出せます:

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # エディターのツール一覧
```

エディターが起動していなければ、これらのコマンドはプロジェクト用のヘッドレスエディターを起動します。全コマンドは [CLI リファレンス](../cli/docs/README.ja.md)にあります。

<a id="tools"></a>
### ツール

Godot-MCP には **9 ファミリー**、**24 個の組み込みツール**があります:

| ファミリー | ツール |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | 実行中のゲーム内でのログ、C# リフレクション、エラー収集 |
| **ping**, **session**, **skills** | `godot-cli` とセットアップ用のシステムツール。エージェントには表示されない |

すべてのツールと引数: [tools.ja.md](tools.ja.md)。

<a id="extensions"></a>
### 拡張

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

[Apache-2.0](../LICENSE) © Ivan Murzak。アドオンは [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) と Code-Exec も使用しており、これらには独自のライセンスがあります。

そのライセンスは [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) です。商用製品を含めて無料で使用できますが、ライセンサーの製品と競合する製品を作ることはできません。
