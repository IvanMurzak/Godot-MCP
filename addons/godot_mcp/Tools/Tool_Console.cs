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
    /// Console / log tool family (<c>console-*</c>) — the Godot analog of Unity-MCP's <c>Tool_Console</c>.
    /// Each tool method lives in its own partial-class file (GetLogs / ClearLogs) and reads/clears the
    /// process-wide <see cref="GodotLogCollector"/> that the plugin's logging path feeds.
    ///
    /// <para>
    /// Unlike Unity (which taps <c>Application.logMessageReceivedThreaded</c> to capture every engine
    /// line), Godot's C# API exposes no global managed log hook, so the collector records the plugin's own
    /// editor activity (<c>GD.Print</c>/<c>GD.PushWarning</c>/<c>GD.PushError</c>) — see
    /// <see cref="GodotLogCollector"/> for the rationale.
    /// </para>
    ///
    /// This family is engine-runtime logic with no Godot editor-API surface (it only touches the
    /// pure-managed collector), so it intentionally lives OUTSIDE <c>#if TOOLS</c> — it is discovered by
    /// the McpPlugin assembly scanner and is unit-testable in the plain xUnit host.
    /// </summary>
    [AiToolType]
    public partial class Tool_Console
    {
    }
}
