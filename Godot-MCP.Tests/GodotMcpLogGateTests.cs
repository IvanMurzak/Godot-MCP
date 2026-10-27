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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Data;
using Microsoft.Extensions.Logging;
using Xunit;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed log gate (<see cref="GodotMcpLogGate"/>) and the
    /// <see cref="GodotMcpLogger"/> routing: for each configured <see cref="GodotMcpLogLevel"/>, which
    /// framework <see cref="MicrosoftLogLevel"/> values are enabled vs suppressed, the Microsoft→Godot
    /// severity mapping, and that the logger only routes enabled lines (with the <c>[McpPlugin]</c> prefix)
    /// to its injected sink. All pure-managed — the real <c>GD.*</c> sink is wired in
    /// <c>GodotMcpConnection.cs</c> (<c>#if TOOLS</c>) and verified via the Suite-3 smoke.
    /// </summary>
    public class GodotMcpLogGateTests
    {
        // --- Severity mapping (Microsoft LogLevel -> GodotMcpLogLevel bucket) ---

        [Theory]
        [InlineData(MicrosoftLogLevel.Trace, GodotMcpLogLevel.Trace)]
        [InlineData(MicrosoftLogLevel.Debug, GodotMcpLogLevel.Debug)]
        [InlineData(MicrosoftLogLevel.Information, GodotMcpLogLevel.Info)]
        [InlineData(MicrosoftLogLevel.Warning, GodotMcpLogLevel.Warning)]
        [InlineData(MicrosoftLogLevel.Error, GodotMcpLogLevel.Error)]
        [InlineData(MicrosoftLogLevel.Critical, GodotMcpLogLevel.Error)] // Godot has no separate critical channel
        [InlineData(MicrosoftLogLevel.None, GodotMcpLogLevel.None)]
        public void Map_FoldsMicrosoftLevelToGodotBucket(MicrosoftLogLevel ms, GodotMcpLogLevel expected)
        {
            Assert.Equal(expected, GodotMcpLogGate.Map(ms));
        }

        // --- Threshold gating per configured level ---

        [Fact]
        public void Trace_EnablesEverything()
        {
            foreach (var ms in EmittableLevels)
                Assert.True(GodotMcpLogGate.IsEnabled(ms, GodotMcpLogLevel.Trace), $"{ms} should be enabled at Trace");
        }

        [Fact]
        public void Info_SuppressesTraceAndDebug_EnablesInfoAndAbove()
        {
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Trace, GodotMcpLogLevel.Info));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Debug, GodotMcpLogLevel.Info));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Information, GodotMcpLogLevel.Info));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Warning, GodotMcpLogLevel.Info));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Error, GodotMcpLogLevel.Info));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Critical, GodotMcpLogLevel.Info));
        }

        [Fact]
        public void Warning_EnablesWarningErrorCritical_SuppressesInfoDebugTrace()
        {
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Trace, GodotMcpLogLevel.Warning));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Debug, GodotMcpLogLevel.Warning));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Information, GodotMcpLogLevel.Warning));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Warning, GodotMcpLogLevel.Warning));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Error, GodotMcpLogLevel.Warning));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Critical, GodotMcpLogLevel.Warning));
        }

        [Fact]
        public void Error_EnablesErrorAndCriticalOnly()
        {
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Trace, GodotMcpLogLevel.Error));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Debug, GodotMcpLogLevel.Error));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Information, GodotMcpLogLevel.Error));
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Warning, GodotMcpLogLevel.Error));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Error, GodotMcpLogLevel.Error));
            Assert.True(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.Critical, GodotMcpLogLevel.Error));
        }

        [Fact]
        public void None_SuppressesEverything()
        {
            foreach (var ms in EmittableLevels)
                Assert.False(GodotMcpLogGate.IsEnabled(ms, GodotMcpLogLevel.None), $"{ms} should be suppressed at None");
        }

        [Fact]
        public void MicrosoftNone_NeverEnabled_EvenAtTrace()
        {
            // A message mapped to None (LogLevel.None) is never actually logged, even at the most verbose threshold.
            Assert.False(GodotMcpLogGate.IsEnabled(MicrosoftLogLevel.None, GodotMcpLogLevel.Trace));
        }

        // --- Logger routing (via injected capturing sink) ---

        [Fact]
        public void Logger_SuppressedLine_DoesNotRouteToSink()
        {
            var captured = new List<(GodotLogType Type, string Message)>();
            var logger = new GodotMcpLogger("com.example.ConnectionManager",
                () => GodotMcpLogLevel.Warning,
                (type, msg) => captured.Add((type, msg)));

            logger.LogInformation("connecting to hub");

            Assert.Empty(captured); // Info is below the Warning threshold → not routed.
        }

        [Fact]
        public void Logger_EnabledLine_RoutesPrefixedShortCategoryAtMappedSeverity()
        {
            var captured = new List<(GodotLogType Type, string Message)>();
            var logger = new GodotMcpLogger("com.example.ConnectionManager",
                () => GodotMcpLogLevel.Trace,
                (type, msg) => captured.Add((type, msg)));

            logger.LogWarning("handshake slow");

            var line = Assert.Single(captured);
            Assert.Equal(GodotLogType.Warning, line.Type);
            Assert.StartsWith(GodotMcpLogger.Prefix, line.Message);
            Assert.Contains("ConnectionManager", line.Message); // short category (namespace dropped)
            Assert.Contains("handshake slow", line.Message);
            Assert.DoesNotContain("com.example", line.Message); // namespace not shown
        }

        [Theory]
        [InlineData(MicrosoftLogLevel.Information, GodotLogType.Log)]
        [InlineData(MicrosoftLogLevel.Debug, GodotLogType.Log)]
        [InlineData(MicrosoftLogLevel.Trace, GodotLogType.Log)]
        [InlineData(MicrosoftLogLevel.Warning, GodotLogType.Warning)]
        [InlineData(MicrosoftLogLevel.Error, GodotLogType.Error)]
        [InlineData(MicrosoftLogLevel.Critical, GodotLogType.Error)]
        public void Logger_RoutesEachSeverityToTheRightGodotLogType(MicrosoftLogLevel ms, GodotLogType expected)
        {
            var captured = new List<(GodotLogType Type, string Message)>();
            var logger = new GodotMcpLogger("Cat",
                () => GodotMcpLogLevel.Trace,
                (type, msg) => captured.Add((type, msg)));

            logger.Log(ms, default, "msg", null, (s, _) => s);

            var line = Assert.Single(captured);
            Assert.Equal(expected, line.Type);
        }

        [Fact]
        public void Logger_ReadsLevelLive_DropdownChangeAppliesWithoutRebuild()
        {
            var captured = new List<(GodotLogType Type, string Message)>();
            var level = GodotMcpLogLevel.Error; // start strict
            var logger = new GodotMcpLogger("Cat", () => level, (type, msg) => captured.Add((type, msg)));

            logger.LogInformation("first"); // suppressed at Error
            Assert.Empty(captured);

            level = GodotMcpLogLevel.Trace;   // simulate the dock dropdown moving to Trace
            logger.LogInformation("second");  // now enabled — proves the level is read live, not cached
            Assert.Single(captured);
            Assert.Contains("second", captured[0].Message);
        }

        // --- Tool traffic (printed, not collected for the console source) ---

        static (List<string> collected, List<string> echoed) Route(string category, System.Action<ILogger> log)
        {
            var collected = new List<string>();
            var echoed = new List<string>();
            var logger = new GodotMcpLogger(category, () => GodotMcpLogLevel.Trace,
                (_, msg) => collected.Add(msg), (_, msg) => echoed.Add(msg));
            log(logger);
            return (collected, echoed);
        }

        [Fact]
        public void ToolCallEcho_GoesToTheEchoSink()
        {
            var (collected, echoed) = Route("com.IvanMurzak.McpPlugin.McpToolManager", l =>
            {
                l.LogInformation("Run tool 'rql-query' with parameters[1]");
                l.LogInformation("Success Response to AI:\n1 result");
                l.LogError("Error Response to AI:\nerror unknown-member");
            });
            Assert.Empty(collected);
            Assert.Equal(3, echoed.Count);
        }

        [Fact]
        public void ToolFailuresAndOtherManagerLines_AreCollected()
        {
            var (collected, echoed) = Route("com.IvanMurzak.McpPlugin.McpToolManager", l =>
            {
                l.LogError(new System.InvalidOperationException("boom"), "Error Response to AI:\nFailed to run tool 'x'");
                l.LogWarning("Tool with Name 'x' already exists. Skipping addition.");
            });
            Assert.Equal(2, collected.Count);
            Assert.Empty(echoed);
            Assert.Contains("boom", collected[0]);

            (collected, echoed) = Route("com.IvanMurzak.McpPlugin.ToolRunnerCollection", l =>
                l.LogError(new System.NullReferenceException(), "Tool execution failed for 'x': Object reference not set"));
            Assert.Single(collected);
            Assert.Empty(echoed);
        }

        [Theory]
        [InlineData("McpToolManager", "Run tool 'x' with no parameters.", true)]
        [InlineData("McpToolManager", "Processing Response to AI:\n…", true)]
        [InlineData("McpToolManager", "Failed to run tool 'x'", false)]
        [InlineData("ToolRunnerCollection", "Run tool 'x'", false)]
        [InlineData("HubConnection", "Success Response to AI", false)]
        public void IsToolTraffic_MatchesTheEchoShapesOnly(string category, string text, bool expected)
        {
            Assert.Equal(expected, GodotMcpLogger.IsToolTraffic(category, text, null));
            Assert.False(GodotMcpLogger.IsToolTraffic(category, text, new System.Exception("x")));
        }

        [Fact]
        public void WithoutAnEchoSink_EverythingGoesToTheSink()
        {
            var captured = new List<string>();
            var logger = new GodotMcpLogger("McpToolManager", () => GodotMcpLogLevel.Trace, (_, msg) => captured.Add(msg));
            logger.LogInformation("Run tool 'x' with no parameters.");
            Assert.Single(captured);
        }

        static readonly MicrosoftLogLevel[] EmittableLevels =
        {
            MicrosoftLogLevel.Trace,
            MicrosoftLogLevel.Debug,
            MicrosoftLogLevel.Information,
            MicrosoftLogLevel.Warning,
            MicrosoftLogLevel.Error,
            MicrosoftLogLevel.Critical
        };
    }
}
