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
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Structured result of the <c>runtime-errors-get</c> tool — the answer to "did the RUNNING game raise
    /// any new errors since I last looked?". The cornerstone of an unattended "keep fixing until no errors"
    /// loop for real gameplay/runtime bugs: poll with the largest <see cref="RuntimeError.Sequence"/> seen
    /// so far and only NEWER errors come back.
    ///
    /// <para>
    /// <see cref="Available"/> reports whether in-game runtime error capture is actually wired up — it is
    /// false when the in-game runtime was never initialized with capture enabled (the default-OFF posture),
    /// so an agent can distinguish "capture is on and the game is clean" (<c>available:true, count:0</c>)
    /// from "capture was never enabled, so absence of errors proves nothing" (<c>available:false</c>).
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host and ships into a game build.
    /// </summary>
    [System.Serializable]
    [Description("Result of 'runtime-errors-get': whether capture is available, the captured runtime errors " +
        "newer than the requested sequence, counts, and the highest sequence seen (poll with it next time).")]
    public class RuntimeErrorsResult
    {
        [JsonInclude, JsonPropertyName("available")]
        [Description("True when in-game runtime error capture is wired up (the runtime was initialized with " +
            "capture enabled). When false, the runtime was never started with capture, so an empty 'errors' " +
            "list proves nothing — enable capture via GodotMcpRuntime's WithRuntimeErrorCapture().")]
        public bool Available { get; set; } = false;

        [JsonInclude, JsonPropertyName("ok")]
        [Description("True when no error-severity runtime errors are present in 'errors' (warnings do not " +
            "flip this to false). Always true when capture is unavailable or the buffer is empty.")]
        public bool Ok { get; set; } = true;

        [JsonInclude, JsonPropertyName("count")]
        [Description("Number of runtime errors returned in 'errors' (after the 'sinceSequence' filter and the " +
            "'maxEntries' cap).")]
        public int Count { get; set; } = 0;

        [JsonInclude, JsonPropertyName("errorCount")]
        [Description("Number of error-severity entries in 'errors' (managed faults + engine Error/Script/Shader).")]
        public int ErrorCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("warningCount")]
        [Description("Number of warning-severity entries in 'errors' (engine Warning).")]
        public int WarningCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("highestSequence")]
        [Description("The highest sequence number across ALL captured runtime errors (not just the returned " +
            "page). Pass this as 'sinceSequence' on the next call to poll only newer errors. 0 when none " +
            "have ever been captured.")]
        public long HighestSequence { get; set; } = 0;

        [JsonInclude, JsonPropertyName("truncated")]
        [Description("True when more matching errors existed than the 'maxEntries' cap returned. The NEWEST " +
            "are kept; call again (the buffer is bounded, so very old errors may already have been evicted).")]
        public bool Truncated { get; set; } = false;

        [JsonInclude, JsonPropertyName("errors")]
        [Description("The captured runtime errors newer than 'sinceSequence', oldest-first within the page so " +
            "they read in chronological order. Empty when 'ok' is true / capture is unavailable.")]
        public List<RuntimeError> Errors { get; set; } = new();

        [JsonInclude, JsonPropertyName("note")]
        [Description("Human-readable summary, e.g. 'No new runtime errors (capture active).' or '2 runtime " +
            "error(s) since sequence 5.' or 'Runtime error capture is not enabled in this game.'.")]
        public string Note { get; set; } = string.Empty;

        public RuntimeErrorsResult() { }

        public override string ToString() => Note;
    }
}
