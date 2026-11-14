[English](connection.md) · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · [Español](connection.es.md) · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · **العربية**

<div dir="rtl">

# الاتصال والخادم

يتصل المكوّن الإضافي للمحرر بخادم MCP بأحد وضعين. اضبط الوضع في لوحة المحرر، أو تجاوزه عند بدء العملية بمتغيرات البيئة `GODOT_MCP_*` (مفيدة لـ CI والتشغيل بلا واجهة والتطوير المحلي). تتغلب البيئة دائمًا على الإعدادات المحفوظة. يمرّر `godot-cli open` هذه المتغيرات نيابةً عنك عبر `--mode` و`--url` و`--cloud-url` و`--token`.

<a id="cloud-mode-default-ai-gamedev"></a>
## وضع Cloud (الافتراضي): ai-game.dev

يتصل المكوّن الإضافي بـ `https://ai-game.dev`؛ ولا تشغّل أنت أي خادم. ‏Cloud هو الوضع الافتراضي لأنه لا يحتاج إلى منافذ مفتوحة ويتجنب تعارض المنافذ مع البرامج الأخرى؛ وهو مجاني. لا يخزّن المُرحِّل (relay) أي محتوى للطلبات أو الاستجابات، ويحتفظ بالبيانات الوصفية فقط لمدة 90 يومًا.

سجّل الدخول مرة واحدة باستخدام `godot-cli login`. يستخدم تدفق الجهاز في OAuth 2.1 (RFC 8628): يطبع رمزًا قصيرًا، ويفتح متصفحك، وبعد موافقتك يحفظ بيانات اعتماد في مخزن الجهاز (`~/.ai-game-dev/credentials.json`). يلتقطها المكوّن الإضافي للمحرر، فلا رمز (token) تحتاج إلى نسخه.

| المتغير | الغرض | القيمة الافتراضية |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | ‏`Cloud` أو `Custom` (دون تمييز حالة الأحرف). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | عنوان URL الأساسي للسحابة. تُحذف اللاحقة `/mcp` إن وُجدت. | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | مجلد مطلق لمخزن بيانات الاعتماد، للاختبار المعزول. يُقرأ أيضًا من ملف `.env` للمشروع. | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | رمز Bearer للوضع النشط؛ تجاوز يدوي لـ CI والتشغيل بلا واجهة. | لا شيء |

<a id="custom-mode-your-own-server"></a>
## وضع Custom: خادمك الخاص

يتصل المكوّن الإضافي بعنوان خادم تختاره: خادم محلي، أو خادم تستضيفه بنفسك. استخدمه للعمل دون اتصال أو في بيئة معزولة (air-gapped).

| المتغير | الغرض | القيمة الافتراضية |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | اضبطه على `Custom`. | `Cloud` |
| `GODOT_MCP_HOST` | عنوان الخادم، وهو عنوان http(s) مطلق. | `http://localhost:<port>`، بمنفذ مشتق من مسار المشروع |
| `GODOT_MCP_TOKEN` | رمز Bearer المُرسل إلى ذلك الخادم. ضبطه يختار التفويض `token`. | لا شيء |
| `GODOT_MCP_AUTH_OPTION` | ‏`none` / `oauth` / `token`؛ يتجاوز القاعدة السابقة. | الخيار المحفوظ، وإلا `none` |

</div>

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # only if the server requires auth
```

<div dir="rtl">

يضبطها `godot-cli open` نيابةً عنك. لخادم محلي يتطلب رمزًا تختاره بنفسك، افتح المحرر بهذا الرمز، ثم اضغط **Start Server** في اللوحة (يختار `--auth Required` التفويض `token`):

</div>

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

<div dir="rtl">

- الرمز المضبوط في البيئة (أو في ملف `.env` للمشروع في وضع Custom) يسري أيضًا على الخادم المحلي الذي يشغّله المحرر وعلى إعدادات الوكلاء المكتوبة بزر **Configure**. ولا يُحفظ في ملف الإعدادات.
- مع `oauth` يُرسَل رمز حسابك في ai-game.dev **إلى خادم localhost فقط** (`localhost` و`127.0.0.0/8` و`::1`). لأي مضيف آخر اضبط `GODOT_MCP_TOKEN`.
- الخادم في وضع `token` يرفض أي اتصال بلا رمز؛ ويسجّل المكوّن الإضافي ما يجب ضبطه.

<a id="the-server"></a>
## الخادم

الخادم هو [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) المشترك (`gamedev-mcp-server`)، وهو ملف تنفيذي واحد لـ Unity-MCP وGodot-MCP وUnreal-MCP، يُصدَر من مستودعه الخاص. تثبّت الإضافة الإصدار الذي تستخدمه (`ServerVersion` في `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

<a id="let-the-addon-run-it"></a>
### دع الإضافة تشغّله

في وضع Custom افتح بطاقة **Server** في اللوحة واضغط **Start Server**. تقوم الإضافة بما يلي:

- تنزّل `gamedev-mcp-server-<rid>.zip` للإصدار المثبّت عبر HTTPS من إصدار GameDev-MCP-Server على GitHub (`win-x64` و`win-x86` و`win-arm64` و`linux-x64` و`linux-arm64` و`osx-x64` و`osx-arm64`)؛
- تخزّنه مؤقتًا في `.godot/mcp-server/<rid>/` ولا تنزّله مجددًا إلا عند تغيّر الإصدار المثبّت؛
- تشغّله على منفذ **Server URL** الخاص بك (افتراضيًا منفذ مشتق من مسار المشروع) وتتصل به.

يستمع الخادم على واجهة loopback فقط، ويرد بـ `403` على أي طلب يحمل ترويسة `Origin` غير مسموح بها (حماية من DNS rebinding). يستخدم التفويض المختار في اللوحة: مع `none` تستطيع أي عملية على جهازك استدعاءه، ومع `token` يتطلب رمز Bearer. قواعد الربط والتفويض وOrigin: [README الخاص بـ GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

يوقفه **Stop Server**؛ ويوقفه إغلاق المحرر أيضًا. يأتي عنوان التنزيل من الإصدار المثبّت ومنصتك فقط. عند انعدام الاتصال تسجّل الإضافة تحذيرًا ولا يبدأ الخادم. تحت CI (`CI` / `GITHUB_ACTIONS`) يُتخطّى التنزيل.

قد تستغرق استدعاءات الأدوات حتى 5 دقائق. يوقف `script-execute` المقتطف بنفسه عند انتهاء مهلته `timeout` (30 ثانية افتراضيًا).

لأغراض التطوير، يجعل `GODOT_MCP_SERVER_PATH=<absolute path>` المحرر يشغّل ذلك الملف التنفيذي الموجود من `gamedev-mcp-server` بدلًا من الإصدار المثبّت (بلا تنزيل، وبلا فحص للإصدار، وبلا تنظيف للعمليات اليتيمة). يُقرأ مرة واحدة عند كل تحميل لتجميعة المكوّن الإضافي، من بيئة العملية أو من ملف `.env` للمشروع؛ والمسار الذي لا يشير إلى ملف يُتجاهل مع تحذير. على Linux وmacOS يجب أن يكون الملف قابلًا للتنفيذ مسبقًا.

<a id="run-it-yourself"></a>
### شغّله بنفسك

نزّل [إصدارًا من GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) أو استخدم صورة Docker [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server):

</div>

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # an MCP client launches it directly
```

<div dir="rtl">

ثم وجّه المكوّن الإضافي إليه في وضع Custom (`GODOT_MCP_HOST=http://localhost:8080`). كل الوسائط ومتغيرات البيئة موجودة في [README الخاص بـ GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="editor-side-security-notes"></a>
## ملاحظات أمنية من جهة المحرر

- **رمز المحرر يُخزَّن نصًا صريحًا.** يحفظ المكوّن الإضافي إعدادات اتصاله، ومنها `token` و`cloudToken`، بصيغة JSON صريحة في `user://godot-mcp-config.json`. تعامل مع هذا الملف كسرّ: لا تودعه في المستودع ولا تشاركه. للتدوير امسح الرمز في اللوحة (أو احذف الملف) وأعد الاتصال. الرمز القادم من بيئة العملية، أو من `.env` في وضع Custom، لا يُكتب في هذا الملف.
- **جسر التحكم التطويري (dev-control) معطّل ما لم يكن `GODOT_MCP_DEV_CONTROL=1`.** هو جسر HTTP للتطوير فقط لقيادة اللوحة في الاختبارات: للمحرر فقط، مربوط بـ `127.0.0.1`، بلا مصادقة، ولا يبدأ أبدًا بدون ذلك المتغير.

التفاصيل: [runtime-security.ar.md](runtime-security.ar.md#editor-side-surfaces-accepted-posture).

</div>
