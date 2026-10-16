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
    /// Pure-managed (CI-unit-testable) classifier mapping a <c>res://</c> resource path to the Dialogic
    /// resource kind by its file extension. Used by the read-only <c>dialogic-get</c> tool to decide whether
    /// to read a timeline (<c>.dtl</c>) or a character (<c>.dch</c>) — no Godot API needed for the decision.
    /// </summary>
    public static class DialogicResourceKind
    {
        /// <summary>A Dialogic timeline (<c>.dtl</c>).</summary>
        public const string Timeline = "timeline";

        /// <summary>A Dialogic character (<c>.dch</c>).</summary>
        public const string Character = "character";

        /// <summary>A starter-config snapshot not bound to a saved resource.</summary>
        public const string Defaults = "defaults";

        /// <summary>The lower-cased extension of <paramref name="path"/> (no dot), or empty when there is none.</summary>
        public static string ExtensionOf(string? path)
        {
            if (string.IsNullOrWhiteSpace(path)) return string.Empty;
            var idx = path!.LastIndexOf('.');
            if (idx < 0 || idx == path.Length - 1) return string.Empty;
            return path.Substring(idx + 1).ToLowerInvariant();
        }

        /// <summary>
        /// Classify a resource path by extension, throwing <see cref="ArgumentException"/> for an empty path or
        /// an extension other than <c>.dtl</c> / <c>.dch</c>.
        /// </summary>
        public static string FromPath(string? resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
                throw new ArgumentException("A resource path is required.", nameof(resourcePath));

            var ext = ExtensionOf(resourcePath);
            return ext switch
            {
                DialogicAddon.TimelineExtension => Timeline,
                DialogicAddon.CharacterExtension => Character,
                _ => throw new ArgumentException(
                    $"Unsupported Dialogic resource extension '.{ext}'. Use '.dtl' (timeline) or '.dch' (character).",
                    nameof(resourcePath)),
            };
        }
    }
}
