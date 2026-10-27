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

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>An undo history as seen before and after an Undo / Redo: its version and its current action's name.</summary>
    public readonly struct HistoryState
    {
        public HistoryState(ulong version, string? action) { Version = version; Action = action ?? ""; }
        public ulong Version { get; }
        public string Action { get; }
    }

    /// <summary>
    /// What <c>editor | call Undo()</c> / <c>Redo()</c> report, from the histories it watched (global, edited scene): the
    /// history whose version moved names the action (undo: its current action before the step; redo: after it). Nothing
    /// moved: there was nothing to undo / redo, which is an error rather than a silent no-op. Pure (unit-tested).
    /// </summary>
    public static class HistoryStepText
    {
        /// <summary>The undo-redo action name of an rql-mutate: "RQL: " and its query on one line, cut to a readable length.</summary>
        public static string ActionName(string? query, int max = 120)
        {
            var text = string.Join(" ", (query ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (text.Length == 0) return "RQL mutation";
            if (text.Length > max) text = text.Substring(0, max - 1) + "…";
            return "RQL: " + text;
        }

        public static string Describe(bool undo, IReadOnlyList<HistoryState> before, IReadOnlyList<HistoryState> after)
        {
            for (int i = 0; i < before.Count && i < after.Count; i++)
            {
                if (before[i].Version == after[i].Version) continue;
                var name = undo ? before[i].Action : after[i].Action;
                return (undo ? "undone: " : "redone: ") + (name.Length > 0 ? name : "(unnamed action)");
            }
            throw new InvalidOperationException(undo
                ? "nothing to undo: the edited scene's and the global undo histories have no action left to undo"
                : "nothing to redo: no undone action is waiting (a new change clears the redo history)");
        }
    }
}
#endif
