/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>animation-defaults</c>).</summary>
        public const string DefaultsToolId = "animation-defaults";

        /// <summary>Editor tool id — create an <c>AnimationPlayer</c> node (<c>animation-player-create</c>).</summary>
        public const string PlayerCreateToolId = "animation-player-create";

        /// <summary>Editor tool id — add an <c>AnimationLibrary</c> to a player (<c>animation-library-add</c>).</summary>
        public const string LibraryAddToolId = "animation-library-add";

        /// <summary>Editor tool id — create an <c>Animation</c> in a library (<c>animation-create</c>).</summary>
        public const string CreateToolId = "animation-create";

        /// <summary>Editor tool id — add a track to an animation (<c>animation-add-track</c>).</summary>
        public const string AddTrackToolId = "animation-add-track";

        /// <summary>Editor tool id — insert a keyframe on a track (<c>animation-insert-key</c>).</summary>
        public const string InsertKeyToolId = "animation-insert-key";

        /// <summary>Editor tool id — read a player's animations / an animation's config (<c>animation-get</c>).</summary>
        public const string GetToolId = "animation-get";
    }
}
