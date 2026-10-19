/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Editor-only (RQL needs the editor-only com.IvanMurzak.Rql package); also compiled by the xUnit suite.
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>Color: "(r, g, b, a)" output; "(r, g, b[, a])", [r, g, b[, a]], "#RRGGBB[AA]" or {r, g, b, a} input.</summary>
    public sealed class Godot_Color_Adapter : GodotFloatTupleAdapter<Color>
    {
        public override string Description => "Color: write as \"(r, g, b, a)\" with 0..1 components, [r, g, b] or \"#RRGGBB\"";
        protected override string Expected => "(r, g, b, a), [r, g, b] or #RRGGBB";
        protected override int Width => 4;
        protected override float Component(Color c, int index) => index switch { 0 => c.R, 1 => c.G, 2 => c.B, _ => c.A };
        protected override Color? Make(float[] f) =>
            f.Length == 4 ? new Color(f[0], f[1], f[2], f[3]) : f.Length == 3 ? new Color(f[0], f[1], f[2]) : (Color?)null;

        // "#rgb", "#rrggbb", "#rrggbbaa" (Godot's managed HTML parser; no native call).
        protected override Color? ParseExtra(string text)
        {
            var t = text.Trim();
            return t.StartsWith("#", System.StringComparison.Ordinal) && Color.HtmlIsValid(t) ? Color.FromHtml(t) : (Color?)null;
        }
    }
}
#endif
