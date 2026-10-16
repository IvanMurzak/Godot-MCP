/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    public partial class Tool_Beehave
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>beehave-defaults</c>).</summary>
        public const string DefaultsToolId = "beehave-defaults";

        /// <summary>Editor tool id — create a BeehaveTree root (<c>beehave-tree-create</c>).</summary>
        public const string TreeCreateToolId = "beehave-tree-create";

        /// <summary>Editor tool id — add a Selector/Sequence composite (<c>beehave-add-composite</c>).</summary>
        public const string AddCompositeToolId = "beehave-add-composite";

        /// <summary>Editor tool id — add an Inverter/Limiter decorator (<c>beehave-add-decorator</c>).</summary>
        public const string AddDecoratorToolId = "beehave-add-decorator";

        /// <summary>Editor tool id — add an Action/Condition leaf placeholder (<c>beehave-add-leaf</c>).</summary>
        public const string AddLeafToolId = "beehave-add-leaf";

        /// <summary>Editor tool id — read a tree's structure/scalar config (<c>beehave-get</c>).</summary>
        public const string GetToolId = "beehave-get";
    }
}
