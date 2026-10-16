/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Particles
{
    public partial class Tool_Particles
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>particles-defaults</c>).</summary>
        public const string DefaultsToolId = "particles-defaults";

        /// <summary>Editor tool id — create an emitter (<c>particles-create</c>).</summary>
        public const string CreateToolId = "particles-create";

        /// <summary>Editor tool id — configure an emitter (<c>particles-configure</c>).</summary>
        public const string ConfigureToolId = "particles-configure";

        /// <summary>Editor tool id — start/stop emission (<c>particles-set-emitting</c>).</summary>
        public const string SetEmittingToolId = "particles-set-emitting";

        /// <summary>Editor tool id — read an emitter's config (<c>particles-get</c>).</summary>
        public const string GetToolId = "particles-get";
    }
}
