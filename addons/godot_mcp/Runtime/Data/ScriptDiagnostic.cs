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
    /// One script diagnostic surfaced by the <c>script-validate</c> tool — a parse / compile error (or
    /// warning) tied to a specific <c>res://</c> script file. Mirrors the shape of an editor "Errors" panel
    /// row so the agent gets structured, actionable feedback instead of a silent "the game runs fine".
    ///
    /// <para>
    /// Field availability depends on the live Godot version (the addon's SDK floor is 4.3, but the editor
    /// may be newer — see <see cref="ScriptDiagnosticSeverity"/>):
    /// <list type="bullet">
    /// <item><b>Godot 4.5+</b>: the engine's <c>Logger</c> hook delivers file + line + message, so
    /// <see cref="Line"/> and <see cref="Message"/> are populated precisely.</item>
    /// <item><b>Godot 4.3 / 4.4</b>: no managed log hook exists, so validation falls back to a coarse
    /// per-file <c>Reload()</c> probe — <see cref="Line"/> is <c>-1</c> (unknown) and <see cref="Message"/>
    /// carries the engine <c>Error</c> code (e.g. <c>"ParseError"</c>) rather than the human text.</item>
    /// </list>
    /// </para>
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host.
    /// </summary>
    [System.Serializable]
    [Description("A single script diagnostic (parse/compile error or warning): the res:// path, 1-based " +
        "line (-1 when unknown), message, and severity.")]
    public class ScriptDiagnostic
    {
        [JsonInclude, JsonPropertyName("path")]
        [Description("res:// path of the script the diagnostic belongs to, e.g. 'res://scripts/player.gd'.")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("line")]
        [Description("1-based source line of the diagnostic, or -1 when the live Godot version cannot report " +
            "a line (Godot < 4.5 falls back to a per-file error code with no line).")]
        public int Line { get; set; } = -1;

        [JsonInclude, JsonPropertyName("message")]
        [Description("Human-readable diagnostic text (Godot 4.5+), or the engine Error code name (e.g. " +
            "'ParseError') on older versions where the message text is not reachable.")]
        public string Message { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("severity")]
        [Description("Diagnostic severity: Error or Warning.")]
        public ScriptDiagnosticSeverity Severity { get; set; } = ScriptDiagnosticSeverity.Error;

        public ScriptDiagnostic() { }

        public ScriptDiagnostic(string path, int line, string message,
            ScriptDiagnosticSeverity severity = ScriptDiagnosticSeverity.Error)
        {
            Path = path ?? string.Empty;
            Line = line;
            Message = message ?? string.Empty;
            Severity = severity;
        }

        public override string ToString()
            => $"[{Severity}] {Path}{(Line >= 0 ? $":{Line}" : string.Empty)} — {Message}";
    }

    /// <summary>
    /// Severity of a <see cref="ScriptDiagnostic"/>. GDScript parse/compile problems are reported as
    /// <see cref="Error"/>; the Godot 4.5 <c>Logger</c> hook can additionally surface engine
    /// <see cref="Warning"/> lines captured during validation.
    /// </summary>
    [Description("Severity of a script diagnostic: Error or Warning.")]
    public enum ScriptDiagnosticSeverity
    {
        [Description("A parse / compile error — the script will not run as written.")]
        Error = 0,
        [Description("A non-fatal warning surfaced during validation.")]
        Warning = 1,
    }
}
