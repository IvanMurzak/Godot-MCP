[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · **العربية**

<div dir="rtl">

# التثبيت اليدوي

‏`godot-cli install-plugin ./MyGodotProject` ينفّذ كل خطوات هذه الصفحة بأمر واحد متساوي القوة (idempotent) (انظر [README](README.ar.md#quick-start)). استخدم هذه الصفحة للتثبيت من Asset Library أو لمشروع تُديره يدويًا.

<a id="requirements"></a>
## المتطلبات

- ‏**Godot 4.3 أو أحدث، إصدار .NET (mono).** إصدار GDScript القياسي وحده لا يستطيع ترجمة الإضافة. يثبّت ملف csproj الخاص بالإضافة `Godot.NET.Sdk/4.3.0` حدًّا أدنى؛ ويختبر CI الإصدارات من Godot 4.3 إلى 4.7.
- ‏ **.NET 8 SDK** (`net8.0`).

<a id="1-add-the-addon"></a>
## 1. أضِف الإضافة

اختر طريقة واحدة:

- ‏**Godot Asset Library.** في المحرر افتح تبويب **AssetLib**، وابحث عن **Godot-MCP**، ثم **Download** و**Install**. تُوضع الملفات في `res://addons/godot_mcp/`. صفحة الويب: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- ‏**GitHub Release.** نزّل `godot-mcp-addon-<version>.zip` من [أحدث إصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest) واستخرجه في جذر المشروع (الأرشيف يحتوي أصلًا على `addons/godot_mcp/`).
- **من المصدر.** انسخ `addons/godot_mcp/` من هذا المستودع إلى مجلد `addons/` في المشروع.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. أضِف حزم NuGet وفهرس الامتدادات

يترجم Godot كل ملف `.cs` في المشروع إلى تجميعة واحدة، لذا يجب أن يشير ملف `.csproj` للمشروع إلى الحزم التي تستخدمها الإضافة. استخدم هذه الإصدارات بالضبط (فهي تطابق `Godot-MCP.csproj` الخاص بالإضافة):

</div>

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

<div dir="rtl">

| الحزمة | الدور |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | نواة الانعكاس والتسلسل |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | عميل المكوّن الإضافي لـ MCP |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | ‏RQL، طبقة البيانات لأدوات المحرر (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | ‏Code-Exec، الذي يعمل خلف `script-execute` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | ‏Roslyn، مترجم C# الذي يشغّله `script-execute` |

- تحمل حزم المحرر الأربع `Condition` يستبعدها من إصدارات التصدير، فلا تحتوي اللعبة المنشورة على RQL ولا Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)).
- العنصر `<EmbeddedResource>` إلزامي: تقرأ الإضافة الفهرس من تجميعتها، ويجب أن يكون `LogicalName` الخاص به `Godot-MCP.extensions.catalog.json` بالضبط.
- أبقِ `<EnableDynamicLoading>true</EnableDynamicLoading>` في `<PropertyGroup>` (قالب C# في Godot يتضمنه). بدونه لا تُنسخ تجميعات الحزم بجوار تجميعة اللعبة، وتفشل اللعبة المُشغَّلة من المحرر في إنشاء أي سكربت C# ("Cannot instantiate C# script because the associated class could not be found").

شغّل `dotnet restore` ثم ابنِ المشروع. لا حاجة إلى نسخ أي ملفات DLL: أثناء تشغيل المحرر يعثر محلّل التجميعات في الإضافة عليها في مجلد الحزم العام لـ NuGet عبر ملف `*.deps.json` الناتج عن البناء.

<a id="3-enable-the-plugin"></a>
## 3. فعّل المكوّن الإضافي

‏**Project → Project Settings → Plugins → Godot-MCP → Enable.** تطبع لوحة Output:

</div>

```
[Godot-MCP] plugin loaded
```

<div dir="rtl">

<a id="4-connect-an-ai-agent"></a>
## 4. اربط وكيل ذكاء اصطناعي

ثبّت أي عميل MCP (Claude Code أو Claude Desktop أو Cursor أو VS Code مع Copilot أو Codex أو Gemini أو غيرها)، ثم اكتب إعداداته:

</div>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list shows every agent id
```

<div dir="rtl">

يكتب زر **Configure** في لوحة المحرر الإعدادات نفسها. افتراضيًا يستخدم عنوان URL السحابي المثبّت على المشروع `<host>/mcp/p/<pin>`، فيصل الوكيل الذي يُشغَّل في هذا المجلد إلى محرر هذا المشروع حتى عند اتصال عدة محررات؛ مرّر `--no-pin` لاستخدام عنوان `<host>/mcp` المجرّد. في وضع Cloud تحمل الإعدادات مفتاح هذا المشروع (`Authorization: Bearer agd_pk_…`)؛ وبدون تسجيل دخول، أو مع `--oauth`، تقتصر على عنوان URL ويسجّل الوكيل دخوله عبر OAuth الخاص به. أوضاع الاتصال ومتغيرات البيئة: [connection.ar.md](connection.ar.md).

</div>
