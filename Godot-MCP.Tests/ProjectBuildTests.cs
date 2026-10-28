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
using com.IvanMurzak.Godot.MCP.Reload;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>script-build's state file (it outlives the assembly reload it causes) and its reading of the MSBuild file log.</summary>
    public class ProjectBuildTests
    {
        [Fact]
        public void Record_RoundTrips()
        {
            var record = new BuildRecord
            {
                RequestId = "req-1",
                ProcessId = 4321,
                LogPath = "C:/proj/.godot/godot_mcp/build.log",
                ProjectDir = "C:/proj",
                DllWriteTicksAtStart = 638_000_000_000_000_000,
                StartedUtcTicks = 638_000_000_100_000_000,
                LoadId = "abc",
            };

            var parsed = BuildRecord.Parse(record.Serialize());

            Assert.NotNull(parsed);
            Assert.Equal(record.RequestId, parsed!.RequestId);
            Assert.Equal(record.ProcessId, parsed.ProcessId);
            Assert.Equal(record.LogPath, parsed.LogPath);
            Assert.Equal(record.ProjectDir, parsed.ProjectDir);
            Assert.Equal(record.DllWriteTicksAtStart, parsed.DllWriteTicksAtStart);
            Assert.Equal(record.StartedUtcTicks, parsed.StartedUtcTicks);
            Assert.Equal(record.LoadId, parsed.LoadId);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("ProcessId=12\n")]
        public void Record_Unreadable_IsNull(string? text) => Assert.Null(BuildRecord.Parse(text));

        [Fact]
        public void Record_ValueWithANewline_StaysOneLine()
        {
            var parsed = BuildRecord.Parse(new BuildRecord { RequestId = "r", LogPath = "a\nb=c" }.Serialize());
            Assert.Equal("a b=c", parsed!.LogPath);
        }

        [Fact]
        public void Log_StillBeingWritten_HasNoOutcome()
        {
            Assert.Null(BuildOutcome.FromLog(null));
            Assert.Null(BuildOutcome.FromLog("  Determining projects to restore...\n  Game -> C:\\proj\\bin\\Game.dll\n"));
        }

        [Fact]
        public void Log_Succeeded_CountsDistinctWarnings()
        {
            const string log =
                "C:\\proj\\Player.cs(3,5): warning CS0168: The variable 'x' is declared but never used [C:\\proj\\Game.csproj]\n" +
                "  Game -> C:\\proj\\.godot\\mono\\temp\\bin\\Debug\\Game.dll\n\nBuild succeeded.\n\n" +
                "C:\\proj\\Player.cs(3,5): warning CS0168: The variable 'x' is declared but never used [C:\\proj\\Game.csproj]\n" +
                "    1 Warning(s)\n    0 Error(s)\n\nTime Elapsed 00:00:01.20\n";

            var outcome = BuildOutcome.FromLog(log, "C:\\proj");

            Assert.NotNull(outcome);
            Assert.True(outcome!.Succeeded);
            Assert.Equal(1, outcome.Warnings);
            Assert.Equal("1 warning", outcome.WarningsText);
        }

        [Fact]
        public void Log_Failed_ListsDistinctErrorsRelativeToTheProject()
        {
            const string log =
                "C:\\proj\\probe\\Broken.cs(1,42): error CS1525: Invalid expression term '}' [C:\\proj\\Game.csproj]\n" +
                "C:\\proj\\probe\\Broken.cs(1,42): error CS1002: ; expected [C:\\proj\\Game.csproj]\n\nBuild FAILED.\n\n" +
                "C:\\proj\\probe\\Broken.cs(1,42): error CS1525: Invalid expression term '}' [C:\\proj\\Game.csproj]\n" +
                "C:\\proj\\probe\\Broken.cs(1,42): error CS1002: ; expected [C:\\proj\\Game.csproj]\n    0 Warning(s)\n    2 Error(s)\n";

            var outcome = BuildOutcome.FromLog(log, "C:/proj/");

            Assert.False(outcome!.Succeeded);
            Assert.Equal(new[] { "probe/Broken.cs(1,42): error CS1525: Invalid expression term '}'", "probe/Broken.cs(1,42): error CS1002: ; expected" }, outcome.Errors);
            Assert.StartsWith("C# build failed with 2 errors", outcome.FailureText());
            Assert.Contains("probe/Broken.cs(1,42): error CS1002", outcome.FailureText());
        }

        [Fact]
        public void Log_FailedWithoutACompilerError_ShowsTheTail()
        {
            var outcome = BuildOutcome.FromLog("MSBUILD : error : the restore was interrupted.\n\nBuild FAILED.\n");

            Assert.False(outcome!.Succeeded);
            Assert.Empty(outcome.Errors);
            Assert.Contains("the restore was interrupted", outcome.FailureText());
        }

        [Fact]
        public void Log_ManyErrors_AreCapped()
        {
            var log = string.Join("\n", Enumerable.Range(1, 30).Select(i => $"A.cs({i},1): error CS1002: ; expected")) + "\nBuild FAILED.\n";

            var text = BuildOutcome.FromLog(log)!.FailureText();

            Assert.Contains("… and 10 more", text);
        }
    }
}
