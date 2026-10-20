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
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// The literal default values of a shader's uniforms, read from its code (pure, unit-tested). The rendering server
    /// knows them too, but a headless editor runs the dummy renderer, which keeps none; this covers the literal forms —
    /// <c>= 2.5</c>, <c>= -3</c>, <c>= true</c>, <c>= vec3(1.0, 0.5, 0.0)</c>, <c>= vec4(1.0)</c> — and leaves anything
    /// computed (expressions, nested constructors, matrices, arrays) unknown.
    /// </summary>
    public static class ShaderUniformDefaults
    {
        // uniform [precision] <type> <name> [: hints] = <default>;  (hints such as hint_range(0, 1) hold no '=' or ';')
        private static readonly Regex Uniform = new Regex(
            @"\buniform\s+(?:(?:lowp|mediump|highp)\s+)?(?<type>\w+)\s+(?<name>\w+)\s*(?::[^=;]*)?=\s*(?<value>[^;]+);",
            RegexOptions.CultureInvariant);

        private static readonly Regex Constructor = new Regex(@"^(?<type>\w+)\s*\((?<args>[^()]*)\)$", RegexOptions.CultureInvariant);

        private static readonly Regex Comments = new Regex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline | RegexOptions.CultureInvariant);

        /// <summary>The components of every uniform default written as a literal, by uniform name (booleans as 1 / 0).</summary>
        public static Dictionary<string, double[]> Parse(string? code)
        {
            var defaults = new Dictionary<string, double[]>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(code)) return defaults;
            foreach (Match m in Uniform.Matches(Comments.Replace(code, " ")))
                if (Literal(m.Groups["type"].Value, m.Groups["value"].Value.Trim()) is { } parts)
                    defaults[m.Groups["name"].Value] = parts;
            return defaults;
        }

        // A scalar literal, or a vector constructor of scalar literals (one argument fills every component).
        private static double[]? Literal(string type, string text)
        {
            if (Scalar(text) is { } scalar) return new[] { scalar };
            var m = Constructor.Match(text);
            if (!m.Success || Width(m.Groups["type"].Value) is not { } width) return null;
            var args = m.Groups["args"].Value.Split(',');
            var parts = new double[args.Length];
            for (int i = 0; i < args.Length; i++)
                if (Scalar(args[i].Trim()) is { } v) parts[i] = v;
                else return null;
            if (parts.Length == 1 && width > 1)
            {
                var fill = parts[0];
                parts = new double[width];
                for (int i = 0; i < width; i++) parts[i] = fill;
            }
            return parts.Length == width ? parts : null;
        }

        private static int? Width(string type)
        {
            var t = type.TrimStart('i', 'u', 'b');
            return t switch { "vec2" => 2, "vec3" => 3, "vec4" => 4, _ => (int?)null };
        }

        private static double? Scalar(string text)
        {
            if (text == "true") return 1;
            if (text == "false") return 0;
            var t = text.TrimEnd('f', 'F', 'u', 'U');
            return t.Length > 0 && double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;
        }

        /// <summary>
        /// The CLR value of parsed components for a uniform of Godot type <paramref name="type"/> (what the Variant of that type
        /// would hold: double, long, bool, vectors, Color); null when they do not fit the type.
        /// </summary>
        public static object? ToValue(Variant.Type type, double[] p)
        {
            float F(int i) => (float)p[i];
            int I(int i) => (int)p[i];
            return (type, p.Length) switch
            {
                (Variant.Type.Bool, 1) => p[0] != 0,
                (Variant.Type.Int, 1) => (long)p[0],
                (Variant.Type.Float, 1) => p[0],
                (Variant.Type.Vector2, 2) => new Vector2(F(0), F(1)),
                (Variant.Type.Vector3, 3) => new Vector3(F(0), F(1), F(2)),
                (Variant.Type.Vector4, 4) => new Vector4(F(0), F(1), F(2), F(3)),
                (Variant.Type.Vector2I, 2) => new Vector2I(I(0), I(1)),
                (Variant.Type.Vector3I, 3) => new Vector3I(I(0), I(1), I(2)),
                (Variant.Type.Vector4I, 4) => new Vector4I(I(0), I(1), I(2), I(3)),
                (Variant.Type.Color, 3) => new Color(F(0), F(1), F(2)),
                (Variant.Type.Color, 4) => new Color(F(0), F(1), F(2), F(3)),
                _ => null,
            };
        }
    }
}
#endif
