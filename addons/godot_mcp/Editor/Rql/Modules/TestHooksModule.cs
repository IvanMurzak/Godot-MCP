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
    /// mutation committed to ("scene" or "global"), <c>test.action(history)</c> names its current action. Godot's C# API has
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
               .AddFunction("test.action", args => History(args)?.GetCurrentActionName(), "TEST ONLY: the current action of a history (\"scene\" or \"global\")");
        }

        private static object? Step(object?[] args, bool undo)
        {
            var history = History(args) ?? throw new InvalidOperationException("no undo manager (the plugin is not installed)");
            return undo ? history.Undo() : history.Redo();
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
