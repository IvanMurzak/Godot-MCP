/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Modules
{
    /// <summary>
    /// Test-only functions for the live smoke (scripts/smoke_rql.py), registered only when the editor runs with
    /// <c>GODOT_MCP_RQL_TEST_HOOKS=1</c>: <c>test.undo(history)</c> / <c>test.redo(history)</c> step the undo history an RQL
    /// mutation committed to ("scene" or "global"), <c>test.action(history)</c> names its current action,
    /// <c>test.undoManager()</c> is true only when the editor's undo manager and its histories are reachable (the smoke's
    /// probe: without one, mutations apply without undo entries), <c>test.addToGroup(node, group, persistent)</c> joins a
    /// group like a tool script would (also without <c>persistent</c>, which RQL itself never does). Godot's C# API has
    /// no editor-level undo; these drive the history's UndoRedo directly, which the editor's own Undo bookkeeping does not
    /// see — fine for a disposable test editor, never for an agent, hence the gate.
    /// </summary>
    public sealed class TestHooksModule : IRqlModule
    {
        public const string EnvVar = "GODOT_MCP_RQL_TEST_HOOKS";

        // EditorUndoRedoManager.SpecialHistory.Global.
        private const int GlobalHistory = 0;

        public int Order => 100;

        public void Configure(RqlModuleContext ctx)
        {
            if (System.Environment.GetEnvironmentVariable(EnvVar) != "1") return;
            ctx.AddFunction("test.undo", args => Step(args, undo: true), "TEST ONLY: undo the last action of a history (\"scene\" or \"global\")")
               .AddFunction("test.redo", args => Step(args, undo: false), "TEST ONLY: redo the next action of a history (\"scene\" or \"global\")")
               .AddFunction("test.action", args => History(args)?.GetCurrentActionName(), "TEST ONLY: the current action of a history (\"scene\" or \"global\")")
               .AddFunction("test.undoManager", args => HasUndoManager(), "TEST ONLY: whether the editor's undo manager and its histories are reachable")
               .AddFunction("test.addToGroup", AddToGroup, "TEST ONLY: Node.AddToGroup(group, persistent) on a node, bypassing RQL (no undo)");
        }

        private static object? Step(object?[] args, bool undo)
        {
            var history = History(args) ?? throw new InvalidOperationException("no undo manager (the plugin is not installed)");
            RqlGodot.InvalidateGroups(); // the step may change groups behind RQL
            return undo ? history.Undo() : history.Redo();
        }

        // Both histories a mutation commits to are reachable (global; the edited scene's, when one is open).
        private static bool HasUndoManager() =>
            History(new object?[] { "global" }) != null && (RqlGodot.EditedRoot() == null || History(new object?[] { "scene" }) != null);

        private static object? AddToGroup(object?[] args)
        {
            if (args.Length != 3 || !(args[0] is Node node) || !(args[1] is string group) || !(args[2] is bool persistent))
                return new RqlError("type-mismatch", "test.addToGroup(node, group, persistent) expects a node, a group name and a bool");
            node.AddToGroup(group, persistent);
            RqlGodot.InvalidateGroups(node);
            return true;
        }

        private static UndoRedo? History(object?[] args)
        {
            var manager = RqlGodot.UndoRedo?.Invoke();
            if (manager == null || !GodotObject.IsInstanceValid(manager)) return null;
            var which = args.Length > 0 && args[0] is string s ? s : "scene";
            int id;
            if (string.Equals(which, "global", StringComparison.OrdinalIgnoreCase)) id = GlobalHistory;
            else if (RqlGodot.EditedRoot() is { } root) id = manager.GetObjectHistoryId(root);
            else return null;
            return manager.GetHistoryUndoRedo(id);
        }
    }
}
#endif
