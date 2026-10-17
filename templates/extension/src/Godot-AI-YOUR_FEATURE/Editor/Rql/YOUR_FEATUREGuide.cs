/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    /// <summary>
    /// The agent guide of the YOUR_DISPLAY_NAME RQL module, rendered into the <c>godot-rql</c> skill by the core addon
    /// (<see cref="YOUR_FEATUREModule"/> registers it with <c>ctx.AddGuide</c>). Pure strings (no Godot API, not behind
    /// <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e manifest (<c>tests/e2e/e2e-tools.json</c>)
    /// runs each one, in this order. Keep the examples runnable top to bottom against the e2e setup.
    /// </summary>
    public static class YOUR_FEATUREGuide
    {
        public const string Topic = "YOUR_DISPLAY_NAME";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "YOUR_TOOL_PREFIX | select godotVersion editorScale   the editor's version and UI scale\n" +
            "YOUR_TOOL_PREFIX | call Echo(\"ci-roundtrip\")   a callable method: returns its argument";

        public const string Markdown =
            "The `YOUR_TOOL_PREFIX` source is this module's entry point: read its members with `rql-query`, call its methods with " +
            "`rql-mutate`:\n\n```\n" + Examples + "\n```\n\n" +
            "Replace this sample with what the extension adds: adapters for the Godot types it covers (validated members, " +
            "methods), sources for state that no node owns, and runnable examples.";

        /// <summary>The queries of <see cref="Examples"/>, in order.</summary>
        public static string[] Queries =>
            Examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();
    }
}
