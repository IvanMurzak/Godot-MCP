[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · [日本語](custom-tools.ja.md) · [Español](custom-tools.es.md) · [فارسی](custom-tools.fa.md) · **हिन्दी** · [العربية](custom-tools.ar.md)

# Custom tools

प्रोजेक्ट के C# code में अपने MCP tools जोड़ें। Tool family एक `partial class` है जिस पर `[AiToolType]` लगा होता है; हर tool एक method है जिस पर `[AiTool("tool-name", …)]` लगा होता है, और method पर तथा हर parameter पर एक `[Description]` होता है ताकि LLM जाने कि इसे कब और कैसे call करना है।

कोई भी Godot API call (`Node`, `Resource`, `EditorInterface`, …) main thread पर ही चलनी **चाहिए**। उसे `MainThread.Instance.Run(...)` में wrap करें; plugin ReflectorNet के `MainThread` को Godot के main-thread dispatcher से चलाता है।

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
        // जो काम Godot API को नहीं छूता, वह यहाँ background thread पर चल सकता है।

        return MainThread.Instance.Run(() =>
        {
            // EditorInterface / Node / Resource को यहाँ, main thread पर छुएँ।
            return "[Success] Operation completed.";
        });
    }
}
```

- हाथ से format की गई strings नहीं, बल्कि एक structured model (ReflectorNet उसे serialize करता है) या `void` लौटाएँ।
- Parameter को nullable type और default से वैकल्पिक बनाएँ: `string? optional = null`।
- `[AiToolType(Target = ToolTarget.Runtime)]` किसी family को सिर्फ़ game के लिए बनाता है, `ToolTarget.Editor` सिर्फ़ editor के लिए; default, `Both`, उसे हर जगह register करता है। Game tools: [runtime.md](runtime.hi.md)।
- `godot-skill-create` system tool आपके लिए ऐसी file लिखता है; अगले build के बाद यह callable हो जाती है।

<a id="c-assembly-reloads"></a>
## C# assembly reloads

Built DLL बदलने के लगभग आधे सेकंड बाद Godot प्रोजेक्ट की C# assembly reload करता है (`script-build`, editor का Build button, या terminal में `dotnet build`)। Plugin उसी assembly का हिस्सा है, इसलिए वह unload होता है, फिर से बनता है और कुछ सेकंड में फिर से connect हो जाता है। उस समय चल रही tool call unload को रोक देती, इसलिए plugin reload के आसपास calls को gate करता है:

- नई DLL disk पर आते ही नई calls retryable error के साथ नामंज़ूर होती हैं ("Godot is about to reload the project's C# assembly … Retry in a few seconds"); पहले से चल रही calls पहले पूरी होती हैं (Godot 10 s तक इंतज़ार करता है);
- unload शुरू होने पर plugin calls लेना बंद कर देता है और किसी call के लिए अब भी queue में पड़ा main-thread काम छोड़ देता है; server ऐसी call को connection के साथ खोई हुई रिपोर्ट करता है, इसलिए उसे भी retry करें;
- reload के बाद plugin अपने आप फिर से connect होता है, और `script-build` अपना result देता है।

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## Known issue: .NET 10 पर reload के बाद crash या hang

.NET 10 पर C# assembly reload के कुछ देर बाद editor crash या hang हो सकता है। यह .NET runtime का bug है, [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441), जो runtime की main branch पर fix हो चुका है ([#134983](https://github.com/dotnet/runtime/pull/134983)); .NET 10 का servicing fix [#136481](https://github.com/dotnet/runtime/pull/136481) है।

जब तक आपके runtime में fix नहीं आता, editor process के लिए यह variable सेट करें:

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

`godot-cli` द्वारा शुरू किए editors (`open`, `open --headless`, `exec`) को यह अपने आप मिलता है। अगर आप Godot खुद शुरू करते हैं, तो इसे system-wide सेट करें (Windows: `setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`; macOS / Linux: Godot शुरू करने वाले shell या launcher में export करें) और editor restart करें। आपके .NET 10 runtime में fix आ जाने पर इसे हटा दें।
