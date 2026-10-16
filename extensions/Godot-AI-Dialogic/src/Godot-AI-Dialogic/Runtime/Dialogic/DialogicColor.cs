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
    /// Pure-managed (CI-unit-testable) helper that normalizes an LLM/user-supplied color string into a canonical
    /// 6-digit <c>rrggbb</c> hex (no <c>#</c>), so the editor character tool can feed it to Godot's
    /// <c>Color.FromHtml</c> and the read-back round-trips deterministically. Keeping the parsing pure-managed
    /// means an invalid color can never reach a live Godot <c>Color</c> and the rule is verified with no Godot
    /// binary.
    /// </summary>
    public static class DialogicColor
    {
        /// <summary>The fallback color (a friendly blue) used when the input is empty or invalid: <c>rrggbb</c>.</summary>
        public const string Default = "3aa0ff";

        /// <summary>
        /// Normalize a color string to a 6-digit lower-case <c>rrggbb</c> hex (no <c>#</c>). Accepts an optional
        /// leading <c>#</c>, and <c>rrggbb</c> or <c>rrggbbaa</c> (the alpha is dropped). Returns
        /// <see cref="Default"/> for null/empty/invalid input.
        /// </summary>
        public static string NormalizeHex(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return Default;
            var s = value!.Trim();
            if (s.StartsWith("#")) s = s.Substring(1);
            s = s.ToLowerInvariant();

            if ((s.Length == 6 || s.Length == 8) && IsHex(s))
                return s.Substring(0, 6);

            return Default;
        }

        /// <summary>A normalized 6-hex as a <c>#rrggbb</c> display string.</summary>
        public static string ToDisplay(string sixHex) => "#" + sixHex;

        static bool IsHex(string s)
        {
            foreach (var c in s)
            {
                var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f');
                if (!isHex) return false;
            }
            return true;
        }
    }
}
