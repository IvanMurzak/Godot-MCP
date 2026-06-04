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
    /// Identity + content record for a script file on disk — returned by <c>script-read</c> and as the
    /// confirmation payload of <c>script-create</c>/<c>script-update</c>/<c>script-delete</c>/
    /// <c>script-attach-to-node</c>. Carries the script's <c>res://</c> path, its language
    /// (<c>"CSharp"</c>/<c>"GDScript"</c>), and — for reads — the file content + a short status note about
    /// any triggered compile/reload. The Godot analog of the Unity-MCP <c>script-read</c> / <c>script-
    /// update-or-create</c> result (both languages, since Godot is dual-language).
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host. The editor-side handlers fill <see cref="Content"/> only for reads and <see cref="Status"/>
    /// with the settle/reload outcome.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("Identity + (for reads) content of a script file: its res:// path, language, optional " +
        "content, and a status note about any triggered compile/reload.")]
    public class ScriptInfo
    {
        [JsonInclude, JsonPropertyName("resourcePath")]
        [Description("res:// path of the script file, e.g. 'res://scripts/player.gd' or 'res://scripts/Enemy.cs'.")]
        public string ResourcePath { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("language")]
        [Description("Script language: 'CSharp' for a .cs file, 'GDScript' for a .gd file.")]
        public string Language { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("content")]
        [Description("The script's text content. Populated by 'script-read'; null on the write/delete/attach " +
            "confirmation payloads (which echo identity only).")]
        public string? Content { get; set; } = null;

        [JsonInclude, JsonPropertyName("lineCount")]
        [Description("Number of lines in 'content' when present (the read slice's line count), else 0.")]
        public int LineCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("status")]
        [Description("Short human-readable status note, e.g. 'Script created; build settled.' or 'Script " +
            "read.'. For C# writes/deletes this records the bounded compile/reload settle outcome; null when " +
            "no status applies.")]
        public string? Status { get; set; } = null;

        public ScriptInfo() { }

        public override string ToString()
            => $"Script '{ResourcePath}' ({Language}){(Status != null ? $" — {Status}" : string.Empty)}";
    }
}
