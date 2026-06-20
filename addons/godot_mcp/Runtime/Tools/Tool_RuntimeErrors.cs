/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// In-game runtime-error tool family (<c>runtime-errors-*</c>) — surfaces errors raised inside a RUNNING
    /// game (GDScript runtime errors, <c>push_error</c>/<c>push_warning</c>, shader errors, and C# unhandled /
    /// unobserved-Task exceptions) to the agent, closing the gap (issue #160) where in-game runtime errors
    /// were invisible (only editor-side script errors were captured).
    ///
    /// <para>
    /// Each tool method lives in its own partial-class file (Get / Clear) and reads/clears the process-wide
    /// <see cref="RuntimeErrorCollector"/> that <c>RuntimeErrorCapture</c> feeds. When the in-game runtime was
    /// NOT started with error capture enabled (the default-OFF posture), the collector is null and
    /// <c>runtime-errors-get</c> returns <c>available:false</c> so the agent does not read silence as health.
    /// </para>
    ///
    /// This family is engine-runtime logic with no Godot editor-API surface (it only touches the pure-managed
    /// collector), so it lives OUTSIDE <c>#if TOOLS</c> — it ships into a game build, is discovered by the
    /// McpPlugin assembly scanner, and is unit-testable in the plain xUnit host. Unlike the editor tool
    /// families it is opt-in for the running game: a developer registers it via
    /// <c>GodotMcpRuntime.Initialize(b =&gt; b.WithRuntimeErrorTool())</c> (or the whole addon assembly).
    /// </summary>
    [AiToolType]
    public partial class Tool_RuntimeErrors
    {
    }
}
