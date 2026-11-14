[English](tools.md) · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · [Español](tools.es.md) · **فارسی** · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

<div dir="rtl">

# مرجع ابزارها

ابزارهای داخلی Godot-MCP تقریباً هر کاری را که در ویرایشگر Godot انجام می‌دهید پوشش می‌دهند و در خانواده‌های زیر دسته‌بندی شده‌اند. این ابزارها فشرده‌اند، بنابراین عامل به فراخوانی‌ها و توکن‌های کمتری نیاز دارد ([اندازه‌گیری‌شده در ارزیابی عامل RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md)). لایه‌ی داده‌ی ویرایشگر [RQL](rql.md) (Reflection Query Language) است: `rql-query`، `rql-mutate` و `rql-schema` نودهای صحنه، ویژگی‌های Godot، ریسورس‌ها، فایل‌ها، صحنه‌ها و وضعیت ویرایشگر را با یک کوئری می‌خوانند و تغییر می‌دهند، به جای یک ابزار برای هر عملیات.

با `toolFormat` پیش‌فرض (`Rql`)، بقیه‌ی ابزارها مقدارهای RQL را به عنوان آرگومان می‌گیرند و متن فشرده برمی‌گردانند؛ `Json` اسکیماها و نتیجه‌های JSONِ ReflectorNet را نگه می‌دارد. اسکرین‌شات‌ها یک تصویر PNG برمی‌گردانند. همه‌ی ابزارهای ویرایشگر به محض فعال شدن پلاگین در دسترس‌اند.

خانواده‌های **console**، **reflection** و **runtime-errors** به یک *بازیِ در حال اجرا* خدمت می‌کنند ([استفاده در Runtime](runtime.fa.md) را ببینید). در ویرایشگر، به جای آن‌ها از منبع `console` در RQL و `call` / `CallMethod` در RQL استفاده کنید.

| خانواده | ابزارها | چه می‌کند |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | کوئری و تغییر ویرایشگرِ زنده. همه یا هیچ و قابل بازگردانی؛ چند دستور در یک فراخوانی، جدا شده با `;`. [rql.md](rql.md) را ببینید. |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | فایل‌های C# (`.cs`) و GDScript (`.gd`)، اتصال اسکریپت به نود، اعتبارسنجی GDScript، بیلد C# پروژه و اجرای C# در ویرایشگر. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | گرفتن تصویر از viewport ویرایشگر، یک دوربین، یا یک نود که به‌تنهایی رندر شده، به صورت PNG که LLM می‌تواند ببیند. |
| **console** | `console-get-logs`, `console-clear-logs` | در بازیِ در حال اجرا: خواندن و پاک کردن لاگ‌های جمع‌آوری‌شده. |
| **reflection** | `reflection-method-find`, `reflection-method-call` | در بازیِ در حال اجرا: پیدا کردن و فراخوانی متدهای C# در هر اسمبلی بارگذاری‌شده از طریق ReflectorNet. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | در بازیِ در حال اجرا: دریافت خطاهای زمان اجرای GDScript، `push_error` / `push_warning`، خطاهای شیدر و استثناهای C#. **پیش‌فرض خاموش.** |
| **ping** | `ping` | بررسی آمادگی. ابزار سیستمی. |
| **session** | `session-close` | ذخیره و بستن ویرایشگری که `godot-cli` اجرا کرده است. ابزار سیستمی. |
| **skills** | `godot-skill-create`, `godot-skill-generate` | نوشتن یک فایل ابزار C# جدید در پروژه؛ بازتولید فایل‌های `SKILL.md`. ابزارهای سیستمی. |

‏**ابزارهای سیستمی** روی endpoint HTTPِ `/api/system-tools/` در سرور MCP ارائه می‌شوند (برای مثال `godot-cli run-system-tool ping`). در `tools/list` به عامل‌های هوش مصنوعی نمایش داده نمی‌شوند.

<a id="rql"></a>
## rql

- ‏`rql-query`: یک کوئری فقط‌خواندنی که به صورت ردیف‌های فشرده برگردانده می‌شود. `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- ‏`rql-mutate`: کوئری‌ای که با `set` یا `call` تمام می‌شود. همه یا هیچ، قابل بازگردانی (Ctrl+Z)، و `dry_run` پیش‌نمایش آن را نشان می‌دهد. ویرایش صحنه آن را ذخیره‌نشده علامت می‌زند؛ فایل‌های ریسورسِ ویرایش‌شده و `project.godot` ذخیره می‌شوند.
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- ‏`rql-schema`: منبع‌ها، توابع و کلاس‌های نود در صحنه‌ی در حال ویرایش؛ با نام یک تایپ، اعضا، متدها و ویژگی‌های Godot آن. `rql-schema "<extension>"` راهنمای یک افزونه‌ی نصب‌شده را چاپ می‌کند.

منبع‌ها شامل `nodes`، `root`، `selection`، `scenes`، `files`، `folders`، `classes`، `console`، `editor`، `project`، `settings`، `inputActions`، `autoloads`، `groups`، `layers` و `tools` هستند. کل زبان، از جمله TileSetها، Themeها، پارامترهای ShaderMaterial و MeshLibraryها، در [rql.md](rql.md) و skillِ `godot-rql` آمده است.

<a id="script"></a>
## script

- ‏`script-read`، `script-create`، `script-update`، `script-delete`: عملیات CRUD روی فایل‌های `.cs` و `.gd`.
- ‏`script-attach-to-node`: اتصال یک اسکریپت به یک نود.
- ‏`script-validate`: پارس و کامپایل فایل‌های GDScript و برگرداندن عیب‌یابی ساختاریافته.
- ‏`script-build`: بیلد C# پروژه (`dotnet build`، Debug) تا اسکریپت‌های جدید بارگذاری شوند. بیلد در پس‌زمینه اجرا می‌شود؛ وقتی موفق شود Godot اسمبلی C# را دوباره بارگذاری می‌کند، پلاگین دوباره وصل می‌شود و نتیجه (هشدارها یا خطاها) پس از بارگذاری مجدد می‌رسد. [بارگذاری مجدد اسمبلی C#](custom-tools.fa.md#c-assembly-reloads) را ببینید.
- ‏`script-execute`: کامپایل و اجرای C# در ویرایشگر با Roslyn، از طریق [Code-Exec](https://github.com/IvanMurzak/Code-Exec). دستورهای ساده که آخرین عبارت نتیجه است (با `using`های خودش، کلاس‌های محلی، `await`)، یا یک کلاس با یک متد static. تایپ‌های کامپایل‌شده‌ی بازی قابل مشاهده‌اند؛ `include` فایل‌های کمکی `.cs` را از `<project>/.ai-game-dev/snippets` اضافه می‌کند. مقدار و آنچه کد چاپ کرده را برمی‌گرداند (`Console`؛ و `GD.Print`، push_warning و push_error وقتی پروژه با Godot.NET.Sdk 4.5+‎ بیلد شود). از ترمینال: `godot-cli exec '1 + 2'`.

<a id="screenshot"></a>
## screenshot

- ‏`screenshot-viewport`: viewport ویرایشگر.
- ‏`screenshot-camera`: نما از یک دوربین مشخص.
- ‏`screenshot-isolated`: یک نود که به‌تنهایی و از زاویه‌ی انتخابی رندر شده است.

هر سه، تصویر را در گفتگو برمی‌گردانند. برای نوشتن فایل هم `savePath` را بدهید: `res://Screenshots/shot.png`، `Screenshots/shot.png` (نسبت به ریشه‌ی پروژه) یا یک مسیر مطلق درون پروژه. پوشه‌های ناموجود ساخته می‌شوند و اگر پسوندی نباشد `.png` اضافه می‌شود. یک `savePath` رد می‌شود (نه تصویر، نه فایل) وقتی بیرون از پروژه باشد (`..`، `user://`)، از یک symlink یا junction عبور کند، به درون `res://.godot/` یا همین پلاگین اشاره کند، `.png` نباشد، یا نام فایلی موجود باشد: هرگز بازنویسی نمی‌کند. Godot فایل PNG را مثل هر فایل دیگر پروژه import می‌کند؛ پوشه‌ای با فایل `.gdignore` از این کار جلوگیری می‌کند.

<a id="console-reflection-runtime-errors-running-game"></a>
## console، reflection، runtime-errors (بازیِ در حال اجرا)

- ‏`console-get-logs` / `console-clear-logs`: خواندن (با فیلتر) و پاک کردن لاگ‌هایی که پلاگین جمع کرده، از جمله عیب‌یابی اتصال خودش.
- ‏`reflection-method-find` / `reflection-method-call`: پیدا کردن متدهای C# (از جمله private) در همه‌ی اسمبلی‌های بارگذاری‌شده، و فراخوانی یکی از آن‌ها با آرگومان.
- ‏`runtime-errors-get`: خطاهای ثبت‌شده‌ی درون بازی، قدیمی‌ترین اول، جدیدترین‌ها نگه داشته می‌شوند. مقدار `highestSequence` قبلی را به عنوان `sinceSequence` بدهید تا فقط خطاهای جدید را بگیرید. وقتی ثبت خطا هرگز فعال نشده باشد `available:false` برمی‌گرداند.
- ‏`runtime-errors-clear`: پاک کردن بافر؛ شمارنده‌ی ترتیب به شمارش ادامه می‌دهد.

ثبت خطاهای زمان اجرا تا وقتی بازی `builder.WithRuntimeErrorCapture()` را فراخوانی نکند خاموش است. جزئیات: [ثبت خطاهای زمان اجرای درون بازی](runtime.fa.md#capturing-in-game-runtime-errors).

<a id="ping-session-skills-system-tools"></a>
## ping، session، skills (ابزارهای سیستمی)

- ‏`ping`: یک پیام را بازمی‌گرداند، یا `pong` برمی‌گرداند. `curl -X POST <host>/api/system-tools/ping -d '{}'`.
- ‏`session-close`: ذخیره و بستن ویرایشگری که `godot-cli exec` / `open --headless` اجرا کرده است (صحنه‌های بازِ دارای فایل و ریسورس‌هایی که تغییر داده‌اند). به nonceِ همان نشست از `.ai-game-dev/session.json` نیاز دارد، پس هرگز ویرایشگر خود شما را نمی‌بندد. `godot-cli close --owned-only` آن را فراخوانی می‌کند.
- ‏`godot-skill-create`: نوشتن یک فایل ابزار C# جدید در پروژه. پس از بیلد دوباره‌ی پروژه قابل فراخوانی می‌شود.
- ‏`godot-skill-generate`: بازتولید همه‌ی فایل‌های `SKILL.md` از ابزارهایی که ویرایشگر ثبت کرده، در پوشه‌ی skillهای عامل هوش مصنوعیِ انتخاب‌شده.

<a id="visibility"></a>
## نمایش

هر ابزار، پرامپت و ریسورس یک کلید **Visible** در پنجره‌های Tools / Prompts / Resources داک دارد (`godot-cli configure` همین کار را از ترمینال انجام می‌دهد). این کلید فقط تعیین می‌کند که کلاینت‌های MCP آن مورد را در `tools/list` ببینند یا نه، که context آن‌ها را کوچک نگه می‌دارد. این کنترل دسترسی نیست: ابزار پنهان وقتی کلاینت آن را با نام فراخوانی کند همچنان اجرا می‌شود.

<a id="extensions"></a>
## افزونه‌ها

افزونه‌ها منبع‌ها، آداپتورها و یک راهنمای عامل به ابزارهای `rql-*` و skillِ `godot-rql` اضافه می‌کنند. هیچ ابزار MCP جداگانه‌ای اضافه نمی‌کنند. فهرست آن‌ها در [README](README.fa.md#extensions) است.

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

<div dir="rtl">

نصب‌کننده فقط پکیج NuGet افزونه را اضافه می‌کند؛ پلاگین شخص ثالثِ موردنیاز را خودتان در پروژه نصب کنید. بخش **Extensions** در داک دکمه‌ی **Install All** دارد. `godot-cli update` پلاگین را به‌روزرسانی می‌کند و افزونه‌های شناور (`Version="*"`) را دوباره به آخرین نسخه‌شان resolve می‌کند؛ افزونه‌ای که به نسخه‌ای سنجاق کرده‌اید حفظ می‌شود. اگر افزونه‌ای به McpPluginِ جدیدتری از نسخه‌ی سنجاق‌شده در پلاگین نیاز داشته باشد، restore با `NU1605` شکست می‌خورد تا وقتی که پلاگین را به‌روزرسانی کنید یا نسخه‌ی افزونه را سنجاق کنید.

</div>
