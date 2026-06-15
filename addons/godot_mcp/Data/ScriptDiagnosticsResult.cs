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
    /// Structured result of the <c>script-validate</c> tool — the answer to "is this script (or the whole
    /// project) free of parse/compile errors?". Carries an explicit <see cref="Ok"/> verdict, the set of
    /// scanned script paths, the captured <see cref="Diagnostics"/>, and a <see cref="Fidelity"/> note that
    /// tells the agent how precise the result is on the live Godot version (4.5+ gives line+message; older
    /// versions give a coarse per-file pass/fail).
    ///
    /// <para>
    /// This exists to close the gap reported on Discord (plugin 0.6.0): "the plugin lacks a way to
    /// communicate parse errors in GDScript files ... Claude Code doesn't get any feedback on parse errors,
    /// so it thinks the game is running without errors." An agent can now call <c>script-validate</c> and
    /// branch on <see cref="Ok"/> instead of guessing from a screenshot.
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host.
    /// </summary>
    [System.Serializable]
    [Description("Result of 'script-validate': an Ok verdict, the scanned script paths, captured " +
        "diagnostics, and a fidelity note about how precise the diagnostics are on the live Godot version.")]
    public class ScriptDiagnosticsResult
    {
        [JsonInclude, JsonPropertyName("ok")]
        [Description("True when no error-severity diagnostics were found across every scanned script.")]
        public bool Ok { get; set; } = true;

        [JsonInclude, JsonPropertyName("scannedCount")]
        [Description("Number of script files that were validated.")]
        public int ScannedCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("errorCount")]
        [Description("Number of error-severity diagnostics in 'diagnostics'.")]
        public int ErrorCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("warningCount")]
        [Description("Number of warning-severity diagnostics in 'diagnostics'.")]
        public int WarningCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("scannedPaths")]
        [Description("res:// paths of the script files that were validated.")]
        public List<string> ScannedPaths { get; set; } = new();

        [JsonInclude, JsonPropertyName("diagnostics")]
        [Description("The captured diagnostics (errors first, then warnings). Empty when 'ok' is true.")]
        public List<ScriptDiagnostic> Diagnostics { get; set; } = new();

        [JsonInclude, JsonPropertyName("fidelity")]
        [Description("How precise the diagnostics are on the live Godot version: 'Precise' (4.5+: line + " +
            "message captured) or 'Coarse' (Godot < 4.5: per-file pass/fail with the engine error code, no line).")]
        public ScriptDiagnosticsFidelity Fidelity { get; set; } = ScriptDiagnosticsFidelity.Coarse;

        [JsonInclude, JsonPropertyName("truncated")]
        [Description("True when a full-project scan hit the file cap and only the first N '.gd' files were " +
            "validated — so 'ok' is NOT a guaranteed all-clear for the whole project. Validate a specific " +
            "'scriptPath' (or sub-tree) to cover the rest. Always false for a single-path validation.")]
        public bool Truncated { get; set; } = false;

        [JsonInclude, JsonPropertyName("note")]
        [Description("Human-readable summary, e.g. 'No script errors found (12 scanned).' or '2 errors in 1 " +
            "script.'. Includes a fidelity caveat on older Godot versions and a truncation hint when the " +
            "full-scan cap was hit.")]
        public string Note { get; set; } = string.Empty;

        public ScriptDiagnosticsResult() { }

        public override string ToString() => Note;
    }

    /// <summary>
    /// How precise a <see cref="ScriptDiagnosticsResult"/> is, driven by the live Godot version's logging
    /// surface. <see cref="Precise"/> means line + message were captured (Godot 4.5+ <c>Logger</c> hook);
    /// <see cref="Coarse"/> means only a per-file pass/fail + engine error code was available (Godot &lt; 4.5).
    /// </summary>
    [Description("Diagnostic precision: Precise (line + message) or Coarse (per-file pass/fail, no line).")]
    public enum ScriptDiagnosticsFidelity
    {
        [Description("Per-file pass/fail with the engine error code, no line (Godot < 4.5).")]
        Coarse = 0,
        [Description("Line + message captured via the engine Logger hook (Godot 4.5+).")]
        Precise = 1,
    }
}
