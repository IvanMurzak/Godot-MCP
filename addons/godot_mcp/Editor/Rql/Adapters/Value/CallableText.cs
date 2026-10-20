/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// The text form of a Callable (pure, unit-tested): <c>"&lt;target&gt;::&lt;method&gt;"</c>, the target being a node path
    /// below the edited scene root (<c>"."</c> for the root), a <c>res://</c> path or a <c>Class#instance id</c> reference;
    /// e.g. <c>"Player::_on_hit"</c>. The method is split off at the last <c>"::"</c> (a sub-resource path holds one too).
    /// </summary>
    public static class CallableText
    {
        public const string Separator = "::";

        private static readonly Regex Identifier = new Regex(@"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

        /// <summary>A reference to any object, as RQL's <c>$ref</c> writes it: <c>Class#instance id</c>.</summary>
        public static readonly Regex Reference = new Regex(@"^(?<type>[A-Za-z_][A-Za-z0-9_.]*)#(?<id>\d+)$", RegexOptions.CultureInvariant);

        public static string Format(string target, string method) => target + Separator + method;

        /// <summary>Split <paramref name="text"/> into its target and method; false (with the reason) when it is not that form.</summary>
        public static bool TryParse(string? text, out string target, out string method, out string? reason)
        {
            target = method = "";
            reason = null;
            var t = (text ?? "").Trim();
            var cut = t.LastIndexOf(Separator, StringComparison.Ordinal);
            if (cut <= 0 || cut + Separator.Length >= t.Length)
            {
                reason = $"a Callable is written \"<target>::<method>\", e.g. \"Player::_on_hit\" (target: a node path, a res:// path or Class#id), not '{text}'";
                return false;
            }
            target = t.Substring(0, cut).Trim();
            method = t.Substring(cut + Separator.Length).Trim();
            if (!Identifier.IsMatch(method))
            {
                reason = $"'{method}' is not a method name";
                return false;
            }
            return true;
        }
    }
}
#endif
