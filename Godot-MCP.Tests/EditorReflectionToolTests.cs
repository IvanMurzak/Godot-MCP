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
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the editor-introspection tool family (issue #16): the in-memory
    /// <see cref="GodotLogCollector"/> (the only piece with non-trivial logic — bounded FIFO ring buffer,
    /// newest-first query, severity / time-window filtering, stack-trace strip) and the result models
    /// (<see cref="LogEntry"/>, <see cref="EditorStateData"/>, <see cref="SelectionData"/>). None of these
    /// touch a live Godot editor, so they run in the plain xUnit host with no Godot binary. The
    /// editor-driving handlers themselves — <c>Tool_Editor.*.cs</c> (play state), <c>Tool_Editor.Selection.*.cs</c>
    /// (selection), and the editor effect of <c>Tool_Console.*</c> / <c>Tool_Reflection.*</c> — are verified
    /// by the headless Godot smoke (test.md Suite 3).
    /// </summary>
    public class EditorReflectionToolTests
    {
        // ---- GodotLogCollector: append / query ordering ------------------------------------------

        [Fact]
        public void Query_ReturnsNewestFirst()
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            c.Append(new LogEntry(GodotLogType.Log, "first", t0));
            c.Append(new LogEntry(GodotLogType.Log, "second", t0.AddSeconds(1)));
            c.Append(new LogEntry(GodotLogType.Log, "third", t0.AddSeconds(2)));

            var result = c.Query();

            Assert.Equal(3, result.Length);
            Assert.Equal("third", result[0].Message);
            Assert.Equal("second", result[1].Message);
            Assert.Equal("first", result[2].Message);
        }

        [Fact]
        public void Query_MaxEntries_CapsToMostRecent()
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            for (int i = 0; i < 5; i++)
                c.Append(new LogEntry(GodotLogType.Log, $"m{i}", t0.AddSeconds(i)));

            var result = c.Query(maxEntries: 2);

            Assert.Equal(2, result.Length);
            Assert.Equal("m4", result[0].Message);
            Assert.Equal("m3", result[1].Message);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-5)]
        public void Query_NonPositiveMaxEntries_ClampedToOne(int maxEntries)
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            c.Append(new LogEntry(GodotLogType.Log, "a", t0));
            c.Append(new LogEntry(GodotLogType.Log, "b", t0.AddSeconds(1)));

            var result = c.Query(maxEntries: maxEntries);

            Assert.Single(result);
            Assert.Equal("b", result[0].Message);
        }

        // ---- GodotLogCollector: severity filter --------------------------------------------------

        [Fact]
        public void Query_LogTypeFilter_RestrictsToSeverity()
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            c.Append(new LogEntry(GodotLogType.Log, "info", t0));
            c.Append(new LogEntry(GodotLogType.Warning, "warn", t0.AddSeconds(1)));
            c.Append(new LogEntry(GodotLogType.Error, "err", t0.AddSeconds(2)));

            var errors = c.Query(logTypeFilter: GodotLogType.Error);
            Assert.Single(errors);
            Assert.Equal("err", errors[0].Message);

            var warnings = c.Query(logTypeFilter: GodotLogType.Warning);
            Assert.Single(warnings);
            Assert.Equal("warn", warnings[0].Message);

            var all = c.Query();
            Assert.Equal(3, all.Length);
        }

        // ---- GodotLogCollector: time window ------------------------------------------------------

        [Fact]
        public void Query_LastMinutes_FiltersByWindow()
        {
            var c = new GodotLogCollector();
            var now = DateTime.UtcNow;
            c.Append(new LogEntry(GodotLogType.Log, "old", now.AddMinutes(-30)));
            c.Append(new LogEntry(GodotLogType.Log, "recent", now.AddMinutes(-1)));

            var result = c.Query(lastMinutes: 5);

            Assert.Single(result);
            Assert.Equal("recent", result[0].Message);
        }

        [Fact]
        public void Query_LastMinutesZero_ReturnsAll()
        {
            var c = new GodotLogCollector();
            var now = DateTime.UtcNow;
            c.Append(new LogEntry(GodotLogType.Log, "old", now.AddHours(-2)));
            c.Append(new LogEntry(GodotLogType.Log, "recent", now));

            var result = c.Query(lastMinutes: 0);

            Assert.Equal(2, result.Length);
        }

        // ---- GodotLogCollector: stack-trace strip ------------------------------------------------

        [Fact]
        public void Query_IncludeStackTraceFalse_StripsStackTrace()
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            c.Append(new LogEntry(GodotLogType.Error, "boom", t0, stackTrace: "at Foo()\nat Bar()"));

            var stripped = c.Query(includeStackTrace: false);
            Assert.Single(stripped);
            Assert.Null(stripped[0].StackTrace);

            var kept = c.Query(includeStackTrace: true);
            Assert.Single(kept);
            Assert.Equal("at Foo()\nat Bar()", kept[0].StackTrace);
        }

        // ---- GodotLogCollector: bounded ring buffer ----------------------------------------------

        [Fact]
        public void Append_BeyondCapacity_EvictsOldest()
        {
            var c = new GodotLogCollector();
            var t0 = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            // One past capacity: the very first entry must be evicted.
            for (int i = 0; i < GodotLogCollector.Capacity + 1; i++)
                c.Append(new LogEntry(GodotLogType.Log, $"m{i}", t0.AddSeconds(i)));

            Assert.Equal(GodotLogCollector.Capacity, c.Count);

            // Newest is the last appended; the evicted "m0" must be gone even when asking for everything.
            var all = c.Query(maxEntries: GodotLogCollector.Capacity);
            Assert.Equal($"m{GodotLogCollector.Capacity}", all[0].Message);
            Assert.DoesNotContain(all, e => e.Message == "m0");
        }

        // ---- GodotLogCollector: clear ------------------------------------------------------------

        [Fact]
        public void Clear_EmptiesBuffer()
        {
            var c = new GodotLogCollector();
            c.Append(GodotLogType.Log, "x");
            Assert.Equal(1, c.Count);

            c.Clear();

            Assert.Equal(0, c.Count);
            Assert.Empty(c.Query());
        }

        // ---- GodotLogCollector: Append convenience overload + null guard -------------------------

        [Fact]
        public void Append_PartsOverload_StampsNowUtc()
        {
            var c = new GodotLogCollector();
            var before = DateTime.UtcNow.AddSeconds(-1);
            c.Append(GodotLogType.Warning, "hello", stackTrace: "trace");
            var after = DateTime.UtcNow.AddSeconds(1);

            var e = Assert.Single(c.Query(includeStackTrace: true));
            Assert.Equal(GodotLogType.Warning, e.LogType);
            Assert.Equal("hello", e.Message);
            Assert.Equal("trace", e.StackTrace);
            Assert.InRange(e.Timestamp, before, after);
        }

        [Fact]
        public void Append_Null_IsIgnored()
        {
            var c = new GodotLogCollector();
            c.Append((LogEntry)null!);
            Assert.Equal(0, c.Count);
        }

        // ---- Models: serialization round-trips ---------------------------------------------------

        [Fact]
        public void LogEntry_RoundTripsJson()
        {
            var t0 = new DateTime(2026, 3, 4, 12, 0, 0, DateTimeKind.Utc);
            var entry = new LogEntry(GodotLogType.Error, "kaput", t0, "at X()");

            var json = JsonSerializer.Serialize(entry);
            var back = JsonSerializer.Deserialize<LogEntry>(json);

            Assert.NotNull(back);
            Assert.Equal(GodotLogType.Error, back!.LogType);
            Assert.Equal("kaput", back.Message);
            Assert.Equal("at X()", back.StackTrace);
            Assert.Equal(t0, back.Timestamp);

            // Property names use the camelCase JSON contract.
            Assert.Contains("\"logType\"", json);
            Assert.Contains("\"stackTrace\"", json);
        }

        [Fact]
        public void EditorStateData_RoundTripsJson()
        {
            var data = new EditorStateData
            {
                IsPlaying = true,
                PlayingScene = "res://main.tscn",
                EditorVersion = "4.5.1.stable.mono",
            };

            var json = JsonSerializer.Serialize(data);
            var back = JsonSerializer.Deserialize<EditorStateData>(json);

            Assert.NotNull(back);
            Assert.True(back!.IsPlaying);
            Assert.Equal("res://main.tscn", back.PlayingScene);
            Assert.Equal("4.5.1.stable.mono", back.EditorVersion);
            Assert.Contains("\"isPlaying\"", json);
            Assert.Contains("\"playingScene\"", json);
            Assert.Contains("\"editorVersion\"", json);
        }

        [Fact]
        public void SelectionData_RoundTripsJson_WithNodes()
        {
            var data = new SelectionData
            {
                Nodes =
                {
                    new NodeData { InstanceId = 10, Name = "A", Path = "/root/A", Type = "Node2D" },
                    new NodeData { InstanceId = 20, Name = "B", Path = "/root/B", Type = "Sprite2D" },
                },
            };
            data.Count = data.Nodes.Count;
            data.ActiveNode = data.Nodes.Last();

            var json = JsonSerializer.Serialize(data);
            var back = JsonSerializer.Deserialize<SelectionData>(json);

            Assert.NotNull(back);
            Assert.Equal(2, back!.Count);
            Assert.Equal(2, back.Nodes.Count);
            Assert.Equal("A", back.Nodes[0].Name);
            Assert.NotNull(back.ActiveNode);
            Assert.Equal("B", back.ActiveNode!.Name);
            Assert.Contains("\"activeNode\"", json);
        }

        [Fact]
        public void SelectionData_Empty_HasNullActiveAndZeroCount()
        {
            var data = new SelectionData();
            var json = JsonSerializer.Serialize(data);
            var back = JsonSerializer.Deserialize<SelectionData>(json);

            Assert.NotNull(back);
            Assert.Empty(back!.Nodes);
            Assert.Null(back.ActiveNode);
            Assert.Equal(0, back.Count);
        }
    }

    /// <summary>
    /// The single <see cref="GodotLogCollector.GetOrCreate"/> fallback fact that mutates the process-wide
    /// <see cref="GodotLogCollector.Current"/> static — extracted out of the otherwise-parallel
    /// <see cref="EditorReflectionToolTests"/> so it joins the serial
    /// <see cref="GodotLogCollectorCurrentCollection"/> (shared with <see cref="GodotLogCollectorTests"/> /
    /// <see cref="ConsoleToolTests"/>) and can never race the concurrent <c>Current = null</c> in their ctors
    /// (issue #195). Snapshots/restores <c>Current</c> around the test, mirroring those classes' pattern, so
    /// it is non-destructive even when run alongside them in the same serial collection.
    /// </summary>
    [Collection(GodotLogCollectorCurrentCollection.Name)]
    public class GodotLogCollectorGetOrCreateTests : IDisposable
    {
        readonly GodotLogCollector? _savedCurrent;

        public GodotLogCollectorGetOrCreateTests()
        {
            _savedCurrent = GodotLogCollector.Current;
            GodotLogCollector.Current = null;
        }

        public void Dispose()
        {
            GodotLogCollector.Current = _savedCurrent;
        }

        [Fact]
        public void GetOrCreate_WhenCurrentNull_ReturnsAndCachesFresh()
        {
            GodotLogCollector.Current = null;
            var first = GodotLogCollector.GetOrCreate();
            Assert.NotNull(first);
            // Caches into Current, so a second call returns the SAME instance.
            Assert.Same(first, GodotLogCollector.GetOrCreate());
            Assert.Same(first, GodotLogCollector.Current);
        }
    }
}
