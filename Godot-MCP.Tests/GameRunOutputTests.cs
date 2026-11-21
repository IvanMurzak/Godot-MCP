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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>The <c>game-run</c> result text: distinct errors and warnings first with where they happened, exit noise dropped.</summary>
    public class GameRunOutputTests
    {
        // A headless run's stdout + stderr (Godot 4.7, a missing spawn point read from data), in arrival order.
        static readonly string[] Run =
        {
            "Godot Engine v4.7.2.stable.mono.official.ed1daf0bf - https://godotengine.org",
            "",
            "GAME READY",
            "ERROR: Node not found: \"Spawnpoints/Spawn_5\" (relative to \"/root/Main/Spawner\").",
            "   at: get_node (scene/main/node.cpp:1975)",
            "   GDScript backtrace (most recent call first):",
            "       [0] _ready (res://scripts/spawner.gd:12)",
            "SCRIPT ERROR: Invalid access to property or key 'position' on a base object of type 'null instance'.",
            "   at: _ready (res://scripts/spawner.gd:15)",
            "USER WARNING: low ammo",
            "   at: push_warning (core/variant/variant_utility.cpp:1034)",
            "SPAWNED 3",
            "WARNING: 1 ObjectDB instance was leaked at exit (run with `--verbose` for details).",
            "   at: cleanup (core/object/object.cpp:2536)",
        };

        [Fact]
        public void Format_ListsErrorsAndWarningsWithWhereTheyHappened_ThenPrinted()
        {
            var text = GameRunOutput.Format("the main scene", 3, 0, 30, Run);

            Assert.Equal(
                "Ran the main scene for 3 s of game time: exit code 0, 2 error(s), 1 warning(s).\n" +
                "Errors:\n" +
                "- Node not found: \"Spawnpoints/Spawn_5\" (relative to \"/root/Main/Spawner\").\n" +
                "  at: get_node (scene/main/node.cpp:1975)\n" +
                "  GDScript backtrace (most recent call first):\n" +
                "  [0] _ready (res://scripts/spawner.gd:12)\n" +
                "- Invalid access to property or key 'position' on a base object of type 'null instance'.\n" +
                "  at: _ready (res://scripts/spawner.gd:15)\n" +
                "Warnings:\n" +
                "- low ammo\n" +
                "  at: push_warning (core/variant/variant_utility.cpp:1034)\n" +
                "Printed:\n" +
                "GAME READY\n" +
                "SPAWNED 3", text);
        }

        [Fact]
        public void Format_RepeatedError_IsListedOnceWithItsCount()
        {
            var perFrame = Enumerable.Repeat(new[] { "ERROR: boom", "   at: _process (res://a.gd:3)" }, 120).SelectMany(x => x).ToArray();
            var text = GameRunOutput.Format("the main scene", 2, 0, 30, perFrame);
            Assert.Equal("Ran the main scene for 2 s of game time: exit code 0, 120 error(s), 0 warning(s).\n" +
                "Errors:\n- boom (x120)\n  at: _process (res://a.gd:3)", text);
        }

        [Fact]
        public void Format_CleanRun_HasNoIssueBlocks()
        {
            var text = GameRunOutput.Format("res://scenes/level.tscn", 1.5, 0, 30, new[] { "GAME READY", "SPAWNED 4" });
            Assert.Equal("Ran res://scenes/level.tscn for 1.5 s of game time: exit code 0, 0 error(s), 0 warning(s).\nPrinted:\nGAME READY\nSPAWNED 4", text);
        }

        [Fact]
        public void Format_StoppedAndCutRun_SaysSo()
        {
            var text = GameRunOutput.Format("the main scene", 3, null, 30, new string[0], droppedLines: 7);
            Assert.Equal("Ran the main scene for 3 s of game time: still running after 30 s of real time, stopped, 0 error(s), 0 warning(s). " +
                "Output cut: 7 line(s) in the middle were not kept.", text);
        }

        [Fact]
        public void Lines_KeepHeadAndTail_CountTheMiddle()
        {
            var lines = new GameRunLines();
            var total = 2 * GameRunLines.Keep + 5;
            for (var i = 1; i <= total; i++) lines.Add("l" + i);
            var kept = lines.Lines;
            Assert.Equal(5, lines.Dropped);
            Assert.Equal(2 * GameRunLines.Keep, kept.Count);
            Assert.Equal("l1", kept[0]);
            Assert.Equal("l" + GameRunLines.Keep, kept[GameRunLines.Keep - 1]);
            Assert.Equal("l" + (GameRunLines.Keep + 6), kept[GameRunLines.Keep]);
            Assert.Equal("l" + total, kept[^1]);
        }

        [Fact]
        public void Format_KeepsTheLastPrintedLines()
        {
            var lines = Enumerable.Range(1, GameRunOutput.MaxPrintedLines + 5).Select(i => "line " + i).ToArray();
            var text = GameRunOutput.Format("the main scene", 3, 0, 30, lines);
            Assert.Contains($"Printed (last {GameRunOutput.MaxPrintedLines} of {lines.Length} lines):\nline 6\n", text);
            Assert.DoesNotContain("line 5\n", text);
            Assert.EndsWith("line " + lines.Length, text);
        }
    }
}
