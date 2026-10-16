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
    public partial class Tool_Dialogic
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>dialogic-defaults</c>).</summary>
        public const string DefaultsToolId = "dialogic-defaults";

        /// <summary>Editor tool id — create a Dialogic timeline resource (<c>dialogic-timeline-create</c>).</summary>
        public const string TimelineCreateToolId = "dialogic-timeline-create";

        /// <summary>Editor tool id — create a Dialogic character resource (<c>dialogic-character-create</c>).</summary>
        public const string CharacterCreateToolId = "dialogic-character-create";

        /// <summary>Editor tool id — append a text event to a timeline (<c>dialogic-timeline-add-text</c>).</summary>
        public const string TimelineAddTextToolId = "dialogic-timeline-add-text";

        /// <summary>Editor tool id — read a timeline/character resource's config (<c>dialogic-get</c>).</summary>
        public const string GetToolId = "dialogic-get";
    }
}
