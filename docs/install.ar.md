[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · **العربية**

<div dir="rtl">

# التثبيت اليدوي

‏`godot-cli install-plugin ./MyGodotProject` ينفّذ كل خطوات هذه الصفحة بأمر واحد متساوي القوة (idempotent) (انظر [README](README.ar.md#quick-start)). استخدم هذه الصفحة للتثبيت من Asset Library أو لمشروع تُديره يدويًا.

<a id="requirements"></a>
## المتطلبات

- ‏**Godot 4.3 أو أحدث، إصدار .NET (mono).** يمكن أن تبقى شيفرة لعبتك بلغة GDScript، لكن الإضافة مكتوبة بـ C#: افتح المشروع في إصدار .NET من المحرر، وأعطه ملف مشروع C# (`.csproj`). لمشروع GDScript قائم استخدم **Project → Tools → C# → Create C# solution** مرة واحدة. يثبّت ملف csproj الخاص بالإضافة `Godot.NET.Sdk/4.3.0` حدًّا أدنى؛ ويختبر CI إصدارات Godot 4.3 و4.4 و4.5 و4.6 و4.7.
- ‏ **.NET 10 SDK (موصى به)؛ ولا يزال .NET 8 مدعومًا**. يمكن أن يستهدف المشروع `net10.0` أو `net8.0` (الحد الأدنى؛ انتهى دعم .NET 9 ولا يُختبَر): تُوزَّع الإضافة كشيفرة مصدرية وتُترجَم بـ `TargetFramework` الخاص بمشروعك، ولا يغيّره `godot-cli install-plugin` أبدًا. يختبر CI ‏`net8.0` و`net10.0`. إذا كانت بيئة تشغيل .NET 10 مثبّتة، أيًّا كان `TargetFramework` لديك (يعمل محرر Godot على أحدث بيئة تشغيل مثبّتة)، فاقرأ [المشكلة المعروفة في إعادة التحميل](custom-tools.ar.md).
- ‏ **Windows أو macOS أو Linux.** يعمل Godot-MCP على الأنظمة الثلاثة.

<a id="1-add-the-addon"></a>
## 1. أضِف الإضافة

اختر طريقة واحدة:

- ‏**Godot Asset Library.** في المحرر افتح تبويب **AssetLib**، وابحث عن **Godot-MCP**، ثم **Download** و**Install**. تُوضع الملفات في `res://addons/godot_mcp/`. صفحة الويب: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- ‏**GitHub Release.** نزّل `godot-mcp-addon-<version>.zip` من [أحدث إصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest) واستخرجه في جذر المشروع (الأرشيف يحتوي أصلًا على `addons/godot_mcp/`).
- **من المصدر.** انسخ `addons/godot_mcp/` من هذا المستودع إلى مجلد `addons/` في المشروع.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. أضِف حزم NuGet وفهرس الامتدادات

يترجم Godot كل ملف `.cs` في المشروع إلى تجميعة واحدة، لذا يجب أن يشير ملف `.csproj` للمشروع إلى الحزم التي تستخدمها الإضافة. استخدم بالضبط إصدارات الإصدار (release) الذي تثبّته: يكتبها `godot-cli install-plugin` نيابةً عنك، ويسردها ملف zip الخاص بكل إصدار في `addons/godot_mcp/nuget-deps.json`، ويحتويها كل وسم إصدار في ملف `Godot-MCP.csproj` الخاص به ([أحدث إصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

</div>

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="<version>" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="<version>" />
</ItemGroup>

<!-- Editor-only: exported games (ExportDebug / ExportRelease) ship no RQL and no Roslyn. -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
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
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | ‏Code-Exec، الذي يعمل خلف `csharp-exec` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | ‏Roslyn، مترجم C# الذي يشغّله `csharp-exec` |

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

<a id="web-export"></a>
## التصدير إلى الويب

لا يصدّر أي محرر Godot 4 .NET (من 4.3 إلى 4.7) إلى الويب: يرفض كل منها برسالة "Exporting to Web is currently not supported in Godot 4 when using C#/.NET"، ولا تتضمن قوالب التصدير الخاصة بـ .NET قالبًا للويب. ومع ذلك تُصدَّر لعبة **GDScript** المثبّتة فيها الإضافة إلى الويب من **المحرر القياسي (غير .NET) من الإصدار نفسه**. لا يستطيع ذلك المحرر تحميل شيفرة C# الخاصة بالإضافة، واللعبة لا تشغّلها أبدًا. واصل التطوير في محرر .NET.

1. ‏**أضِف إعدادًا مسبقًا للويب (Web preset)** في أيٍّ من المحررين: **Project → Export… → Add… → Web**. يسمح لك محرر .NET بإضافته؛ وإنما يرفض التصدير به فقط. اختياري: تحت **Resources → Filters to exclude files** أضِف `addons/godot_mcp/*`، فتبقى أيقونات الإضافة (نحو 100 KB) خارج ملف `.pck`.
2. ‏**احصل على المحرر القياسي** لإصدارك من [godotengine.org](https://godotengine.org/download/archive/) (مثلًا `Godot_v4.5.1-stable_win64.exe.zip`)، وثبّت قوالب التصدير الخاصة به (**Editor → Manage Export Templates** في ذلك المحرر، أو ملف `.tpz` الخاص به). تُثبَّت هذه القوالب منفصلة عن قوالب .NET.
3. ‏**صدِّر.** ‏`godot-cli export Web build/web/index.html` (الوسيط الأول هو اسم الإعداد المسبق). عندما يكون المحرر الذي يحدده `godot-cli` نسخة .NET، فإنه يصدّر بدلًا منه بالمحرر القياسي من الإصدار نفسه: نسخة رسمية غير مُعاد تسميتها (`Godot_v4.5.1-stable_win64[_console].exe`، `Godot_v4.5.1-stable_linux.x86_64`) بجوار مجلد محرر .NET، أو على `PATH`، أو في مجلدات التثبيت المعتادة. على macOS، أو مع ملف تنفيذي مُعاد تسميته، مرّره عبر `--editor-path` (على macOS: `Godot.app/Contents/MacOS/Godot`). أو افتح المشروع في المحرر القياسي واستخدم **Project → Export…**، واختر الإعداد المسبق للويب، ثم **Export Project**. يحذّر ذلك المحرر من أن المشروع يستخدم C# ويعرض *Unable to load addon script* للملف `GodotMcpPlugin.cs`: كلاهما متوقَّع. يبقى المكوّن الإضافي مفعّلًا، ويحمّله محرر .NET كما كان.

لا يمكن تصدير مشروع يحتوي على سكربتات C# أخرى (شيفرة اللعبة، أو سكربتات إضافة أخرى) إلى الويب في Godot 4: يتوقف `godot-cli export` بتلك الرسالة عند إعداد مسبق للويب، أيًّا كان المحرر الذي يستخدمه.

</div>
