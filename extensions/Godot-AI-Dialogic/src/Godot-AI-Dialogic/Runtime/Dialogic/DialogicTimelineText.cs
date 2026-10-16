/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// Pure-managed (CI-unit-testable) helper for Dialogic's <b>plain-text timeline format</b> — the
    /// version-stable surface the editor tools author. A Dialogic <c>.dtl</c> file IS its timeline text (the
    /// addon's loader does <c>from_text(file.get_as_text())</c>), so building one line of text is the whole job
    /// and it can be verified with no Godot binary.
    ///
    /// <para>
    /// A text event is a single line: <c>text</c> for narration, or <c>Speaker: text</c> when a character
    /// speaks (mirrors Dialogic 2.0-alpha-19's <c>DialogicTextEvent.to_text()</c>: colons in the body are
    /// escaped as <c>\:</c> and a speaker name containing spaces is wrapped in quotes).
    /// </para>
    /// </summary>
    public static class DialogicTimelineText
    {
        /// <summary>Placeholder body Dialogic uses for an empty text event.</summary>
        public const string EmptyEventBody = "<Empty Text Event>";

        /// <summary>
        /// Build one text-event line. The body's colons are escaped (<c>:</c> → <c>\:</c>); a non-empty
        /// <paramref name="speaker"/> is prefixed as <c>Speaker: </c> (quoted when it contains spaces). An
        /// empty body becomes <see cref="EmptyEventBody"/>.
        /// </summary>
        public static string BuildTextLine(string? speaker, string? text)
        {
            var body = (text ?? string.Empty).Replace(":", "\\:").Trim();
            if (body.Length == 0)
                body = EmptyEventBody;

            if (string.IsNullOrWhiteSpace(speaker))
                return body;

            var name = speaker!.Trim();
            if (name.Contains(" "))
                name = "\"" + name + "\"";

            return name + ": " + body;
        }

        /// <summary>
        /// Append <paramref name="newLine"/> as a new line to <paramref name="existingText"/> (trimming a
        /// trailing newline first). When the existing text is empty, returns <paramref name="newLine"/> alone.
        /// </summary>
        public static string Append(string? existingText, string newLine)
        {
            if (string.IsNullOrEmpty(existingText))
                return newLine;
            return existingText!.TrimEnd('\n', '\r') + "\n" + newLine;
        }

        /// <summary>Count the non-empty lines (≈ text events) in a timeline's text.</summary>
        public static int CountEvents(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var lines = text!.Replace("\r\n", "\n").Split('\n');
            var count = 0;
            foreach (var line in lines)
                if (!string.IsNullOrWhiteSpace(line))
                    count++;
            return count;
        }
    }
}
