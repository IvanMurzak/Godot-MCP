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
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// <c>editor | call BuildProject()</c>'s report (<see cref="BuildOutputText"/>) and the guard against the editor crash a
    /// deleted C# [GlobalClass] caused (<see cref="StaleGlobalClass"/>), both found by wave-3 agent runs.
    /// </summary>
    public class RqlBuildAndStaleClassTests
    {
        private const string Dir = @"C:\proj";

        [Fact]
        public void SuccessCountsDistinctWarnings()
        {
            var output = @"C:\proj\scripts\EnemyTable.cs(23,16): warning CS8603: Possible null reference return. [C:\proj\Game.csproj]" + "\n" +
                         @"C:\proj\scripts\EnemyTable.cs(23,16): warning CS8603: Possible null reference return. [C:\proj\Game.csproj]" + "\n";
            var text = BuildOutputText.Summarize(0, output, Dir);
            Assert.StartsWith("build succeeded (1 warning)", text);
            Assert.Contains("reloads the C# assembly", text);
            Assert.StartsWith("build succeeded (0 warnings)", BuildOutputText.Summarize(0, "", Dir));
        }

        [Fact]
        public void FailureListsDistinctErrorsRelativeToTheProject()
        {
            var output = string.Join("\r\n",
                @"C:\proj\Player.cs(3,5): error CS1002: ; expected [C:\proj\Game.csproj]",
                @"C:\proj\Player.cs(9,1): error CS0246: The type or namespace name 'Foo' could not be found [C:\proj\Game.csproj]",
                "",
                "Build FAILED.",
                @"C:\proj\Player.cs(3,5): error CS1002: ; expected [C:\proj\Game.csproj]",
                @"C:\proj\Player.cs(9,1): error CS0246: The type or namespace name 'Foo' could not be found [C:\proj\Game.csproj]",
                "    2 Error(s)");
            var ex = Assert.Throws<InvalidOperationException>(() => BuildOutputText.Summarize(1, output, Dir));
            Assert.Equal("build failed with 2 errors:\nPlayer.cs(3,5): error CS1002: ; expected\n" +
                         "Player.cs(9,1): error CS0246: The type or namespace name 'Foo' could not be found", ex.Message);
        }

        [Fact]
        public void ManyErrorsAreCut()
        {
            var output = string.Join("\n", Enumerable.Range(1, 30).Select(i => $@"C:\proj\A.cs({i},1): error CS1002: ; expected"));
            var ex = Assert.Throws<InvalidOperationException>(() => BuildOutputText.Summarize(1, output, Dir));
            Assert.StartsWith("build failed with 30 errors:", ex.Message);
            Assert.EndsWith("… and 10 more", ex.Message);
            Assert.Equal(BuildOutputText.MaxErrors + 2, ex.Message.Split('\n').Length);
        }

        [Fact]
        public void FailureWithoutCompilerErrorsShowsTheTail()
        {
            var ex = Assert.Throws<InvalidOperationException>(() =>
                BuildOutputText.Summarize(1, "The SDK 'Godot.NET.Sdk/9.9.9' specified could not be found.\nrestore failed", Dir));
            Assert.Equal("build failed (exit code 1):\nThe SDK 'Godot.NET.Sdk/9.9.9' specified could not be found.\nrestore failed", ex.Message);
            // An MSBuild error with a code counts as an error line.
            var msb = Assert.Throws<InvalidOperationException>(() =>
                BuildOutputText.Summarize(1, "MSBUILD : error MSB1009: Project file does not exist.", Dir));
            Assert.Equal("build failed with 1 error:\nMSBUILD : error MSB1009: Project file does not exist.", msb.Message);
        }

        // ---------------------------------------------------------------- stale global classes

        private static readonly List<(string, string)> Classes = new List<(string, string)>
        {
            ("EnemyData", "res://scripts/EnemyData.cs"),   // deleted, still registered until a build
            ("Weapon", "res://scripts/weapon.gd"),          // exists
        };

        private static bool Exists(string path) => path == "res://scripts/weapon.gd";

        [Theory]
        [InlineData("@export var rows: Array[EnemyData] = []")]
        [InlineData("var e := EnemyData.new()")]
        [InlineData("extends EnemyData")]
        public void AReferenceToADeletedScriptsClassIsFound(string source)
        {
            var hit = StaleGlobalClass.FindReference(source, Classes, Exists);
            Assert.Equal(("EnemyData", "res://scripts/EnemyData.cs"), hit);
        }

        [Theory]
        [InlineData("var w: Weapon")]                    // its file exists
        [InlineData("var enemy_data_count := 3")]         // a different word
        [InlineData("class_name EnemyDataTable")]         // a longer identifier
        [InlineData("")]
        public void OtherSourcesPass(string source) => Assert.Null(StaleGlobalClass.FindReference(source, Classes, Exists));

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
