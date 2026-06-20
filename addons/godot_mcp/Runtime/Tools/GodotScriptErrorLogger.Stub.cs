/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Godot < 4.5 fallback for the engine-error logger bridge. On the addon's SDK floor (Godot.NET.Sdk/4.3.0,
// which the required `dotnet build (.NET 8)` CI gate pins) Godot.Logger / OS.AddLogger do not exist, so the
// real bridge (GodotScriptErrorLogger.cs, guarded by #if GODOT4_5_OR_GREATER) is compiled OUT and this stub
// is compiled IN. TryInstall is a no-op that installs nothing, so ScriptErrorCapture.Current stays null:
// passive engine-log capture is unavailable and script-validate falls back to the per-file Reload()
// error-code probe (Coarse fidelity).
//
// Like its 4.5+ counterpart this stub lives under Runtime/ and is NOT gated by #if TOOLS — it ships into a
// game build so the in-game runtime error-capture (Runtime/RuntimeErrorCapture.cs) compiles and degrades
// gracefully on a < 4.5 export (TryInstall returns null → no engine-error capture, but the C# unhandled-
// exception hooks still work; see issue #160).
#if !GODOT4_5_OR_GREATER
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// No-op stub of the engine-error logger bridge for Godot &lt; 4.5 (no <c>OS.AddLogger</c> /
    /// <c>Godot.Logger</c>). Keeps <c>Tool_Script.Validate.cs</c> compiling on the SDK floor; with no live
    /// capture installed, <see cref="ScriptErrorCapture.Current"/> stays null and the tool uses the coarse
    /// per-file <c>Reload()</c> probe instead.
    /// </summary>
    public static class GodotScriptErrorLoggerBridge
    {
        /// <summary>No-op on Godot &lt; 4.5; always returns null. (Signature matches the 4.5+ bridge — the
        /// editor passive-log path.)</summary>
        public static ScriptErrorCapture? TryInstall(GodotLogCollector collector) => null;

        /// <summary>No-op on Godot &lt; 4.5; always returns null. (Signature matches the 4.5+ bridge — the
        /// in-game runtime structured-capture path. On the floor there is no engine error stream to hook, so
        /// the runtime degrades to C#-exception capture only; see RuntimeErrorCapture.)</summary>
        public static ScriptErrorCapture? TryInstall(ScriptErrorCapture capture) => null;

        /// <summary>
        /// No-op on Godot &lt; 4.5 (nothing was installed, so there is nothing to remove). Matches the 4.5+
        /// bridge signature so <c>GodotMcpPlugin.Teardown</c> compiles on the SDK floor. Idempotent.
        /// </summary>
        public static void Uninstall() { }
    }
}
#endif
