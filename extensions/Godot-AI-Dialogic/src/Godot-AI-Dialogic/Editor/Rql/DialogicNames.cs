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
    /// The Dialogic addon's names, the contract of the module: its resource classes are GDScript <c>class_name</c>s (no C#
    /// type, not in ClassDB), so they are referenced by string only. Verified against <c>dialogic-godot/dialogic</c>
    /// 2.0-alpha-18 / -20. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class DialogicNames
    {
        public const string AddonName = "Dialogic";

        public const string TimelineClass = "DialogicTimeline";
        public const string CharacterClass = "DialogicCharacter";

        public const string TimelineExtension = "dtl";
        public const string CharacterExtension = "dch";

        // DialogicCharacter's GDScript properties (snake_case, read / written with props["…"]).
        public const string DisplayName = "display_name";
        public const string Color = "color";
        public const string Description = "description";

        // DialogicTimeline: from_text(text) parses the .dtl text; its saver writes only when this meta is set.
        public const string FromTextMethod = "from_text";
        public const string NotSavedMeta = "timeline_not_saved";

        public const string InstallHint =
            "Dialogic is not installed: install 'Dialogic' (Godot Asset Library, or dialogic-godot/dialogic 2.0 into " +
            "res://addons/dialogic/), enable it under Project Settings -> Plugins so its Dialogic autoload is registered, " +
            "then rebuild.";

        /// <summary>The first lines of a timeline created without text.</summary>
        public static string StarterTimelineText() =>
            DialogicTimelineText.BuildTextLine("Narrator", "Welcome to your new Dialogic timeline!") + "\n" +
            DialogicTimelineText.BuildTextLine(null, "Edit it in the Dialogic editor, or append lines with AddText.");
    }
}
