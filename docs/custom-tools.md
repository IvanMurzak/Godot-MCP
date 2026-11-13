# Custom tools

Add your own MCP tools in the project's C# code. A tool family is a `partial class` marked `[AiToolType]`; each
tool is a method marked `[AiTool("tool-name", …)]`, with a `[Description]` on the method and on each parameter so
the LLM knows when and how to call it.

Any Godot API call (`Node`, `Resource`, `EditorInterface`, …) **must** run on the main thread. Wrap it in
`MainThread.Instance.Run(...)`; the plugin backs ReflectorNet's `MainThread` with Godot's main-thread dispatcher.

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

- Return a structured model (ReflectorNet serializes it) or `void`, not hand-formatted strings.
- Make a parameter optional with a nullable type and a default: `string? optional = null`.
- `[AiToolType(Target = ToolTarget.Runtime)]` makes a family game-only, `ToolTarget.Editor` editor-only; the
  default, `Both`, registers it everywhere. Game tools: [runtime.md](runtime.md).
- The `godot-skill-create` system tool writes such a file for you; it becomes callable after the next build.

## C# assembly reloads

Godot reloads the project's C# assembly about half a second after the built DLL changes (`script-build`, the
editor's Build button, or `dotnet build` in a terminal). The plugin is part of that assembly, so it is unloaded,
rebuilt and reconnects within a few seconds. A tool call still running then would block the unload, so the plugin
gates calls around a reload:

- once the new DLL is on disk, new calls are refused with a retryable error ("Godot is about to reload the
  project's C# assembly … Retry in a few seconds"); calls already running finish first (Godot waits up to 10 s);
- when the unload starts, the plugin stops taking calls and drops main-thread work still queued for a call; the
  server reports such a call as lost with the connection, so retry it too;
- after the reload the plugin reconnects by itself, and `script-build` delivers its result.

## Known issue: crash or hang after a reload on .NET 10

On .NET 10 the editor can crash or hang shortly after a C# assembly reload. This is a .NET runtime bug,
[dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441), fixed on the runtime's main branch
([#134983](https://github.com/dotnet/runtime/pull/134983)); the .NET 10 servicing fix is
[#136481](https://github.com/dotnet/runtime/pull/136481).

Until your runtime has the fix, set this variable for the editor process:

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

Editors started by `godot-cli` (`open`, `open --headless`, `exec`) get it automatically. If you start Godot
yourself, set it system-wide (Windows: `setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`; macOS / Linux: export it in
the shell or launcher that starts Godot) and restart the editor. Remove it once your .NET 10 runtime includes the
fix.
