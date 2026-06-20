/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// One stack frame of a captured GDScript (or other script-language) backtrace — the structured, deep
    /// call-stack frame surfaced on a <see cref="RuntimeError"/> (issue #163). It is the pure-managed
    /// materialization of a single frame of Godot 4.5+'s <c>ScriptBacktrace</c>: the live Godot object is
    /// NON-thread-safe and is read on the originating (possibly non-main) thread inside the logger callback,
    /// so its <c>GetFrameFunction</c>/<c>GetFrameFile</c>/<c>GetFrameLine</c> accessors are copied into these
    /// plain primitives BEFORE the record crosses any thread boundary — no live Godot handle is ever stored
    /// or forwarded.
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host and ships into a game build. Frames are ordered innermost-first (frame 0 is where the error was
    /// raised), mirroring Godot's <c>ScriptBacktrace</c> frame ordering.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("One frame of a captured script-language (e.g. GDScript) backtrace: function, file, line.")]
    public class RuntimeErrorFrame
    {
        [JsonInclude, JsonPropertyName("function")]
        [Description("The function called at this stack frame (e.g. '_process'); empty when the engine omitted it.")]
        public string Function { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("file")]
        [Description("The source file of this stack frame's call site (e.g. 'res://scripts/player.gd'); empty " +
            "when the engine omitted it.")]
        public string File { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("line")]
        [Description("The 1-based line number of this stack frame's call site, or -1 when unknown.")]
        public int Line { get; set; } = -1;

        public RuntimeErrorFrame() { }

        public RuntimeErrorFrame(string? function, string? file, int line)
        {
            Function = function ?? string.Empty;
            File = file ?? string.Empty;
            Line = line;
        }

        public override string ToString()
        {
            var loc = string.IsNullOrEmpty(File)
                ? string.Empty
                : (Line >= 0 ? $" {File}:{Line}" : $" {File}");
            var fn = string.IsNullOrEmpty(Function) ? "<unknown>" : Function;
            return $"at {fn}{loc}";
        }
    }
}
