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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for <see cref="LogEntry"/> — the structured result row returned by the
    /// <c>console-get-logs</c> tool. Asserts the JSON property names are the camelCase the rest of the tool
    /// surface uses (so a client reads <c>logType</c>/<c>message</c>/<c>timestamp</c>/<c>stackTrace</c>
    /// consistently), the optional <c>stackTrace</c> normalization (empty → null), and the human-readable
    /// <see cref="LogEntry.ToString(bool)"/> rendering. Pure-managed, so it runs in the plain xUnit host.
    /// </summary>
    public class LogEntryTests
    {
        [Fact]
        public void Serializes_WithExpectedCamelCaseJsonNames()
        {
            var entry = new LogEntry(
                GodotLogType.Error,
                "boom",
                new DateTime(2026, 6, 20, 1, 2, 3, DateTimeKind.Utc),
                stackTrace: "at Foo()");

            var json = JsonSerializer.Serialize(entry);

            Assert.Contains("\"logType\"", json);
            Assert.Contains("\"message\"", json);
            Assert.Contains("\"timestamp\"", json);
            Assert.Contains("\"stackTrace\"", json);

            var restored = JsonSerializer.Deserialize<LogEntry>(json);
            Assert.NotNull(restored);
            Assert.Equal(GodotLogType.Error, restored!.LogType);
            Assert.Equal("boom", restored.Message);
            Assert.Equal("at Foo()", restored.StackTrace);
        }

        [Fact]
        public void Constructor_EmptyOrNullStackTrace_NormalizesToNull()
        {
            // The ctor collapses empty/null stack traces to null so 'no trace' is a single canonical value.
            Assert.Null(new LogEntry(GodotLogType.Log, "m", DateTime.UtcNow, stackTrace: null).StackTrace);
            Assert.Null(new LogEntry(GodotLogType.Log, "m", DateTime.UtcNow, stackTrace: "").StackTrace);
            Assert.Equal("x", new LogEntry(GodotLogType.Log, "m", DateTime.UtcNow, stackTrace: "x").StackTrace);
        }

        [Fact]
        public void Constructor_NullMessage_NormalizesToEmpty()
        {
            Assert.Equal(string.Empty, new LogEntry(GodotLogType.Log, null!, DateTime.UtcNow).Message);
        }

        [Fact]
        public void ToString_DefaultExcludesStackTrace()
        {
            var ts = new DateTime(2026, 6, 20, 1, 2, 3, 456, DateTimeKind.Utc);
            var entry = new LogEntry(GodotLogType.Warning, "careful", ts, stackTrace: "at Bar()");

            var s = entry.ToString();

            Assert.Contains("[Warning]", s);
            Assert.Contains("careful", s);
            Assert.Contains("2026-06-20 01:02:03.456", s);
            Assert.DoesNotContain("Stack Trace:", s); // default ToString() omits the trace
        }

        [Fact]
        public void ToString_IncludeStackTrace_AppendsTraceWhenPresent()
        {
            var ts = new DateTime(2026, 6, 20, 1, 2, 3, 456, DateTimeKind.Utc);
            var withTrace = new LogEntry(GodotLogType.Error, "boom", ts, stackTrace: "at Baz()");

            var s = withTrace.ToString(includeStackTrace: true);

            Assert.Contains("Stack Trace:", s);
            Assert.Contains("at Baz()", s);
        }

        [Fact]
        public void ToString_IncludeStackTrace_NoTrace_OmitsTraceBlock()
        {
            var entry = new LogEntry(GodotLogType.Log, "plain", DateTime.UtcNow);

            // includeStackTrace=true but there is no trace → no "Stack Trace:" block.
            Assert.DoesNotContain("Stack Trace:", entry.ToString(includeStackTrace: true));
        }
    }
}
