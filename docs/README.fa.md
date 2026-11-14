<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: عامل‌های هوش مصنوعی از طریق پلاگین godot_mcp در ویرایشگر Godot کار می‌کنند" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · **فارسی** · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

<div dir="rtl">

‏**Godot-MCP** عامل‌های هوش مصنوعی (Claude Code، Cursor، Codex، GitHub Copilot، Gemini یا هر کلاینت [MCP](https://modelcontextprotocol.io/) دیگری) را به **ویرایشگر Godot** وصل می‌کند. عامل می‌تواند صحنه‌ها، نودها، ریسورس‌ها و اسکریپت‌های شما را بخواند و تغییر دهد، کد C# را در ویرایشگر اجرا کند و اسکرین‌شات‌ها را ببیند. این یک پلاگین (addon) ویرایشگر به زبان C# برای Godot 4.3+‎ (.NET) است، همراه با یک CLI به نام [`godot-cli`](../cli/docs/README.fa.md)، و همتای Godotِ [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

</div>

<p align="center">
  <img src="img/terminal.svg" alt="یک نشست ترمینال: godot-cli پلاگین را نصب می‌کند، وارد حساب می‌شود، Claude Code را پیکربندی می‌کند و ویرایشگر را باز می‌کند" width="100%"/>
</p>

<div dir="rtl">

<a id="how-it-works"></a>
## چگونه کار می‌کند

</div>

<p align="center">
  <img src="img/architecture.svg" alt="یک عامل هوش مصنوعی با پروتکل MCP با یک سرور صحبت می‌کند (ai-game.dev یا یک gamedev-mcp-server محلی)؛ سرور از طریق SignalR به پلاگین ویرایشگر godot_mcp می‌رسد؛ پلاگین ابزارها را روی نخ اصلی Godot اجرا می‌کند" width="100%"/>
</p>

<div dir="rtl">

پلاگین به یک سرور MCP وصل می‌شود: به طور پیش‌فرض سرور میزبانی‌شده‌ی [ai-game.dev](https://ai-game.dev)، یا [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) خودتان که پلاگین می‌تواند آن را برایتان دانلود و اجرا کند. عامل شما به همان سرور وصل می‌شود و ابزارهای پلاگین را فراخوانی می‌کند.

<a id="features"></a>
## ویژگی‌ها

‏✅ ‏**یک زبان کوئری** — [RQL](rql.md) نودها، صحنه‌ها، ریسورس‌ها و تنظیمات را می‌خواند و ویرایش می‌کند\
‏✅ ‏**ویرایش برگشت‌پذیر** — تغییرات RQL همه یا هیچ‌اند و با Ctrl+Z برمی‌گردند\
‏✅ ‏**C# و GDScript** — ساخت، به‌روزرسانی، اعتبارسنجی و اتصال اسکریپت‌های `.cs` / `.gd`\
‏✅ ‏**اجرای C# در ویرایشگر** — `script-execute` قطعه‌کدی را روی تایپ‌های بازی شما اجرا می‌کند\
‏✅ ‏**بازخورد تصویری** — اسکرین‌شات از viewport، یک دوربین یا یک نود\
‏✅ ‏**ابری یا خودمیزبان** — ورود به ai-game.dev، یا سرور محلی‌ای که پلاگین اجرا می‌کند\
‏✅ ‏**افزونه‌ها** — ماژول‌های اختیاری RQL برای انیمیشن، ناوبری، tilemap و پلاگین‌ها\
‏✅ ‏**Runtime درون بازی** — ابزارهای خودتان را از بازیِ در حال اجرا ارائه کنید (پیش‌فرض خاموش)\
‏✅ ‏**ابزارهای خودتان** — هر ابزار یک متد C# با اتریبیوت ([راهنما](custom-tools.fa.md))

<a id="quick-start"></a>
## شروع سریع

پیش‌نیازها: Godot 4.3 یا جدیدتر، **نسخه‌ی .NET** (بیلد مخصوص GDScript نمی‌تواند پلاگین را کامپایل کند)، .NET 8 SDK، و Node.js 22.14+‎ برای `godot-cli`.

</div>

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # optional: a new Godot C# project
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + enable the plugin
godot-cli login                                       # sign in to ai-game.dev (browser, once per machine)
godot-cli setup-mcp claude-code ./MyGodotProject      # write the agent's MCP config
godot-cli open ./MyGodotProject                       # build C#, start the editor, connect
```

<div dir="rtl">

سپس از عامل خود بخواهید: *«۳ مکعب روی دایره‌ای به شعاع ۲ بساز.»*

‏`godot-cli setup-mcp --list` همه‌ی عامل‌های پشتیبانی‌شده را نشان می‌دهد. برای نصب از زبانه‌ی **AssetLib** ویرایشگر، از [فایل zip یک ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest) یا به صورت دستی، [راهنمای نصب دستی](install.fa.md) را دنبال کنید: در این حالت فایل `.csproj` پروژه به این ارجاع‌های NuGet نیاز دارد.

| پکیج | نسخه |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0` (فقط ویرایشگر) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1` (فقط ویرایشگر) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` (فقط ویرایشگر) |

<a id="usage"></a>
## استفاده

با زبان ساده با عامل خود صحبت کنید؛ خودش ابزارها را انتخاب می‌کند. از پوشه‌ی پروژه می‌توانید همان ابزارها را در ترمینال فراخوانی کنید:

</div>

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # list the editor's tools
```

<div dir="rtl">

وقتی هیچ ویرایشگری در حال اجرا نیست، این دستورها یک ویرایشگر headless برای پروژه اجرا می‌کنند. همه‌ی دستورها در [مرجع CLI](../cli/docs/README.fa.md) آمده‌اند.

<a id="tools"></a>
### ابزارها

‏Godot-MCP همراه با **24 ابزار داخلی** در **9 خانواده** ارائه می‌شود:

| خانواده | ابزارها |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | لاگ‌ها، reflection در C# و ثبت خطا درون بازیِ در حال اجرا |
| **ping**, **session**, **skills** | ابزارهای سیستمی برای `godot-cli` و راه‌اندازی، که به عامل‌ها نمایش داده نمی‌شوند |

همه‌ی ابزارها و آرگومان‌ها: [مرجع ابزارها](tools.fa.md).

<a id="extensions"></a>
### افزونه‌ها

افزونه‌ها پکیج‌های اختیاری NuGet هستند که RQL را گسترش می‌دهند؛ هیچ ابزار جداگانه‌ای اضافه نمی‌کنند.

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

<div dir="rtl">

| افزونه | چه اضافه می‌کند | پیش‌نیاز |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | کتابخانه‌ها، ترک‌ها و کلیدهای AnimationPlayer | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | درخت‌های رفتاری و نودهای آن‌ها | پلاگین [Beehave](https://github.com/bitbrain/beehave) |
| [CSG](../extensions/Godot-AI-CSG/README.md) | شکل‌های CSG با ابعاد اعتبارسنجی‌شده | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | تایم‌لاین‌ها و شخصیت‌ها | پلاگین [Dialogic](https://github.com/dialogic-godot/dialogic)، Godot 4.4+‎ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | تنظیمات GridMap، ردیف‌های سلول و ویرایش سلول | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | عامل‌ها، ناحیه‌ها و لینک‌های ناوبری، bake | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | ذرات GPU در 2D و 3D | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | دوربین‌های مجازی، follow و look-at | پلاگین [Phantom Camera](https://github.com/ramokz/phantom-camera)، Godot 4.4+‎ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | منبع زمین، اندازه‌ی ناحیه و متریال‌ها | پلاگین [Terrain3D](https://github.com/TokisanGames/Terrain3D)، Godot 4.4+‎ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | ردیف‌های سلول TileMapLayer و ویرایش سلول | |

پلاگین شخص ثالثِ موردنیاز را خودتان در پروژه نصب کنید؛ بخش **Extensions** در داک ویرایشگر می‌تواند همه را نصب کند.

<a id="documentation"></a>
## مستندات

| صفحه | محتوا |
| --- | --- |
| [نصب دستی](install.fa.md) | ‏AssetLib، فایل zip ریلیز، ارجاع‌های NuGet، راه‌اندازی عامل |
| [اتصال و سرور](connection.fa.md) | حالت‌های Cloud و Custom، متغیرها، سرور |
| [مرجع ابزارها](tools.fa.md) | همه‌ی ابزارها و آرگومان‌هایشان، اسکرین‌شات‌ها، افزونه‌ها |
| [RQL در Godot-MCP](rql.md) | منبع‌ها، توابع، نوشتن، بازگردانی و ذخیره |
| [ابزارهای سفارشی](custom-tools.fa.md) | نوشتن ابزار، بارگذاری مجدد اسمبلی C#، مشکل شناخته‌شده‌ی .NET 10 |
| [استفاده در Runtime](runtime.fa.md) | ‏MCP درون بازیِ در حال اجرا: راه‌اندازی، ثبت خطا، C# درون بازی |
| [امنیت Runtime](runtime-security.fa.md) | آنچه Runtime درون بازی تضمین می‌کند و آنچه شما باید انجام دهید |
| [مرجع CLI](../cli/docs/README.fa.md) | همه‌ی دستورهای `godot-cli`، کدهای خروج و قالب‌های خروجی |
| [معماری](ARCHITECTURE.md) · [انتشار](RELEASING.md) · [افزونه‌ها](EXTENSION-MONOREPO.md) | برای مشارکت‌کنندگان |

<a id="contributing"></a>
## مشارکت

برای بیلد و اجرای تست‌های واحد به فایل اجرایی Godot نیازی نیست:

</div>

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

<div dir="rtl">

قراردادها، تست smoke ویرایشگر headless و مهم‌ترین دام‌ها در [AGENTS.md](../AGENTS.md) آمده‌اند. مخزن را fork کنید و یک Pull Request به `main` باز کنید. پرسش‌ها و نمایش کارها: [Discord](https://discord.gg/cfbdMZX99G).

مرتبط: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## مجوز

‏[Apache-2.0](../LICENSE) © Ivan Murzak. پلاگین همچنین از [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) و Code-Exec استفاده می‌کند که مجوز خودشان را دارند.

آن مجوز [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) است: استفاده از آن، از جمله در محصولات تجاری، رایگان است، مگر برای ساختن محصولی که با محصول صاحب مجوز رقابت کند.

</div>
