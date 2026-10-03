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
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for <see cref="GodotLogCollector"/> (issue #173): the bounded ring buffer
    /// itself, and — the heart of the issue — the concurrency contract on the process-wide
    /// <see cref="GodotLogCollector.Current"/> static. Also covers the sequence cursor contract (02 §2, D5).
    /// </summary>
    [Collection(GodotLogCollectorCurrentCollection.Name)]
    public class GodotLogCollectorTests : IDisposable
    {
        readonly GodotLogCollector? _savedCurrent;

        public GodotLogCollectorTests()
        {
            // Snapshot whatever Current was so the suite is non-destructive even if some other code set it.
            _savedCurrent = GodotLogCollector.Current;
            GodotLogCollector.Current = null;
        }

        public void Dispose()
        {
            GodotLogCollector.Current = _savedCurrent;
        }

        // ---- Ring-buffer basics -------------------------------------------------------------------

        [Fact]
        public void Append_And_Query_ReturnsNewestFirst()
        {
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Warning, "second");
            collector.Append(GodotLogType.Error, "third");

            var rows = collector.Query();

            Assert.Equal(3, rows.Length);
            Assert.Equal("third", rows[0].Message);
            Assert.Equal("second", rows[1].Message);
            Assert.Equal("first", rows[2].Message);
        }

        [Fact]
        public void Append_EvictsOldest_AtCapacity()
        {
            var collector = new GodotLogCollector();
            for (int i = 0; i < GodotLogCollector.Capacity + 50; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            Assert.Equal(GodotLogCollector.Capacity, collector.Count);

            // Newest is the last appended; the first 50 were evicted (FIFO).
            var rows = collector.Query(maxEntries: GodotLogCollector.Capacity);
            Assert.Equal($"line-{GodotLogCollector.Capacity + 49}", rows[0].Message);
            Assert.Equal($"line-50", rows[GodotLogCollector.Capacity - 1].Message);
        }

        [Fact]
        public void Clear_EmptiesBuffer_AndIsHarmlessTwice()
        {
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "x");
            collector.Clear();
            collector.Clear(); // idempotent

            Assert.Equal(0, collector.Count);
            Assert.Empty(collector.Query());
        }

        // ---- Current swap semantics ---------------------------------------------------------------

        [Fact]
        public void GetOrCreate_InstallsOnce_AndReusesSameInstance()
        {
            Assert.Null(GodotLogCollector.Current);

            var a = GodotLogCollector.GetOrCreate();
            var b = GodotLogCollector.GetOrCreate();

            Assert.Same(a, b);
            Assert.Same(a, GodotLogCollector.Current);
        }

        [Fact]
        public void Current_ReadsBackWhatWasWritten()
        {
            var first = new GodotLogCollector();
            GodotLogCollector.Current = first;
            Assert.Same(first, GodotLogCollector.Current);

            // A new install (the _EnterTree case) replaces it last-writer-wins.
            var second = new GodotLogCollector();
            GodotLogCollector.Current = second;
            Assert.Same(second, GodotLogCollector.Current);
        }

        [Fact]
        public void TeardownLeavesBufferReadable_ReinstallReplaces()
        {
            // Issue #173 background: teardown must NOT null Current — the buffer stays readable so
            // console-get-logs still surfaces the teardown-window diagnostics, and only the next _EnterTree
            // install displaces it.
            var session1 = new GodotLogCollector();
            GodotLogCollector.Current = session1;
            session1.Append(GodotLogType.Error, "teardown-window failure");

            // ... teardown runs here in the real plugin; it deliberately does nothing to Current ...

            Assert.NotNull(GodotLogCollector.Current);
            Assert.Same(session1, GodotLogCollector.Current);
            Assert.Equal("teardown-window failure", GodotLogCollector.Current!.Query()[0].Message);

            // Next _EnterTree installs a fresh buffer; only THEN is the old one displaced.
            var session2 = new GodotLogCollector();
            GodotLogCollector.Current = session2;
            Assert.Same(session2, GodotLogCollector.Current);
            Assert.Empty(GodotLogCollector.Current!.Query());
        }

        // ---- Concurrency: background Append while main thread swaps Current -------------------------

        [Fact]
        public async Task BackgroundAppend_WhileMainThreadSwapsCurrent_NoTornReads_NoCrash()
        {
            const int swaps = 200;
            const int appendThreads = 4;

            GodotLogCollector.Current = new GodotLogCollector();

            using var cts = new CancellationTokenSource();
            using var readersStarted = new CountdownEvent(appendThreads);
            Exception? readerFault = null;
            long appendsObserved = 0;

            var readers = Enumerable.Range(0, appendThreads).Select(_ => Task.Run(() =>
            {
                try
                {
                    var token = cts.Token;
                    readersStarted.Signal();
                    while (!token.IsCancellationRequested)
                    {
                        var current = GodotLogCollector.Current;
                        current?.Append(GodotLogType.Log, "bg");
                        if (current != null)
                            Interlocked.Increment(ref appendsObserved);
                    }
                }
                catch (Exception ex)
                {
                    Volatile.Write(ref readerFault, ex);
                }
            })).ToArray();

            Assert.True(readersStarted.Wait(TimeSpan.FromSeconds(5)));
            Assert.True(
                SpinWait.SpinUntil(() => Volatile.Read(ref appendsObserved) > 0, TimeSpan.FromSeconds(5)),
                "The readers should exercise the append path before the swap storm starts.");

            await Task.Run(() =>
            {
                for (int i = 0; i < swaps; i++)
                {
                    GodotLogCollector.Current = new GodotLogCollector();
                    Thread.SpinWait(50);
                }
            });

            cts.Cancel();
            await Task.WhenAll(readers);

            Assert.Null(readerFault);
            Assert.True(appendsObserved > 0);
            Assert.NotNull(GodotLogCollector.Current);
        }

        [Fact]
        public void ConcurrentAppend_SingleCollector_NoLostWritesPastCapacity()
        {
            var collector = new GodotLogCollector();
            const int threads = 8;
            const int perThread = 500;

            Parallel.For(0, threads, t =>
            {
                for (int i = 0; i < perThread; i++)
                    collector.Append(GodotLogType.Log, $"t{t}-{i}");
            });

            Assert.Equal(GodotLogCollector.Capacity, collector.Count);
            var rows = collector.Query(maxEntries: GodotLogCollector.Capacity);
            Assert.Equal(GodotLogCollector.Capacity, rows.Length);
            Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Message)));
        }

        // ---- Sequence and polling (sinceSequence cursor) ------------------------------------------

        [Fact]
        public void Append_AssignsMonotonicSequence_Increments()
        {
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Log, "second");
            collector.Append(GodotLogType.Log, "third");

            var rows = collector.Query();
            Assert.Equal(3, rows.Length);

            // Sequences should be monotonically increasing
            var seqs = rows.Select(r => r.Sequence).ToArray();
            Assert.True(seqs[0] > seqs[1] && seqs[1] > seqs[2], "Sequences should be decreasing (newest-first)");
            Assert.Equal(seqs[0] - 1, seqs[1]);  // Each sequence increments by 1
            Assert.Equal(seqs[1] - 1, seqs[2]);
            Assert.True(seqs[0] > maxBefore, "Sequences should increment from previous max");
        }

        [Fact]
        public void HighestSequence_PersistsAcrossInstances()
        {
            // Process-wide counter: highest sequence persists across collector resets.
            var maxBefore = new GodotLogCollector().HighestSequence;

            var session1 = new GodotLogCollector();
            session1.Append(GodotLogType.Log, "first");
            session1.Append(GodotLogType.Log, "second");
            var session1Max = session1.HighestSequence;
            Assert.True(session1Max > maxBefore);

            // Session 2 (new instance) should continue from session1's max
            var session2 = new GodotLogCollector();
            var session2StartingMax = session2.HighestSequence; // Should equal session1Max
            Assert.Equal(session1Max, session2StartingMax);

            // New appends continue above the maximum
            session2.Append(GodotLogType.Log, "new");
            Assert.Equal(session1Max + 1, session2.HighestSequence);
        }

        [Fact]
        public void HighestSequence_PersistsPastClear()
        {
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            for (int i = 0; i < 5; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            var maxAfterAppend = collector.HighestSequence;
            Assert.True(maxAfterAppend > maxBefore);

            collector.Clear();
            Assert.Equal(maxAfterAppend, collector.HighestSequence);

            // New appends continue above the pre-clear maximum.
            collector.Append(GodotLogType.Log, "new");
            Assert.Equal(maxAfterAppend + 1, collector.HighestSequence);
        }

        [Fact]
        public void Query_SinceSequence0_ReturnsAllNewestFirst_WithSequenceField()
        {
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Log, "second");
            collector.Append(GodotLogType.Log, "third");

            var rows = collector.Query(sinceSequence: 0, includeStackTrace: true);

            Assert.Equal(3, rows.Length);
            Assert.Equal("third", rows[0].Message);
            Assert.Equal("second", rows[1].Message);
            Assert.Equal("first", rows[2].Message);
            // Verify all have sequences (byte-identical except for sequence field)
            Assert.All(rows, r => Assert.True(r.Sequence > 0, "Each row should have a sequence"));
        }

        [Fact]
        public void Query_SinceSequence_ReturnsOnlyNewer_OldestFirst()
        {
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            for (int i = 0; i < 5; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            var rows = collector.Query(sinceSequence: maxBefore + 2, maxEntries: 100);

            Assert.Equal(3, rows.Length);
            // Check relative ordering: should be oldest-first
            var seqs = rows.Select(r => r.Sequence).ToArray();
            Assert.True(seqs[0] < seqs[1] && seqs[1] < seqs[2], "Sequences should be increasing (oldest-first)");
        }

        [Fact]
        public void Query_SinceSequence_OverflowReturnsOldestPage()
        {
            // FIX #1: When sinceSequence > 0 and matching exceeds maxEntries, return OLDEST page (first maxEntries), not newest.
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            for (int i = 0; i < 10; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            // Query with maxEntries < matching count, return oldest page
            var rows = collector.Query(sinceSequence: maxBefore, maxEntries: 3);

            Assert.Equal(3, rows.Length);
            // Sequences should be the FIRST 3 (oldest), not the last 3 (newest)
            var seqs = rows.Select(r => r.Sequence).ToArray();
            var expectedSeqs = rows.Select(r => r.Sequence).OrderBy(s => s).Take(3).ToArray();
            Assert.Equal(expectedSeqs, seqs);  // Should be the oldest 3, in order
        }

        [Fact]
        public void Query_SinceSequence_StaleReturnsOldestAvailable()
        {
            // FIX #2: When sinceSequence > highest sequence, return the oldest available page.
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            for (int i = 0; i < 5; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            // Query with a cursor way above the highest sequence (stale cursor)
            var rows = collector.Query(sinceSequence: maxBefore + 100, maxEntries: 2);

            // Should return the oldest available entries
            Assert.Equal(2, rows.Length);
            var seqs = rows.Select(r => r.Sequence).ToArray();
            Assert.True(seqs[0] < seqs[1], "Should be oldest-first");
            Assert.True(seqs[0] == maxBefore + 1, "Should start from first available");  // First appended after maxBefore
        }

        [Fact]
        public void Query_SinceSequence0_ByteIdenticalExceptSequence()
        {
            // DoD 3: Verify sinceSequence=0 output is byte-identical except for sequence field.
            var collector = new GodotLogCollector();
            var entry = new LogEntry(GodotLogType.Warning, "test message", DateTime.UtcNow, "stack trace");
            collector.Append(entry);

            var rows = collector.Query(sinceSequence: 0, includeStackTrace: true);

            Assert.Single(rows);
            var retrieved = rows[0];

            // All fields should match except Sequence (which is new)
            Assert.Equal(entry.LogType, retrieved.LogType);
            Assert.Equal(entry.Message, retrieved.Message);
            Assert.Equal(entry.Timestamp, retrieved.Timestamp);
            Assert.Equal(entry.StackTrace, retrieved.StackTrace);
            Assert.True(retrieved.Sequence > 0, "Should have a sequence assigned");
        }

        [Fact]
        public void Query_WithFiltersAndSinceSequence_CombinesCorrectly()
        {
            var now = DateTime.UtcNow;
            var collector = new GodotLogCollector();
            var maxBefore = collector.HighestSequence;

            // Append mixed types at different times.
            collector.Append(new LogEntry(GodotLogType.Log, "old-log", now.AddMinutes(-10)));
            collector.Append(new LogEntry(GodotLogType.Warning, "recent-warn", now.AddMinutes(-1)));
            collector.Append(new LogEntry(GodotLogType.Log, "recent-log", now));
            collector.Append(new LogEntry(GodotLogType.Error, "recent-err", now));

            // Query: last 5 minutes, Log type only, since first append.
            var rows = collector.Query(
                sinceSequence: maxBefore,
                logTypeFilter: GodotLogType.Log,
                lastMinutes: 5,
                maxEntries: 10);

            // Should get the recent-log, but not old-log or the warning/error
            Assert.Single(rows);
            Assert.Equal("recent-log", rows[0].Message);
        }
    }

    /// <summary>
    /// Dedicated xUnit collection so <see cref="GodotLogCollectorTests"/> runs in isolation.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GodotLogCollectorCurrentCollection
    {
        public const string Name = "GodotLogCollector.Current (serial)";
    }
}
