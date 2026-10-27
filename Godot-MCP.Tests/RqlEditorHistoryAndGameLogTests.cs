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
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The pure parts of what wave-3 agent runs found missing: <c>editor | call Undo() / Redo()</c> (the report and the action
    /// names), the <c>gameLog</c> source (Godot's game log text as entries) and the radians warning of a degree-sized Rotation.
    /// </summary>
    public class RqlEditorHistoryAndGameLogTests
    {
        // ---------------------------------------------------------------- undo / redo

        [Fact]
        public void ActionNameIsTheQueryOnOneLine()
        {
            Assert.Equal("RQL: nodes | where name == \"A\" | set Visible = false",
                HistoryStepText.ActionName("nodes |\n  where name == \"A\"\r\n| set   Visible = false"));
            Assert.Equal("RQL mutation", HistoryStepText.ActionName("   "));
            Assert.Equal("RQL mutation", HistoryStepText.ActionName(null));
        }

        [Fact]
        public void LongQueriesAreCut()
        {
            var name = HistoryStepText.ActionName(new string('x', 500), max: 20);
            Assert.Equal("RQL: " + new string('x', 19) + "…", name);
        }

        private static List<HistoryState> States(params (ulong v, string a)[] s) => s.Select(x => new HistoryState(x.v, x.a)).ToList();

        // ---------------------------------------------------------------- files: deps / usedBy

        [Theory]
        [InlineData("uid://b1x::Texture2D::res://art/a.png", "uid://b1x", "res://art/a.png")]
        [InlineData("uid://b1x::::res://art/a.png", "uid://b1x", "res://art/a.png")]
        [InlineData("res://scenes/house.tscn", null, "res://scenes/house.tscn")]
        [InlineData("res://scenes/house.tscn::PackedScene", null, "res://scenes/house.tscn")]
        [InlineData("uid://b1x::Texture2D::", "uid://b1x", null)]
        [InlineData("", null, null)]
        [InlineData(null, null, null)]
        public void DependencyEntriesGiveTheirUidAndPath(string? entry, string? uid, string? path) =>
            Assert.Equal((uid, path), com.IvanMurzak.Godot.MCP.Tools.ResPathNormalizer.DependencyParts(entry));

        [Fact]
        public void PendingUndoListsTheCurrentActionFirst()
        {
            var names = new[] { "a", "b", "c", "d" };
            Assert.Equal(new[] { "c", "b", "a" }, HistoryStepText.Pending(undo: true, current: 2, count: 4, i => names[i]).ToArray());
            Assert.Equal(new[] { "d (global)" }, HistoryStepText.Pending(undo: false, current: 2, count: 4, i => names[i], " (global)").ToArray());
        }

        [Fact]
        public void PendingIsEmptyAtTheEndsAndCapped()
        {
            Assert.Empty(HistoryStepText.Pending(undo: true, current: -1, count: 3, i => "x"));
            Assert.Empty(HistoryStepText.Pending(undo: false, current: 2, count: 3, i => "x"));
            Assert.Equal(HistoryStepText.MaxListed, HistoryStepText.Pending(undo: true, current: 99, count: 100, i => "x" + i).Count());
            Assert.Equal("x99", HistoryStepText.Pending(undo: true, current: 99, count: 100, i => "x" + i).First());
            Assert.Equal(new[] { "(unnamed action)" }, HistoryStepText.Pending(undo: false, current: -1, count: 1, i => "").ToArray());
        }

        [Fact]
        public void UndoNamesTheActionTheMovedHistoryHadBefore()
        {
            var before = States((4, "Global thing"), (10, "RQL: rename"));
            var after = States((4, "Global thing"), (9, "RQL: add crates"));
            Assert.Equal("undone: RQL: rename", HistoryStepText.Describe(undo: true, before, after));
        }

        [Fact]
        public void RedoNamesTheActionTheMovedHistoryHasAfter()
        {
            var before = States((3, "older"), (9, "RQL: add crates"));
            var after = States((4, "Project setting"), (9, "RQL: add crates"));
            Assert.Equal("redone: Project setting", HistoryStepText.Describe(undo: false, before, after));
        }

        [Theory]
        [InlineData(true, "nothing to undo")]
        [InlineData(false, "nothing to redo")]
        public void NothingMovedIsAnError(bool undo, string message)
        {
            var same = States((1, "a"), (2, "b"));
            var ex = Assert.Throws<InvalidOperationException>(() => HistoryStepText.Describe(undo, same, same));
            Assert.StartsWith(message, ex.Message);
        }

        [Fact]
        public void AnUnnamedActionStillReports() =>
            Assert.Equal("undone: (unnamed action)", HistoryStepText.Describe(true, States((2, "")), States((1, ""))));

        // ---------------------------------------------------------------- game log

        // What Godot 4.5's file logger wrote for a game run from the editor (wave-3 run: a push_error and an out-of-range index).
        private const string Log =
            "Godot Engine v4.5.1.stable.mono.official.f62fdbde1 - https://godotengine.org\n" +
            "Vulkan 1.4.351 - Forward+ - Using Device #0: NVIDIA - NVIDIA GeForce RTX 4090\n" +
            "\n" +
            "W3 game ready\n" +
            "ERROR: W3 boom at 1s\n" +
            "   at: push_error (core/variant/variant_utility.cpp:1024)\n" +
            "   GDScript backtrace (most recent call first):\n" +
            "       [0] _process (res://w3/game.gd:10)\n" +
            "SCRIPT ERROR: Invalid access of index '3' on a base object of type: 'Array'.\n" +
            "   at: _process (res://w3/game.gd:12)\n" +
            "WARNING: low fps\n" +
            "     at: something (x.cpp:1)\n" +
            "door open = true\n";

        [Fact]
        public void ParsesPrintsErrorsAndWarningsWithTheirLocations()
        {
            var e = GameLogText.Parse(Log);
            Assert.Equal(new[] { "Log", "Error", "Error", "Warning", "Log" }, e.Select(x => x.LogType));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, e.Select(x => x.Sequence));
            Assert.Equal("W3 game ready", e[0].Message);
            Assert.Equal("W3 boom at 1s", e[1].Message);
            Assert.Equal("at: push_error (core/variant/variant_utility.cpp:1024)\nGDScript backtrace (most recent call first):\n[0] _process (res://w3/game.gd:10)",
                e[1].StackTrace);
            Assert.Equal("Invalid access of index '3' on a base object of type: 'Array'.", e[2].Message);
            Assert.Equal("at: _process (res://w3/game.gd:12)", e[2].StackTrace);
            Assert.Equal("low fps", e[3].Message);
            Assert.Equal("door open = true", e[4].Message);
            Assert.Equal("", e[4].StackTrace);
        }

        [Fact]
        public void HandlesCrLfAndNoBannerAndEmpty()
        {
            var e = GameLogText.Parse("hello\r\nUSER ERROR: bad\r\n   at: f (res://a.gd:1)\r\n");
            Assert.Equal(2, e.Count);
            Assert.Equal("hello", e[0].Message);
            Assert.Equal(("Error", "bad", "at: f (res://a.gd:1)"), (e[1].LogType, e[1].Message, e[1].StackTrace));
            Assert.Empty(GameLogText.Parse(""));
            Assert.Empty(GameLogText.Parse(null));
        }

        [Fact]
        public void ABannerWithoutRendererLineKeepsTheEntries()
        {
            // A headless game: the version line, then its prints at once (no renderer line, no blank line).
            var e = GameLogText.Parse("Godot Engine v4.5.1.stable.mono.official - https://godotengine.org\nfirst print\nERROR: x\n");
            Assert.Equal(new[] { "first print", "x" }, e.Select(x => x.Message));
        }

        [Fact]
        public void SplitMutationPartsAreRecognized()
        {
            Assert.True(HistoryStepText.IsPart("undone: RQL: q" + HistoryStepText.ScenePart));
            Assert.True(HistoryStepText.IsPart("redone: RQL: q" + HistoryStepText.GlobalPart));
            Assert.False(HistoryStepText.IsPart("undone: RQL: q"));
        }

        [Fact]
        public void IndentedPrintsStayPrints()
        {
            // Only an error / warning collects indented lines; a print may itself start with spaces.
            var e = GameLogText.Parse("a\n   indented print\n");
            Assert.Equal(new[] { "a", "   indented print" }, e.Select(x => x.Message));
        }

        // ---------------------------------------------------------------- radians

        [Theory]
        [InlineData("Rotation")]
        [InlineData("rotation")]
        [InlineData("Rotation.X")]
        [InlineData("GlobalRotation")]
        public void RadiansMembers(string member) => Assert.True(RotationUnits.IsRadiansMember(member));

        [Theory]
        [InlineData("RotationDegrees")]
        [InlineData("RotationDegrees.Y")]
        [InlineData("props[\"rotation\"]")]
        [InlineData("Position")]
        public void OtherMembers(string member) => Assert.False(RotationUnits.IsRadiansMember(member));

        [Fact]
        public void DegreeSizedAnglesWarnWithTheDegreeMember()
        {
            // The wave-3 agent's sun: set Rotation = "(-45, 35, 0)".
            var w = RotationUnits.Warning("Rotation", new double[] { -45, 35, 0 });
            Assert.NotNull(w);
            Assert.Contains("Rotation is in radians", w);
            Assert.Contains("set RotationDegrees = (-45, 35, 0)", w);
            Assert.Contains("(-2578.31, 2005.35, 0)°", w);
        }

        [Fact]
        public void OneAngleAndLowerCaseMembers()
        {
            var w = RotationUnits.Warning("rotation.y", new double[] { 90 });
            Assert.Contains("set RotationDegrees.y = 90", w);
            Assert.Contains("5156.62°", w);
        }

        [Theory]
        [InlineData(3.14159, 0, 0)]
        [InlineData(-6.28, 1.5, 0)]
        [InlineData(0, 0, 0)]
        public void RadianSizedAnglesDoNotWarn(double x, double y, double z) =>
            Assert.Null(RotationUnits.Warning("Rotation", new[] { x, y, z }));

        [Fact]
        public void DegreeMembersNeverWarn() => Assert.Null(RotationUnits.Warning("RotationDegrees", new double[] { 90, 0, 0 }));
    }
}
