[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · **فارسی** · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

<div dir="rtl">

# نصب دستی

‏`godot-cli install-plugin ./MyGodotProject` همه‌ی مراحل این صفحه را با یک دستور idempotent انجام می‌دهد (به [README](README.fa.md#quick-start) مراجعه کنید). از این صفحه برای نصب از Asset Library یا پروژه‌ای که دستی مدیریت می‌شود استفاده کنید.

<a id="requirements"></a>
## پیش‌نیازها

- ‏**Godot 4.3 یا جدیدتر، نسخه‌ی .NET (mono).** کد بازی شما می‌تواند GDScript بماند، اما پلاگین به زبان C# است: پروژه را در بیلد .NET ویرایشگر باز کنید و یک فایل پروژه‌ی C# (`.csproj`) به آن بدهید. برای یک پروژه‌ی GDScript موجود، یک بار **Project → Tools → C# → Create C# solution** را اجرا کنید. فایل csproj پلاگین حداقل نسخه را `Godot.NET.Sdk/4.3.0` تعیین می‌کند؛ CI نسخه‌های Godot 4.3، 4.4، 4.5، 4.6 و 4.7 را تست می‌کند.
- ‏ **.NET 10 SDK (توصیه‌شده)؛ .NET 8 همچنان پشتیبانی می‌شود**. پروژه می‌تواند `net10.0` یا `net8.0` (حداقل؛ پشتیبانی .NET 9 تمام شده و تست نمی‌شود) را هدف بگیرد: افزونه به‌صورت سورس منتشر می‌شود و با `TargetFramework` پروژه‌ی شما کامپایل می‌شود و `godot-cli install-plugin` هرگز آن را تغییر نمی‌دهد. CI ‏`net8.0` و `net10.0` را تست می‌کند. اگر runtimeِ .NET 10 نصب است، فارغ از `TargetFramework` شما (ویرایشگر Godot روی جدیدترین runtimeِ نصب‌شده اجرا می‌شود)، [مشکل شناخته‌شده‌ی بارگذاری مجدد](custom-tools.fa.md) را بخوانید.
- ‏ **Windows،‏ macOS یا Linux.** ‏Godot-MCP روی هر سه کار می‌کند.

<a id="1-add-the-addon"></a>
## ۱. افزودن پلاگین

یکی را انتخاب کنید:

- ‏**Godot Asset Library.** در ویرایشگر زبانه‌ی **AssetLib** را باز کنید، **Godot-MCP** را جستجو کنید، سپس **Download** و **Install** را بزنید. فایل‌ها در `res://addons/godot_mcp/` قرار می‌گیرند. صفحه‌ی وب: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- ‏**GitHub Release.** فایل `godot-mcp-addon-<version>.zip` را از [آخرین ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest) دانلود کنید و در ریشه‌ی پروژه از حالت فشرده خارج کنید (آرشیو خودش شامل `addons/godot_mcp/` است).
- ‏**از سورس.** پوشه‌ی `addons/godot_mcp/` را از این مخزن در پوشه‌ی `addons/` پروژه کپی کنید.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## ۲. افزودن پکیج‌های NuGet و کاتالوگ افزونه‌ها

‏Godot همه‌ی فایل‌های `.cs` پروژه را در یک اسمبلی کامپایل می‌کند، پس فایل `.csproj` پروژه باید به پکیج‌هایی که پلاگین استفاده می‌کند ارجاع دهد. دقیقاً نسخه‌های ریلیزی را که نصب می‌کنید به کار ببرید: `godot-cli install-plugin` آن‌ها را برایتان می‌نویسد، فایل zip هر ریلیز آن‌ها را در `addons/godot_mcp/nuget-deps.json` فهرست می‌کند، و هر تگ ریلیز آن‌ها را در `Godot-MCP.csproj` خود دارد ([آخرین ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

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

| پکیج | نقش |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | هسته‌ی reflection و سریال‌سازی |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | کلاینت پلاگین MCP |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | ‏RQL، لایه‌ی داده‌ی ابزارهای ویرایشگر (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | ‏Code-Exec، پشتوانه‌ی `csharp-exec` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | ‏Roslyn، کامپایلر C# که `csharp-exec` اجرا می‌کند |

- چهار پکیج مخصوص ویرایشگر یک `Condition` دارند که آن‌ها را از بیلدهای اکسپورت بیرون نگه می‌دارد، پس بازیِ منتشرشده نه RQL دارد و نه Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)).
- ‏`<EmbeddedResource>` الزامی است: پلاگین کاتالوگ را از اسمبلی خود می‌خواند و `LogicalName` آن باید دقیقاً `Godot-MCP.extensions.catalog.json` باشد.
- ‏`<EnableDynamicLoading>true</EnableDynamicLoading>` را در `<PropertyGroup>` نگه دارید (قالب C# در Godot آن را دارد). بدون آن، اسمبلی‌های پکیج‌ها کنار اسمبلی بازی کپی نمی‌شوند و بازی‌ای که از ویرایشگر اجرا شود در ساختن همه‌ی اسکریپت‌های C# شکست می‌خورد ("Cannot instantiate C# script because the associated class could not be found").

‏`dotnet restore` را اجرا کنید، سپس بیلد بگیرید. نیازی به کپی کردن DLL نیست: در زمان اجرای ویرایشگر، resolver اسمبلیِ پلاگین آن‌ها را از طریق `*.deps.json` بیلد در پوشه‌ی global-packages در NuGet پیدا می‌کند.

<a id="3-enable-the-plugin"></a>
## ۳. فعال کردن پلاگین

‏**Project → Project Settings → Plugins → Godot-MCP → Enable.** پنل Output این را چاپ می‌کند:

</div>

```
[Godot-MCP] plugin loaded
```

<div dir="rtl">

<a id="4-connect-an-ai-agent"></a>
## ۴. اتصال یک عامل هوش مصنوعی

هر کلاینت MCP را نصب کنید (Claude Code، Claude Desktop، Cursor، VS Code با Copilot، Codex، Gemini، …)، سپس پیکربندی آن را بنویسید:

</div>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list shows every agent id
```

<div dir="rtl">

دکمه‌ی **Configure** در داک ویرایشگر همین پیکربندی را می‌نویسد. به طور پیش‌فرض از URL ابریِ سنجاق‌شده به پروژه `<host>/mcp/p/<pin>` استفاده می‌کند، تا عاملی که در این پوشه اجرا می‌شود حتی وقتی چند ویرایشگر وصل هستند به ویرایشگر همین پروژه برسد؛ برای URL ساده‌ی `<host>/mcp` گزینه‌ی `--no-pin` را بدهید. در حالت Cloud پیکربندی کلید همین پروژه را در خود دارد (`Authorization: Bearer agd_pk_…`)؛ بدون ورود، یا با `--oauth`، فقط شامل URL است و عامل با OAuth خودش وارد می‌شود. حالت‌های اتصال و متغیرهای محیطی: [connection.fa.md](connection.fa.md).

<a id="web-export"></a>
## اکسپورت Web

هیچ‌کدام از ویرایشگرهای .NET در Godot 4 (4.3 تا 4.7) برای Web اکسپورت نمی‌کنند: هر کدام با پیام "Exporting to Web is currently not supported in Godot 4 when using C#/.NET" رد می‌کنند، و قالب‌های اکسپورت .NET هیچ قالبی برای Web ندارند. با این حال، بازی **GDScript** که پلاگین روی آن نصب است از **ویرایشگر استاندارد (غیر .NET) همان نسخه** برای Web اکسپورت می‌شود. آن ویرایشگر نمی‌تواند C# پلاگین را بارگذاری کند، و بازی هم هرگز آن را اجرا نمی‌کند. توسعه را در ویرایشگر .NET ادامه دهید.

1. ‏**یک preset برای Web اضافه کنید**، در هر کدام از دو ویرایشگر: **Project → Export… → Add… → Web**. ویرایشگر .NET اجازه‌ی افزودن آن را می‌دهد؛ فقط از اکسپورت با آن خودداری می‌کند. اختیاری: در **Resources → Filters to exclude files** مقدار `addons/godot_mcp/*` را اضافه کنید تا آیکون‌های پلاگین (حدود 100 KB) وارد `.pck` نشوند.
2. ‏**ویرایشگر استاندارد نسخه‌ی خود را بگیرید**، از [godotengine.org](https://godotengine.org/download/archive/) (مثلاً `Godot_v4.5.1-stable_win64.exe.zip`)، و قالب‌های اکسپورت آن را نصب کنید (**Editor → Manage Export Templates** در همان ویرایشگر، یا فایل `.tpz` آن). این‌ها جدا از قالب‌های .NET نصب می‌شوند.
3. ‏**اکسپورت.** ‏`godot-cli export Web build/web/index.html` (آرگومان اول نام preset است). وقتی ویرایشگری که `godot-cli` پیدا می‌کند یک بیلد .NET باشد، به جای آن با ویرایشگر استاندارد همان نسخه اکسپورت می‌کند: یک بیلد رسمی و تغییرنام‌نیافته (`Godot_v4.5.1-stable_win64[_console].exe`، `Godot_v4.5.1-stable_linux.x86_64`) کنار پوشه‌ی ویرایشگر .NET، روی `PATH` یا در پوشه‌های معمول نصب. در macOS، یا با فایل اجرایی تغییرنام‌یافته، آن را با `--editor-path` بدهید (در macOS: `Godot.app/Contents/MacOS/Godot`). یا پروژه را در ویرایشگر استاندارد باز کنید، از **Project → Export…** استفاده کنید، preset مربوط به Web را انتخاب کنید و **Export Project** را بزنید. آن ویرایشگر هشدار می‌دهد که پروژه از C# استفاده می‌کند و برای `GodotMcpPlugin.cs` پیام *Unable to load addon script* را نشان می‌دهد: هر دو مورد انتظار است. پلاگین فعال می‌ماند و ویرایشگر .NET مثل قبل آن را بارگذاری می‌کند.

پروژه‌ای که اسکریپت‌های C# دیگری دارد (کد بازی، یا اسکریپت‌های پلاگینی دیگر) در Godot 4 قابل اکسپورت برای Web نیست: ‏`godot-cli export` برای یک preset مربوط به Web، با هر ویرایشگری که استفاده کند، با همان پیام متوقف می‌شود.

</div>
