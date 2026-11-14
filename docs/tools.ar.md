[English](tools.md) · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · [Español](tools.es.md) · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · **العربية**

<div dir="rtl">

# مرجع الأدوات

تغطي أدوات Godot-MCP المدمجة كل ما يمكنك فعله تقريبًا في محرر Godot، وهي مجمّعة في العائلات أدناه. وهي موجزة، لذا يحتاج الوكيل إلى استدعاءات أقل ورموز أقل ([كما قيس في تقييم الوكلاء الخاص بـ RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md)). طبقة البيانات في المحرر هي [RQL](rql.md) (Reflection Query Language): تقرأ `rql-query` و`rql-mutate` و`rql-schema` عقد المشهد وخصائص Godot والموارد والملفات والمشاهد وحالة المحرر وتغيّرها في استعلام واحد، بدلًا من أداة لكل عملية.

مع `toolFormat` الافتراضي (`Rql`) تأخذ الأدوات الأخرى قيم RQL وسائطَ وتُعيد نصًا موجزًا؛ أما `Json` فيُبقي مخططات JSON ونتائجها من ReflectorNet. تُعيد لقطات الشاشة صورة PNG. كل أدوات المحرر متاحة فور تفعيل الإضافة.

تخدم العائلات **console** و**reflection** و**runtime-errors** *لعبة قيد التشغيل* (انظر [الاستخدام داخل اللعبة](runtime.ar.md)). في المحرر استخدم مصدر RQL `console` و`call` / `CallMethod` في RQL بدلًا منها.

| العائلة | الأدوات | ما تفعله |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | الاستعلام عن المحرر الحي وتغييره. كل شيء أو لا شيء، وقابل للتراجع. انظر [rql.md](rql.md). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | ملفات C# (`.cs`) وGDScript (`.gd`)، وإرفاق سكربت بعقدة، والتحقق من GDScript، وبناء كود C# للمشروع، وتشغيل C# في المحرر. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | التقاط نافذة عرض المحرر، أو كاميرا، أو عقدة واحدة مرسومة وحدها، كصورة PNG يراها النموذج اللغوي. |
| **console** | `console-get-logs`, `console-clear-logs` | في لعبة قيد التشغيل: قراءة السجلات المجمّعة ومسحها. |
| **reflection** | `reflection-method-find`, `reflection-method-call` | في لعبة قيد التشغيل: إيجاد دوال C# واستدعاؤها في أي تجميعة محمّلة عبر ReflectorNet. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | في لعبة قيد التشغيل: استطلاع أخطاء وقت التشغيل في GDScript و`push_error` / `push_warning` وأخطاء المظلِّلات واستثناءات C#. **معطّلة افتراضيًا.** |
| **ping** | `ping` | فحص الجاهزية. أداة نظام. |
| **session** | `session-close` | حفظ محرر شغّله `godot-cli` وإغلاقه. أداة نظام. |
| **skills** | `godot-skill-create`, `godot-skill-generate` | كتابة ملف أداة C# جديد في المشروع؛ إعادة توليد ملفات `SKILL.md`. أدوات نظام. |

تُخدَم **أدوات النظام** على نقطة HTTP ‏`/api/system-tools/` في خادم MCP (مثلًا `godot-cli run-system-tool ping`). ولا تُعرض لوكلاء الذكاء الاصطناعي في `tools/list`.

<a id="rql"></a>
## rql

- ‏`rql-query`: استعلام للقراءة فقط، يُعاد صفوفًا موجزة. `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- ‏`rql-mutate`: استعلام ينتهي بـ `set` أو `call`. كل شيء أو لا شيء، قابل للتراجع (Ctrl+Z)، و`dry_run` يعرض معاينته. تعديلات المشهد تعلّمه كغير محفوظ؛ أما ملفات الموارد المعدّلة و`project.godot` فتُحفظ.
  - ‏`nodes | where name starts-with "Enemy" | set Visible = false`
  - ‏`root | call AddChild("Node3D", "Props")`
  - ‏`nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - ‏`project | call AddInputAction("jump", ["Key:Space"])`
  - ‏`editor | call SaveScene()`
- ‏`rql-schema`: المصادر والدوال وأصناف العقد في المشهد المحرَّر؛ ومع اسم نوع: أعضاؤه ودواله وخصائص Godot الخاصة به. يطبع `rql-schema "<extension>"` دليل امتداد مثبّت.

تشمل المصادر `nodes` و`root` و`selection` و`scenes` و`files` و`folders` و`classes` و`console` و`editor` و`project` و`settings` و`inputActions` و`autoloads` و`groups` و`layers` و`tools`. اللغة الكاملة، بما فيها TileSets وThemes ومعاملات ShaderMaterial وMeshLibraries، موجودة في [rql.md](rql.md) وفي مهارة `godot-rql`.

<a id="script"></a>
## script

- ‏`script-read` و`script-create` و`script-update` و`script-delete`: عمليات CRUD على ملفات `.cs` و`.gd`.
- ‏`script-attach-to-node`: إرفاق سكربت بعقدة.
- ‏`script-validate`: تحليل ملفات GDScript وترجمتها وإعادة تشخيصات منظّمة.
- ‏`script-build`: بناء كود C# للمشروع (`dotnet build`، Debug) لتُحمَّل السكربتات الجديدة. يجري البناء في الخلفية؛ وعند نجاحه يعيد Godot تحميل تجميعة C#، ويعيد المكوّن الإضافي الاتصال، وتصل النتيجة (تحذيرات أو أخطاء) بعد إعادة التحميل. انظر [إعادة تحميل تجميعة C#](custom-tools.ar.md#c-assembly-reloads).
- ‏`script-execute`: ترجمة C# وتشغيله في المحرر بواسطة Roslyn، عبر [Code-Exec](https://github.com/IvanMurzak/Code-Exec). عبارات عادية يكون آخر تعبير فيها هو النتيجة (مع `using` خاصة، وأصناف محلية، و`await`)، أو صنف بدالة ساكنة. الأنواع المترجمة للعبة مرئية؛ ويضيف `include` ملفات `.cs` مساعدة من `<project>/.ai-game-dev/snippets`. تُعيد القيمة وما طبعه الكود (`Console`؛ و`GD.Print` وpush_warning وpush_error عندما يُبنى المشروع بـ Godot.NET.Sdk 4.5+). من الطرفية: `godot-cli exec '1 + 2'`.

<a id="screenshot"></a>
## screenshot

- ‏`screenshot-viewport`: نافذة عرض المحرر.
- ‏`screenshot-camera`: المشهد من كاميرا محددة.
- ‏`screenshot-isolated`: عقدة واحدة مرسومة وحدها، من زاوية تختارها.

تُعيد الثلاث الصورةَ في المحادثة. مرّر `savePath` لكتابة ملف أيضًا: `res://Screenshots/shot.png` أو `Screenshots/shot.png` (نسبةً إلى جذر المشروع) أو مسار مطلق داخل المشروع. تُنشأ المجلدات المفقودة وتُضاف `.png` عند غياب الامتداد. يُرفض `savePath` (فلا صورة ولا ملف) إذا كان خارج المشروع (`..` و`user://`)، أو يمرّ عبر رابط رمزي أو junction، أو يشير إلى داخل `res://.godot/` أو هذه الإضافة، أو ليس `.png`، أو يسمّي ملفًا موجودًا: فهو لا يكتب فوق أي ملف أبدًا. يستورد Godot ملف PNG كأي ملف في المشروع؛ ويتجنّب ذلك مجلدٌ يحتوي ملف `.gdignore`.

<a id="console-reflection-runtime-errors-running-game"></a>
## console وreflection وruntime-errors (لعبة قيد التشغيل)

- ‏`console-get-logs` / `console-clear-logs`: قراءة السجلات التي جمعها المكوّن الإضافي (مع التصفية) ومسحها، بما فيها تشخيصات الاتصال الخاصة به.
- ‏`reflection-method-find` / `reflection-method-call`: إيجاد دوال C# (والخاصة منها أيضًا) عبر كل تجميعة محمّلة، واستدعاء إحداها بوسائط.
- ‏`runtime-errors-get`: الأخطاء الملتقطة داخل اللعبة، الأقدم أولًا، مع الاحتفاظ بالأحدث. مرّر `highestSequence` السابق كـ `sinceSequence` لتحصل على الأخطاء الجديدة فقط. تُعيد `available:false` إذا لم يُفعَّل الالتقاط قط.
- ‏`runtime-errors-clear`: مسح المخزن المؤقت؛ ويستمر عدّاد التسلسل في العدّ.

التقاط أخطاء وقت التشغيل معطّل حتى تستدعي اللعبة `builder.WithRuntimeErrorCapture()`. التفاصيل: [التقاط أخطاء وقت التشغيل داخل اللعبة](runtime.ar.md#capturing-in-game-runtime-errors).

<a id="ping-session-skills-system-tools"></a>
## ping وsession وskills (أدوات النظام)

- ‏`ping`: يُعيد الرسالة كما هي، أو يُعيد `pong`. `curl -X POST <host>/api/system-tools/ping -d '{}'`.
- ‏`session-close`: حفظ وإغلاق محرر شغّله `godot-cli exec` / `open --headless` (المشاهد المفتوحة التي لها ملف والموارد التي غيّرتها). يحتاج إلى nonce تلك الجلسة من `.ai-game-dev/session.json`، فلا يغلق محررك الخاص أبدًا. يستدعيه `godot-cli close --owned-only`.
- ‏`godot-skill-create`: كتابة ملف أداة C# جديد في المشروع. يصبح قابلًا للاستدعاء بعد إعادة بناء المشروع.
- ‏`godot-skill-generate`: إعادة توليد كل ملفات `SKILL.md` من الأدوات التي سجّلها المحرر، في مجلد المهارات لوكيل الذكاء الاصطناعي المختار.

<a id="visibility"></a>
## الظهور

لكل أداة وموجّه (prompt) ومورد مفتاح **Visible** في نوافذ Tools / Prompts / Resources في اللوحة (ويفعل `godot-cli configure` الشيء نفسه من الطرفية). وهو يحدد فقط ما إذا كان عملاء MCP يرون العنصر في `tools/list`، مما يُبقي سياقهم صغيرًا. وليس تحكمًا في الوصول: فالأداة المخفية تعمل مع ذلك عندما يستدعيها العميل باسمها.

<a id="extensions"></a>
## الامتدادات

تضيف الامتدادات مصادر RQL ومحوّلات ودليلًا للوكيل إلى أدوات `rql-*` ومهارة `godot-rql`. ولا تضيف أدوات MCP خاصة بها. القائمة موجودة في [README](README.ar.md#extensions).

</div>

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

<div dir="rtl">

لا يضيف المثبّت سوى حزمة NuGet الخاصة بالامتداد؛ ثبّت بنفسك أي إضافة خارجية مطلوبة في المشروع. يحتوي قسم **Extensions** في اللوحة على **Install All**. يحدّث `godot-cli update` الإضافة ويعيد حلّ الامتدادات العائمة (`Version="*"`) إلى أحدث إصداراتها؛ أما الامتداد الذي ثبّتّه على إصدار معيّن فيبقى كما هو. إذا احتاج امتداد إلى McpPlugin أحدث مما تثبّته الإضافة، تفشل الاستعادة مع `NU1605` حتى تحدّث الإضافة أو تثبّت إصدار الامتداد.

</div>
