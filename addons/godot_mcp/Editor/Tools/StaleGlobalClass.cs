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
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// A global script class whose file is gone. A C# <c>[GlobalClass]</c> stays registered until the editor restarts (a
    /// rebuild or a filesystem scan does not drop it) after its <c>.cs</c> file is deleted, and Godot 4.5 crashes the editor
    /// (access violation in <c>GDScript.reload</c>) when a GDScript naming that class is parsed — what script-create /
    /// script-update / script-validate / script-attach-to-node do. The reference is refused before the parse. Pure (unit-tested).
    /// </summary>
    public static class StaleGlobalClass
    {
        /// <summary>The registered classes (name, path) whose file does not exist.</summary>
        public static List<(string name, string path)> Stale(IEnumerable<(string name, string path)> classes, Func<string, bool> fileExists) =>
            classes.Where(c => !string.IsNullOrEmpty(c.name) && !fileExists(c.path)).ToList();

        /// <summary>The first of the <paramref name="stale"/> classes that <paramref name="source"/> names as a word.</summary>
        public static (string name, string path)? FindReference(string? source, IReadOnlyList<(string name, string path)> stale)
        {
            if (string.IsNullOrEmpty(source) || stale.Count == 0) return null;
            var match = Regex.Match(source, @"(?<![\w])(?:" + string.Join("|", stale.Select(c => Regex.Escape(c.name))) + @")(?![\w])");
            return match.Success ? stale.First(c => c.name == match.Value) : ((string, string)?)null;
        }

        /// <summary>Why a script naming the stale class is refused, and what to do.</summary>
        public static string Problem(string name, string path) =>
            $"'{name}' is the global class of '{path}', which no longer exists; Godot crashes the editor resolving it. " +
            "Godot keeps a deleted C# [GlobalClass] registered until the editor restarts (a rebuild does not drop it): " +
            "remove the reference, restore the file, or restart the editor";
    }
}
#endif
