[English](connection.md) · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · [Español](connection.es.md) · **فارسی** · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

<div dir="rtl">

# اتصال و سرور

پلاگین ویرایشگر در یکی از دو حالت به یک سرور MCP وصل می‌شود. حالت را در داک ویرایشگر تنظیم کنید، یا هنگام شروع پروسه آن را با متغیرهای محیطی `GODOT_MCP_*` جایگزین کنید (برای CI، اجراهای headless و توسعه‌ی محلی مفید است). محیط همیشه بر پیکربندی ذخیره‌شده اولویت دارد. ‏`godot-cli open` این‌ها را از طریق `--mode`، `--url`، `--cloud-url` و `--token` برایتان ارسال می‌کند.

<a id="cloud-mode-default-ai-gamedev"></a>
## حالت Cloud (پیش‌فرض): ai-game.dev

پلاگین به `https://ai-game.dev` وصل می‌شود؛ شما هیچ سروری اجرا نمی‌کنید. ‏Cloud پیش‌فرض است چون به هیچ پورت بازی نیاز ندارد و از تداخل پورت با برنامه‌های دیگر جلوگیری می‌کند؛ و رایگان است. رله هیچ محتوای درخواست یا پاسخی را ذخیره نمی‌کند و فقط فراداده (metadata) را به مدت ۹۰ روز نگه می‌دارد.

یک بار با `godot-cli login` وارد شوید. این دستور از OAuth 2.1 device flow (RFC 8628) استفاده می‌کند: یک کد کوتاه چاپ می‌کند، مرورگرتان را باز می‌کند و پس از تأیید شما، یک اعتبارنامه در مخزن دستگاه (`~/.ai-game-dev/credentials.json`) ذخیره می‌کند. پلاگین ویرایشگر آن را برمی‌دارد، پس توکنی برای کپی کردن وجود ندارد.

| متغیر | کاربرد | پیش‌فرض |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | ‏`Cloud` یا `Custom` (بدون حساسیت به حروف بزرگ و کوچک). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | ‏URL پایه‌ی Cloud. یک `/mcp` در انتها حذف می‌شود. | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | پوشه‌ی مطلق مخزن اعتبارنامه، برای تست ایزوله. از `.env` پروژه هم خوانده می‌شود. | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | توکن Bearer برای حالت فعال؛ جایگزینی دستی برای CI و اجراهای headless. | ندارد |

<a id="custom-mode-your-own-server"></a>
## حالت Custom: سرور خودتان

پلاگین به URL سروری که انتخاب می‌کنید وصل می‌شود: یک سرور محلی یا یک سرور خودمیزبان. از آن برای کار آفلاین یا در محیط ایزوله (air-gapped) استفاده کنید.

| متغیر | کاربرد | پیش‌فرض |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | روی `Custom` تنظیم کنید. | `Cloud` |
| `GODOT_MCP_HOST` | ‏URL سرور، یک URL مطلق http(s). | `http://localhost:<port>`، با پورتی که از مسیر پروژه به دست می‌آید |
| `GODOT_MCP_TOKEN` | توکن Bearer که به آن سرور فرستاده می‌شود. تنظیم آن مجوزدهی `token` را انتخاب می‌کند. | ندارد |
| `GODOT_MCP_AUTH_OPTION` | ‏`none` / `oauth` / `token`؛ قاعده‌ی بالا را نادیده می‌گیرد. | انتخاب ذخیره‌شده، وگرنه `none` |

</div>

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # only if the server requires auth
```

<div dir="rtl">

‏`godot-cli open` آن‌ها را برایتان تنظیم می‌کند. برای سرور محلی‌ای که توکنی به انتخاب خودتان می‌خواهد، ویرایشگر را با آن توکن باز کنید، سپس در داک **Start Server** را بزنید (`--auth Required` مجوزدهی `token` را انتخاب می‌کند):

</div>

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

<div dir="rtl">

- توکنی که در محیط تنظیم شده (یا در حالت Custom در `.env` پروژه) روی سرور محلی‌ای که ویرایشگر اجرا می‌کند و روی پیکربندی‌های عاملی که با **Configure** نوشته می‌شوند هم اعمال می‌شود. در فایل پیکربندی ذخیره نمی‌شود.
- با `oauth`، توکن حساب ai-game.dev شما **فقط به سرور localhost** فرستاده می‌شود (`localhost`، `127.0.0.0/8`، `::1`). برای هر میزبان دیگری `GODOT_MCP_TOKEN` را تنظیم کنید.
- سروری در حالت `token` اتصال بدون توکن را رد می‌کند؛ پلاگین در لاگ می‌نویسد چه چیزی را باید تنظیم کنید.

<a id="the-server"></a>
## سرور

سرور، [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) مشترک (`gamedev-mcp-server`) است: یک فایل اجرایی برای Unity-MCP، Godot-MCP و Unreal-MCP که از مخزن خودش منتشر می‌شود. پلاگین نسخه‌ای را که استفاده می‌کند سنجاق می‌کند (`ServerVersion` در `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

<a id="let-the-addon-run-it"></a>
### اجرای آن توسط پلاگین

در حالت Custom، کارت **Server** در داک را باز کنید و **Start Server** را بزنید. پلاگین:

- فایل `gamedev-mcp-server-<rid>.zip` نسخه‌ی سنجاق‌شده را از طریق HTTPS از ریلیز GitHubِ GameDev-MCP-Server دانلود می‌کند (`win-x64`، `win-x86`، `win-arm64`، `linux-x64`، `linux-arm64`، `osx-x64`، `osx-arm64`)؛
- آن را در `.godot/mcp-server/<rid>/` کش می‌کند و فقط وقتی نسخه‌ی سنجاق‌شده تغییر کند دوباره دانلود می‌کند؛
- آن را روی پورتِ **Server URL** شما (به طور پیش‌فرض پورتی که از مسیر پروژه به دست می‌آید) اجرا می‌کند و به آن وصل می‌شود.

سرور فقط روی loopback گوش می‌دهد و به درخواستی با هدر `Origin` غیرمجاز پاسخ `403` می‌دهد (دفاع در برابر DNS rebinding). از مجوزدهی‌ای که در داک انتخاب شده استفاده می‌کند: با `none` هر پروسه‌ای روی دستگاه شما می‌تواند آن را فراخوانی کند، با `token` به توکن Bearer نیاز دارد. قواعد bind، مجوزدهی و Origin: [README سرور GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

‏**Stop Server** آن را متوقف می‌کند؛ بستن ویرایشگر هم آن را متوقف می‌کند. URL دانلود فقط از نسخه‌ی سنجاق‌شده و پلتفرم شما ساخته می‌شود. در حالت آفلاین، پلاگین یک هشدار در لاگ می‌نویسد و سرور اجرا نمی‌شود. در CI (`CI` / `GITHUB_ACTIONS`) دانلود انجام نمی‌شود.

فراخوانی ابزارها می‌تواند تا ۵ دقیقه طول بکشد. ‏`script-execute` وقتی `timeout` آن تمام شود (پیش‌فرض ۳۰ ثانیه) خودش قطعه‌کد را متوقف می‌کند.

برای توسعه، `GODOT_MCP_SERVER_PATH=<absolute path>` باعث می‌شود ویرایشگر به جای ریلیز سنجاق‌شده، همان فایل اجرایی موجود `gamedev-mcp-server` را اجرا کند (بدون دانلود، بدون بررسی نسخه، بدون پاک‌سازی پروسه‌های یتیم). این متغیر در هر بار بارگذاری اسمبلی پلاگین یک بار، از محیط پروسه یا `.env` پروژه خوانده می‌شود؛ مسیری که به هیچ فایلی اشاره نکند با یک هشدار نادیده گرفته می‌شود. در Linux و macOS فایل باید از قبل قابل اجرا باشد.

<a id="run-it-yourself"></a>
### اجرای آن توسط خودتان

یک [ریلیز GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) دانلود کنید یا از ایمیج Docker‏ [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) استفاده کنید:

</div>

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # an MCP client launches it directly
```

<div dir="rtl">

سپس در حالت Custom پلاگین را به آن هدایت کنید (`GODOT_MCP_HOST=http://localhost:8080`). همه‌ی آرگومان‌ها و متغیرهای محیطی در [README سرور GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme) آمده‌اند.

<a id="editor-side-security-notes"></a>
## نکات امنیتی سمت ویرایشگر

- ‏**توکن ویرایشگر به صورت متن ساده ذخیره می‌شود.** پلاگین پیکربندی اتصال خود، از جمله `token` و `cloudToken`، را به صورت JSON ساده در `user://godot-mcp-config.json` ذخیره می‌کند. با این فایل مانند یک راز رفتار کنید: آن را commit یا به اشتراک نگذارید. برای تعویض، توکن را در داک پاک کنید (یا فایل را حذف کنید) و دوباره وصل شوید. توکنی که از محیط پروسه، یا در حالت Custom از `.env`، آمده باشد در این فایل نوشته نمی‌شود.
- ‏**پل dev-control خاموش است مگر با `GODOT_MCP_DEV_CONTROL=1`.** این یک پل HTTP فقط برای توسعه است که برای کنترل داک در تست‌ها به کار می‌رود: فقط در ویرایشگر، متصل به `127.0.0.1`، بدون احراز هویت، و هرگز بدون آن متغیر اجرا نمی‌شود.

جزئیات: [runtime-security.fa.md](runtime-security.fa.md#editor-side-surfaces-accepted-posture).

</div>
