[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · [日本語](custom-tools.ja.md) · [Español](custom-tools.es.md) · **فارسی** · [हिन्दी](custom-tools.hi.md) · [العربية](custom-tools.ar.md)

<div dir="rtl">

# ابزارهای سفارشی

ابزارهای MCP خودتان را در کد C# پروژه اضافه کنید. یک خانواده‌ی ابزار، یک `partial class` با اتریبیوت `[AiToolType]` است؛ هر ابزار متدی با اتریبیوت `[AiTool("tool-name", …)]` است، با یک `[Description]` روی متد و روی هر پارامتر تا LLM بداند کی و چگونه آن را فراخوانی کند.

هر فراخوانی API در Godot (`Node`، `Resource`، `EditorInterface`، …) **باید** روی نخ اصلی اجرا شود. آن را در `MainThread.Instance.Run(...)` بپیچید؛ پلاگین `MainThread`ِ ReflectorNet را با dispatcher نخ اصلی Godot پشتیبانی می‌کند.

</div>

```csharp
[AiToolType]
public partial class Tool_MyFeature
{
    [AiTool("my-custom-task", Title = "Do a custom task")]
    [Description("Explain to the LLM what this does and when to call it.")]
    public string CustomTask
    (
        [Description("Explain to the LLM what this parameter is.")]
        string inputData
    )
    {
        // Work that does not touch the Godot API can run here, on a background thread.

        return MainThread.Instance.Run(() =>
        {
            // Touch EditorInterface / Node / Resource here, on the main thread.
            return "[Success] Operation completed.";
        });
    }
}
```

<div dir="rtl">

- یک مدل ساختاریافته (که ReflectorNet آن را سریال‌سازی می‌کند) یا `void` برگردانید، نه رشته‌هایی که دستی قالب‌بندی شده‌اند.
- برای اختیاری کردن یک پارامتر از تایپ nullable و یک مقدار پیش‌فرض استفاده کنید: `string? optional = null`.
- ‏`[AiToolType(Target = ToolTarget.Runtime)]` یک خانواده را فقط مخصوص بازی می‌کند و `ToolTarget.Editor` فقط مخصوص ویرایشگر؛ پیش‌فرض، `Both`، آن را همه‌جا ثبت می‌کند. ابزارهای بازی: [runtime.fa.md](runtime.fa.md).
- ابزار سیستمی `godot-skill-create` چنین فایلی را برایتان می‌نویسد؛ پس از بیلد بعدی قابل فراخوانی می‌شود.

<a id="c-assembly-reloads"></a>
## بارگذاری مجدد اسمبلی C#

‏Godot حدود نیم ثانیه پس از تغییر DLL بیلدشده (`script-build`، دکمه‌ی Build ویرایشگر، یا `dotnet build` در ترمینال) اسمبلی C# پروژه را دوباره بارگذاری می‌کند. پلاگین بخشی از همان اسمبلی است، پس ظرف چند ثانیه unload، دوباره ساخته و دوباره وصل می‌شود. فراخوانی ابزاری که در آن لحظه هنوز در حال اجراست جلوی unload را می‌گیرد، پس پلاگین فراخوانی‌ها را در اطراف بارگذاری مجدد کنترل می‌کند:

- وقتی DLL جدید روی دیسک قرار گرفت، فراخوانی‌های جدید با یک خطای قابل تکرار رد می‌شوند ("Godot is about to reload the project's C# assembly … Retry in a few seconds")؛ فراخوانی‌هایی که در حال اجرا هستند ابتدا تمام می‌شوند (Godot تا ۱۰ ثانیه صبر می‌کند)؛
- وقتی unload شروع شد، پلاگین دیگر فراخوانی نمی‌پذیرد و کارهای نخ اصلی که هنوز برای یک فراخوانی در صف هستند را دور می‌ریزد؛ سرور چنین فراخوانی‌ای را همراه با اتصال، ازدست‌رفته گزارش می‌کند، پس آن را هم دوباره امتحان کنید؛
- پس از بارگذاری مجدد، پلاگین خودش دوباره وصل می‌شود و `script-build` نتیجه‌اش را تحویل می‌دهد.

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## مشکل شناخته‌شده: کرش یا هنگ پس از بارگذاری مجدد روی .NET 10

روی .NET 10 ویرایشگر ممکن است کمی پس از بارگذاری مجدد اسمبلی C# کرش کند یا هنگ کند. این یک باگ runtimeِ .NET است، [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441)، که در شاخه‌ی main آن runtime رفع شده است ([#134983](https://github.com/dotnet/runtime/pull/134983))؛ اصلاحیه‌ی servicing برای .NET 10 ‏[#136481](https://github.com/dotnet/runtime/pull/136481) است.

تا وقتی runtime شما این اصلاحیه را ندارد، این متغیر را برای پروسه‌ی ویرایشگر تنظیم کنید:

</div>

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

<div dir="rtl">

ویرایشگرهایی که `godot-cli` اجرا می‌کند (`open`، `open --headless`، `exec`) آن را خودکار دریافت می‌کنند. اگر Godot را خودتان اجرا می‌کنید، آن را در سطح سیستم تنظیم کنید (Windows: ‏`setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`؛ macOS / Linux: آن را در shell یا لانچری که Godot را اجرا می‌کند export کنید) و ویرایشگر را دوباره اجرا کنید. وقتی runtimeِ .NET 10 شما این اصلاحیه را داشت، آن را حذف کنید.

</div>
