[English](runtime.md) · [中文](runtime.zh-CN.md) · [日本語](runtime.ja.md) · [Español](runtime.es.md) · **فارسی** · [हिन्दी](runtime.hi.md) · [العربية](runtime.ar.md)

<div dir="rtl">

# استفاده در Runtime (درون بازی)

پلاگین ویرایشگر، **ویرایشگر Godot** را وصل می‌کند. Godot-MCP می‌تواند درون یک **بازیِ در حال اجرا یا اکسپورت‌شده** (debug یا release) هم اجرا شود، تا یک LLM بتواند وضعیت زنده‌ی بازی را بخواند و هدایت کند: برای مثال یک بازی شطرنج که حرکت‌های ربات آن از طریق دو ابزاری که شما می‌نویسید از یک LLM می‌آید.

حالت Runtime عمداً با ویرایشگر فرق دارد:

- ‏**هرگز خودش وصل نمی‌شود.** کد شما تصمیم می‌گیرد که آیا و کی `Connect()` را فراخوانی کند.
- ‏**به طور پیش‌فرض هیچ چیزی ارائه نمی‌کند.** هر ابزار (`[AiToolType]`)، پرامپت (`[AiPromptType]`) و ریسورس (`[AiResourceType]`) را خودتان ثبت می‌کنید؛ هر کدام اختیاری است. ابزارهای ویرایشگرِ پلاگین پشت `#if TOOLS` هستند و در بازیِ اکسپورت‌شده کامپایل نمی‌شوند.

پیش از انتشار بیلدی که `GodotMcpRuntime.Initialize` را فراخوانی می‌کند، [runtime-security.fa.md](runtime-security.fa.md) را بخوانید.

<a id="set-it-up"></a>
## راه‌اندازی

وقتی یک `SceneTree` وجود دارد، `GodotMcpRuntime.Initialize(...)` را یک بار فراخوانی کنید، برای مثال از `_Ready()`ِ یک autoload:

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

| فراخوانی | چه می‌کند |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | شروع پیکربندی؛ `configure` می‌تواند `null` باشد برای حالت پیش‌فرضِ بدون ابزار که از محیط پیکربندی می‌شود. |
| `WithConfig(Action<GodotMcpConfig>)` | تنظیم `Host`، `Token`، `ConnectionMode`، `AuthOption` در کد. فراخوانی‌ها به ترتیب ترکیب می‌شوند. |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | ثبت کلاس‌های `[AiToolType]`: یک اسمبلی کامل، یا تایپ‌های مشخص. |
| `WithPromptsFromAssembly` / `WithPrompts` | همین کار برای کلاس‌های `[AiPromptType]`. |
| `WithResourcesFromAssembly` / `WithResources` | همین کار برای کلاس‌های `[AiResourceType]`. |
| `WithoutMainThreadDispatcher()` | صرف‌نظر از dispatcher خودکار نخ اصلی (فقط اگر dispatcher خودتان را نصب می‌کنید). |
| `ConnectWhenRunFromEditor()` | اتصال در بازی‌ای که از ویرایشگر اجرا شده هم (پیش‌فرض خاموش، پایین‌تر را ببینید). |
| `WithRuntimeErrorCapture()` | ثبت خطاهای درون بازی و ثبت `runtime-errors-*`. پیش‌فرض خاموش. |
| `WithScriptExecute()` | ثبت `script-execute` در بازی. پیش‌فرض خاموش؛ به فعال‌سازی در csproj نیاز دارد. |
| `Build()` | یک `GodotMcpRuntimeHandle` برمی‌گرداند که وصل نیست. |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | باز کردن، بستن یا از بین بردن اتصال. |

‏`Build()` همچنین یک نود dispatcher نخ اصلی به `SceneTree`ِ در حال اجرا اضافه می‌کند (مگر اینکه انصراف دهید)، تا handlerهای ابزار بتوانند از `MainThread.Instance.Run(...)` استفاده کنند.

<a id="server-url-and-token"></a>
## ‏URL سرور و توکن

بازی هرگز پیکربندی ذخیره‌شده‌ی ویرایشگر را بارگذاری نمی‌کند. تنظیمات را از کد شما (`WithConfig`) و از متغیرهای محیطی پروسه `GODOT_MCP_*` یا یک فایل `.env` پروژه می‌گیرد، که به صورت زنده خوانده می‌شوند و بر کد اولویت دارند. متغیرها در [runtime-security.fa.md](runtime-security.fa.md#environment-variables) فهرست شده‌اند. حالت Custom روی یک میزبان loopback با توکن، پیکربندی توصیه‌شده برای بازی است.

<a id="games-run-from-the-editor"></a>
## بازی‌هایی که از ویرایشگر اجرا می‌شوند

بازی‌ای که با Play (F5) اجرا شود محیط ویرایشگر، از جمله `GODOT_MCP_HOST` و `GODOT_MCP_TOKEN`، را به ارث می‌برد. بنابراین به طور پیش‌فرض `handle.Connect()` در چنین بازی‌ای کاری نمی‌کند و `false` برمی‌گرداند (و دلیلش را در لاگ می‌نویسد): ویرایشگر تنها پلاگینی می‌ماند که عامل با آن صحبت می‌کند. بازی‌های اکسپورت‌شده و بازی‌هایی که بیرون از ویرایشگر اجرا می‌شوند به طور عادی وصل می‌شوند. برای اینکه بازیِ اجراشده از ویرایشگر هم وصل شود، از `builder.ConnectWhenRunFromEditor()` یا `GODOT_MCP_RUNTIME_FROM_EDITOR=1` استفاده کنید. در این صورت یک GameDev-MCP-Server با نقش‌های پلاگین هر دو اتصال را نگه می‌دارد و تا وقتی ویرایشگر وصل است فراخوانی ابزارها را به ویرایشگر می‌فرستد.

<a id="a-game-tool"></a>
## یک ابزار بازی

ابزار Runtime مثل ابزار ویرایشگر نوشته می‌شود ([custom-tools.fa.md](custom-tools.fa.md)). خانواده‌های مخصوص بازی را با `Target = ToolTarget.Runtime` علامت بزنید تا ویرایشگر آن‌ها را ثبت نکند و عامل حتی وقتی ویرایشگر وصل است به بازی برسد.

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

پرامپت‌ها و ریسورس‌ها به همین شکل ثبت می‌شوند: یک `partial class` با اتریبیوت `[AiPromptType]` / `[AiResourceType]` و اعضایی با اتریبیوت `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`. متد ریسورس `ResponseResourceContent[]` برمی‌گرداند (برای مثال `ResponseResourceContent.CreateText(uri, json, "application/json")`). همه‌ی این تایپ‌ها از پکیج `com.IvanMurzak.McpPlugin` می‌آیند.

<a id="capturing-in-game-runtime-errors"></a>
## ثبت خطاهای زمان اجرای درون بازی

ابزارهای ویرایشگر خطاهایی را که درون بازیِ در حال اجرا رخ می‌دهند نمی‌بینند، پس عامل ممکن است بازی را اجرا کند، لاگ‌های ساکت را بخواند و به اشتباه نتیجه بگیرد که بازی سالم است. `builder.WithRuntimeErrorCapture()` این شکاف را می‌بندد. سه کانال best-effort نصب می‌کند و `runtime-errors-get` / `runtime-errors-clear` را ثبت می‌کند:

1. ‏**خطاهای موتور (Godot 4.5+‎)** از طریق `OS.AddLogger`: خطاهای زمان اجرای GDScript، `push_error` / `push_warning` و خطاهای شیدر، همراه با `file`، `line` و `function`. در Godot 4.5+‎ یک خطای GDScript کل پشته‌ی فراخوانی را هم در `frames` (درونی‌ترین اول) و `stackTrace` دارد.
2. ‏**استثناهای مدیریت‌نشده‌ی C#** (`AppDomain.CurrentDomain.UnhandledException`) با stack trace کامل managed.
3. ‏**استثناهای مشاهده‌نشده‌ی `Task` در C#** (`TaskScheduler.UnobservedTaskException`)، که فقط برای لاگ مشاهده می‌شوند.

در Godot پایین‌تر از 4.5 کانال موتور در دسترس نیست؛ کانال‌های C# همچنان کار می‌کنند. `handle.Dispose()` هوک‌ها را برمی‌دارد. پیام‌ها و stack traceهای ثبت‌شده عیناً به عامل می‌رسند و ممکن است شامل مسیرها، نام کاربران یا رازها باشند، پس ثبت خطا را فقط روی یک اتصال مطمئن loopback + توکن فعال کنید.

<a id="running-c-in-the-game"></a>
## اجرای C# درون بازی

‏`builder.WithScriptExecute()` به عامل اجازه می‌دهد کد C# بفرستد که بازیِ در حال اجرا آن را با Roslyn کامپایل و روی نخ اصلی خود اجرا می‌کند، با تایپ‌ها، autoloadها و درخت صحنه‌ی خود بازی. بیلدهای اکسپورت Roslyn را کنار می‌گذارند، پس فایل `.csproj` بازی باید آن را فعال کند:

</div>

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

<div dir="rtl">

یک `godot-cli install-plugin` به‌روز، ارجاع‌های پکیج Code-Exec و Roslyn را با یک `Condition` می‌نویسد که به این ویژگی احترام می‌گذارد؛ RQL هرگز همراه بازی منتشر نمی‌شود. بدون این فعال‌سازی، `WithScriptExecute()` یک هشدار در لاگ می‌نویسد و چیزی ثبت نمی‌کند.

> **این اجرای کد دلخواه است.** هر کسی که بتواند روی اتصال ابزار فراخوانی کند می‌تواند هر کدی را در پروسه‌ی بازی اجرا کند. فقط در بیلدهای توسعه، روی یک میزبان loopback با توکن از آن استفاده کنید. فقط CoreCLR دسکتاپ: NativeAOT و iOS نمی‌توانند کدی را که در زمان اجرا تولید شده اجرا کنند.

</div>
