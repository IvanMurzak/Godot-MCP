<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: عامل‌های هوش مصنوعی از طریق پلاگین godot_mcp در ویرایشگر Godot کار می‌کنند" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · **فارسی** · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

<div dir="rtl">

‏**Godot-MCP** عامل‌های هوش مصنوعی (Claude Code، Cursor، Codex، GitHub Copilot، Gemini یا هر کلاینت [MCP](https://modelcontextprotocol.io/) دیگری) را به **ویرایشگر Godot** وصل می‌کند. عامل می‌تواند صحنه‌ها، نودها، ریسورس‌ها و اسکریپت‌های شما را بخواند و تغییر دهد، کد C# را بیلد و اجرا کند و اسکرین‌شات‌ها را ببیند. این یک پلاگین (addon) ویرایشگر است همراه با یک CLI به نام [`godot-cli`](../cli/docs/README.fa.md)، و همتای Godotِ [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

‏**پیش‌نیازها:** **بیلد .NET** ویرایشگر Godot 4.3+‎ و **.NET 8 SDK** که یک بار نصب می‌شود. اسکریپت‌های شما می‌توانند GDScript بمانند. ‏`godot-cli` (اختیاری) به Node.js 22.14+‎ نیاز دارد. روی Windows،‏ macOS و Linux کار می‌کند.

<a id="why"></a>
## چرا Godot-MCP

‏✅ ‏**Godot 4.3 تا 4.7** — هر ریلیز در CI روی 4.3، 4.4، 4.5، 4.6 و 4.7 تست می‌شود\
‏✅ ‏**بیلد واقعی C#** — `script-build` دستور `dotnet build` را اجرا می‌کند و خطاهای کامپایلر را برمی‌گرداند\
‏✅ ‏**بررسی GDScript** — `script-validate` خطاهای parse و کامپایل فایل‌های `.gd` را برمی‌گرداند\
‏✅ ‏**ویرایش اتمیک و برگشت‌پذیر** — تغییرات یک کوئری همه یا هیچ اعمال می‌شوند، هر کدام با یک Ctrl+Z\
‏✅ ‏**اسکرین‌شات** — viewport، یک دوربین، یا یک نود که به‌تنهایی رندر می‌شود، به عامل برگردانده می‌شود\
‏✅ ‏**ابزارهای Runtime** — لاگ‌ها، reflection در C# و خطاهای زمان اجرا از بازیِ در حال اجرا (اختیاری)\
‏✅ ‏**ابری رایگان یا کاملاً محلی** — به طور پیش‌فرض ai-game.dev (بدون پورت باز)، یا سرور محلی بدون حساب کاربری\
‏✅ ‏**بدون تله‌متری** — پلاگین و `godot-cli` هیچ داده‌ی استفاده یا تحلیلی ارسال نمی‌کنند

<a id="gdscript"></a>
## با پروژه‌های GDScript کار می‌کند

کد بازی شما GDScript می‌ماند. خود پلاگین به زبان C# است، پس پروژه به چند گام یک‌باره نیاز دارد:

‏✅ ‏**ویرایشگر Godot .NET** — پروژه را در بیلد .NET ویرایشگر Godot 4.3+‎ باز کنید (همان پروژه، همان صحنه‌ها)\
‏✅ ‏**.NET 8 SDK** — یک بار روی هر دستگاه نصب می‌شود\
‏✅ ‏**C# solution** — پروژه‌ی موجود: **Project → Tools → C# → Create C# solution**؛ پروژه‌ی جدید: `create-project --dotnet`\
‏✅ ‏**پلاگین** — `godot-cli install-plugin` آن را به همراه ارجاع‌های NuGet اضافه و فعال می‌کند

‏**آیا پروژه‌ای که فقط GDScript دارد به C# solution نیاز دارد؟** بله، یک بار: پلاگین به زبان C# است و در اسمبلی C# پروژه کامپایل می‌شود. ‏**Create C# solution** یک فایل `.csproj` و یک فایل `.sln` در ریشه‌ی پروژه اضافه می‌کند. اسکریپت‌های `.gd`، صحنه‌ها و ریسورس‌های شما تغییر نمی‌کنند و مثل قبل اجرا می‌شوند؛ عامل GDScript را هم می‌خواند، می‌نویسد و اعتبارسنجی می‌کند. خروجی‌های اکسپورت از آن پس از قالب‌های اکسپورت .NET در Godot استفاده می‌کنند و اسمبلی‌های کوچک runtime پلاگین را شامل می‌شوند، که تا وقتی کد شما `GodotMcpRuntime.Initialize()` را فراخوانی نکند غیرفعال می‌مانند ([چگونه آن‌ها را کنار بگذارید](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users)).

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

پلاگین به یک سرور MCP وصل می‌شود، و عامل شما هم به همان سرور وصل می‌شود و ابزارهای پلاگین را فراخوانی می‌کند. سرور یا یک [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) محلی است که پلاگین برایتان دانلود و اجرا می‌کند، یا رله‌ی میزبانی‌شده‌ی [ai-game.dev](https://ai-game.dev). پلاگین هیچ پورتی از خودش باز نمی‌کند.

‏Cloud پیش‌فرض است چون به هیچ پورت بازی نیاز ندارد و از تداخل پورت با برنامه‌های دیگر جلوگیری می‌کند؛ و رایگان است. حالت محلی یک جایگزین کامل برای کار آفلاین یا در محیط ایزوله (air-gapped) است.

<a id="stability"></a>
### پایداری

- ‏**نسخه‌گذاری:** پلاگین و `godot-cli` همیشه با یک شماره‌ی نسخه‌ی یکسان منتشر می‌شوند؛ پیش از به‌روزرسانی [یادداشت‌های ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest) را بخوانید.
- ‏**چه چیزی بین نسخه‌ها تغییر می‌کند:** یک ریلیز می‌تواند نسخه‌های NuGet موردنیاز پلاگین و نسخه‌ی GameDev-MCP-Server که دانلود می‌کند را تغییر دهد. ‏`godot-cli update` (یا `install-plugin`) نسخه‌های NuGet منطبق با پلاگینی که نصب می‌کنید را می‌نویسد، و پلاگین سرور منطبق با خودش را خودش دریافت می‌کند.
- ‏**دروازه‌ی ریلیز:** ریلیز فقط پس از قبول شدن تست‌های .NET، تست‌های CLI و تست‌های ویرایشگر Godot 4.3–4.7 منتشر می‌شود.
- ‏**تاریخچه‌ی تغییرات:** هر [ریلیز GitHub](https://github.com/IvanMurzak/Godot-MCP/releases) تغییرات ادغام‌شده‌اش را فهرست می‌کند؛ `godot-cli` یک [changelog](../cli/CHANGELOG.md) هم دارد.

<a id="quick-start"></a>
## شروع سریع

‏`godot-cli` را نصب کنید (Node.js 22.14+‎) و پلاگین را به پروژه‌تان اضافه کنید:

</div>

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + enable the plugin
```

<div dir="rtl">

سپس انتخاب کنید عامل چگونه به ویرایشگر برسد. هر دو راه‌اندازی کامل‌اند. ‏Cloud پیش‌فرض پلاگین است: بدون پورت باز، بدون تداخل پورت با برنامه‌های دیگر، و رایگان. حالت محلی همه چیز را روی دستگاه شما نگه می‌دارد، برای کار آفلاین یا در محیط ایزوله.

‏**گزینه‌ی A: محلی، بدون حساب کاربری.** فراخوانی ابزارها روی دستگاه شما می‌ماند.

1. ‏**یک توکن تنظیم کنید.** پروژه را در ویرایشگر Godot .NET باز کنید. در داک **AI Game Developer** گزینه‌ی **Custom** را انتخاب کنید، **Authorization → token** را تنظیم کنید، **Generate** و سپس **Start Server** را بزنید. ‏**Server URL** را یادداشت کنید (`http://localhost:<port>`، با پورتی که از مسیر پروژه به دست می‌آید).
   یا خودتان یک توکن انتخاب کنید، ویرایشگر را از ترمینال با آن باز کنید، سپس **Start Server** را بزنید:
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. ‏**عامل خود را** با همان توکن **وصل کنید**:

</div>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

<div dir="rtl">

> ‏**بدون توکن** سرور محلی از مجوزدهی `none` استفاده می‌کند. فقط روی loopback گوش می‌دهد، پس فقط برنامه‌های روی کامپیوتر خودتان به آن دسترسی دارند.
> در این حالت `godot-cli open ./MyGodotProject` ویرایشگر و سرور محلی آن را در یک گام اجرا می‌کند و URL را برای `setup-mcp --url` چاپ می‌کند.

‏**گزینه‌ی B: ابر ai-game.dev.** روی هر دستگاه یک بار وارد شوید؛ پس از آن پیکربندی‌های عامل کلیدی را در خود دارند که به این پروژه محدود است.

</div>

```bash
godot-cli login                                       # OAuth device login in the browser
godot-cli setup-mcp claude-code ./MyGodotProject      # write the agent's MCP config
godot-cli open ./MyGodotProject                       # build C#, start the editor, connect
```

<div dir="rtl">

سپس از عامل خود بخواهید: *«۳ مکعب روی دایره‌ای به شعاع ۲ بساز.»*

برای نصب از زبانه‌ی **AssetLib** ویرایشگر، از [فایل zip یک ریلیز](https://github.com/IvanMurzak/Godot-MCP/releases/latest) یا به صورت دستی، [راهنمای نصب دستی](install.fa.md) را دنبال کنید: در این حالت فایل `.csproj` پروژه به ارجاع‌های NuGet پلاگین (`com.IvanMurzak.ReflectorNet`، `com.IvanMurzak.McpPlugin`، و پکیج‌های مخصوص ویرایشگر `com.IvanMurzak.Rql`، `com.IvanMurzak.CodeExec`، `com.IvanMurzak.CodeExec.Roslyn` و `Microsoft.CodeAnalysis.CSharp`) با نسخه‌های همان ریلیزی که نصب می‌کنید نیاز دارد.

<a id="relay"></a>
### چه چیزی از ai-game.dev عبور می‌کند

فقط در حالت Cloud. رله بدون حالت (stateless) است: فراخوانی ابزارها را بین عامل شما و ویرایشگر جابه‌جا می‌کند بی‌آنکه محتوای آن‌ها را بررسی یا ذخیره کند، و گفتگوها، کد منبع، فایل‌های پروژه، asset‌ها یا داده‌های صحنه‌ی شما را جمع‌آوری نمی‌کند. فراداده‌ی درخواست (آدرس IP، زمان و مدت، نام ابزار و تعداد فراخوانی، اندازه‌ی درخواست و پاسخ) و فراداده‌ی رویدادهای MCP (نوع رویداد، زمان‌بندی، وضعیت، شناسه‌های نشست، نام ابزار، prompt یا resource، و جزئیات خطا) را ثبت می‌کند. این سوابق در یک بازه‌ی غلتان ۹۰ روزه استفاده می‌شوند. جزئیات: [سیاست حفظ حریم خصوصی](https://ai-game.dev/legal/privacy). در حالت محلی ویرایشگر و سرور فقط از طریق localhost با هم صحبت می‌کنند.

<a id="clients"></a>
## اتصال کلاینت هوش مصنوعی

‏`godot-cli setup-mcp <agent> ./MyGodotProject` پیکربندی را برایتان می‌نویسد (`godot-cli setup-mcp --list` شناسه‌ی همه‌ی عامل‌ها را نشان می‌دهد). نام ورودی سرور `ai-game-developer` است؛ `<url>` همان URL است که `setup-mcp` چاپ می‌کند. در حالت Cloud ورودی همچنین `Authorization: Bearer agd_pk_…` را در خود دارد، کلیدی که فقط به این پروژه دسترسی دارد.

</div>

<details>
<summary><b>Claude Code</b> — <code>.mcp.json</code> در پروژه</summary>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Cursor</b> — <code>.cursor/mcp.json</code></summary>

```bash
godot-cli setup-mcp cursor ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>VS Code (Copilot)</b> — <code>.vscode/mcp.json</code></summary>

```bash
godot-cli setup-mcp vscode-copilot ./MyGodotProject
```

```json
{ "servers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Codex</b> — <code>.codex/config.toml</code></summary>

```bash
godot-cli setup-mcp codex ./MyGodotProject
```

```toml
[mcp_servers.ai-game-developer]
enabled = true
url = "<url>"
tool_timeout_sec = 300
startup_timeout_sec = 30
```

<div dir="rtl">

در حالت Cloud کلید در `http_headers` قرار می‌گیرد.

</div>
</details>

<details>
<summary><b>Gemini CLI</b> — <code>.gemini/settings.json</code></summary>

```bash
godot-cli setup-mcp gemini ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<div dir="rtl">

همچنین پشتیبانی می‌شوند: Claude Desktop، Visual Studio (Copilot)، GitHub Copilot CLI، Rider (Junie)، Antigravity، Cline، Open Code، Kilo Code و یک کلاینت عمومی `custom` ([مرجع CLI](../cli/docs/README.fa.md#supported-ai-agents)).

<a id="what-the-agent-can-do"></a>
## عامل چه کارهایی می‌تواند بکند

**پوشش:** عامل تقریباً هر کاری را که شما در ویرایشگر Godot انجام می‌دهید می‌تواند انجام دهد: صحنه‌ها، نودها، ریسورس‌ها، تنظیمات پروژه، اسکریپت‌ها، buildهای C#، اسکرین‌شات‌ها و بازی در حال اجرا.\
**کارایی:** ابزارها فشرده‌اند، بنابراین عامل به فراخوانی‌ها و توکن‌های کمتری نیاز دارد و سریع‌تر و ارزان‌تر کار می‌کند. این در آزمون‌ها اندازه‌گیری شده است: در [ارزیابی عامل RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md) روی یک دنیای بازی آزمایشی (sandbox)، یک کار با RQL به 2–3 فراخوانی ابزار و حدود 8k توکن نیاز داشت، در برابر حدود 37 فراخوانی و 169k توکن با ابزارهای reflection مرسوم find / get / set.

لازم نیست کوئری بنویسید: عامل راهنمای RQL را که همراه ابزارهاست (`rql-schema`، مهارت `godot-rql`) می‌خواند و کوئری‌ها را خودش می‌نویسد.

| قابلیت | ابزارها | از عامل خود بخواهید |
| --- | --- | --- |
| صحنه‌ها و نودها | `rql-query`, `rql-mutate` | *«یک DirectionalLight3D به صحنه‌ی اصلی اضافه کن و آن را ذخیره کن.»* |
| ویرایش گروهی | `rql-mutate` | *«همه‌ی نودهایی را که نامشان با Enemy شروع می‌شود پنهان کن.»* |
| ریسورس‌ها و تنظیمات پروژه | `rql-query`, `rql-mutate` | *«یک input action به نام jump اضافه کن که به Space متصل باشد.»* |
| جستجو در API | `rql-schema` | *«یک CharacterBody3D چه propertyهایی در Godot دارد؟»* |
| اسکریپت‌ها (GDScript و C#) | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *«یک GDScript بنویس که این نود را بچرخاند و آن را متصل کن.»* |
| بیلد C# | `script-build` | *«پروژه‌ی C# را بیلد کن و خطاها را برطرف کن.»* |
| اجرای C# در ویرایشگر | `script-execute` | *«نودهای MeshInstance3D صحنه‌ی باز را بشمار.»* |
| دیدن نتیجه | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *«از viewport اسکرین‌شات بگیر و نورپردازی را بررسی کن.»* |
| بازیِ در حال اجرا (اختیاری) | `console-*`, `reflection-*`, `runtime-errors-*` | *«بازیِ در حال اجرا را برای خطاهای جدید زمان اجرا بررسی کن.»* |

‏`ping`، `session-close`، `godot-skill-create` و `godot-skill-generate` ابزارهای سیستمی برای `godot-cli` و راه‌اندازی هستند؛ عامل‌ها آن‌ها را نمی‌بینند. همه‌ی ابزارها و آرگومان‌ها: [مرجع ابزارها](tools.fa.md).

همین ابزارها از ترمینال و در پوشه‌ی پروژه هم کار می‌کنند (وقتی هیچ ویرایشگری در حال اجرا نیست، یک ویرایشگر headless اجرا می‌شود):

</div>

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # list the editor's tools
```

<div dir="rtl">

<a id="security"></a>
## امنیت

| حوزه | آنچه اعمال می‌شود |
| --- | --- |
| ‏**سرور محلی** | فقط روی loopback گوش می‌دهد. مجوزدهی پیش‌فرض آن `none` است، پس هر پروسه‌ای روی دستگاه شما می‌تواند آن را فراخوانی کند. برای الزامی کردن توکن Bearer، در داک `token` را انتخاب کنید، `GODOT_MCP_TOKEN` را تنظیم کنید، یا ویرایشگر را با `godot-cli open <path> --mode Custom --auth Required --token <token>` باز کنید. |
| ‏**بررسی Origin** | سرور در هر حالت مجوزدهی به درخواستی با هدر `Origin` غیرمجاز پاسخ `403` می‌دهد (دفاع در برابر DNS rebinding). |
| ‏**پلاگین ویرایشگر** | هیچ پورتی باز نمی‌کند؛ خودش به سرور وصل می‌شود. یک پل کنترلی فقط برای توسعه روی `127.0.0.1` تنها با `GODOT_MCP_DEV_CONTROL=1` اجرا می‌شود. |
| ‏**Cloud** | پیکربندی‌های عامل کلیدی دارند که به یک پروژه محدود است؛ ویرایشگر از ورود دستگاه شما استفاده می‌کند. توکن حساب شما فقط به ai-game.dev یا یک سرور localhost فرستاده می‌شود. رله بدون حالت (stateless) است: محتوای فراخوانی ابزارها را بررسی یا ذخیره نمی‌کند ([سیاست حفظ حریم خصوصی](https://ai-game.dev/legal/privacy)). |
| ‏**توکن‌های ذخیره‌شده** | ویرایشگر توکن خود را به صورت JSON ساده در `user://godot-mcp-config.json` ذخیره می‌کند؛ با این فایل مانند یک راز رفتار کنید. |
| ‏**بازی‌های اکسپورت‌شده** | در بازی هیچ چیز اجرا نمی‌شود مگر اینکه کد شما `GodotMcpRuntime.Initialize(...)` و `Connect()` را فراخوانی کند، و فقط با ابزارهایی که خودتان ثبت می‌کنید. ثبت خطا و C# درون بازی هر کدام جداگانه فعال می‌شوند ([امنیت Runtime](runtime-security.fa.md)). |
| ‏**بدون محافظت** | فراخوانی ابزارها پروژه‌ی شما را تغییر می‌دهد و C# را در ویرایشگر اجرا می‌کند: از کنترل نسخه استفاده کنید، و هرگز سروری با مجوزدهی `none` را در معرض شبکه قرار ندهید. |

جزئیات: [اتصال و سرور](connection.fa.md) و [README سرور GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="extensions"></a>
## افزونه‌ها

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

‏**توسعه‌دهندگان بازی می‌توانند آزادانه از همه چیز استفاده کنند، از جمله در بازی‌های تجاری.** ‏Godot-MCP تحت مجوز [Apache-2.0](../LICENSE) است. وابستگی‌های سمت ویرایشگر آن، RQL و Code-Exec، از [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) استفاده می‌کنند که فقط شرکت‌هایی را محدود می‌کند که ابزارهای هوش مصنوعی رقیب برای توسعه‌ی بازی می‌سازند.

</div>
