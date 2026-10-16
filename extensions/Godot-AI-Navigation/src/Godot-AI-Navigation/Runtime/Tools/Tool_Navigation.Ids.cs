/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    public partial class Tool_Navigation
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>navigation-defaults</c>).</summary>
        public const string DefaultsToolId = "navigation-defaults";

        /// <summary>Editor tool id — create a navigation region (<c>navigation-region-create</c>).</summary>
        public const string RegionCreateToolId = "navigation-region-create";

        /// <summary>Editor tool id — assign a region's navigation resource (<c>navigation-region-set-mesh</c>).</summary>
        public const string RegionSetMeshToolId = "navigation-region-set-mesh";

        /// <summary>Editor tool id — create a navigation agent (<c>navigation-agent-create</c>).</summary>
        public const string AgentCreateToolId = "navigation-agent-create";

        /// <summary>Editor tool id — configure a navigation agent (<c>navigation-agent-configure</c>).</summary>
        public const string AgentConfigureToolId = "navigation-agent-configure";

        /// <summary>Editor tool id — create a navigation link (<c>navigation-link-create</c>).</summary>
        public const string LinkCreateToolId = "navigation-link-create";

        /// <summary>Editor tool id — read a navigation node's config (<c>navigation-get</c>).</summary>
        public const string GetToolId = "navigation-get";
    }
}
