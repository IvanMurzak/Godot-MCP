<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: وكلاء الذكاء الاصطناعي يعملون في محرر Godot عبر الإضافة godot_mcp" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · **العربية**

</div>

<div dir="rtl">

‏**Godot-MCP** يربط وكلاء الذكاء الاصطناعي (Claude Code وCursor وCodex وGitHub Copilot وGemini أو أي عميل [MCP](https://modelcontextprotocol.io/) آخر) بـ **محرر Godot**. يستطيع الوكيل قراءة مشاهدك وعُقدك ومواردك وسكربتاتك وتعديلها، وبناء C# وتشغيله، والاطلاع على لقطات الشاشة. وهو إضافة محرر ترافقها أداة سطر أوامر هي [`godot-cli`](../cli/docs/README.ar.md)، وهو النظير الخاص بـ Godot لمشروع [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

‏**المتطلبات:** **إصدار .NET** من محرر Godot 4.3+ و**.NET 8 SDK**، يُثبَّت مرة واحدة. يمكن أن تبقى سكربتاتك بلغة GDScript. تحتاج أداة `godot-cli` الاختيارية إلى Node.js 22.14+. يعمل على Windows وmacOS وLinux.

<a id="why"></a>
## لماذا Godot-MCP

‏✅ ‏**من Godot 4.3 إلى 4.7** — يُختبر كل إصدار في CI على 4.3 و4.4 و4.5 و4.6 و4.7\
‏✅ ‏**بناء C# حقيقي** — يشغّل `script-build` الأمر `dotnet build` ويعيد أخطاء المترجم\
‏✅ ‏**فحص GDScript** — يعيد `script-validate` تشخيصات التحليل والترجمة لملفات `.gd`\
‏✅ ‏**تعديلات ذرّية قابلة للتراجع** — تُطبَّق تغييرات الاستعلام كلها أو لا شيء، وكل منها يُلغى بـ Ctrl+Z واحدة\
‏✅ ‏**لقطات الشاشة** — نافذة العرض، أو كاميرا، أو عقدة واحدة تُعرض منفردة، تُعاد إلى الوكيل\
‏✅ ‏**أدوات وقت التشغيل** — السجلات وانعكاس C# وأخطاء وقت التشغيل من لعبة قيد التشغيل (اختياري)\
‏✅ ‏**سحابة مجانية أو محلي بالكامل** — ai-game.dev افتراضيًا (بلا منافذ مفتوحة)، أو خادم محلي بلا حساب\
‏✅ ‏**بلا قياس عن بُعد (telemetry)** — لا ترسل الإضافة ولا `godot-cli` أي بيانات استخدام أو تحليلات

<a id="gdscript"></a>
## تعمل مع مشاريع GDScript

تبقى شيفرة لعبتك بلغة GDScript. الإضافة نفسها مكتوبة بـ C#، لذا يحتاج المشروع إلى بضع خطوات تُنفَّذ مرة واحدة:

‏✅ ‏**محرر Godot .NET** — افتح المشروع في إصدار .NET من Godot 4.3+ (المشروع نفسه، والمشاهد نفسها)\
‏✅ ‏**.NET 8 SDK** — يُثبَّت مرة واحدة لكل جهاز\
‏✅ ‏**حل C# (C# solution)** — لمشروع قائم: **Project → Tools → C# → Create C# solution**؛ ولمشروع جديد: `create-project --dotnet`\
‏✅ ‏**الإضافة** — يضيفها `godot-cli install-plugin` مع مراجع NuGet الخاصة بها ويفعّلها

‏**هل يحتاج مشروع GDScript فقط إلى حل C#؟** نعم، مرة واحدة: الإضافة مكتوبة بـ C# وتُترجم ضمن تجميعة C# الخاصة بالمشروع. يضيف **Create C# solution** ملف `.csproj` وملف `.sln` في جذر المشروع. لا تتغير سكربتات `.gd` ولا المشاهد ولا الموارد، وتعمل كما كانت؛ ويقرأ الوكيل GDScript ويكتبها ويتحقق منها أيضًا. بعد ذلك تستخدم عمليات التصدير قوالب التصدير الخاصة بـ .NET في Godot وتتضمن تجميعات وقت التشغيل الصغيرة للإضافة، والتي تبقى غير نشطة ما لم تستدعِ شيفرتك `GodotMcpRuntime.Initialize()` ([كيف تستبعدها](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users)).

</div>

<p align="center">
  <img src="img/terminal.svg" alt="جلسة طرفية: godot-cli يثبّت الإضافة ويسجّل الدخول ويهيّئ Claude Code ويفتح المحرر" width="100%"/>
</p>

<div dir="rtl">

<a id="how-it-works"></a>
## كيف يعمل

</div>

<p align="center">
  <img src="img/architecture.svg" alt="وكيل ذكاء اصطناعي يتحدث بروتوكول MCP مع خادم (ai-game.dev أو gamedev-mcp-server محلي)؛ يصل الخادم إلى إضافة المحرر godot_mcp عبر SignalR؛ وتشغّل الإضافة الأدوات على الخيط الرئيسي لـ Godot" width="100%"/>
</p>

<div dir="rtl">

تتصل الإضافة بخادم MCP، ويتصل وكيلك بالخادم نفسه ويستدعي أدوات الإضافة. الخادم إما [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) محلي تنزّله الإضافة وتشغّله نيابةً عنك، وإما المُرحِّل (relay) المستضاف [ai-game.dev](https://ai-game.dev). لا تفتح الإضافة أي منفذ خاص بها.

‏Cloud هو الوضع الافتراضي لأنه لا يحتاج إلى منافذ مفتوحة ويتجنب تعارض المنافذ مع البرامج الأخرى؛ وهو مجاني. الوضع المحلي بديل كامل للعمل دون اتصال أو في بيئة معزولة (air-gapped).

<a id="stability"></a>
### الاستقرار

- ‏**ترقيم الإصدارات:** تُصدَر الإضافة و`godot-cli` دائمًا برقم الإصدار نفسه؛ اقرأ [ملاحظات الإصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest) قبل التحديث.
- ‏**ما الذي يتغير بين الإصدارات:** قد يغيّر الإصدار إصدارات NuGet التي تحتاجها الإضافة وإصدار GameDev-MCP-Server الذي تنزّله. يكتب `godot-cli update` (أو `install-plugin`) إصدارات NuGet المطابقة للإضافة التي تثبّتها، وتجلب الإضافة الخادم المطابق لها بنفسها.
- ‏**بوابة الإصدار:** لا يُصدَر أي إصدار إلا بعد نجاح اختبارات .NET واختبارات CLI واختبارات محرر Godot 4.3–4.7.
- ‏**سجل التغييرات:** يسرد كل [إصدار على GitHub](https://github.com/IvanMurzak/Godot-MCP/releases) التغييرات المدمجة فيه؛ ويحتفظ `godot-cli` أيضًا بـ [سجل تغييرات](../cli/CHANGELOG.md).

<a id="quick-start"></a>
## البدء السريع

ثبّت `godot-cli` (Node.js 22.14+) وأضف الإضافة إلى مشروعك:

</div>

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + enable the plugin
```

<div dir="rtl">

ثم اختر كيف يصل الوكيل إلى المحرر. كلاهما إعداد كامل. ‏Cloud هو الافتراضي في الإضافة: بلا منافذ مفتوحة، وبلا تعارض منافذ مع البرامج الأخرى، ومجاني. الوضع المحلي يُبقي كل شيء على جهازك، للعمل دون اتصال أو في بيئة معزولة.

‏**الخيار A: محلي، بلا حساب.** تبقى استدعاءات الأدوات على جهازك.

1. ‏**اضبط رمزًا (token).** افتح المشروع في محرر Godot .NET. في لوحة **AI Game Developer** اختر **Custom**، واضبط **Authorization → token**، واضغط **Generate** ثم **Start Server**. دوّن **Server URL** (`http://localhost:<port>`، بمنفذ مشتق من مسار المشروع).
   أو اختر رمزًا بنفسك، وافتح المحرر به من الطرفية، ثم اضغط **Start Server**:
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. ‏**اربط وكيلك** بالرمز نفسه:

</div>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

<div dir="rtl">

> ‏**بدون رمز** يستخدم الخادم المحلي التفويض `none`. يستمع على واجهة loopback فقط، فلا تصل إليه إلا البرامج الموجودة على حاسوبك.
> في هذه الحالة يشغّل `godot-cli open ./MyGodotProject` المحرر وخادمه المحلي في خطوة واحدة ويطبع عنوان URL لاستخدامه مع `setup-mcp --url`.

‏**الخيار B: سحابة ai-game.dev.** سجّل الدخول مرة واحدة لكل جهاز؛ بعدها تحمل إعدادات الوكلاء مفتاحًا مرتبطًا بهذا المشروع.

</div>

```bash
godot-cli login                                       # OAuth device login in the browser
godot-cli setup-mcp claude-code ./MyGodotProject      # write the agent's MCP config
godot-cli open ./MyGodotProject                       # build C#, start the editor, connect
```

<div dir="rtl">

ثم اطلب من وكيلك: *«أنشئ 3 مكعبات على دائرة نصف قطرها 2.»*

للتثبيت من تبويب **AssetLib** في المحرر، أو من [ملف zip لإصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest)، أو يدويًا، اتبع [دليل التثبيت اليدوي](install.ar.md): يحتاج ملف `.csproj` للمشروع حينها إلى مراجع NuGet الخاصة بالإضافة (`com.IvanMurzak.ReflectorNet` و`com.IvanMurzak.McpPlugin`، والحزم الخاصة بالمحرر فقط `com.IvanMurzak.Rql` و`com.IvanMurzak.CodeExec` و`com.IvanMurzak.CodeExec.Roslyn` و`Microsoft.CodeAnalysis.CSharp`) بإصدارات الإصدار الذي تثبّته.

<a id="relay"></a>
### ما الذي يمر عبر ai-game.dev

في وضع Cloud فقط. المُرحِّل عديم الحالة (stateless): يوجّه استدعاءات الأدوات بين وكيلك والمحرر دون فحص محتواها أو حفظه، ولا يجمع محادثاتك أو شيفرتك المصدرية أو ملفات مشروعك أو أصولك أو بيانات مشاهدك. يسجّل البيانات الوصفية للطلبات (عنوان IP، والوقت والمدة، واسم الأداة وعدد الاستدعاءات، وحجم الطلب والاستجابة) والبيانات الوصفية لأحداث MCP (نوع الحدث، والتوقيت، والحالة، ومعرّفات الجلسة، واسم الأداة أو الموجّه (prompt) أو المورد، وتفاصيل الأخطاء). تُستخدم هذه السجلات ضمن نافذة متجددة مدتها 90 يومًا. التفاصيل: [سياسة الخصوصية](https://ai-game.dev/legal/privacy). في الوضع المحلي يتواصل المحرر والخادم عبر localhost فقط.

<a id="clients"></a>
## اربط عميل الذكاء الاصطناعي

يكتب `godot-cli setup-mcp <agent> ./MyGodotProject` الإعدادات نيابةً عنك (يعرض `godot-cli setup-mcp --list` معرّفات كل الوكلاء). اسم مُدخل الخادم هو `ai-game-developer`؛ و`<url>` هو العنوان الذي يطبعه `setup-mcp`. في وضع Cloud يحمل المُدخل أيضًا `Authorization: Bearer agd_pk_…`، وهو مفتاح لا يصل إلا إلى هذا المشروع.

</div>

<details>
<summary><b>Claude Code</b> — <code>.mcp.json</code> في المشروع</summary>

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

في وضع Cloud يوضع المفتاح في `http_headers`.

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

مدعومة أيضًا: Claude Desktop وVisual Studio (Copilot) وGitHub Copilot CLI وRider (Junie) وAntigravity وCline وOpen Code وKilo Code وعميل عام `custom` ([مرجع CLI](../cli/docs/README.ar.md#supported-ai-agents)).

<a id="what-the-agent-can-do"></a>
## ما الذي يستطيع الوكيل فعله

**التغطية:** يستطيع الوكيل فعل كل ما يمكنك فعله تقريبًا في محرر Godot: المشاهد، والعقد، والموارد، وإعدادات المشروع، والسكربتات، وبناء C#، ولقطات الشاشة، واللعبة قيد التشغيل.\
**الكفاءة:** الأدوات موجزة، لذا يحتاج الوكيل إلى استدعاءات أقل ورموز (tokens) أقل، ويعمل أسرع وبتكلفة أقل. وقد قيس ذلك في الاختبارات: في [تقييم الوكلاء الخاص بـ RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md) على عالم لعبة تجريبي (sandbox)، استغرقت مهمة 2–3 استدعاءات أدوات ونحو 8k رمز مع RQL، مقابل نحو 37 استدعاءً و169k رمز مع أدوات reflection التقليدية من نوع find / get / set.

لا تكتب أنت الاستعلامات: يقرأ الوكيل دليل RQL المرفق بالأدوات (`rql-schema`، ومهارة `godot-rql`) ويكتبها بنفسه.

| القدرة | الأدوات | اطلب من وكيلك |
| --- | --- | --- |
| المشاهد والعقد | `rql-query`, `rql-mutate` | *«أضف DirectionalLight3D إلى المشهد الرئيسي واحفظه.»* |
| التعديلات الجماعية | `rql-mutate` | *«أخفِ كل عقدة يبدأ اسمها بـ Enemy.»* |
| الموارد وإعدادات المشروع | `rql-query`, `rql-mutate` | *«أضف إجراء إدخال jump مربوطًا بمفتاح Space.»* |
| البحث في الواجهة البرمجية | `rql-schema` | *«ما خصائص Godot التي يملكها CharacterBody3D؟»* |
| السكربتات (GDScript وC#) | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *«اكتب سكربت GDScript يدوّر هذه العقدة وأرفقه بها.»* |
| بناء C# | `script-build` | *«ابنِ مشروع C# وأصلح الأخطاء.»* |
| تشغيل C# في المحرر | `script-execute` | *«احسب عدد عقد MeshInstance3D في المشهد المفتوح.»* |
| رؤية النتيجة | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *«التقط لقطة شاشة لنافذة العرض وتحقق من الإضاءة.»* |
| اللعبة قيد التشغيل (اختياري) | `console-*`, `reflection-*`, `runtime-errors-*` | *«افحص اللعبة قيد التشغيل بحثًا عن أخطاء وقت تشغيل جديدة.»* |

‏`ping` و`session-close` و`godot-skill-create` و`godot-skill-generate` أدوات نظام لـ `godot-cli` وللإعداد؛ ولا يراها الوكلاء. كل الأدوات ووسائطها: [مرجع الأدوات](tools.ar.md).

تعمل الأدوات نفسها من الطرفية داخل مجلد المشروع (يبدأ محرر بلا واجهة إذا لم يكن هناك محرر قيد التشغيل):

</div>

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # list the editor's tools
```

<div dir="rtl">

<a id="security"></a>
## الأمان

| المجال | ما ينطبق |
| --- | --- |
| ‏**الخادم المحلي** | يستمع على واجهة loopback فقط. تفويضه الافتراضي هو `none`، لذا تستطيع أي عملية على جهازك استدعاءه. لاشتراط رمز Bearer اختر `token` في اللوحة، أو اضبط `GODOT_MCP_TOKEN`، أو افتح المحرر بـ `godot-cli open <path> --mode Custom --auth Required --token <token>`. |
| ‏**فحص Origin** | يرد الخادم بـ `403` على أي طلب يحمل ترويسة `Origin` غير مسموح بها، في كل أوضاع التفويض (حماية من DNS rebinding). |
| ‏**إضافة المحرر** | لا تفتح أي منفذ؛ بل تتصل هي بالخادم. لا يبدأ جسر تحكم للتطوير فقط على `127.0.0.1` إلا مع `GODOT_MCP_DEV_CONTROL=1`. |
| ‏**Cloud** | تحمل إعدادات الوكلاء مفتاحًا مرتبطًا بمشروع واحد؛ ويستخدم المحرر تسجيل الدخول على جهازك. لا يُرسَل رمز حسابك إلا إلى ai-game.dev أو إلى خادم localhost. المُرحِّل عديم الحالة (stateless): لا يفحص محتوى استدعاءات الأدوات ولا يحفظه ([سياسة الخصوصية](https://ai-game.dev/legal/privacy)). |
| ‏**الرموز المخزّنة** | يحفظ المحرر رمزه بصيغة JSON صريحة في `user://godot-mcp-config.json`؛ تعامل مع هذا الملف كسرّ. |
| ‏**الألعاب المُصدَّرة** | لا يعمل شيء داخل اللعبة ما لم تستدعِ شيفرتك `GodotMcpRuntime.Initialize(...)` و`Connect()`، ومع الأدوات التي تسجّلها أنت فقط. التقاط الأخطاء وC# داخل اللعبة يُفعَّل كل منهما على حدة ([أمان وقت التشغيل](runtime-security.ar.md)). |
| ‏**ما لا تتم حمايته** | تغيّر استدعاءات الأدوات مشروعك وتشغّل C# في المحرر: استخدم نظام التحكم بالإصدارات، ولا تعرّض أبدًا خادمًا بتفويض `none` لأي شبكة. |

التفاصيل: [الاتصال والخادم](connection.ar.md) و[README الخاص بـ GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="extensions"></a>
## الامتدادات

الامتدادات حزم NuGet اختيارية توسّع RQL؛ ولا تضيف أدوات خاصة بها.

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

<div dir="rtl">

| الامتداد | ما يضيفه | المتطلبات |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | مكتبات AnimationPlayer ومساراتها ومفاتيحها | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | أشجار السلوك وعقدها | إضافة [Beehave](https://github.com/bitbrain/beehave) |
| [CSG](../extensions/Godot-AI-CSG/README.md) | أشكال CSG بأبعاد مُتحقَّق منها | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | الخطوط الزمنية والشخصيات | إضافة [Dialogic](https://github.com/dialogic-godot/dialogic)، Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | إعدادات GridMap وصفوف الخلايا وتحرير الخلايا | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | وكلاء التنقل ومناطقه وروابطه، والخَبز (baking) | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | جسيمات GPU في 2D و3D | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | كاميرات افتراضية، والتتبع والنظر (follow / look-at) | إضافة [Phantom Camera](https://github.com/ramokz/phantom-camera)، Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | مصدر التضاريس وحجم المنطقة والمواد | إضافة [Terrain3D](https://github.com/TokisanGames/Terrain3D)، Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | صفوف خلايا TileMapLayer وتحرير الخلايا | |

ثبّت إضافة الطرف الثالث المطلوبة في المشروع بنفسك؛ ويستطيع قسم **Extensions** في اللوحة تثبيتها كلها.

<a id="documentation"></a>
## التوثيق

| الصفحة | محتواها |
| --- | --- |
| [التثبيت اليدوي](install.ar.md) | ‏AssetLib، ملف zip للإصدار، مراجع NuGet، إعداد الوكيل |
| [الاتصال والخادم](connection.ar.md) | وضعا Cloud وCustom، المتغيرات، الخادم |
| [مرجع الأدوات](tools.ar.md) | كل أداة ووسائطها، لقطات الشاشة، الامتدادات |
| [RQL في Godot-MCP](rql.md) | المصادر، الدوال، الكتابة، التراجع والحفظ |
| [الأدوات المخصّصة](custom-tools.ar.md) | كتابة الأدوات، إعادة تحميل تجميعة C#، المشكلة المعروفة في .NET 10 |
| [الاستخدام داخل اللعبة](runtime.ar.md) | ‏MCP داخل لعبة قيد التشغيل: الإعداد، التقاط الأخطاء، C# في اللعبة |
| [أمان وقت التشغيل](runtime-security.ar.md) | ما يضمنه وقت التشغيل داخل اللعبة وما يجب عليك فعله |
| [مرجع CLI](../cli/docs/README.ar.md) | كل أوامر `godot-cli`، رموز الخروج وصيغ المخرجات |
| [Architecture](ARCHITECTURE.md) · [Releasing](RELEASING.md) · [Extensions](EXTENSION-MONOREPO.md) | للمساهمين |

<a id="contributing"></a>
## المساهمة

لا حاجة إلى ملف Godot التنفيذي للبناء واختبارات الوحدات:

</div>

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

<div dir="rtl">

الأعراف واختبار الدخان للمحرر بلا واجهة وأهم المزالق موجودة في [AGENTS.md](../AGENTS.md). انسخ المستودع (fork) وافتح طلب سحب نحو `main`. الأسئلة وعرض الأعمال: [Discord](https://discord.gg/cfbdMZX99G).

ذو صلة: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## الترخيص

‏**يستطيع مطورو الألعاب استخدام كل شيء بحرية، بما في ذلك الألعاب التجارية.** ‏Godot-MCP مرخّص بموجب [Apache-2.0](../LICENSE). أما اعتمادياته من جهة المحرر، RQL وCode-Exec، فتستخدم [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/)، التي لا تقيّد إلا الشركات التي تبني أدوات ذكاء اصطناعي منافسة لتطوير الألعاب.

</div>
