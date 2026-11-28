**English** · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

# Manual installation

`godot-cli install-plugin ./MyGodotProject` does every step on this page in one idempotent command (see the [README](../README.md#quick-start)). Use this page for an Asset Library install or a hand-managed project.

## Requirements

- **Godot 4.3 or later, .NET (mono) edition.** Your game code can stay GDScript, but the addon is C#: open the project in the .NET editor build, and give it a C# project file (`.csproj`). For an existing GDScript project use **Project → Tools → C# → Create C# solution** once. The addon's csproj pins `Godot.NET.Sdk/4.3.0` as its floor; CI tests Godot 4.3, 4.4, 4.5, 4.6 and 4.7.
- **.NET 10 SDK (recommended); .NET 8 still supported.** The project may target `net10.0` or `net8.0` (the minimum; .NET 9 is out of support and not tested): the addon ships as source and compiles with your project's `TargetFramework`, which `godot-cli install-plugin` never changes. CI tests `net8.0` and `net10.0`. If the .NET 10 runtime is installed, whatever your `TargetFramework` (the Godot editor runs on the newest installed runtime), read the [known reload issue](custom-tools.md#known-issue-crash-or-hang-after-a-reload-on-net-10).
- **Windows, macOS or Linux.** Godot-MCP works on all three.

## 1. Add the addon

Pick one:

- **Godot Asset Library.** In the editor open the **AssetLib** tab, search for **Godot-MCP**, then **Download** and **Install**. The files land in `res://addons/godot_mcp/`. Web page: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- **GitHub Release.** Download `godot-mcp-addon-<version>.zip` from the [latest release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) and extract it into the project root (the archive already contains `addons/godot_mcp/`).
- **From source.** Copy `addons/godot_mcp/` from this repository into the project's `addons/` folder.

## 2. Add the NuGet packages and the extension catalog

Godot compiles every `.cs` file in the project into one assembly, so the project's `.csproj` must reference the packages the addon uses. Use exactly the versions of the release you install: `godot-cli install-plugin` writes them for you, a release zip lists them in `addons/godot_mcp/nuget-deps.json`, and every release tag has them in its `Godot-MCP.csproj` ([latest release](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="<version>" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="<version>" />
  <PackageReference Include="com.IvanMurzak.Rql"         Version="<version>" />
</ItemGroup>

<!-- Editor-only: exported games (ExportDebug / ExportRelease) ship no Roslyn. -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- Embed the extension catalog, or the dock's Extensions list is empty. -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" Condition="Exists('addons/godot_mcp/extensions.catalog.json')" />
</ItemGroup>
```

| Package | Role |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | Reflection core: finds and invokes the tool methods (McpPlugin's dependency) |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | MCP plugin client |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL, the value layer of every tool, in the editor and in a game (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec, behind `csharp-exec` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn, the C# compiler `csharp-exec` runs |

- The three editor-only packages carry a `Condition` that leaves them out of export builds, so a shipped game contains no Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)). RQL has no `Condition`: in-game tools use it too. A project an older `install-plugin` set up has RQL with the editor-only `Condition`; run `godot-cli install-plugin` again (it removes it) or delete the `Condition` by hand.
- The `<EmbeddedResource>` is required: the addon reads the catalog from its assembly, and its `LogicalName` must be exactly `Godot-MCP.extensions.catalog.json`.
- Keep `<EnableDynamicLoading>true</EnableDynamicLoading>` in the `<PropertyGroup>` (Godot's C# template has it). Without it the package assemblies are not copied next to the game assembly, and a game started from the editor fails to create every C# script ("Cannot instantiate C# script because the associated class could not be found").

Run `dotnet restore`, then build. No DLLs need copying: at editor runtime the addon's assembly resolver finds them in the NuGet global-packages folder through the build's `*.deps.json`.

## 3. Enable the plugin

**Project → Project Settings → Plugins → Godot-MCP → Enable.** The Output panel prints:

```
[Godot-MCP] plugin loaded
```

## 4. Connect an AI agent

Install any MCP client (Claude Code, Claude Desktop, Cursor, VS Code with Copilot, Codex, Gemini, …), then write its config:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list shows every agent id
```

The editor dock's **Configure** button writes the same config. By default it uses the project-pinned cloud URL `<host>/mcp/p/<pin>`, so an agent started in this folder reaches this project's editor even when several editors are connected; pass `--no-pin` for the bare `<host>/mcp` URL. In Cloud mode the config carries this project's key (`Authorization: Bearer agd_pk_…`); without a sign-in, or with `--oauth`, it is URL-only and the agent signs in with its own OAuth. Connection modes and environment variables: [connection.md](connection.md).

## Web export

No Godot 4 .NET editor (4.3 to 4.7) exports to Web: each one refuses with "Exporting to Web is currently not supported in Godot 4 when using C#/.NET", and the .NET export templates have no Web template. A **GDScript** game with the addon installed still exports to Web from the **standard (non-.NET) editor of the same version**. That editor cannot load the addon's C#, and the game never runs it. Keep developing in the .NET editor.

1. **Add a Web preset** in either editor: **Project → Export… → Add… → Web**. The .NET editor lets you add it; it only refuses to export with it. Optional: under **Resources → Filters to exclude files** add `addons/godot_mcp/*`, which keeps the addon's icons (about 100 KB) out of the `.pck`.
2. **Get the standard editor** of your version from [godotengine.org](https://godotengine.org/download/archive/) (e.g. `Godot_v4.5.1-stable_win64.exe.zip`), and install its export templates (**Editor → Manage Export Templates** in that editor, or its `.tpz`). These are a separate install from the .NET templates.
3. **Export.** `godot-cli export Web build/web/index.html` (the first argument is the preset name). When the editor `godot-cli` resolves is a .NET build, it exports with the standard editor of the same version instead: an official, unrenamed build (`Godot_v4.5.1-stable_win64[_console].exe`, `Godot_v4.5.1-stable_linux.x86_64`) beside the .NET editor's folder, on `PATH` or in the usual install folders. On macOS, or with a renamed binary, pass it with `--editor-path` (on macOS: `Godot.app/Contents/MacOS/Godot`). Or open the project in the standard editor and use **Project → Export…**, select the Web preset, **Export Project**. That editor warns that the project uses C# and shows *Unable to load addon script* for `GodotMcpPlugin.cs`: both are expected. The plugin stays enabled, and the .NET editor loads it as before.

A project with other C# scripts (game code, or another addon's) cannot be exported to Web in Godot 4: `godot-cli export` stops with that message for a Web preset, whichever editor it uses.
