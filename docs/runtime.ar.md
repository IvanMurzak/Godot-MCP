[English](runtime.md) · [中文](runtime.zh-CN.md) · [日本語](runtime.ja.md) · [Español](runtime.es.md) · [فارسی](runtime.fa.md) · [हिन्दी](runtime.hi.md) · **العربية**

<div dir="rtl">

# الاستخدام داخل اللعبة (وقت التشغيل)

يربط المكوّن الإضافي للمحرر **محرر Godot**. ويستطيع Godot-MCP أيضًا العمل داخل **لعبة قيد التشغيل أو مُصدَّرة** (debug أو release)، فيتمكن النموذج اللغوي من قراءة حالة اللعبة الحية وقيادتها: مثلًا لعبة شطرنج تأتي حركات خصمها الآلي من نموذج لغوي عبر أداتين تكتبهما أنت.

يختلف وضع وقت التشغيل عن المحرر عن قصد:

- **لا يتصل من تلقاء نفسه أبدًا.** كودك هو الذي يقرر هل يستدعي `Connect()` ومتى.
- **لا يعرض شيئًا افتراضيًا.** تسجّل بنفسك كل أداة (`[AiToolType]`) وموجّه (`[AiPromptType]`) ومورد (`[AiResourceType]`)؛ وكل نوع منها اختياري. أدوات المحرر في الإضافة موضوعة خلف `#if TOOLS` ولا تُترجم ضمن لعبة مُصدَّرة.

اقرأ [runtime-security.ar.md](runtime-security.ar.md) قبل أن تنشر إصدارًا يستدعي `GodotMcpRuntime.Initialize`.

<a id="set-it-up"></a>
## الإعداد

استدعِ `GodotMcpRuntime.Initialize(...)` مرة واحدة بعد وجود `SceneTree`، مثلًا من `_Ready()` في عقدة autoload:

</div>

```csharp
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;   // GodotMcpConnectionMode
using com.IvanMurzak.Godot.MCP.Runtime;      // GodotMcpRuntime
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;   // AuthOption
using Godot;

public partial class GameMcp : Node
{
    private GodotMcpRuntimeHandle? _mcp;

    public override async void _Ready()
    {
        _mcp = GodotMcpRuntime.Initialize(builder =>
        {
            builder.WithConfig(config =>
            {
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // your own server
                config.Host           = "http://localhost:8080";           // prefer loopback
                config.AuthOption     = McpServerConsts.AuthOption.token;  // require a token
                // Token: prefer GODOT_MCP_TOKEN from the environment over a value in code.
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] classes
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] classes
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] classes
        }).Build();

        await _mcp.Connect();   // explicit opt-in; retries in the background while KeepConnected is true
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

<div dir="rtl">

| الاستدعاء | ما يفعله |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | بدء الإعداد؛ يمكن أن يكون `configure` مساويًا لـ `null` للحصول على الإعداد الافتراضي بلا أدوات والمضبوط من البيئة. |
| `WithConfig(Action<GodotMcpConfig>)` | ضبط `Host` و`Token` و`ConnectionMode` و`AuthOption` في الكود. تتراكب الاستدعاءات بالترتيب. |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | تسجيل أصناف `[AiToolType]`: تجميعة كاملة، أو أنواع محددة. |
| `WithPromptsFromAssembly` / `WithPrompts` | الشيء نفسه لأصناف `[AiPromptType]`. |
| `WithResourcesFromAssembly` / `WithResources` | الشيء نفسه لأصناف `[AiResourceType]`. |
| `WithoutMainThreadDispatcher()` | تخطّي موزّع الخيط الرئيسي التلقائي (فقط إذا ثبّتّ موزّعك الخاص). |
| `ConnectWhenRunFromEditor()` | الاتصال أيضًا في لعبة شُغّلت من المحرر (معطّل افتراضيًا، انظر أدناه). |
| `WithRuntimeErrorCapture()` | التقاط الأخطاء داخل اللعبة وتسجيل `runtime-errors-*`. معطّل افتراضيًا. |
| `WithScriptExecute()` | تسجيل `script-execute` في اللعبة. معطّل افتراضيًا؛ ويحتاج إلى الاشتراك في csproj. |
| `Build()` | يُعيد `GodotMcpRuntimeHandle` غير متصل. |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | فتح الاتصال أو إغلاقه أو تفكيكه. |

يضيف `Build()` أيضًا عقدة موزّع للخيط الرئيسي إلى `SceneTree` الجاري (ما لم تُلغِ ذلك)، فيستطيع معالجو الأدوات استخدام `MainThread.Instance.Run(...)`.

<a id="server-url-and-token"></a>
## عنوان الخادم والرمز

لا تحمّل اللعبة أبدًا الإعدادات المحفوظة للمحرر. بل تأخذ الإعدادات من كودك (`WithConfig`) ومن متغيرات بيئة العملية `GODOT_MCP_*` أو من ملف `.env` للمشروع، وهذه تُقرأ مباشرةً وتتغلب على الكود. المتغيرات مدرجة في [runtime-security.ar.md](runtime-security.ar.md#environment-variables). وضع Custom على مضيف loopback مع رمز هو الإعداد الموصى به للعبة.

<a id="games-run-from-the-editor"></a>
## الألعاب المُشغَّلة من المحرر

اللعبة التي تبدأ بـ Play (F5) ترث بيئة المحرر، بما فيها `GODOT_MCP_HOST` و`GODOT_MCP_TOKEN`. لذا افتراضيًا لا يفعل `handle.Connect()` شيئًا في مثل هذه اللعبة ويُعيد `false` (ويسجّل السبب): فيبقى المحرر هو المكوّن الإضافي الوحيد الذي يتحدث إليه الوكيل. أما الألعاب المُصدَّرة والألعاب المُشغَّلة خارج المحرر فتتصل كالمعتاد. لتوصيل لعبة مُشغَّلة من المحرر أيضًا، استخدم `builder.ConnectWhenRunFromEditor()` أو `GODOT_MCP_RUNTIME_FROM_EDITOR=1`. عندئذٍ يُبقي GameDev-MCP-Server الداعم لأدوار المكوّنات الإضافية كلا الاتصالين ويوجّه استدعاءات الأدوات إلى المحرر ما دام متصلًا.

<a id="a-game-tool"></a>
## أداة للعبة

تُكتب أداة وقت التشغيل كما تُكتب أداة المحرر ([custom-tools.ar.md](custom-tools.ar.md)). علّم العائلات الخاصة باللعبة بـ `Target = ToolTarget.Runtime` حتى لا يسجّلها المحرر ويصل الوكيل إلى اللعبة حتى أثناء اتصال المحرر.

</div>

```csharp
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;              // [AiToolType], [AiTool]
using com.IvanMurzak.McpPlugin.Common;       // ToolTarget
using com.IvanMurzak.ReflectorNet.Utils;     // MainThread
using Godot;

[AiToolType(Target = ToolTarget.Runtime)]
public partial class GameMcpTools
{
    [AiTool("game-scene-tree-summary", Title = "Game Scene-Tree Summary", ReadOnlyHint = true)]
    [Description("Summary of the running game's SceneTree: current scene and root child names.")]
    public SceneTreeSummary GameSceneTreeSummary()
    {
        return MainThread.Instance.Run(() =>
        {
            var summary = new SceneTreeSummary();
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return summary;

            summary.CurrentSceneName = tree.CurrentScene?.Name ?? "<none>";
            foreach (var child in tree.Root.GetChildren())
                summary.RootChildNames.Add(child.Name);
            return summary;
        });
    }
}

public sealed class SceneTreeSummary
{
    public string CurrentSceneName { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> RootChildNames { get; set; } = new();
}
```

<div dir="rtl">

تُسجَّل الموجّهات والموارد بالطريقة نفسها: `partial class` معلَّمة بـ `[AiPromptType]` / `[AiResourceType]` وأعضاؤها معلَّمة بـ `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`. دالة المورد تُعيد `ResponseResourceContent[]` (مثلًا `ResponseResourceContent.CreateText(uri, json, "application/json")`). كل هذه الأنواع تأتي من الحزمة `com.IvanMurzak.McpPlugin`.

<a id="capturing-in-game-runtime-errors"></a>
## التقاط أخطاء وقت التشغيل داخل اللعبة

لا ترى أدوات المحرر الأخطاء التي تقع داخل لعبة قيد التشغيل، فقد يشغّل الوكيل اللعبة ويقرأ سجلات صامتة ويستنتج خطأً أنها سليمة. يسدّ `builder.WithRuntimeErrorCapture()` هذه الثغرة. فهو يثبّت ثلاث قنوات بأفضل جهد ممكن ويسجّل `runtime-errors-get` / `runtime-errors-clear`:

1. **أخطاء المحرك (Godot 4.5+)** عبر `OS.AddLogger`: أخطاء وقت التشغيل في GDScript و`push_error` / `push_warning` وأخطاء المظلِّلات، مع `file` و`line` و`function`. على Godot 4.5+ يحمل خطأ GDScript أيضًا مكدّس الاستدعاءات الكامل في `frames` (الأعمق أولًا) و`stackTrace`.
2. **استثناءات C# غير المعالَجة** (`AppDomain.CurrentDomain.UnhandledException`) مع تتبّع المكدّس المُدار كاملًا.
3. **استثناءات `Task` غير المرصودة في C#** (`TaskScheduler.UnobservedTaskException`)، تُرصد للتسجيل فقط.

على Godot الأقدم من 4.5 تكون قناة المحرك غير متاحة؛ وتظل قنوات C# تعمل. يزيل `handle.Dispose()` هذه الخطافات. تصل الرسائل وتتبّعات المكدّس الملتقطة إلى الوكيل حرفيًا وقد تحتوي مسارات أو أسماء مستخدمين أو أسرارًا، لذا فعّل الالتقاط فقط على اتصال موثوق عبر loopback مع رمز.

<a id="running-c-in-the-game"></a>
## تشغيل C# داخل اللعبة

يتيح `builder.WithScriptExecute()` للوكيل إرسال كود C# تترجمه اللعبة الجارية بـ Roslyn وتشغّله على خيطها الرئيسي، مع أنواع اللعبة نفسها وعقد autoload وشجرة المشهد. تستبعد إصدارات التصدير Roslyn، لذا يشترك ملف `.csproj` للعبة صراحةً:

</div>

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

<div dir="rtl">

يكتب `godot-cli install-plugin` الحالي مراجع حزم Code-Exec وRoslyn مع `Condition` يحترم هذه الخاصية؛ أما RQL فلا يُشحن أبدًا. بدون هذا الاشتراك يسجّل `WithScriptExecute()` تحذيرًا ولا يسجّل شيئًا.

> **هذا تنفيذ لكود عشوائي.** أي جهة تستطيع استدعاء الأدوات على الاتصال تستطيع تشغيل أي كود في عملية اللعبة. استخدمه في إصدارات التطوير فقط، على مضيف loopback مع رمز. يعمل على CoreCLR لسطح المكتب فقط: لا تستطيع NativeAOT وiOS تشغيل كود مولَّد في وقت التشغيل.

</div>
