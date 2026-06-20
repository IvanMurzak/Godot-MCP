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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the <c>console-*</c> tool WRAPPER (<see cref="Tool_Console"/>) — distinct
    /// from <see cref="GodotLogCollectorTests"/>, which exercises the ring buffer directly. These tests drive
    /// <see cref="Tool_Console.GetLogs"/> / <see cref="Tool_Console.ClearLogs"/> exactly as the framework
    /// dispatcher would and assert the wrapper's own contract:
    /// <list type="bullet">
    /// <item><c>GetLogs</c> reads the process-wide <see cref="GodotLogCollector.GetOrCreate"/> collector and
    /// forwards its <c>maxEntries</c> / <c>logTypeFilter</c> / <c>includeStackTrace</c> / <c>lastMinutes</c>
    /// arguments straight into <see cref="GodotLogCollector.Query"/> (newest-first, post-order cap).</item>
    /// <item><c>GetLogs</c> rejects <c>maxEntries &lt; 1</c> with an <see cref="ArgumentException"/> at the
    /// tool boundary (unlike the buffer's <c>Query</c>, which silently clamps the floor to 1).</item>
    /// <item><c>ClearLogs</c> empties the same shared collector that <c>GetLogs</c> reads.</item>
    /// </list>
    ///
    /// <para>
    /// The family is pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it runs in the plain xUnit
    /// host. It mutates the process-wide <see cref="GodotLogCollector.Current"/> static, so it joins the same
    /// serial collection as <see cref="GodotLogCollectorTests"/> (so the two classes never race the static)
    /// and snapshots/restores <c>Current</c> around every test.
    /// </para>
    /// </summary>
    [Collection(GodotLogCollectorCurrentCollection.Name)]
    public class ConsoleToolTests : IDisposable
    {
        readonly GodotLogCollector? _savedCurrent;

        public ConsoleToolTests()
        {
            _savedCurrent = GodotLogCollector.Current;
            GodotLogCollector.Current = null; // start from a known-empty slate each test.
        }

        public void Dispose()
        {
            GodotLogCollector.Current = _savedCurrent;
        }

        // ---- console-get-logs returns the collector's rows ---------------------------------------

        [Fact]
        public void GetLogs_ReturnsCollectorRows_NewestFirst()
        {
            var collector = GodotLogCollector.GetOrCreate();
            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Warning, "second");
            collector.Append(GodotLogType.Error, "third");

            var rows = new Tool_Console().GetLogs();

            Assert.Equal(3, rows.Length);
            // Query returns newest-first.
            Assert.Equal("third", rows[0].Message);
            Assert.Equal("second", rows[1].Message);
            Assert.Equal("first", rows[2].Message);
        }

        [Fact]
        public void GetLogs_NoCollectorYet_AutoCreatesEmpty_ReturnsNoRows()
        {
            // Current is null (set in the ctor); the wrapper must GetOrCreate() rather than NRE.
            Assert.Null(GodotLogCollector.Current);

            var rows = new Tool_Console().GetLogs();

            Assert.Empty(rows);
            Assert.NotNull(GodotLogCollector.Current); // GetOrCreate published a fresh buffer.
        }

        [Fact]
        public void GetLogs_ForwardsSeverityFilter_ToQuery()
        {
            var collector = GodotLogCollector.GetOrCreate();
            collector.Append(GodotLogType.Log, "info");
            collector.Append(GodotLogType.Warning, "warn");
            collector.Append(GodotLogType.Error, "err");

            var onlyErrors = new Tool_Console().GetLogs(logTypeFilter: GodotLogType.Error);

            Assert.Single(onlyErrors);
            Assert.Equal(GodotLogType.Error, onlyErrors[0].LogType);
            Assert.Equal("err", onlyErrors[0].Message);
        }

        [Fact]
        public void GetLogs_ForwardsMaxEntriesCap_KeepingNewest()
        {
            var collector = GodotLogCollector.GetOrCreate();
            collector.Append(GodotLogType.Log, "a");
            collector.Append(GodotLogType.Log, "b");
            collector.Append(GodotLogType.Log, "c");

            var capped = new Tool_Console().GetLogs(maxEntries: 2);

            // Cap is applied AFTER newest-first ordering, so the two most-recent survive.
            Assert.Equal(2, capped.Length);
            Assert.Equal("c", capped[0].Message);
            Assert.Equal("b", capped[1].Message);
        }

        [Fact]
        public void GetLogs_IncludeStackTraceFalse_StripsStackTrace()
        {
            var collector = GodotLogCollector.GetOrCreate();
            collector.Append(GodotLogType.Error, "boom", stackTrace: "at Foo()\nat Bar()");

            var stripped = new Tool_Console().GetLogs(); // includeStackTrace defaults to false
            Assert.Single(stripped);
            Assert.Null(stripped[0].StackTrace);

            var withTrace = new Tool_Console().GetLogs(includeStackTrace: true);
            Assert.Single(withTrace);
            Assert.Equal("at Foo()\nat Bar()", withTrace[0].StackTrace);
        }

        [Theory]
        // The tool boundary rejects a non-positive cap; the buffer's Query would silently clamp it to 1.
        [InlineData(0)]
        [InlineData(-5)]
        public void GetLogs_RejectsNonPositiveMaxEntries(int maxEntries)
        {
            var ex = Assert.Throws<ArgumentException>(() => new Tool_Console().GetLogs(maxEntries: maxEntries));
            Assert.Equal("maxEntries", ex.ParamName);
        }

        // ---- console-clear-logs empties the shared collector -------------------------------------

        [Fact]
        public void ClearLogs_EmptiesTheCollector_GetLogsThenReturnsNone()
        {
            var collector = GodotLogCollector.GetOrCreate();
            collector.Append(GodotLogType.Log, "one");
            collector.Append(GodotLogType.Log, "two");
            Assert.Equal(2, new Tool_Console().GetLogs().Length); // sanity: populated.

            new Tool_Console().ClearLogs();

            Assert.Empty(new Tool_Console().GetLogs());
            Assert.Equal(0, collector.Count); // same shared buffer.
        }

        [Fact]
        public void ClearLogs_NoCollectorYet_AutoCreates_DoesNotThrow()
        {
            // Current is null; ClearLogs must GetOrCreate() (no NRE) and leave an empty buffer.
            Assert.Null(GodotLogCollector.Current);

            var ex = Record.Exception(() => new Tool_Console().ClearLogs());

            Assert.Null(ex);
            Assert.NotNull(GodotLogCollector.Current);
            Assert.Empty(new Tool_Console().GetLogs());
        }

        [Fact]
        public void ToolIds_AreStableNames()
        {
            Assert.Equal("console-get-logs", Tool_Console.ConsoleGetLogsToolId);
            Assert.Equal("console-clear-logs", Tool_Console.ConsoleClearLogsToolId);
        }
    }
}
