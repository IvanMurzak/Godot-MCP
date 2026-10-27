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
        /// <summary>The first registered class (name, path) whose file does not exist and that <paramref name="source"/> names as a word.</summary>
        public static (string name, string path)? FindReference(string? source, IEnumerable<(string name, string path)> classes, Func<string, bool> fileExists)
        {
            if (string.IsNullOrEmpty(source)) return null;
            foreach (var (name, path) in classes)
            {
                if (string.IsNullOrEmpty(name) || fileExists(path)) continue;
                if (Regex.IsMatch(source, @"(?<![\w])" + Regex.Escape(name) + @"(?![\w])")) return (name, path);
            }
            return null;
        }

        /// <summary>Why a script naming the stale class is refused, and what to do.</summary>
        public static string Problem(string name, string path) =>
            $"'{name}' is the global class of '{path}', which no longer exists; Godot crashes the editor resolving it. " +
            "Godot keeps a deleted C# [GlobalClass] registered until the editor restarts (a rebuild does not drop it): " +
            "remove the reference, restore the file, or restart the editor";
    }
}
#endif
