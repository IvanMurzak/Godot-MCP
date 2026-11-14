[English](install.md) · [中文](install.zh-CN.md) · **日本語** · [Español](install.es.md) · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

# 手動インストール

`godot-cli install-plugin ./MyGodotProject` は、このページのすべての手順を冪等なひとつのコマンドで実行します([README](README.ja.md#quick-start) を参照)。このページは、Asset Library からのインストールや、手作業で管理するプロジェクトのためのものです。

<a id="requirements"></a>
## 要件

- **Godot 4.3 以降の .NET(mono)版。** 標準の GDScript 専用ビルドではアドオンをコンパイルできません。アドオンの csproj は下限として `Godot.NET.Sdk/4.3.0` を固定しており、CI は Godot 4.3 から 4.7 までをテストしています。
- **.NET 8 SDK**(`net8.0`)。

<a id="1-add-the-addon"></a>
## 1. アドオンを追加する

いずれかを選んでください:

- **Godot Asset Library。** エディターで **AssetLib** タブを開き、**Godot-MCP** を検索して、**Download**、**Install** の順に押します。ファイルは `res://addons/godot_mcp/` に配置されます。Web ページ: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245)。
- **GitHub Release。** [最新リリース](https://github.com/IvanMurzak/Godot-MCP/releases/latest)から `godot-mcp-addon-<version>.zip` をダウンロードし、プロジェクトのルートに展開します(アーカイブにはすでに `addons/godot_mcp/` が含まれています)。
- **ソースから。** このリポジトリの `addons/godot_mcp/` を、プロジェクトの `addons/` フォルダーにコピーします。

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. NuGet パッケージと拡張カタログを追加する

Godot はプロジェクト内のすべての `.cs` ファイルをひとつのアセンブリにコンパイルするため、プロジェクトの `.csproj` はアドオンが使うパッケージを参照する必要があります。必ず次のバージョンを使ってください(アドオンの `Godot-MCP.csproj` と一致しています):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.5.2" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="8.22.2" />
</ItemGroup>

<!-- エディターのみ: エクスポートしたゲーム(ExportDebug / ExportRelease)には RQL も Roslyn も含まれない。 -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="0.3.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="4.12.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- 拡張カタログを埋め込む。埋め込まないとドックの Extensions 一覧が空になる。 -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| パッケージ | 役割 |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | リフレクションとシリアライズのコア |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | MCP プラグインクライアント |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL、エディターツールのデータ層(PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec、`script-execute` の基盤(PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn、`script-execute` が実行する C# コンパイラ |

- エディター専用の 4 つのパッケージには、エクスポートビルドから除外する `Condition` が付いているため、配布するゲームには RQL も Roslyn も含まれません([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game))。
- `<EmbeddedResource>` は必須です: アドオンはカタログを自身のアセンブリから読み込み、その `LogicalName` は正確に `Godot-MCP.extensions.catalog.json` でなければなりません。
- `<PropertyGroup>` には `<EnableDynamicLoading>true</EnableDynamicLoading>` を残してください(Godot の C# テンプレートにはこれがあります)。これがないとパッケージのアセンブリがゲームのアセンブリの隣にコピーされず、エディターから起動したゲームはすべての C# スクリプトの作成に失敗します("Cannot instantiate C# script because the associated class could not be found")。

`dotnet restore` を実行してからビルドします。DLL をコピーする必要はありません: エディター実行時に、アドオンのアセンブリリゾルバーがビルドの `*.deps.json` を通じて NuGet のグローバルパッケージフォルダーから見つけます。

<a id="3-enable-the-plugin"></a>
## 3. プラグインを有効化する

**Project → Project Settings → Plugins → Godot-MCP → Enable。** Output パネルに次のように表示されます:

```
[Godot-MCP] plugin loaded
```

<a id="4-connect-an-ai-agent"></a>
## 4. AI エージェントを接続する

任意の MCP クライアント(Claude Code、Claude Desktop、Cursor、Copilot 付きの VS Code、Codex、Gemini など)をインストールし、その設定を書き込みます:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list で全エージェント ID を表示
```

エディタードックの **Configure** ボタンも同じ設定を書き込みます。既定ではプロジェクトに固定されたクラウド URL `<host>/mcp/p/<pin>` を使うため、このフォルダーで起動したエージェントは、複数のエディターが接続していてもこのプロジェクトのエディターに届きます。素の `<host>/mcp` URL にするには `--no-pin` を渡してください。Cloud モードでは設定にこのプロジェクトのキー(`Authorization: Bearer agd_pk_…`)が含まれます。サインインしていない場合や `--oauth` を指定した場合は URL のみとなり、エージェントは自身の OAuth でサインインします。接続モードと環境変数: [connection.md](connection.ja.md)。
