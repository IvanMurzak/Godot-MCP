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
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The guard against the editor crash a deleted C# [GlobalClass] caused (<see cref="StaleGlobalClass"/>), found by a wave-3 agent run.
    /// </summary>
    public class StaleGlobalClassTests
    {
        // ---------------------------------------------------------------- stale global classes

        private static readonly List<(string, string)> Classes = new List<(string, string)>
        {
            ("EnemyData", "res://scripts/EnemyData.cs"),   // deleted, still registered until the editor restarts
            ("Weapon", "res://scripts/weapon.gd"),          // exists
        };

        private static bool Exists(string path) => path == "res://scripts/weapon.gd";

        [Theory]
        [InlineData("@export var rows: Array[EnemyData] = []")]
        [InlineData("var e := EnemyData.new()")]
        [InlineData("extends EnemyData")]
        public void AReferenceToADeletedScriptsClassIsFound(string source)
        {
            var hit = StaleGlobalClass.FindReference(source, StaleGlobalClass.Stale(Classes, Exists));
            Assert.Equal(("EnemyData", "res://scripts/EnemyData.cs"), hit);
        }

        [Theory]
        [InlineData("var w: Weapon")]                    // its file exists
        [InlineData("var enemy_data_count := 3")]         // a different word
        [InlineData("class_name EnemyDataTable")]         // a longer identifier
        [InlineData("")]
        [InlineData("var n = $EnemyData")]                 // a node path, not the class
        [InlineData("var n = %EnemyData")]
        [InlineData("# EnemyData used to live here")]     // a comment
        [InlineData("print(\"EnemyData\")")]              // a string
        [InlineData("var d = \"\"\"\nEnemyData\n\"\"\"")]  // a multi-line string
        public void OtherSourcesPass(string source) => Assert.Null(StaleGlobalClass.FindReference(source, StaleGlobalClass.Stale(Classes, Exists)));

        [Fact]
        public void OnlyMissingFilesAreStale() =>
            Assert.Equal(new[] { ("EnemyData", "res://scripts/EnemyData.cs") }, StaleGlobalClass.Stale(Classes, Exists));

        [Fact]
        public void TheProblemNamesTheClassTheFileAndTheWayOut()
        {
            var text = StaleGlobalClass.Problem("EnemyData", "res://scripts/EnemyData.cs");
            Assert.Contains("'EnemyData'", text);
            Assert.Contains("res://scripts/EnemyData.cs", text);
            Assert.Contains("restart the editor", text);
        }
    }
}
