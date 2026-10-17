/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// The checks of the module's creation calls, run before anything is written. Each returns the rejection reason, or
    /// null when the value is valid (normalized values come back through out parameters). Pure-managed (no Godot API), so
    /// it is unit-tested.
    /// </summary>
    public static class DialogicRules
    {
        /// <summary>The default character color (a friendly blue), as six hex digits.</summary>
        public const string DefaultColor = "3aa0ff";

        /// <summary>
        /// A res:// file path for a Dialogic resource: backslashes become slashes, a missing <c>res://</c> is added and a
        /// missing extension is appended (<c>res://intro</c> → <c>res://intro.dtl</c>); another extension is rejected.
        /// </summary>
        public static string? ResourcePath(string? path, string extension, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(path)) return $"a res:// path for the .{extension} file is required";
            var p = path!.Trim().Replace('\\', '/');
            if (p.StartsWith("user://", StringComparison.Ordinal) || p.Contains("://") && !p.StartsWith("res://", StringComparison.Ordinal))
                return $"'{path}' is not a res:// path";
            if (!p.StartsWith("res://", StringComparison.Ordinal)) p = "res://" + p.TrimStart('/');
            var file = p.Substring(p.LastIndexOf('/') + 1);
            if (file.Length == 0) return $"'{path}' names a folder, not a .{extension} file";
            var dot = file.LastIndexOf('.');
            if (dot < 0) p += "." + extension;
            else if (!string.Equals(file.Substring(dot + 1), extension, StringComparison.OrdinalIgnoreCase))
                return $"'{path}' must be a .{extension} file";
            normalized = p;
            return null;
        }

        /// <summary>A character needs a display name.</summary>
        public static string? DisplayName(string? displayName) =>
            string.IsNullOrWhiteSpace(displayName) ? "a character needs a display name" : null;

        /// <summary>
        /// A color as <c>#rrggbb</c> (or <c>rrggbb</c>, <c>#rrggbbaa</c>, any case); empty means <see cref="DefaultColor"/>.
        /// <paramref name="hex"/> receives the six lower-case digits.
        /// </summary>
        public static string? Color(string? value, out string hex)
        {
            hex = DefaultColor;
            if (string.IsNullOrWhiteSpace(value)) return null;
            var s = value!.Trim();
            if (s.StartsWith("#", StringComparison.Ordinal)) s = s.Substring(1);
            s = s.ToLowerInvariant();
            if ((s.Length == 6 || s.Length == 8) && IsHex(s))
            {
                hex = s.Substring(0, 6);
                return null;
            }
            return $"color must be a hex color like \"#e91e63\" (got '{value}')";
        }

        private static bool IsHex(string s)
        {
            foreach (var c in s)
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
            return true;
        }
    }
}
