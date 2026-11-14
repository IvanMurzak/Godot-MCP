[English](install.md) · **中文** · [日本語](install.ja.md) · [Español](install.es.md) · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

# 手动安装

`godot-cli install-plugin ./MyGodotProject` 用一条幂等命令完成本页的所有步骤（见 [README](README.zh-CN.md#quick-start)）。本页适用于通过 Asset Library 安装或手动管理的项目。

<a id="requirements"></a>
## 要求

- **Godot 4.3 或更高版本的 .NET（mono）版。** 标准的仅 GDScript 版本无法编译该插件。插件的 csproj 以 `Godot.NET.Sdk/4.3.0` 作为最低版本；CI 测试 Godot 4.3 至 4.7。
- **.NET 8 SDK**（`net8.0`）。

<a id="1-add-the-addon"></a>
## 1. 添加插件

任选其一：

- **Godot Asset Library。** 在编辑器中打开 **AssetLib** 选项卡，搜索 **Godot-MCP**，然后点击 **Download** 和 **Install**。文件会放到 `res://addons/godot_mcp/`。网页：[godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245)。
- **GitHub Release。** 从[最新发布版](https://github.com/IvanMurzak/Godot-MCP/releases/latest)下载 `godot-mcp-addon-<version>.zip` 并解压到项目根目录（压缩包中已包含 `addons/godot_mcp/`）。
- **从源码。** 将本仓库中的 `addons/godot_mcp/` 复制到项目的 `addons/` 文件夹。

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. 添加 NuGet 包和扩展目录

Godot 会把项目中的所有 `.cs` 文件编译成一个程序集，因此项目的 `.csproj` 必须引用插件所用的包。请严格使用以下版本（与插件的 `Godot-MCP.csproj` 一致）：

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.5.2" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="8.22.2" />
</ItemGroup>

<!-- 仅编辑器：导出的游戏（ExportDebug / ExportRelease）不包含 RQL 和 Roslyn。 -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="0.3.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="4.12.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- 嵌入扩展目录，否则停靠面板的 Extensions 列表为空。 -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| 包 | 作用 |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | 反射与序列化核心 |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | MCP 插件客户端 |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL，编辑器工具的数据层（PolyForm Shield） |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec，`script-execute` 的底层实现（PolyForm Shield） |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn，`script-execute` 使用的 C# 编译器 |

- 四个仅编辑器使用的包带有 `Condition`，使其不进入导出构建，因此发布的游戏既不包含 RQL 也不包含 Roslyn（[ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)）。
- `<EmbeddedResource>` 是必需的：插件从自身程序集中读取该目录，其 `LogicalName` 必须严格为 `Godot-MCP.extensions.catalog.json`。
- 在 `<PropertyGroup>` 中保留 `<EnableDynamicLoading>true</EnableDynamicLoading>`（Godot 的 C# 模板自带）。没有它，包的程序集不会被复制到游戏程序集旁边，从编辑器启动的游戏将无法创建任何 C# 脚本（"Cannot instantiate C# script because the associated class could not be found"）。

运行 `dotnet restore`，然后构建。无需复制任何 DLL：在编辑器运行时，插件的程序集解析器会通过构建生成的 `*.deps.json` 在 NuGet 全局包文件夹中找到它们。

<a id="3-enable-the-plugin"></a>
## 3. 启用插件

**Project → Project Settings → Plugins → Godot-MCP → Enable。** Output 面板会输出：

```
[Godot-MCP] plugin loaded
```

<a id="4-connect-an-ai-agent"></a>
## 4. 连接 AI 智能体

安装任意 MCP 客户端（Claude Code、Claude Desktop、Cursor、带 Copilot 的 VS Code、Codex、Gemini 等），然后写入其配置：

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list 列出所有智能体 id
```

编辑器停靠面板的 **Configure** 按钮会写入相同的配置。默认使用绑定到项目的云端 URL `<host>/mcp/p/<pin>`，这样即使连接了多个编辑器，在此文件夹中启动的智能体也能访问本项目的编辑器；传入 `--no-pin` 则使用不带绑定的 `<host>/mcp` URL。在 Cloud 模式下，配置中带有本项目的密钥（`Authorization: Bearer agd_pk_…`）；未登录或使用 `--oauth` 时，配置只含 URL，智能体通过自己的 OAuth 登录。连接模式和环境变量：[connection.md](connection.zh-CN.md)。
