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
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Pure-managed post-processing for <c>script-validate</c> diagnostics — the classification logic that
    /// decides which engine-reported rows are GENUINE errors versus artifacts of validating a file's source
    /// through a throwaway probe. Kept binary-free (no Godot API, no <c>#if TOOLS</c>) so it is unit-testable
    /// in the plain xUnit host, mirroring the <see cref="ScriptErrorCapture"/> router it complements.
    ///
    /// <para>
    /// The one artifact this filters is the <b>global-class self-hide false positive</b> (issue #194). The
    /// validator reloads a throwaway <c>GDScript</c> built from a real project file's source to harvest its
    /// parse errors. If that file declares <c>class_name X</c>, Godot has ALREADY registered <c>X</c> as a
    /// global script class (from the on-disk file itself), so reloading the probe — whose source re-declares
    /// <c>class_name X</c> — makes the engine emit <c>Class "X" hides a global script class.</c>. That row is
    /// spurious: the real file is valid and opens without error in the editor; the "conflict" is the file
    /// hiding its OWN registration. This filter drops exactly those rows (a "hides a global script class"
    /// message whose class name is one the file under validation itself declares), so a valid
    /// <c>class_name</c> script validates <c>ok:true</c> with no diagnostics.
    /// </para>
    /// </summary>
    public static class ScriptDiagnosticsFilter
    {
        /// <summary>The engine's stable marker for the self-hide parse error (Godot's GDScript parser emits
        /// <c>Class "%s" hides a global script class.</c>). Matched case-insensitively as a substring so a
        /// <c>"Parse Error: "</c> prefix or a trailing period does not defeat it.</summary>
        const string HidesGlobalClassMarker = "hides a global script class";

        /// <summary>
        /// Matches a top-level GDScript <c>class_name &lt;Identifier&gt;</c> declaration and captures the
        /// declared name. Anchored at line start (after optional leading whitespace) so a <c>class_name</c>
        /// token inside a comment (<c># class_name Foo</c>) or mid-expression is not picked up. <c>class_name</c>
        /// must precede an identifier: a Unicode letter or underscore, then Unicode letters / decimal digits /
        /// underscores — GDScript 4 permits non-ASCII identifiers (e.g. <c>class_name Αρχή</c>), so the capture
        /// uses <c>\p{L}</c>/<c>\p{Nd}</c> rather than ASCII-only classes, keeping it consistent with
        /// <see cref="IsIdentifierChar"/> (which already uses the Unicode-aware <c>char.IsLetterOrDigit</c>).
        /// Multiline + culture-invariant.
        /// </summary>
        static readonly Regex ClassNameDeclPattern = new(
            @"^[ \t]*class_name[ \t]+([\p{L}_][\p{L}\p{Nd}_]*)",
            RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Extract every <c>class_name</c> a GDScript source declares (usually zero or one; a file can only
        /// register one global class, but this returns all matches defensively). Pure string parsing — no
        /// engine call. Returns an empty list when <paramref name="source"/> is null/empty or declares none.
        /// </summary>
        public static IReadOnlyList<string> ExtractClassNames(string? source)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(source))
                return names;

            foreach (Match m in ClassNameDeclPattern.Matches(source))
            {
                var name = m.Groups[1].Value;
                if (name.Length > 0 && !names.Contains(name))
                    names.Add(name);
            }
            return names;
        }

        /// <summary>
        /// True when <paramref name="message"/> is the benign global-class self-hide false positive for a
        /// class the validated file itself declares (issue #194): the message carries the engine's
        /// "hides a global script class" marker AND names one of <paramref name="declaredClassNames"/>. The
        /// engine quotes the offending name (<c>Class "X" hides ...</c>); rather than depend on the exact quote
        /// glyph, the name is matched as a whole identifier token anywhere in the message. Returns false when
        /// the file declares no class_name (nothing to self-hide) or the message is some other error.
        /// </summary>
        public static bool IsGlobalClassSelfHide(string? message, IReadOnlyCollection<string> declaredClassNames)
        {
            if (string.IsNullOrEmpty(message) || declaredClassNames == null || declaredClassNames.Count == 0)
                return false;

            if (message!.IndexOf(HidesGlobalClassMarker, StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            foreach (var name in declaredClassNames)
                if (ContainsWholeIdentifier(message, name))
                    return true;

            return false;
        }

        /// <summary>Convenience overload: extract the class names from <paramref name="source"/> and test the
        /// message against them in one call (the shape the editor validator uses).</summary>
        public static bool IsGlobalClassSelfHide(string? message, string? source)
            => IsGlobalClassSelfHide(message, ExtractClassNames(source));

        /// <summary>
        /// Whether <paramref name="word"/> occurs in <paramref name="haystack"/> bounded on both sides by a
        /// non-identifier character (or a string edge) — so <c>"AI"</c> matches <c>Class "AI" hides…</c> but
        /// not the <c>AI</c> inside <c>MAIN</c> or <c>AItem</c>. Ordinal (GDScript identifiers are
        /// case-sensitive).
        /// </summary>
        static bool ContainsWholeIdentifier(string haystack, string word)
        {
            if (string.IsNullOrEmpty(word))
                return false;

            int idx = 0;
            while ((idx = haystack.IndexOf(word, idx, StringComparison.Ordinal)) >= 0)
            {
                var leftOk = idx == 0 || !IsIdentifierChar(haystack[idx - 1]);
                var end = idx + word.Length;
                var rightOk = end >= haystack.Length || !IsIdentifierChar(haystack[end]);
                if (leftOk && rightOk)
                    return true;
                idx = end;
            }
            return false;
        }

        static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
