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

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// The agent guide of the Dialogic RQL module, rendered into the <c>godot-rql</c> skill (<see cref="DialogicModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, in a project without these files.
    /// </summary>
    public static class DialogicGuide
    {
        public const string Topic = "Dialogic";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "dialogic | select installed hint   is the addon installed (hint: how to install it)\n" +
            "dialogic | call CreateTimeline(\"res://e2e_intro.dtl\", \"Hello from CI\", \"Narrator\")   a timeline with its first line (no text: a starter)\n" +
            "dialogic | call CreateCharacter(\"res://e2e_hero.dch\", \"Hero\", \"#e91e63\", \"The brave protagonist.\")   a character: display name, color, description\n" +
            "dialogic.timelines | where name == \"e2e_intro\" | call AddText(\"A second line\", \"Hero\")   append a text line (speaker optional)\n" +
            "dialogic.characters | where name == \"e2e_hero\" | get resource | set props[\"description\"] = \"The bravest hero.\"   edit a character through its resource (the .dch is saved)\n" +
            "dialogic.timelines | select path eventCount text   the timelines and their text\n" +
            "dialogic.characters | select path displayName color description   the characters";

        public const string Markdown =
            "Dialogic (the dialogic-godot/dialogic 2.0 addon) stores dialogue in resource files: `.dtl` timelines and `.dch` " +
            "characters, whose classes are GDScript class_names (`editor | call CreateResource` cannot make them). The " +
            "`dialogic` source lists the project's files as read-only rows (`dialogic.timelines`, `dialogic.characters`; " +
            "res://addons and hidden folders are skipped) and has the calls that create them. A character row holds the " +
            "loaded resource in `resource`: write through it (`get resource | set props[\"…\"]`; the core saves the .dch with " +
            "Dialogic's saver). A timeline is its text, one event per line (`Speaker: text` for a text event, colons in the text " +
            "escaped as `\\:`), so lines are added with the row's `AddText`. Creating and appending write the files directly " +
            "and are NOT undoable; creating fails when the file exists, a color that is not a hex color fails, and a character " +
            "needs a display name.\n\n```\n" + Examples + "\n```\n\n" +
            "Paths: `.dtl` / `.dch` is appended when missing and `res://` is added; missing folders are created. A timeline's " +
            "speaker is a character's file name (`name`). Character properties: `display_name`, `color` (default \"#3aa0ff\"), " +
            "`description`, `nicknames`, `default_portrait`, `portraits`, `scale`, `offset`, `mirror`. Typical start: a " +
            "\"Narrator\" character and a two-line timeline (`CreateTimeline(path)` without text writes one). Without the addon " +
            "`dialogic | select installed hint` says so, the lists are empty and the calls fail with the install hint.";

        /// <summary>The queries of <see cref="Examples"/>, in order.</summary>
        public static string[] Queries =>
            Examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();
    }
}
