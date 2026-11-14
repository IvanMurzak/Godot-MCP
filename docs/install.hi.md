[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · [فارسی](install.fa.md) · **हिन्दी** · [العربية](install.ar.md)

# मैन्युअल installation

`godot-cli install-plugin ./MyGodotProject` इस पेज का हर step एक idempotent command में कर देता है ([README](README.hi.md#quick-start) देखें)। Asset Library से install या हाथ से संभाले जाने वाले प्रोजेक्ट के लिए यह पेज इस्तेमाल करें।

<a id="requirements"></a>
## ज़रूरतें

- **Godot 4.3 या नया, .NET (mono) edition।** आपका game code GDScript में ही रह सकता है, लेकिन addon C# में है: प्रोजेक्ट को editor के .NET build में खोलें, और उसे एक C# project file (`.csproj`) दें। मौजूदा GDScript प्रोजेक्ट के लिए एक बार **Project → Tools → C# → Create C# solution** इस्तेमाल करें। Addon की csproj न्यूनतम सीमा के रूप में `Godot.NET.Sdk/4.3.0` pin करती है; CI Godot 4.3, 4.4, 4.5, 4.6 और 4.7 पर test करता है।
- **.NET 8 SDK** (`net8.0`)।

<a id="1-add-the-addon"></a>
## 1. Addon जोड़ें

कोई एक चुनें:

- **Godot Asset Library।** Editor में **AssetLib** tab खोलें, **Godot-MCP** खोजें, फिर **Download** और **Install** करें। Files `res://addons/godot_mcp/` में आती हैं। Web पेज: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245)।
- **GitHub Release।** [Latest release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) से `godot-mcp-addon-<version>.zip` download करें और उसे प्रोजेक्ट root में extract करें (archive में पहले से `addons/godot_mcp/` है)।
- **Source से।** इस repository से `addons/godot_mcp/` को प्रोजेक्ट के `addons/` फ़ोल्डर में copy करें।

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. NuGet packages और extension catalog जोड़ें

Godot प्रोजेक्ट की हर `.cs` file को एक assembly में compile करता है, इसलिए प्रोजेक्ट की `.csproj` को उन packages का reference देना होगा जो addon इस्तेमाल करता है। ठीक उसी release के versions इस्तेमाल करें जिसे आप install कर रहे हैं: `godot-cli install-plugin` इन्हें आपके लिए लिखता है, release zip इन्हें `addons/godot_mcp/nuget-deps.json` में सूचीबद्ध करता है, और हर release tag की `Godot-MCP.csproj` में ये होते हैं ([latest release](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="<version>" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="<version>" />
</ItemGroup>

<!-- सिर्फ़ editor: export किए गए games (ExportDebug / ExportRelease) में न RQL जाता है, न Roslyn। -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- Extension catalog embed करें, वरना dock की Extensions सूची खाली रहेगी। -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| Package | भूमिका |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | Reflection और serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | MCP plugin client |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL, editor tools की data layer (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec, `script-execute` के पीछे (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn, वह C# compiler जिसे `script-execute` चलाता है |

- चारों editor-only packages में एक `Condition` है जो उन्हें export builds से बाहर रखती है, इसलिए shipped game में न RQL होता है न Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game))।
- `<EmbeddedResource>` ज़रूरी है: addon catalog को अपनी assembly से पढ़ता है, और उसका `LogicalName` ठीक `Godot-MCP.extensions.catalog.json` होना चाहिए।
- `<PropertyGroup>` में `<EnableDynamicLoading>true</EnableDynamicLoading>` रहने दें (Godot के C# template में यह होता है)। इसके बिना package assemblies game assembly के पास copy नहीं होतीं, और editor से शुरू किया game कोई भी C# script नहीं बना पाता ("Cannot instantiate C# script because the associated class could not be found")।

`dotnet restore` चलाएँ, फिर build करें। कोई DLL copy करने की ज़रूरत नहीं: editor runtime पर addon का assembly resolver उन्हें build की `*.deps.json` के ज़रिए NuGet global-packages फ़ोल्डर में ढूँढ लेता है।

<a id="3-enable-the-plugin"></a>
## 3. Plugin enable करें

**Project → Project Settings → Plugins → Godot-MCP → Enable।** Output panel में यह print होता है:

```
[Godot-MCP] plugin loaded
```

<a id="4-connect-an-ai-agent"></a>
## 4. AI एजेंट connect करें

कोई भी MCP client install करें (Claude Code, Claude Desktop, Cursor, Copilot के साथ VS Code, Codex, Gemini, …), फिर उसका config लिखें:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list हर agent id दिखाता है
```

Editor dock का **Configure** button वही config लिखता है। Default रूप से यह project-pinned cloud URL `<host>/mcp/p/<pin>` इस्तेमाल करता है, ताकि इस फ़ोल्डर में शुरू किया एजेंट कई editors जुड़े होने पर भी इसी प्रोजेक्ट के editor तक पहुँचे; bare `<host>/mcp` URL के लिए `--no-pin` दें। Cloud mode में config में इस प्रोजेक्ट की key होती है (`Authorization: Bearer agd_pk_…`); sign-in के बिना, या `--oauth` के साथ, यह सिर्फ़-URL होता है और एजेंट अपने OAuth से sign in करता है। Connection modes और environment variables: [connection.md](connection.hi.md)।
