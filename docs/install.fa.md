[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · **فارسی** · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

<div dir="rtl">

# نصب دستی

‏`godot-cli install-plugin ./MyGodotProject` همه‌ی مراحل این صفحه را با یک دستور idempotent انجام می‌دهد (به [README](README.fa.md#quick-start) مراجعه کنید). از این صفحه برای نصب از Asset Library یا پروژه‌ای که دستی مدیریت می‌شود استفاده کنید.

<a id="requirements"></a>
## پیش‌نیازها

- ‏**Godot 4.3 یا جدیدتر، نسخه‌ی .NET (mono).** بیلد استاندارد مخصوص GDScript نمی‌تواند پلاگین را کامپایل کند. فایل csproj پلاگین حداقل نسخه را `Godot.NET.Sdk/4.3.0` تعیین می‌کند؛ CI نسخه‌های Godot 4.3 تا 4.7 را تست می‌کند.
- ‏ **.NET 8 SDK** (`net8.0`).

<a id="1-add-the-addon"></a>
## ۱. افزودن پلاگین

یکی را انتخاب کنید:

- ‏**Godot Asset Library.** در ویرایشگر زبانه‌ی **AssetLib** را باز کنید، **Godot-MCP** را جستجو کنید، سپس **Download** و **Install** را بزنید. فایل‌ها در `res://addons/godot_mcp/` قرار می‌گیرند. صفحه‌ی وب: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- ‏**GitHub Release.** فایل `godot-mcp-addon-<version>.zip` را از [آخرین ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest) دانلود کنید و در ریشه‌ی پروژه از حالت فشرده خارج کنید (آرشیو خودش شامل `addons/godot_mcp/` است).
- ‏**از سورس.** پوشه‌ی `addons/godot_mcp/` را از این مخزن در پوشه‌ی `addons/` پروژه کپی کنید.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## ۲. افزودن پکیج‌های NuGet و کاتالوگ افزونه‌ها

‏Godot همه‌ی فایل‌های `.cs` پروژه را در یک اسمبلی کامپایل می‌کند، پس فایل `.csproj` پروژه باید به پکیج‌هایی که پلاگین استفاده می‌کند ارجاع دهد. دقیقاً همین نسخه‌ها را به کار ببرید (با `Godot-MCP.csproj` پلاگین مطابقت دارند):

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

| پکیج | نقش |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | هسته‌ی reflection و سریال‌سازی |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | کلاینت پلاگین MCP |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | ‏RQL، لایه‌ی داده‌ی ابزارهای ویرایشگر (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | ‏Code-Exec، پشتوانه‌ی `script-execute` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | ‏Roslyn، کامپایلر C# که `script-execute` اجرا می‌کند |

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

</div>
