<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: وكلاء الذكاء الاصطناعي يعملون في محرر Godot عبر الإضافة godot_mcp" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · **العربية**

</div>

<div dir="rtl">

‏**Godot-MCP** يربط وكلاء الذكاء الاصطناعي (Claude Code وCursor وCodex وGitHub Copilot وGemini أو أي عميل [MCP](https://modelcontextprotocol.io/) آخر) بـ **محرر Godot**. يستطيع الوكيل قراءة مشاهدك وعُقدك ومواردك وسكربتاتك وتعديلها، وتشغيل C# في المحرر، والاطلاع على لقطات الشاشة. وهو إضافة محرر مكتوبة بـ C# لـ Godot 4.3+ (.NET) ترافقها أداة سطر أوامر هي [`godot-cli`](../cli/docs/README.ar.md)، وهو النظير الخاص بـ Godot لمشروع [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

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

تتصل الإضافة بخادم MCP: الخادم المستضاف [ai-game.dev](https://ai-game.dev) افتراضيًا، أو خادمك الخاص [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) الذي تستطيع الإضافة تنزيله وتشغيله نيابةً عنك. يتصل وكيلك بالخادم نفسه ويستدعي أدوات الإضافة.

<a id="features"></a>
## الميزات

‏✅ ‏**لغة استعلام واحدة** — [RQL](rql.md) تقرأ وتعدّل العقد والمشاهد والموارد والإعدادات\
‏✅ ‏**تعديلات قابلة للتراجع** — تغييرات RQL تُطبَّق كلها أو لا شيء، وتُلغى بـ Ctrl+Z\
‏✅ ‏**C# وGDScript** — إنشاء سكربتات `.cs` / `.gd` وتحديثها والتحقق منها وإرفاقها\
‏✅ ‏**تشغيل C# في المحرر** — `script-execute` يشغّل مقتطفًا على أنواع لعبتك\
‏✅ ‏**تغذية راجعة مرئية** — لقطات شاشة لنافذة العرض أو لكاميرا أو لعقدة واحدة\
‏✅ ‏**سحابي أو مستضاف ذاتيًا** — تسجيل دخول إلى ai-game.dev، أو خادم محلي تشغّله الإضافة\
‏✅ ‏**الامتدادات** — وحدات RQL اختيارية للحركة والتنقل وخرائط البلاطات والإضافات\
‏✅ ‏**داخل اللعبة** — اعرض أدواتك من لعبة قيد التشغيل (معطّل افتراضيًا)\
‏✅ ‏**أدواتك الخاصة** — دالة C# واحدة ذات سمة لكل أداة ([الدليل](custom-tools.ar.md))

<a id="quick-start"></a>
## البدء السريع

المتطلبات: Godot 4.3 أو أحدث، **إصدار .NET** (إصدار GDScript فقط لا يستطيع ترجمة الإضافة)، و.NET 8 SDK، وNode.js 22.14+ لـ `godot-cli`.

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

ثم اطلب من وكيلك: *"أنشئ 3 مكعبات على دائرة نصف قطرها 2."*

‏`godot-cli setup-mcp --list` يعرض كل الوكلاء المدعومين. للتثبيت من تبويب **AssetLib** في المحرر، أو من [ملف zip للإصدار](https://github.com/IvanMurzak/Godot-MCP/releases/latest)، أو يدويًا، اتبع [install.ar.md](install.ar.md): يحتاج ملف `.csproj` للمشروع حينها إلى مراجع NuGet التالية.

| الحزمة | الإصدار |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0` (للمحرر فقط) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1` (للمحرر فقط) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` (للمحرر فقط) |

<a id="usage"></a>
## الاستخدام

تحدّث مع وكيلك بلغة عادية؛ وهو يختار الأدوات. ومن مجلد المشروع يمكنك استدعاء الأدوات نفسها من الطرفية:

</div>

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # list the editor's tools
```

<div dir="rtl">

عندما لا يكون أي محرر قيد التشغيل، تشغّل هذه الأوامر محررًا بلا واجهة (headless) للمشروع. كل الأوامر موجودة في [مرجع CLI](../cli/docs/README.ar.md).

<a id="tools"></a>
### الأدوات

يأتي Godot-MCP مع **24 أداة مدمجة** في **9 عائلات**:

| العائلة | الأدوات |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | السجلات وانعكاس C# والتقاط الأخطاء داخل لعبة قيد التشغيل |
| **ping**, **session**, **skills** | أدوات نظام لـ `godot-cli` والإعداد، لا تظهر للوكلاء |

كل أداة ووسيط: [tools.ar.md](tools.ar.md).

<a id="extensions"></a>
### الامتدادات

الامتدادات حزم NuGet اختيارية توسّع RQL؛ ولا تضيف أدوات خاصة بها.

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

<div dir="rtl">

| الامتداد | ما يضيفه | المتطلبات |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | مكتبات AnimationPlayer ومساراتها ومفاتيحها | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | أشجار السلوك وعقدها | الإضافة [Beehave](https://github.com/bitbrain/beehave) |
| [CSG](../extensions/Godot-AI-CSG/README.md) | أشكال CSG بأبعاد مُتحقَّق منها | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | الخطوط الزمنية والشخصيات | الإضافة [Dialogic](https://github.com/dialogic-godot/dialogic)، Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | إعدادات GridMap وصفوف الخلايا وتحرير الخلايا | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | وكلاء التنقل والمناطق والروابط والتحضير (baking) | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | جسيمات GPU في 2D و3D | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | كاميرات افتراضية، والتتبع والتوجيه نحو هدف | الإضافة [Phantom Camera](https://github.com/ramokz/phantom-camera)، Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | مصدر التضاريس وحجم المنطقة والمواد | الإضافة [Terrain3D](https://github.com/TokisanGames/Terrain3D)، Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | صفوف خلايا TileMapLayer وتحرير الخلايا | |

ثبّت بنفسك أي إضافة خارجية مطلوبة في المشروع؛ ويستطيع قسم **Extensions** في اللوحة تثبيتها كلها.

<a id="documentation"></a>
## التوثيق

| الصفحة | محتواها |
| --- | --- |
| [التثبيت اليدوي](install.ar.md) | AssetLib، ملف zip للإصدار، مراجع NuGet، إعداد الوكيل |
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

‏[Apache-2.0](../LICENSE) © Ivan Murzak. تستخدم الإضافة أيضًا [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) وCode-Exec، ولكل منهما ترخيصه الخاص.

هذا الترخيص هو [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/): مجاني الاستخدام، بما في ذلك في المنتجات التجارية، إلا لبناء منتج ينافس منتجات المرخِّص.

</div>
