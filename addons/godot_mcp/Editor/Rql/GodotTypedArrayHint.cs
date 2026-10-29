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
using System.Globalization;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The object element of a typed array property (<c>Array[Resource]</c>, <c>Array[Node3D]</c>, a script class): Godot
    /// describes it as <c>PROPERTY_HINT_TYPE_STRING</c> "<c>24/17:Resource</c>" (variant type Object, element hint, class) or
    /// <c>PROPERTY_HINT_ARRAY_TYPE</c> with the class name. Pure (no Godot API): unit-tested.
    /// </summary>
    internal static class GodotTypedArrayHint
    {
        /// <summary>Godot's Variant.Type.Object.</summary>
        public const int ObjectVariantType = 24;

        /// <summary>Godot's PROPERTY_HINT_TYPE_STRING / PROPERTY_HINT_ARRAY_TYPE.</summary>
        public const int TypeStringHint = 23, ArrayTypeHint = 31;

        /// <summary>
        /// The element class and its element hint (17 = resource type, 34 = node type, 0 = none) of a typed array of objects,
        /// or null for any other array (untyped, an array of values, nested arrays).
        /// </summary>
        public static (string className, int elementHint)? ObjectElement(int hint, string? hintString)
        {
            if (string.IsNullOrWhiteSpace(hintString)) return null;
            var text = hintString!.Trim();
            var colon = text.IndexOf(':');
            if (colon < 0)
                return hint == ArrayTypeHint && IsClassName(text) ? (text, 0) : null;
            if (hint != TypeStringHint && hint != ArrayTypeHint) return null;
            var head = text.Substring(0, colon);
            var slash = head.IndexOf('/');
            var typeText = slash < 0 ? head : head.Substring(0, slash);
            if (!int.TryParse(typeText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var type) || type != ObjectVariantType)
                return null;
            var elementHint = 0;
            if (slash >= 0 && !int.TryParse(head.Substring(slash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out elementHint))
                return null;
            var className = text.Substring(colon + 1).Trim();
            return IsClassName(className) ? (className, elementHint) : null;
        }

        // A class name (Godot class or script class_name): starts with a letter or '_', no type-string punctuation.
        private static bool IsClassName(string text) =>
            text.Length > 0 && (char.IsLetter(text[0]) || text[0] == '_') && text.IndexOfAny(new[] { '/', ':' }) < 0;
    }
}
#endif
