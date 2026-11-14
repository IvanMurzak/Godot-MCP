<div align="center">

<p><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/cli-hero.svg" alt="godot-cli: پلاگین Godot MCP را نصب کنید، وارد شوید، عامل هوش مصنوعی خود را راه‌اندازی کنید و ویرایشگر Godot را از ترمینال هدایت کنید" width="100%"/></p>

[![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) [![Node](https://img.shields.io/badge/Node-22.14%2B-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=nodedotjs)](https://nodejs.org/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · **فارسی** · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

<div dir="rtl">

‏ **`godot-cli`** خط فرمانِ [Godot-MCP](https://github.com/IvanMurzak/Godot-MCP) است، پلاگین ویرایشگر Godot که عامل‌های هوش مصنوعی (Claude Code، Cursor، Codex، GitHub Copilot، …) را از طریق [Model Context Protocol](https://modelcontextprotocol.io/) به ویرایشگر Godot وصل می‌کند. این ابزار همتای [`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli) و [`unreal-mcp-cli`](https://www.npmjs.com/package/unreal-mcp-cli) است و پشتوانه‌ی آن [ai-game.dev](https://ai-game.dev) است.

</div>

<p align="center"><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/terminal.svg" alt="یک نشست ترمینال: godot-cli پلاگین را نصب می‌کند، وارد حساب می‌شود، Claude Code را پیکربندی می‌کند و ویرایشگر را باز می‌کند" width="100%"/></p>

<div dir="rtl">

<a id="features"></a>
## ویژگی‌ها

‏✅ ‏**نصب با یک دستور** — `install-plugin` پلاگین و ارجاع‌های NuGet را اضافه و فعال می‌کند\
‏✅ ‏**یک بار ورود** — `login` از OAuth device flow استفاده می‌کند؛ ویرایشگر اعتبارنامه را برمی‌دارد\
‏✅ ‏**راه‌اندازی عامل** — `setup-mcp` پیکربندی MCP را برای Claude Code، Cursor، Codex و… می‌نویسد\
‏✅ ‏**کنترل ویرایشگر** — `open`، `close`، `status` و `wait-for-ready` برای ویرایشگر پروژه\
‏✅ ‏**اجرای ابزارها** — `rql`، `exec`، `run-tool` اگر ویرایشگری باز نباشد، یکی headless اجرا می‌کنند\
‏✅ ‏**CI بدون رابط** — `check`، `import`، `test`، `export` و `run-script` بدون ویرایشگر باز\
‏✅ ‏**افزونه‌ها** — `install-extension` ماژول‌های اختیاری RQL را اضافه می‌کند؛ `update` همه را به‌روز می‌کند

<a id="quick-start"></a>
## شروع سریع

به [Node.js](https://nodejs.org/) 22.14 یا جدیدتر و یک ویرایشگر Godot 4.3+‎ نسخه‌ی .NET همراه با .NET 8 SDK نیاز دارد.

</div>

```bash
npm install -g godot-cli

godot-cli install-plugin ./MyGodotProject          # addon + NuGet references + enable the plugin
godot-cli login                                    # sign in to ai-game.dev, once per machine
godot-cli setup-mcp claude-code ./MyGodotProject   # write the agent's MCP config
godot-cli open ./MyGodotProject                    # build C#, start the editor, connect
godot-cli wait-for-ready ./MyGodotProject          # wait until the plugin answers
```

<div dir="rtl">

از پوشه‌ی پروژه C# یا RQL را در ویرایشگر اجرا کنید؛ اگر ویرایشگری باز نباشد، یک ویرایشگر headless اجرا می‌شود:

</div>

```bash
godot-cli exec 'EditorInterface.Singleton.GetEditedSceneRoot()?.Name'
godot-cli rql 'nodes | where name starts-with "Tree" | count'
```

<div dir="rtl">

یا هر دستوری را بدون نصب اجرا کنید: `npx godot-cli install-plugin ./MyGodotProject`.

<a id="commands"></a>
## دستورها

- ‏ **`open [path]`** — اسمبلی C# پروژه را بیلد می‌کند (تا پلاگین در اولین باز شدن بارگذاری شود؛ پایین‌تر را ببینید)، فایل اجرایی ویرایشگر Godot را پیدا می‌کند و `--editor --path <project>` را با متغیرهای محیطی اتصال `GODOT_MCP_*` اجرا می‌کند. `--no-build` بیلد را رد می‌کند. `--headless` به جای آن یک نشست ویرایشگر بدون پنجره اجرا می‌کند ([`exec`](#exec) را ببینید).
- ‏ **`exec [code]`** — اجرای C# در ویرایشگر از طریق `script-execute` ([`exec`](#exec) را ببینید): از ویرایشگرِ در حال اجرا استفاده می‌کند، وگرنه یک ویرایشگر headless اجرا می‌کند و آن را گرم نگه می‌دارد.
- ‏ **`create-project [path]`** — ساخت اسکلت یک پروژه‌ی جدید Godot؛ `--dotnet` یک `.csproj` با Godot.NET.Sdk هم می‌نویسد و `--name` نام آن را تعیین می‌کند.
- ‏ **`build [path]`** — اسمبلی C# پروژه را بیلد می‌کند (`dotnet build`) تا پلاگین `godot_mcp` در باز شدن بعدی ویرایشگر بارگذاری شود. برای پروژه‌های فقط GDScript (بدون `.csproj`) کاری انجام نمی‌دهد. این همان بیلدی است که `open` پیش از اجرا انجام می‌دهد.
- ‏ **`rql [query]`** — یک کوئری RQL از خط فرمان، بدون JSON: `godot-cli rql 'nodes | where name starts-with "Tree" | count'`. کوئری‌ای که آخرین مرحله‌اش `set` یا `call` باشد به `rql-mutate` می‌رود (`--dry-run`، `--max-targets <n>`) و بقیه به `rql-query`؛ `--schema [type]` ابزار `rql-schema` را اجرا می‌کند؛ `-f <file>` (`-f -`: stdin) کوئری را وقتی shell نقل‌قول‌هایش را حذف می‌کند از فایل می‌خواند. مثل `run-tool` یک ویرایشگر headless اجرا می‌کند. پس از `SaveScene()`، `NewScene(...)`، `CreateResource(...)` یا یک ویرایش `godot.resource(...)` فایل‌های نوشته‌شده را چاپ می‌کند (`Saved to disk: res://scenes/forest.tscn`)؛ عملگری که به سبک زبان دیگری نوشته شده (`startsWith`، `like`) یک راهنمایی با املای RQL می‌گیرد. `rql --help` دستورالعمل‌ها را فهرست می‌کند.
- ‏ **`run-tool <tool> [path]`** — یک POST به `<url>/api/tools/<tool>` با ورودی JSON. حالت متنی پاسخ متنی را به صورت متن چاپ می‌کند (`--raw`: JSON پاسخ؛ `--verbose`: endpoint را هم). وقتی هیچ ویرایشگری روی سرور محلی پروژه پاسخ ندهد، مثل `exec` یک ویرایشگر headless اجرا می‌کند (`--no-start` غیرفعالش می‌کند؛ `--once` آن را پس از فراخوانی می‌بندد).
- ‏ **`run-system-tool <tool> [path]`** — یک POST به `<url>/api/system-tools/<tool>` (ابزارهایی که به کلاینت‌های MCP نمایش داده نمی‌شوند). اجرای خودکار مثل `run-tool`.
- ‏ **`check [path]`** — دروازه‌ی CI بدون ویرایشگرِ در حال اجرا: import، `dotnet build`، راه‌اندازی ویرایشگر و بررسی بارگذاری GDScript، که بر اساس لاگ‌ها قضاوت می‌شود ([دستورهای headless](#headless-commands-check-import-test-export-run-script) را ببینید).
- ‏ **`import [path]`** — `godot --headless --import`، که با خطاهای import و اسکریپت در لاگ شکست می‌خورد.
- ‏ **`test [path]`** — اجرای headless تست‌های GUT یا gdUnit4 پروژه با یک timeout سخت؛ نتیجه از گزارش JUnit و لاگ.
- ‏ **`export <preset> <output>`** — اکسپورت headless یک preset (`--debug`، `--pack`)؛ ابتدا قالب‌های اکسپورت و پس از آن خروجی را بررسی می‌کند.
- ‏ **`run-script <script> [args...]`** — اجرای یک GDScriptِ یک‌بارمصرف از نوع `SceneTree` (`godot --headless -s`)؛ کد خروج 0 فقط پس از `quit(0)`.
- ‏ **`commands`** — مانیفست ماشین‌خوان همه‌ی دستورها، آرگومان‌ها و گزینه‌هایشان و کدهای خروج (`--format json`).
- ‏ **`tools [name]`** — ابزارهای هوش مصنوعی ویرایشگر (`tools/list` در MCP؛ اگر ویرایشگری پاسخ ندهد یک ویرایشگر headless اجرا می‌کند و فهرست را در `.ai-game-dev/tools.json` که git آن را نادیده می‌گیرد کش می‌کند). با یک نام: توضیح، آرگومان‌ها و یک نمونه فراخوانی `run-tool` آن ابزار. `--refresh` دوباره از ویرایشگر می‌پرسد؛ `--no-start` از کش یا فهرست داخلی استفاده می‌کند.
- ‏ **`status [path]`** — تشخیص ویرایشگر Godotِ در حال اجرا برای پروژه و بررسی سلامت سرور MCP.
- ‏ **`wait-for-ready [path]`** — پرس‌وجوی مکرر از سرور MCP تا به `ping` پاسخ دهد.
- ‏ **`login [path]`** — احراز هویت با ابر ai-game.dev از طریق OAuth 2.1 device-authorization flow (RFC 8628): مرورگر را باز می‌کند، سپس یک اعتبارنامه‌ی ابری در مخزن مشترک دستگاه (`~/.ai-game-dev/credentials.json`) ذخیره می‌کند که پلاگین ویرایشگر خودکار آن را برمی‌دارد. برای فلگ‌های `--project` / `--base-url` / `--force` / `--tools-only` / `--yes` بخش `login` در پایین را ببینید.
- ‏ **`setup-mcp <agent> [path]`** — پیکربندی کلاینت MCP عامل را می‌نویسد که به URL سنجاق‌شده به پروژه `<host>/mcp/p/<pin>` اشاره می‌کند (تا عامل به ویرایشگر *همین* پروژه برسد). در حالت Cloud پیکربندی کلید همین پروژه را دارد (`Authorization: Bearer agd_pk_…`). `--no-pin` را برای URL ساده‌ی `<host>/mcp`، `--oauth` را برای پیکربندی فقط URL، و `--regenerate-key` را برای تعویض کلید اضافه کنید.
- ‏ **`setup-skills <agent> [path]`** — skill عاملِ `godot-cli` (روند شروع از صفر، دستورالعمل‌های RQL، کدهای خروج) و `ai-game-dev-app` را به صورت آفلاین در مسیر skillهای عامل می‌نویسد. `--list` پشتیبانی skill هر عامل را نشان می‌دهد.
- ‏ **`configure [path]`** — فهرست / نمایش / پنهان کردن ابزارها، پرامپت‌ها و ریسورس‌ها در `.godot-mcp/features.json`ِ محلی پروژه (`--enable-*` نمایش می‌دهد، `--disable-*` پنهان می‌کند؛ ابزار پنهان وقتی با نام فراخوانی شود همچنان اجرا می‌شود).
- ‏ **`close [path]`** — متوقف کردن آرام ویرایشگر Godotِ در حال اجرا برای یک پروژه (`--force` برای kill اجباری). سرور MCP محلی‌ای را که آن ویرایشگر اجرا کرده، اگر بیشتر از آن زنده مانده باشد، هم متوقف می‌کند. `--owned-only` فقط ویرایشگری را می‌بندد که `godot-cli` اجرا کرده (`exec` / `open --headless`).
- ‏ **`install-plugin [path]`** — نصب کامل پلاگین `godot_mcp`: ساختن `res://addons/godot_mcp/` (دانلود ریلیز متناظر از GitHub، یا با `--source <path>` یک کپی محلی)، افزودن `PackageReference`های NuGet که پلاگین نصب‌شده نیاز دارد + `<EmbeddedResource>`ِ کاتالوگ افزونه‌ها به `.csproj` پروژه، و فعال کردن پلاگین. Idempotent است.
- ‏ **`install-extension <id> [path]`** — نصب یک **افزونه**‌ی Godot-MCP (یک پکیج اختیاری ماژول RQL) در پروژه: `<id>` را از کاتالوگ مشترک resolve می‌کند، `<PackageReference>` آن را در `.csproj` پروژه اضافه/به‌روز می‌کند، سپس برای restore دوباره بیلد می‌کند. Idempotent است و رفتارش دقیقاً مثل داک درون ویرایشگر است.
- ‏ **`remove-plugin [path]`** — غیرفعال کردن پلاگین `godot_mcp` در `[editor_plugins]`ِ `project.godot` (فایل‌های پلاگین را حذف نمی‌کند).
- ‏ **`update [path]`** — به‌روزرسانی همه چیز: پلاگین `godot_mcp` پروژه و افزونه‌های نصب‌شده به آخرین نسخه‌هایشان، سپس خودِ `godot-cli`. بیرون از یک پروژه فقط CLI را به‌روز می‌کند. `--check` گزارش می‌دهد (اگر چیزی قدیمی باشد کد خروج 2)؛ `--no-self`، `--no-project`، `--no-extensions` یک مرحله را رد می‌کنند.

<a id="build-before-open-open--build"></a>
### بیلد پیش از باز کردن (`open` / `build`)

‏`open` **پیش از اجرای ویرایشگر، اسمبلی C# پروژه را بیلد می‌کند**. Godot به محض بارگذاری ویرایشگر، `EditorPlugin`ِ یک پلاگینِ فعال را می‌سازد؛ بنابراین در *اولین* باز شدنِ یک پروژه‌ی C# که هرگز بیلد نشده، هنوز اسمبلی‌ای وجود ندارد و Godot با `Unable to load addon script 'res://addons/godot_mcp/Editor/GodotMcpPlugin.cs' … Disabling the addon` شکست می‌خورد. بیلد گرفتن از قبل تضمین می‌کند که اسمبلی هنگام ساخته شدن پلاگین وجود دارد.

- بیلد همان است که `godot-cli build` اجرا می‌کند: `dotnet build <project>.csproj --configuration Debug`.
- ‏**پروژه‌های فقط GDScript** (بدون `.csproj` در ریشه) خودکار رد می‌شوند؛ چیزی برای کامپایل وجود ندارد.
- برای پروژه‌های C# بیلد **بدون شرط** اجرا می‌شود؛ `dotnet build` افزایشی است، پس پروژه‌ی به‌روز به‌سرعت و بدون کار رد می‌شود. برای رد کردن آن `--no-build` را به `open` بدهید (مثلاً اگر قبلاً بیلد گرفته‌اید، یا می‌خواهید سریع‌ترین باز شدن را داشته باشید).
- اگر بیلد **شکست بخورد**، `open` ویرایشگر را **اجرا نمی‌کند** (اجرای آن فقط خطای غیرفعال شدن پلاگین را تکرار می‌کرد)؛ به جای آن خطا نمایش داده می‌شود.

<a id="editor-resolution-open"></a>
### پیدا کردن ویرایشگر (`open`)

‏`open` فایل اجرایی ویرایشگر Godot را به این ترتیب پیدا می‌کند:

1. ‏`--editor-path <path>` (صریح).
2. متغیرهای محیطی `GODOT_BIN` / `GODOT4_BIN`.
3. اولین فایل اجرایی Godot منطبق در `PATH` (در Windows بیلد mono ترجیح داده می‌شود).
4. پوشه‌های نصب رایج هر سیستم‌عامل: Windows بیلد mono را ترجیح می‌دهد؛ macOS فایل اجرایی `.app/Contents/MacOS/Godot` را resolve می‌کند؛ Linux مکان‌های رایج فایل‌های اجرایی استخراج‌شده را می‌گردد.

<a id="connection-env-vars"></a>
### متغیرهای محیطی اتصال

‏`open` این‌ها را به پروسه‌ی ویرایشگر می‌فرستد (نام‌ها با `GodotMcpConfig` پلاگین مطابقت دارند):

| فلگ | متغیر محیطی |
| --- | --- |
| `--url <host>` | `GODOT_MCP_HOST` |
| `--cloud-url <url>` | `GODOT_MCP_CLOUD_URL` |
| `--token <token>` | `GODOT_MCP_TOKEN` |
| `--auth None\|Required` | `GODOT_MCP_AUTH_OPTION` |
| `--mode Cloud\|Custom` | `GODOT_MCP_CONNECTION_MODE` |
| `--log-level <level>` | `GODOT_MCP_LOG_LEVEL` |

<a id="server-url-resolution-run-tool--status--wait-for-ready"></a>
### تعیین URL سرور (`run-tool` / `status` / `wait-for-ready`)

‏URL پایه‌ی سروری که فراخوانی ابزار به آن POST می‌شود (`<base>/api/tools/<name>`، یا برای ابزارهای سیستمی `<base>/api/system-tools/<name>`؛ `status` / `wait-for-ready` ابزار سیستمی `ping` را بررسی می‌کنند) به این ترتیب تعیین می‌شود:

1. ‏`--url <url>` (جایگزینی صریح).
2. متغیر محیطی `GODOT_MCP_HOST` (میزبان حالت Custom).
3. متغیر محیطی `GODOT_MCP_CLOUD_URL` ← URL **هاب `/mcp`** ابری (اگر `/mcp` نباشد اضافه می‌شود).
4. ‏`GODOT_MCP_CONNECTION_MODE=Cloud` ← هاب ابری پیش‌فرض `https://ai-game.dev/mcp`.
5. ‏**نشانگر پروژه‌ی ثبت‌شده** (`serverTarget` در `.ai-game-dev/project.json` که `install-plugin --enroll` می‌نویسد): هدف میزبانی‌شده به هاب `/mcp` خود هدایت می‌شود و هدف `localhost` همان‌طور که هست استفاده می‌شود. این کار **یک پروژه‌ی ابری ثبت‌شده را بدون هیچ پیکربندی محیطی قابل دسترس می‌کند**: اعتبارنامه‌ی ابری ذخیره‌شده (مخزن هر پروژه `<project>/.ai-game-dev/`، سپس مخزن قدیمی پروژه، سپس `~/.ai-game-dev/credentials.json`) خودکار استفاده و پس از انقضا تازه می‌شود.
6. جایگزین محلی: `http://localhost:<derived-port>`، پورت قطعیِ v2 مبتنی بر مسیر پروژه که پلاگین ویرایشگر به طور محلی به آن bind می‌شود (`portOverride` در نشانگر بر پورت مشتق از هش اولویت دارد).

مسیرهای ابری بخش هاب `/mcp` را دارند تا درخواست به هاب برسد و نه به backend ابری (که هیچ `/api/tools/*` ارائه نمی‌کند). برای هدف قرار دادن صریح یک سرور محلی/خودمیزبان `--url http://localhost:<port>` را بدهید.

‏`run-tool`، `run-system-tool` و `exec` مرحله‌ی 6 را با **حالت اتصال پروژه** جایگزین می‌کنند که به همان روش پلاگین خوانده می‌شود (`GODOT_MCP_CONNECTION_MODE`، `.env` پروژه، `user://godot-mcp-config.json`ِ ذخیره‌شده، وگرنه Cloud، پیش‌فرض پلاگین). حالت Custom از سرور محلی بالا استفاده می‌کند. حالت Cloud از هاب ابری با ورودِ دستگاه استفاده می‌کند (هیچ ویرایشگری اجرا نمی‌شود: فقط ویرایشگری که به ai-game.dev وصل است پاسخ می‌دهد). اگر این دستگاه **هرگز** وارد ai-game.dev نشده باشد، به جای آن با یک هشدار از سرور محلی پروژه استفاده می‌شود (ویرایشگری که اکنون اجرا شود آن را سرویس می‌دهد؛ چیزی ذخیره نمی‌شود). ورودی که وجود دارد اما قابل استفاده نیست (منقضی، باطل‌شده، ناخوانا) یک خطاست (کد خروج 6، `NOT_AUTHENTICATED`): دستگاهی که وارد شده هرگز به سرور محلی سوئیچ نمی‌شود. همین برای `open`، `tools` و `open --headless` هم صادق است: در یک پروژه‌ی Cloud روی دستگاهی که وارد شده، `open --headless` هیچ ویرایشگری اجرا نمی‌کند و با کد 6 خارج می‌شود (از `open`، `run-tool` و `exec` استفاده کنید که از طریق ai-game.dev کار می‌کنند). یک `--url` / `--token`ِ صریح همان‌طور که هست استفاده می‌شود؛ `--url` / `GODOT_MCP_HOST`ِ غیرمحلی را `exec` مستقیماً فراخوانی می‌کند و `open --headless` رد می‌کند (ویرایشگر headless محلی است).


<a id="exec"></a>
## `exec`

اجرای C# در ویرایشگر Godotِ پروژه (ابزار `script-execute`ِ پلاگین، مبتنی بر [Code-Exec](https://github.com/IvanMurzak/Code-Exec)):

</div>

```bash
godot-cli exec '1 + 2'                                   # → System.Int32: 3
godot-cli exec -f snippet.cs --using MyGame.Gameplay      # code from a file ("-" or a pipe: stdin)
godot-cli exec 'Helper.Twice(21)' --include helpers       # .cs files/folders under <project>/.ai-game-dev/snippets
```

<div dir="rtl">

کد شامل دستورهای ساده است؛ آخرین عبارت نتیجه است. خروجی، مقدار (`Type: value`) است و، زیر `logs:`، آنچه کد چاپ کرده (`Console`؛ `GD.Print` / push_warning / push_error با Godot.NET.Sdk 4.5+‎). خطای کامپایل به خط قطعه‌کد اشاره می‌کند (`snippet.cs(2,12)`) و با کد 6 خارج می‌شود.

- ‏**ویرایشگرِ در حال اجرای پروژه** (مال شما، با پنجره، یا ویرایشگری که `godot-cli` اجرا کرده) دوباره استفاده می‌شود و هرگز بسته نمی‌شود. ویرایشگر شما باید سرور **محلی** پروژه را سرویس دهد (داک: حالت Custom، سرور اجرا شده).
- ‏**اگر هیچ‌کدام در حال اجرا نباشد:** اسمبلی C# بیلد می‌شود، سپس یک ویرایشگر **headless** اجرا می‌شود (`--headless --editor --path`، یا با `--window` یک پنجره) که به سرور محلی پروژه (`http://localhost:<derived port>`، یا `--url`) وصل است و پلاگینش خودش آن سرور را اجرا می‌کند. ویرایشگر headless پس از `--idle <seconds>` (پیش‌فرض 300، `0` = هرگز) بدون فراخوانی ابزار بسته می‌شود و صحنه‌های بازِ دارای فایل را ذخیره می‌کند (ویرایشگر پنجره‌دار باز می‌ماند). فراخوانی‌های موازی یک بار بیلد می‌گیرند و یک ویرایشگر اجرا می‌کنند؛ برای ویرایشگر کاربر که سرور محلی‌اش پاسخ نمی‌دهد تا 60 ثانیه صبر می‌شود، سپس گزارش داده می‌شود.
- ‏`open --headless [--idle-timeout <s>]` همان نشست را بدون اجرای کد شروع می‌کند؛ `close --owned-only` آن را ذخیره و بسته می‌کند (از طریق ابزار `session-close`ِ پلاگین؛ هرگز ویرایشگر کاربر را نمی‌بندد).

نشست‌ها از `EditorSession`ِ [`@baizor/gamedev-cli-core`](https://www.npmjs.com/package/@baizor/gamedev-cli-core) استفاده می‌کنند: lease در `<project>/.ai-game-dev/session.json` ویرایشگر را به عنوان اجراشده توسط CLI علامت می‌زند (فقط چنین ویرایشگری بسته می‌شود) و سروری را که روی آن کار می‌کند ثبت می‌کند: تا وقتی آن ویرایشگر به‌طور اثبات‌پذیر زنده است، فراخوانی‌های بعدی `exec` / `open` / `close` از همان سرور استفاده می‌کنند، هر چه `--url` بگوید. `<project>/.ai-game-dev/session.lock` باعث می‌شود فراخوانی‌های هم‌زمان فقط یک ویرایشگر اجرا کنند: بیلد C# زیر آن قفل و فقط در فراخوانی‌ای که ویرایشگر را اجرا می‌کند انجام می‌شود. هر دو فایل در `<project>/.ai-game-dev/.gitignore` فهرست شده‌اند. ویرایشگر `AIGD_SESSION_IDLE_SECONDS`، `AIGD_SESSION_OWNER=cli` و `AIGD_SESSION_NONCE` را دریافت می‌کند، به علاوه‌ی `GODOT_MCP_CONNECTION_MODE=Custom`، `GODOT_MCP_HOST` و `GODOT_MCP_START_SERVER=true`. گزینه‌ها: `--ready-timeout <ms>`، `--timeout <ms>` (فراخوانی ابزار، پیش‌فرض 310000: کمی بیشتر از `tool-timeout`ِ ۵ دقیقه‌ای سرور)، `--no-build`، `--editor-path`، `--token`، `--raw`.

‏`run-tool` / `run-system-tool` وقتی فراخوانی روی یک URL **محلی** از یک پروژه‌ی Godot رد شود از همین سیاست پیروی می‌کنند: یک ویرایشگر headless اجرا می‌کنند (ابتدا C# را بیلد می‌کنند، مگر با `--no-build`)، سپس دوباره تلاش می‌کنند. ویرایشگرِ در حال اجرا دوباره استفاده می‌شود و هرگز بسته نمی‌شود؛ `--once` ویرایشگر را پس از آن فقط وقتی می‌بندد که همین فراخوانی آن را اجرا کرده باشد. `--no-start` این رفتار را خاموش می‌کند و URL ابری یا راه دور هرگز چیزی اجرا نمی‌کند. گزینه‌ها: `--editor-path`، `--ready-timeout <ms>`.

<a id="exit-codes-and-output-formats"></a>
## کدهای خروج و قالب‌های خروجی

کدهای خروج با `unity-mcp-cli` و `unreal-mcp-cli` مشترک‌اند (`godot-cli commands --format json` آن‌ها را فهرست می‌کند):

| کد | معنا |
| --- | --- |
| 0 | موفقیت |
| 1 | خطای عمومی یا دسته‌بندی‌نشده؛ همچنین اجرایی که با Ctrl+C / SIGTERM قطع شده (درخت پروسه‌ی Godot kill می‌شود) |
| 2 | خطای استفاده: دستور یا گزینه‌ی ناشناخته، مقدار فلگ یا آرگومان نادرست، ابزار ناشناخته (`NOT_FOUND`) (‏`--help` / `--version` با 0 خارج می‌شوند) |
| 6 | دستور اجرا شد و شکست خورد: ابزار خطا برگرداند، خطاهای اسکریپت یا کامپایل، بدون نتیجه‌ی تست، اعتبارنامه‌ی ردشده |
| 7 | ویرایشگر یا سرور MCP در دسترس نیست (همچنین HTTP 502) |
| 8 | تست‌ها اجرا شدند و حداقل یکی شکست خورد (شکست setup/teardown در سطح suite هم حساب می‌شود) |
| 14 | پایان مهلت (همچنین HTTP 408 / 504) |
| 17 | سرور مشغول است (HTTP 503 / 429)؛ فراخوانی اجرا نشد |
| 18 | ویرایشگر با یک دیالوگ modal مسدود شده است (رزرو شده: در Godotِ headless قابل تشخیص نیست) |

‏`update --check` همچنان وقتی چیزی قدیمی باشد با کد 2 خارج می‌شود.

‏**فراخوانی ابزاری که موفق نشده** (`run-tool`، `run-system-tool`، `exec`، `rql`، `tools`) در هر سه CLI به همان کد خروج و کد خطا نگاشت می‌شود (جدول در `@baizor/gamedev-cli-core` است؛ `commands --format json` آن را به عنوان `errorCodes` فهرست می‌کند). وقتی سرور MCP پاسخ داده باشد، وضعیت HTTP آن تعیین‌کننده است و خطای envelope آن را در خود دارد: `errors[{code, message, httpStatus}]`.

| وضعیت | وضعیت HTTP | خروج | `errors[].code` |
|---|---|---|---|
| آرگومان‌های نادرست: یک فلگ نادرست، آرگومان‌های ابزار که CLI یا سرور رد کرده (هر 4xx که اینجا فهرست نشده) | 400 (یک 422 `[invalid_params]`) | `2` | `USAGE` |
| ابزاری با آن نام وجود ندارد (با `tools` فهرستشان کنید)، یا پروژه‌ای در آن مسیر نیست | 404 | `2` | `NOT_FOUND` |
| اعتبارنامه‌ی قابل استفاده‌ای نیست، یا سرور آن را رد کرد: با `login` وارد شوید | 401, 403 | `6` | `NOT_AUTHENTICATED` |
| ابزار اجرا شد و خطا گزارش کرد (`status: "error"`، هر 5xx که اینجا فهرست نشده) | 409, 422, 500 | `6` | `TOOL_FAILED` |
| کد پروژه بیلد نشد، پس هیچ ویرایشگری اجرا نشد | — | `6` | `COMPILATION_FAILED` |
| اتصال رد یا reset شد، هیچ پلاگین موتوری به سرور وصل نیست، پلاگین پس از ارسال فراخوانی از دست رفت | 502 | `7` | `UNREACHABLE` |
| پاسخی به‌موقع نرسید: `--timeout`، یا timeout ابزارِ خود سرور | 408, 504 | `14` | `TIMEOUT` |
| مشغول بود و فراخوانی را اجرا نکرد (در حال بارگذاری مجدد، محدودیت نرخ، فراخوانی CLI دیگری ویرایشگر را در اختیار دارد)؛ بعداً دوباره تلاش کنید | 503, 429 | `17` | `BUSY` |
| هر چیز دیگر (فراخوانی لغو شد، یک خطای غیرمنتظره) | — | `1` | `ERROR` |

‏`--format text|json|ndjson` (پیش‌فرض `text`) را `run-tool`، `run-system-tool`، `exec`، `status`، `wait-for-ready`، `check`، `import`، `test`، `export`، `run-script` و `commands` می‌پذیرند. `json` دقیقاً یک envelope روی stdout چاپ می‌کند:

</div>

```json
{ "success": false, "command": "check", "data": { "...": "..." },
  "errors": [{ "code": "SCRIPT_ERRORS", "message": "res://player.gd:12: error: Expected expression." }],
  "warnings": [] }
```

<div dir="rtl">

‏`ndjson` رویدادها را روی stderr چاپ می‌کند، هر خط یک شیء JSON (`{"type":"progress","command":…,"message":…}`، و هر خط دیگر به صورت `{"type":"log","command":…,"level":"info|error","message":…}`)، و envelope را به صورت یک خط روی stdout. در هر دو، لاگ‌ها، spinnerها و عنوان‌ها بدون رنگ به stderr می‌روند. کدهای خطا: `USAGE`، `TOOL_FAILED`، `UNREACHABLE`، `TIMEOUT`، `BUSY`، `AWAITING_INPUT`، `TESTS_FAILED`، `TEST_RUN_ERROR`، `COMPILATION_FAILED`، `SCRIPT_ERRORS`، `IMPORT_FAILED`، `PROJECT_OPEN`، `NOT_FOUND`، `EXPORT_FAILED`، `NOT_AUTHENTICATED`، `CLOUD_PROJECT`، `ERROR`. `--raw` مثل قبل کار می‌کند.

<a id="headless-commands-check-import-test-export-run-script"></a>
## دستورهای headless (`check`، `import`، `test`، `export`، `run-script`)

این دستورها Godot را بدون نشست ویرایشگر اجرا می‌کنند و به سرور MCP نیازی ندارند. کد خروج Godot در حالت headless نتیجه‌ی معتبری نیست (یک `--import` با خطای پارس GDScript، یا پلاگینی که بارگذاری نمی‌شود، همچنان با 0 خارج می‌شود)، پس هر دستور اجرا را از روی لاگ آن (`SCRIPT ERROR`، `ERROR:`، `Parse Error`)، نشانگری که ثابت می‌کند مرحله اجرا شده، و فایل‌هایی که باید تولید کند قضاوت می‌کند. هر پروسه یک `--timeout` سخت دارد (کل درخت پروسه‌ی Godot kill می‌شود: کد خروج 14). لاگ‌ها و اسکریپت‌های موقت به `<project>/.godot/godot-cli/` می‌روند. `check`، `import`، `test` و `export` پروژه‌ای را که در یک ویرایشگر باز است رد می‌کنند (کد خروج 6، `PROJECT_OPEN`: یک import بر سر `.godot/` با آن ویرایشگر درگیر می‌شود)؛ `--allow-open` این را نادیده می‌گیرد و `close` ویرایشگرِ `godot-cli` را متوقف می‌کند. فایل اجرایی از `--editor-path`، `GODOT_BIN`، `PATH` یا پوشه‌های نصب معمول می‌آید.

</div>

```bash
godot-cli check                     # 0 clean, 6 SCRIPT_ERRORS / COMPILATION_FAILED, 14 timeout
godot-cli check --strict            # GDScript and C# compiler warnings fail too
godot-cli test --format github      # ::error annotations; 0 pass, 8 failures, 6 no verdict / no runner
godot-cli export "Windows Desktop" build/game.exe
godot-cli run-script tools/migrate.gd -- --dry-run
```

<div dir="rtl">

- ‏ **`check [path] [--strict] [--include-addons]`** چهار پروسه‌ی جداگانه اجرا می‌کند (یک `--build-solutions --quit`ِ سرد و تکی روی پلاگین‌های `[Tool]`ِ C# کرش می‌کند): `--headless --import`؛ `dotnet build` برای پروژه‌ی C# (خطاها به صورت `file(line,col): error CS…`)؛ `--headless --editor --quit` که وقتی پلاگین فعال است باید `[Godot-MCP] plugin loaded` را چاپ کند؛ و یک مرحله‌ی GDScript که هر `.gd` زیر `res://` را بارگذاری می‌کند (`res://addons/` فقط با `--include-addons`) و خطاهای پارس را به صورت `res://file:line` گزارش می‌دهد. `--strict` روی هشدارهای GDScript (هشدارهای سطح Warn پروژه که در حافظه به خطا ارتقا می‌یابند و هرگز ذخیره نمی‌شوند) و هشدارهای `CS`ِ C# هم شکست می‌خورد. پیام‌های موتور (مثلاً هشدارهای زمان اجرای یک پلاگین) به عنوان هشدار فهرست می‌شوند و هرگز باعث شکست check نمی‌شوند.
- ‏ **`import [path]`** روی خطاهای اسکریپت (`SCRIPT_ERRORS`) و ریسورس‌هایی که import نمی‌شوند (`IMPORT_FAILED`) شکست می‌خورد.
- ‏ **`test [path]`** ‏GUT (`addons/gut`) یا gdUnit4 (`addons/gdUnit4`) را پیدا می‌کند، پروژه را import می‌کند (`--no-import` آن را رد می‌کند)، و اسکریپت خط فرمان runner را با یک گزارش JUnit اجرا می‌کند (`-gjunit_xml_file`، یا در gdUnit4 ‏`-rd`؛ gdUnit4 با `--ignoreHeadlessMode -c` اجرا می‌شود). کد خروج 0 وقتی همه‌ی تست‌ها قبول شوند، 8 در صورت شکست، 6 (`TEST_RUN_ERROR`) وقتی گزارشی نباشد، هیچ تستی اجرا نشود یا یک اسکریپت تست بارگذاری نشود (تست‌هایش در گزارش نیستند، پس نباید قبول شود)، 6 (`NOT_FOUND`) همراه با راهنمای نصب وقتی هیچ runnerی نصب نیست. `--runner gut|gdunit4`، `--dir <dir>` (تکرارپذیر؛ پیش‌فرض `res://test`، `res://tests`، یا `.gutconfig.json`ِ GUT)، `--filter <text>` (اسکریپت‌های تستی که نام فایلشان شامل آن است)، `--output <file>` (کپی JUnit XML)، `--format text|json|ndjson|github`.
- ‏ **`export <preset> <output> [--debug|--pack]`** بررسی می‌کند که `export_presets.cfg` آن preset را داشته باشد و قالب‌های اکسپورت نسخه‌ی ویرایشگر نصب باشند (مگر با `--pack` یا وقتی preset قالب خودش را نام می‌برد)، `--export-release|--export-debug|--export-pack` را اجرا می‌کند، و شکست می‌خورد (`EXPORT_FAILED`) مگر اینکه فایل خروجی وجود داشته باشد، خالی نباشد و توسط همین اجرا نوشته شده باشد، و لاگ هیچ خطای اکسپورتی نداشته باشد (Godot فایل اجرایی را می‌نویسد و با 0 خارج می‌شود حتی وقتی مثلاً publishِ C# به خاطر نبودن `.sln` شکست خورده باشد).
- ‏ **`run-script <script> [args...]`** دستور `godot --headless --path <project> -s <script> -- <args>` را اجرا می‌کند: اسکریپت از `SceneTree` یا `MainLoop` ارث می‌برد و با `quit(code)` تمام می‌شود. کد خروج 0 برای `quit(0)`؛ 6 برای کد دیگر (`data.scriptExitCode`) یا اسکریپتی که بارگذاری نشود؛ 14 با `--timeout` (پیش‌فرض 300000 ms). import انجام نمی‌دهد، پس وقتی یک ویرایشگر پروژه را باز کرده هم اجرا می‌شود.

<a id="login"></a>
## `login`

‏`godot-cli login` اتصال **ابری** پلاگین ویرایشگر به [ai-game.dev](https://ai-game.dev) را با **OAuth 2.1 device-authorization flow** (RFC 8628) احراز هویت می‌کند: یک کد کاربری کوتاه + URL تأیید چاپ می‌کند، مرورگرتان را باز می‌کند و تا وقتی تأیید کنید پرس‌وجو می‌کند:

</div>

```bash
godot-cli login                       # sign in once per machine (default)
godot-cli login --project ./MyGame    # keep a per-project credential instead
godot-cli login --base-url <url>      # authenticate against a non-default server
godot-cli login --force               # re-authenticate over an existing credential
godot-cli login --tools-only         # CI/automation: tools credential only (no App pickup)
godot-cli login --yes                # non-interactive: confirm replacing another account's credential
```

<div dir="rtl">

- به طور پیش‌فرض اعتبارنامه در **مخزن دستگاه** مشترک `~/.ai-game-dev/credentials.json` ذخیره می‌شود (`0600` در POSIX / DPAPI در Windows)؛ **یک بار برای هر دستگاه** وارد شوید و پلاگین ویرایشگر Godot خودکار آن را برمی‌دارد، پس `godot-cli open --mode Cloud` **بدون `--token`** وصل می‌شود.
- ورود پیش‌فرض کل دستگاه را وارد حساب می‌کند: flow یک مجوز **agent-scope** درخواست می‌کند و **اعتبارنامه‌ی محدودترِ ابزارها (پلاگین)** را از طریق token exchangeِ RFC 8693 از آن مشتق می‌کند، تا هر ابزار AI-Game-Dev روی دستگاه (پلاگین‌های موتور، CLIها، App دسکتاپ) ورود را بردارد.
- ‏`--tools-only` **فقط** اعتبارنامه‌ی ابزارها را صادر می‌کند: چیزی با agent-scope ذخیره نمی‌شود، App دسکتاپ نمی‌تواند ورود را بردارد، و runner به عنوان یک دستگاه مستقل و قابل ابطال در [ai-game.dev](https://ai-game.dev) نمایش داده می‌شود. از آن برای CI و runnerهای اتوماسیون اشتراکی استفاده کنید.
- اگر دستگاه از قبل اعتبارنامه‌ی **حساب دیگری** را داشته باشد، login پیش از جایگزینی آن سؤال می‌کند (رد کردن، اعتبارنامه‌ی تازه‌صادرشده را باطل می‌کند و مخزن را دست‌نخورده می‌گذارد). برای تأیید غیرتعاملی `--yes` را بدهید.
- ‏`--project <path>` (یا `[path]`ِ موقعیتی) یک اعتبارنامه‌ی جداگانه برای پروژه (`<path>/.ai-game-dev/credentials.json`، gitignoreشده) برای حساب‌های جداگانه‌ی هر پروژه نگه می‌دارد. مخزن قدیمی پروژه `<path>/.godot-mcp/credentials.json` **دیگر نوشته نمی‌شود**؛ برای یک ریلیز همچنان خوانده می‌شود (و در اولین استفاده به مخزن دستگاه منتقل می‌شود).
- ‏flow مجموعه‌ی **کامل** اعتبارنامه (access token + refresh tokenِ چرخشی + زمان انقضا) را ذخیره می‌کند؛ **هرگز هیچ personal access token (PAT)ای صادر نمی‌شود**. در صورت هر شکستی چیزی نوشته نمی‌شود، پس اعتبارنامه‌ی موجود پس از خطای رد / انقضا / شبکه دست‌نخورده می‌ماند. دستورها access tokenِ منقضی را خودکار تازه می‌کنند (از طریق `@baizor/gamedev-cli-core`)، پس دستگاهی که وارد شده بدون اجرای دوباره‌ی `login` وارد می‌ماند.

<a id="setup-skills"></a>
## `setup-skills`

‏`godot-cli setup-skills <agent> [path]` برای یک پروژه‌ی Godot **فایل‌های skill** عامل هوش مصنوعی را زیر مسیر skillهای عامل انتخاب‌شده تولید می‌کند (مثلاً `.claude/skills` در Claude Code). از `--list` برای دیدن همه‌ی عامل‌ها و پشتیبانی skill آن‌ها استفاده کنید.

</div>

```bash
godot-cli setup-skills claude-code            # generate into ./.claude/skills
godot-cli setup-skills cursor ./MyGame        # generate into MyGame/.cursor/skills
godot-cli setup-skills --list                 # list agents + their skills paths
```

<div dir="rtl">

به صورت آفلاین (بدون ویرایشگر، بدون سرور، بدون شبکه) این‌ها را می‌نویسد:

- ‏`godot-cli/SKILL.md`: اینکه یک عامل چگونه از این CLI از صفر استفاده می‌کند: اولین دستور ویرایشگر یک ویرایشگر headless اجرا می‌کند، `rql` با دستورالعمل‌هایی برای صحنه‌ها، نودها و ریسورس‌ها، `check` / `test`، `--format json`، و معنای هر کد خروج. عمداً کوتاه است (عامل هر بایتی را که می‌خواند در context نگه می‌دارد)؛ `references/rql.md` و `references/install.md` جزئیات را دارند و فقط در صورت نیاز خوانده می‌شوند. دستورالعمل‌ها از همان منبعی می‌آیند که `godot-cli --help` و `rql --help`، پس skill و راهنما نمی‌توانند از هم فاصله بگیرند.
- ‏`ai-game-dev-app/SKILL.md`: اپلیکیشن همراه، برای ابزارهای خلاقانه‌ای که CLI ندارد.

این کار **idempotent** است: اجرای دوباره همان بایت‌ها را بازنویسی می‌کند. skillهای `godot-mcp` / `godot-mcp-<family>` را که نسخه‌های قبلی از یک کاتالوگ دست‌نویس تولید می‌کردند حذف می‌کند. skillهای هر ابزار کار پلاگین است: پلاگین هر بار که ویرایشگر اجرا شود آن‌ها را از ابزارهایی که واقعاً ثبت می‌کند تولید می‌کند (کلید آن در پنل Skills پلاگین است)، همراه با راهنمای `godot-cli-install` خود.

<a id="install-plugin"></a>
## `install-plugin`

‏`godot-cli install-plugin [path]` یک **نصب‌کننده‌ی واقعی** است: با یک دستور idempotent، یک نصب از صفر در ترمینال را به یک پروژه‌ی کارا تبدیل می‌کند:

</div>

```bash
godot-cli install-plugin ./MyGodotProject                      # download the matching release + install
godot-cli install-plugin --version 0.11.1 ./MyGodotProject     # pin a specific addon release
godot-cli install-plugin --source ./Godot-MCP/addons/godot_mcp ./MyGodotProject   # offline / dev copy
```

<div dir="rtl">

چهار مرحله انجام می‌دهد:

1. ‏**ساختن `res://addons/godot_mcp/`.** به طور پیش‌فرض `godot-mcp-addon-<version>.zip` را از طریق HTTPS **فقط از github.com** دانلود می‌کند (ریلیز `v<version>` در `IvanMurzak/Godot-MCP`؛ نسخه به طور پیش‌فرض نسخه‌ی خود CLI است). میزبان‌های غیر `github.com` و `http` ساده رد می‌شوند. با `--source <path>` به جای آن پلاگین را از یک پوشه‌ی محلی کپی می‌کند (بدون شبکه)؛ `<path>` می‌تواند پوشه‌ای باشد که *خودش* `addons/godot_mcp` است یا پوشه‌ای که آن را *در بر دارد*.
2. ‏**افزودن پکیج‌های NuGet** که پلاگین نیاز دارد (`com.IvanMurzak.ReflectorNet`، `com.IvanMurzak.McpPlugin`) به `.csproj` پروژه، به صورت idempotent: اگر نباشند اضافه می‌کند، نسخه‌ی قدیمی را اصلاح می‌کند و سنجاق درست را دست‌نخورده می‌گذارد. نسخه‌ها از پلاگینی که نصب می‌شود می‌آیند: `nuget-deps.json`ِ آن (که workflow ریلیز از `Godot-MCP.csproj` در هر zip ریلیز تولید می‌کند)، یا، وقتی `--source` به یک checkout از Godot-MCP اشاره کند، `Godot-MCP.csproj`ِ همان checkout. zipهای قدیمی‌تر پلاگین که این مانیفست را ندارند، با یک هشدار به نسخه‌های داخلی CLI برمی‌گردند.
3. ‏**embed کردن کاتالوگ افزونه‌ها** (`<EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />`) در `.csproj` پروژه، به صورت idempotent. بدون آن، رجیستری افزونه‌های پلاگین در زمان اجرای ویرایشگر هیچ کاتالوگی نمی‌خواند و **پنل Extensions خالی است**. تک‌منبع است و همراه با سنجاق‌ها در برابر csproj پلاگین تست هم‌سانی می‌شود. همچنین `<EnableDynamicLoading>true</EnableDynamicLoading>` را تنظیم می‌کند (قالب Godot آن را دارد؛ بدون آن بازی‌ای که از ویرایشگر اجرا شود نمی‌تواند اسکریپت‌های C# را بسازد).
4. ‏**فعال کردن پلاگین** در `[editor_plugins]`ِ `project.godot`.

برای استفاده به عنوان کتابخانه امن است (یک union از نوع `{ kind: 'success' | 'failure' }` برمی‌گرداند؛ هرگز از مرز عمومی exception پرتاب نمی‌کند) و idempotent است: اجرای دوباره روی پروژه‌ای که قبلاً نصب شده هیچ تغییری گزارش نمی‌کند.

<a id="install-extension"></a>
## `install-extension`

‏`godot-cli install-extension <id> [path]` یک **افزونه**‌ی اختیاری Godot-MCP (یک پکیج اختیاری NuGet که RQL را گسترش می‌دهد؛ هیچ ابزار جداگانه‌ای اضافه نمی‌کند) را در یک پروژه‌ی C#ِ Godot نصب می‌کند؛ این کانال ترمینال/کتابخانه برای همان نصبی است که داک **Extensions** درون ویرایشگر انجام می‌دهد:

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject   # add/update the PackageReference
godot-cli install-extension "CSG Tools"                                      # resolve by name; default cwd
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG --version 1.1.1     # override the catalog pin
```

<div dir="rtl">

- ‏`<id>` را در برابر **کاتالوگ مشترک افزونه‌ها** (`addons/godot_mcp/extensions.catalog.json`، تنها منبع حقیقت که داک، CLI و اپلیکیشن همگی از آن استفاده می‌کنند) resolve می‌کند و بر اساس شناسه‌ی پکیج (سپس نام)، بدون حساسیت به حروف بزرگ و کوچک، تطبیق می‌دهد.
- یک `<PackageReference Include="<packageId>" Version="<version>" />` را با خواندن-تغییر-نوشتن در `.csproj`ِ ریشه‌ی پروژه قرار می‌دهد: اگر نباشد **اضافه** می‌شود، نسخه فقط وقتی کاتالوگ (یا `--version`) نسخه‌ی جدیدتری سنجاق کند **ارتقا** می‌یابد، و اگر به‌روز باشد **کاری انجام نمی‌شود**؛ سپس از شما می‌خواهد دوباره بیلد کنید (`godot-cli build`) تا Godot پکیج جدید را restore و کامپایل کند.
- رفتارش دقیقاً مثل `ExtensionInstaller`ِ داک است (همان قواعد افزودن / به‌روزرسانی / بدون تغییر + مقایسه‌ی عددی نسخه؛ با یک مجموعه سناریوی مشترک در هر دو طرف تأیید شده).

<a id="library-api"></a>
## API کتابخانه

این پکیج یک کتابخانه‌ی بدون اثر جانبی هم export می‌کند (ورودی `.`):

</div>

```ts
import { openProject, runTool, setupMcp, installPlugin, installExtension, execCode, ensureEditor } from 'godot-cli';

// The shared extension catalog + lookup helpers are exported too, so a GUI (the app)
// can render the same list the dock + CLI install from:
import { EXTENSIONS_CATALOG, findExtension } from 'godot-cli';

// The headless commands, their log/JUnit parsers and the exit codes:
import { checkProject, importProject, runTests, exportProject, runScript, parseGodotLog, parseJUnit, ExitCode } from 'godot-cli';
```

<div dir="rtl">

هر تابع یک discriminated union برمی‌گرداند (`{ kind: 'success', ... }` / `{ kind: 'failure', error }`) و هرگز از مرز عمومی exception پرتاب نمی‌کند.

<a id="development"></a>
## توسعه

</div>

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```

<div dir="rtl">

<a id="supported-ai-agents"></a>
## عامل‌های هوش مصنوعی پشتیبانی‌شده

‏`godot-cli` پیکربندی‌های آماده‌ی کلاینت MCP را برای همه‌ی عامل‌های اصلی کدنویسی هوش مصنوعی می‌نویسد؛ برای اتصال یکی از آن‌ها `godot-cli setup-mcp <agent> [path]` را اجرا کنید (و برای تولید فایل‌های skill آن `godot-cli setup-skills <agent> [path]` را). از `--list` روی هر کدام از این دو دستور برای دیدن همه‌ی عامل‌های پشتیبانی‌شده استفاده کنید.

عامل‌های پشتیبانی‌شده شامل Claude Code، Claude Desktop، Cursor، VS Code و Visual Studio (Copilot)، GitHub Copilot CLI، Rider (Junie)، Codex، Gemini، Antigravity، Cline، Open Code، Kilo Code و یک کلاینت MCP عمومی `custom` هستند.

</div>

```bash
godot-cli setup-mcp --list                    # list every supported agent id
godot-cli setup-mcp claude-code ./MyGame      # write the agent's MCP client config (pinned to this project)
godot-cli setup-mcp claude-code ./MyGame --no-pin   # write the bare, unpinned <host>/mcp URL instead
godot-cli setup-mcp claude-code ./MyGame --oauth    # URL-only: the agent signs in with its own OAuth
godot-cli setup-mcp claude-code ./MyGame --regenerate-key   # new project key; the old one is revoked
```

<div dir="rtl">

در حالت Cloud، `setup-mcp` برای هر عامل یک **کلید پروژه** در پیکربندی عامل می‌نویسد: `Authorization: Bearer agd_pk_…` (در Codex: ‏`http_headers`). کلید پروژه هرگز منقضی نمی‌شود، به سنجاق همین پروژه گره خورده است (نمی‌تواند به هیچ پروژه‌ی دیگری در حساب شما برسد)، و یک بار برای هر پروژه با ورودِ دستگاه شما (`godot-cli login`) ساخته می‌شود و سپس از `~/.ai-game-dev/project-keys.json` دوباره استفاده می‌شود. `--regenerate-key` آن را جایگزین و کلید قبلی را باطل می‌کند. بدون ورودِ دستگاه (یا وقتی سرور نمی‌تواند کلید صادر کند) پیکربندی فقط شامل URL است و عامل با OAuth خودش احراز هویت می‌کند؛ `--oauth` صراحتاً همین پیکربندی فقط-URL را درخواست می‌کند. یک `--token`ِ صریح همیشه اولویت دارد. پیکربندی سرور محلی (`--url http://localhost:…`) هرگز کلید پروژه ندارد.

> برای مستندات کامل پروژه‌ی Godot-MCP، [README اصلی](../../docs/README.fa.md) را ببینید. با پشتیبانی **[ai-game.dev](https://ai-game.dev)**.

</div>
