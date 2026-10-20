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
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Holds what setters record while an RQL <c>set</c> stage runs (their own undo steps) until the engine reports the
    /// stage's outcome. The engine runs <see cref="MutationPolicy.Committed"/> only after every write of a <c>set</c>
    /// succeeded; when a write throws it restores the earlier writes through the same setters and Committed never runs, so
    /// a stage that never commits leaves nothing behind (<see cref="DiscardAbove"/>). Completed calls are never rolled back,
    /// so what a <c>call</c> stage records is applied at once. Pure managed: no Godot types (unit-tested).
    /// </summary>
    internal sealed class RqlSetStageBuffer
    {
        private readonly List<(int depth, List<Action> pending)> _stages = new List<(int depth, List<Action> pending)>();

        /// <summary>A set stage at nesting depth <paramref name="depth"/> is about to write (the BeforeWrite hook).</summary>
        public void BeforeWrite(IReadOnlyList<MutationRecord> records, int depth)
        {
            if (IsSet(records)) _stages.Add((depth, new List<Action>()));
        }

        /// <summary>Run <paramref name="apply"/> when the running set stage commits, or now when no set stage is running.</summary>
        public void Record(Action apply)
        {
            if (_stages.Count > 0) _stages[_stages.Count - 1].pending.Add(apply);
            else apply();
        }

        /// <summary>The set stage at <paramref name="depth"/> committed (the Committed hook): apply what it recorded, in order.</summary>
        public void Committed(IReadOnlyList<MutationRecord> records, int depth)
        {
            if (!IsSet(records) || _stages.Count == 0 || _stages[_stages.Count - 1].depth != depth) return;
            var pending = _stages[_stages.Count - 1].pending;
            _stages.RemoveAt(_stages.Count - 1);
            // A stage nested in an enclosing set stage (a setter that runs a query) joins that stage's outcome.
            foreach (var apply in pending) Record(apply);
        }

        /// <summary>Drop the stages opened deeper than <paramref name="depth"/> that never committed (rolled back or vetoed).</summary>
        public void DiscardAbove(int depth)
        {
            while (_stages.Count > 0 && _stages[_stages.Count - 1].depth > depth) _stages.RemoveAt(_stages.Count - 1);
        }

        public void Clear() => _stages.Clear();

        // set records carry no method; call records do.
        private static bool IsSet(IReadOnlyList<MutationRecord> records) => records.Count > 0 && records[0].Method == null;
    }
}
#endif
