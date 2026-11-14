[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · [日本語](custom-tools.ja.md) · [Español](custom-tools.es.md) · [فارسی](custom-tools.fa.md) · [हिन्दी](custom-tools.hi.md) · **العربية**

<div dir="rtl">

# الأدوات المخصّصة

أضِف أدوات MCP خاصة بك في كود C# للمشروع. عائلة الأدوات هي `partial class` معلَّمة بـ `[AiToolType]`؛ وكل أداة دالة معلَّمة بـ `[AiTool("tool-name", …)]`، مع `[Description]` على الدالة وعلى كل معامل حتى يعرف النموذج اللغوي متى يستدعيها وكيف.

أي استدعاء لواجهة Godot البرمجية (`Node` و`Resource` و`EditorInterface` وغيرها) **يجب** أن يجري على الخيط الرئيسي. غلّفه في `MainThread.Instance.Run(...)`؛ إذ يربط المكوّن الإضافي `MainThread` الخاص بـ ReflectorNet بموزّع الخيط الرئيسي في Godot.

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

- أعِد نموذجًا منظّمًا (يسلسله ReflectorNet) أو `void`، لا سلاسل نصية منسّقة يدويًا.
- اجعل المعامل اختياريًا بنوع يقبل null وقيمة افتراضية: `string? optional = null`.
- يجعل `[AiToolType(Target = ToolTarget.Runtime)]` العائلةَ خاصة باللعبة، و`ToolTarget.Editor` خاصة بالمحرر؛ أما الافتراضي `Both` فيسجّلها في كل مكان. أدوات اللعبة: [runtime.ar.md](runtime.ar.md).
- تكتب أداة النظام `godot-skill-create` مثل هذا الملف نيابةً عنك؛ ويصبح قابلًا للاستدعاء بعد البناء التالي.

<a id="c-assembly-reloads"></a>
## إعادة تحميل تجميعة C#

يعيد Godot تحميل تجميعة C# للمشروع بعد نحو نصف ثانية من تغيّر ملف DLL المبني (`script-build`، أو زر Build في المحرر، أو `dotnet build` في الطرفية). المكوّن الإضافي جزء من تلك التجميعة، لذا يُفرَّغ ويُعاد بناؤه ويعيد الاتصال خلال ثوانٍ قليلة. أي استدعاء أداة لا يزال قيد التشغيل حينها سيمنع التفريغ، لذا يضبط المكوّن الإضافي الاستدعاءات حول إعادة التحميل:

- بمجرد وجود ملف DLL الجديد على القرص، تُرفض الاستدعاءات الجديدة بخطأ قابل لإعادة المحاولة ("Godot is about to reload the project's C# assembly … Retry in a few seconds")؛ وتنتهي الاستدعاءات الجارية أولًا (ينتظر Godot حتى 10 ثوانٍ)؛
- عند بدء التفريغ يتوقف المكوّن الإضافي عن قبول الاستدعاءات ويُسقط أعمال الخيط الرئيسي التي لا تزال في الطابور لاستدعاء ما؛ ويُبلغ الخادم عن مثل هذا الاستدعاء بأنه فُقد مع الاتصال، فأعد محاولته أيضًا؛
- بعد إعادة التحميل يعيد المكوّن الإضافي الاتصال من تلقاء نفسه، ويسلّم `script-build` نتيجته.

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## مشكلة معروفة: انهيار أو تجمّد بعد إعادة التحميل على .NET 10

على .NET 10 قد ينهار المحرر أو يتجمّد بُعيد إعادة تحميل تجميعة C#. هذا خطأ في بيئة تشغيل .NET، [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441)، أُصلح في الفرع الرئيسي لبيئة التشغيل ([#134983](https://github.com/dotnet/runtime/pull/134983))؛ وإصلاح الصيانة لـ .NET 10 هو [#136481](https://github.com/dotnet/runtime/pull/136481).

إلى أن تتضمن بيئة التشغيل لديك الإصلاح، اضبط هذا المتغير لعملية المحرر:

</div>

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

<div dir="rtl">

المحررات التي يشغّلها `godot-cli` (`open` و`open --headless` و`exec`) تحصل عليه تلقائيًا. إذا شغّلت Godot بنفسك، فاضبطه على مستوى النظام (Windows: ‏`setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`؛ macOS / Linux: صدّره في الصدفة أو المشغّل الذي يبدأ Godot) وأعد تشغيل المحرر. أزِله بمجرد أن تتضمن بيئة تشغيل .NET 10 لديك الإصلاح.

</div>
