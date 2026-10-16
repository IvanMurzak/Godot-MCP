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
    /// Pure-managed (no Godot native types, CI-unit-testable) recommended starter configuration for a Dialogic
    /// timeline + character, which the <c>dialogic-defaults</c> tool returns. Lets the LLM discover a sane
    /// starting point (a 2-line timeline, a named/colored character) before authoring real resources with the
    /// editor tools — and keeps the pure-managed test path + the no-scene E2E entry real.
    /// </summary>
    public static class DialogicDefaults
    {
        /// <summary>Recommended starter character display name.</summary>
        public const string DefaultCharacterName = "Narrator";

        /// <summary>Recommended starter character color (<c>#rrggbb</c>).</summary>
        public const string DefaultCharacterColor = "#" + DialogicColor.Default;

        /// <summary>Recommended starter character description.</summary>
        public const string DefaultCharacterDescription =
            "A starter character created by the Dialogic Tools extension.";

        /// <summary>The recommended starter timeline as plain Dialogic <c>.dtl</c> text (two text events).</summary>
        public static string StarterTimelineText() =>
            DialogicTimelineText.BuildTextLine(DefaultCharacterName, "Welcome to your new Dialogic timeline!") +
            "\n" +
            DialogicTimelineText.BuildTextLine(null,
                "Edit it in the Dialogic editor, or append events with the dialogic-timeline-add-text tool.");

        /// <summary>
        /// A recommended starter configuration. <paramref name="kind"/> narrows the snapshot:
        /// <c>"timeline"</c> / <c>"dtl"</c> returns only the starter timeline; <c>"character"</c> / <c>"dch"</c>
        /// returns only the starter character; anything else (incl. null) returns both. Always reports
        /// <see cref="DialogicResourceInfo.ResourceKind"/> = <c>defaults</c> and <c>Installed = true</c> (it
        /// needs no addon).
        /// </summary>
        public static DialogicResourceInfo For(string? kind = null)
        {
            var k = (kind ?? string.Empty).Trim().ToLowerInvariant();
            var wantTimeline = k != "character" && k != "dch";
            var wantCharacter = k != "timeline" && k != "dtl";

            var timelineText = wantTimeline ? StarterTimelineText() : string.Empty;
            return new DialogicResourceInfo
            {
                Installed = true,
                ResourceKind = DialogicResourceKind.Defaults,
                DisplayName = wantCharacter ? DefaultCharacterName : string.Empty,
                Color = wantCharacter ? DefaultCharacterColor : string.Empty,
                Description = wantCharacter ? DefaultCharacterDescription : string.Empty,
                TimelineText = timelineText,
                EventCount = DialogicTimelineText.CountEvents(timelineText),
            };
        }
    }
}
