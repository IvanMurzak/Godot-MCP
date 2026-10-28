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
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.McpPlugin;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>One MCP tool of the editor connection, as the <c>tools</c> RQL source lists it.</summary>
    public sealed class RqlToolRow
    {
        public RqlToolRow(string name, string? title, string? description, bool visible)
        {
            this.name = name;
            this.title = title;
            this.description = RqlExtensionGuides.Summary(description ?? "");
            this.visible = visible;
        }

        [RqlDescription("the tool name an MCP client calls")]
        public string name { get; }

        [RqlDescription("display title")]
        public string? title { get; }

        [RqlDescription("the first sentence of the tool's description")]
        public string description { get; }

        [RqlDescription("listed by tools/list; a hidden tool (false) still runs when called by its name")]
        public bool visible { get; }
    }

    /// <summary>
    /// The <c>tools</c> RQL source: every MCP tool the editor registered, hidden ones included. MCP clients see only the
    /// visible tools in tools/list, so without this an agent cannot find a hidden tool, which still runs when called by name.
    /// Listing only: showing or hiding stays a user decision (the dock, godot-cli configure). The tool manager is the one the
    /// plugin configuration received (<see cref="RqlInstaller"/>); null before the plugin connected and after a reload.
    /// </summary>
    public static class RqlTools
    {
        public const string SourceName = "tools";

        public const string Description =
            "every MCP tool of this editor, hidden ones included: name title visible description; a tool with visible false is " +
            "not in the client's tool list but still runs when called by its name";

        internal static IToolManager? Manager { get; set; }

        public static List<RqlToolRow> All() => Rows(Manager?.GetAllTools() ?? Enumerable.Empty<IRunTool>());

        /// <summary>The rows of <paramref name="tools"/>, by name.</summary>
        internal static List<RqlToolRow> Rows(IEnumerable<IRunTool> tools) =>
            tools.Select(t => new RqlToolRow(t.Name, t.Title, t.Description, t.Visible))
                 .OrderBy(r => r.name, StringComparer.Ordinal).ToList();
    }
}
#endif
