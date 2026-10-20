/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Rql;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The pure parts of the editor's mutation hooks (RqlGodot.Save.cs) over a real RqlEngine: setter undo steps are kept only
    /// when their set stage commits (RqlSetStageBuffer), and the owner whose snapshot covers a change (RqlMutationOwner).
    /// </summary>
    public class RqlMutationHooksTests
    {
        // ------------------------------------------------------------ set stage buffer

        /// <summary>A row whose setters record "undo steps" the way the Theme map / node groups setters do.</summary>
        public sealed class Row
        {
            private readonly Action<string> _record;
            private int _value;

            public Row(string name, Action<string> record) { this.name = name; _record = record; }

            public string name { get; }

            public int value
            {
                get => _value;
                set { _record($"{name}.value {_value}->{value}"); _value = value; }
            }

            // Fails at write time (after validation): the engine rolls the earlier writes of the stage back.
            public int broken
            {
                get => 0;
                set => throw new InvalidOperationException("broken setter");
            }

            [RqlCallable("records a step and returns true")]
            public bool Touch() { _record($"{name}.Touch"); return true; }

            [RqlCallable("records a step, then throws")]
            public bool Fail() { _record($"{name}.Fail"); throw new InvalidOperationException("call failed"); }
        }

        private sealed class Host
        {
            public readonly RqlSetStageBuffer Buffer = new RqlSetStageBuffer();
            public readonly List<string> Committed = new List<string>();
            public readonly List<Row> Rows;
            public readonly RqlEngine Engine = new RqlEngine();
            public int Depth;

            public Host()
            {
                Rows = new List<Row> { new Row("a", Record), new Row("b", Record) };
                Engine.Mutations.Enabled = true;
                Engine.Mutations.BeforeWrite = records => Buffer.BeforeWrite(records, Depth);
                Engine.Mutations.Committed = records => Buffer.Committed(records, Depth);
                Engine.AddSource("rows", () => Rows);
            }

            private void Record(string step) => Buffer.Record(() => Committed.Add(step));

            // What RqlGodot.Mutate does around the engine.
            public QueryResult Mutate(string query)
            {
                Depth++;
                try { return Engine.Execute(query, null, new QueryOptions { AllowMutation = true }); }
                finally
                {
                    Depth--;
                    Buffer.DiscardAbove(Depth);
                }
            }
        }

        [Fact]
        public void ACommittedSetKeepsItsSetterSteps()
        {
            var host = new Host();
            var r = host.Mutate("rows | set value = 5");
            Assert.True(r.Ok, string.Join("; ", r.Diagnostics.Select(d => d.Message)));
            Assert.Equal(new[] { "a.value 0->5", "b.value 0->5" }, host.Committed);
        }

        [Fact]
        public void ARolledBackSetDropsItsSetterSteps()
        {
            var host = new Host();
            var r = host.Mutate("rows | set value = 5, broken = 1");
            Assert.False(r.Ok);
            // The rollback restored the value through the same setter; neither the write nor its restore left a step.
            Assert.All(host.Rows, row => Assert.Equal(0, row.value));
            Assert.Empty(host.Committed);
            // The next mutation starts clean: its steps are committed.
            Assert.True(host.Mutate("rows | where name == \"a\" | set value = 2").Ok);
            Assert.Equal(new[] { "a.value 0->2" }, host.Committed);
        }

        [Fact]
        public void CallStepsAreKeptAlsoWhenALaterCallThrows()
        {
            var host = new Host();
            Assert.True(host.Mutate("rows | call Touch()").Ok);
            Assert.Equal(new[] { "a.Touch", "b.Touch" }, host.Committed);
            host.Committed.Clear();
            // Completed calls are not rolled back, nor is a failing call's partial change: their steps stay.
            Assert.False(host.Mutate("rows | call Fail()").Ok);
            Assert.Equal(new[] { "a.Fail" }, host.Committed);
        }

        [Fact]
        public void ANestedSetJoinsTheEnclosingStage()
        {
            var buffer = new RqlSetStageBuffer();
            var applied = new List<string>();
            var set = new[] { SetRecord() };
            buffer.BeforeWrite(set, 1);
            buffer.Record(() => applied.Add("outer"));
            buffer.BeforeWrite(set, 2);
            buffer.Record(() => applied.Add("inner"));
            buffer.Committed(set, 2);
            buffer.DiscardAbove(1);
            Assert.Empty(applied); // the inner stage committed into the still-running outer one
            buffer.Committed(set, 1);
            Assert.Equal(new[] { "outer", "inner" }, applied);

            applied.Clear();
            buffer.BeforeWrite(set, 1);
            buffer.BeforeWrite(set, 2);
            buffer.Record(() => applied.Add("inner"));
            buffer.Committed(set, 2);
            buffer.DiscardAbove(0); // the outer stage rolled back: the inner one goes with it
            buffer.Record(() => applied.Add("after"));
            Assert.Equal(new[] { "after" }, applied);
        }

        // A real set record (MutationRecord has no public constructor): the first one a set hands to BeforeWrite.
        private static MutationRecord SetRecord()
        {
            MutationRecord? record = null;
            var engine = new RqlEngine();
            engine.Mutations.Enabled = true;
            engine.Mutations.BeforeWrite = records => record ??= records[0];
            engine.AddSource("rows", () => new List<Row> { new Row("x", _ => { }) });
            engine.Execute("rows | set value = 1", null, new QueryOptions { AllowMutation = true });
            return record!;
        }

        // ------------------------------------------------------------ owner selection

        private sealed class Obj
        {
            public Obj(string name, RqlOwnerKind kind, Obj? viewOf = null) { Name = name; Kind = kind; ViewOf = viewOf; }
            public string Name { get; }
            public RqlOwnerKind Kind { get; }
            public Obj? ViewOf { get; }
        }

        private static string? Owner(params Obj[] enclosing) =>
            RqlMutationOwner.Nearest<Obj>(enclosing, o =>
            {
                var obj = (Obj)o;
                return (obj.Kind == RqlOwnerKind.View ? obj.ViewOf : obj, obj.Kind);
            })?.Name;

        [Fact]
        public void APlainObjectHeldByANodeIsItsOwnOwner()
        {
            var node = new Obj("node", RqlOwnerKind.Stored);
            Assert.Equal("inner", Owner(new Obj("inner", RqlOwnerKind.Plain), node));
            Assert.Equal("inner", Owner(new Obj("inner", RqlOwnerKind.Plain), new Obj("plain C# view", RqlOwnerKind.None), node));
        }

        [Fact]
        public void APlainObjectUnderAViewBelongsToTheViewsObject()
        {
            // set data.Modulate on a TileSet tile row: TileData → RqlTile (view of its atlas source) → TileSet.
            var atlas = new Obj("atlas", RqlOwnerKind.Stored);
            Assert.Equal("atlas", Owner(new Obj("tileData", RqlOwnerKind.Plain), new Obj("tile", RqlOwnerKind.View, atlas),
                new Obj("tileSet", RqlOwnerKind.Stored)));
        }

        [Fact]
        public void TheNearestStoredObjectOwnsTheRest()
        {
            var resource = new Obj("material", RqlOwnerKind.Stored);
            Assert.Equal("material", Owner(new Obj("struct box", RqlOwnerKind.None), resource, new Obj("node", RqlOwnerKind.Stored)));
            Assert.Equal("settings", Owner(new Obj("action", RqlOwnerKind.View, new Obj("settings", RqlOwnerKind.Stored))));
            Assert.Equal("lonely", Owner(new Obj("lonely", RqlOwnerKind.Plain)));
            Assert.Null(Owner(new Obj("view only", RqlOwnerKind.None)));
        }
    }
}
