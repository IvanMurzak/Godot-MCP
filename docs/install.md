# Manual installation

`godot-cli install-plugin ./MyGodotProject` does every step on this page in one idempotent command (see the
[README](../README.md#quick-start)). Use this page for an Asset Library install or a hand-managed project.

## Requirements

- **Godot 4.3 or later, .NET (mono) edition.** The standard GDScript-only build cannot compile the addon.
  The addon's csproj pins `Godot.NET.Sdk/4.3.0` as its floor; CI tests Godot 4.3 to 4.7.
- **.NET 8 SDK** (`net8.0`).

## 1. Add the addon

Pick one:

- **Godot Asset Library.** In the editor open the **AssetLib** tab, search for **Godot-MCP**, then **Download** and
  **Install**. The files land in `res://addons/godot_mcp/`. Web page:
  [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- **GitHub Release.** Download `godot-mcp-addon-<version>.zip` from the
  [latest release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) and extract it into the project root
  (the archive already contains `addons/godot_mcp/`).
- **From source.** Copy `addons/godot_mcp/` from this repository into the project's `addons/` folder.

## 2. Add the NuGet packages and the extension catalog

Godot compiles every `.cs` file in the project into one assembly, so the project's `.csproj` must reference the
packages the addon uses. Use exactly these versions (they match the addon's `Godot-MCP.csproj`):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.5.2" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="8.22.2" />
</ItemGroup>

<!-- Editor-only: exported games (ExportDebug / ExportRelease) ship no RQL and no Roslyn. -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="0.3.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="0.4.1" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="4.12.0" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- Embed the extension catalog, or the dock's Extensions list is empty. -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| Package | Role |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | Reflection and serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | MCP plugin client |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL, the editor tools' data layer (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec, behind `script-execute` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn, the C# compiler `script-execute` runs |

- The four editor-only packages carry a `Condition` that leaves them out of export builds, so a shipped game
  contains neither RQL nor Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)).
- The `<EmbeddedResource>` is required: the addon reads the catalog from its assembly, and its `LogicalName` must be
  exactly `Godot-MCP.extensions.catalog.json`.
- Keep `<EnableDynamicLoading>true</EnableDynamicLoading>` in the `<PropertyGroup>` (Godot's C# template has it).
  Without it the package assemblies are not copied next to the game assembly, and a game started from the editor
  fails to create every C# script ("Cannot instantiate C# script because the associated class could not be found").

Run `dotnet restore`, then build. No DLLs need copying: at editor runtime the addon's assembly resolver finds them
in the NuGet global-packages folder through the build's `*.deps.json`.

## 3. Enable the plugin

**Project → Project Settings → Plugins → Godot-MCP → Enable.** The Output panel prints:

```
[Godot-MCP] plugin loaded
```

## 4. Connect an AI agent

Install any MCP client (Claude Code, Claude Desktop, Cursor, VS Code with Copilot, Codex, Gemini, …), then write
its config:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list shows every agent id
```

The editor dock's **Configure** button writes the same config. By default it uses the project-pinned cloud URL
`<host>/mcp/p/<pin>`, so an agent started in this folder reaches this project's editor even when several editors
are connected; pass `--no-pin` for the bare `<host>/mcp` URL. In Cloud mode the config carries this project's
key (`Authorization: Bearer agd_pk_…`); without a sign-in, or with `--oauth`, it is URL-only and the agent signs in
with its own OAuth. Connection modes and environment variables: [connection.md](connection.md).
