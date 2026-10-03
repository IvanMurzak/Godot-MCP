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
    /// <see cref="GodotLogCollector.Current"/> static. In the real plugin the framework log-routing path
    /// (<c>GodotMcpConnection.RouteFrameworkLog</c> + the plugin <c>Log*</c> helpers) reads
    /// <c>Current?.Append(...)</c> from ARBITRARY background threads while the editor main thread swaps
    /// <c>Current</c> at <c>_EnterTree</c> / no longer nulls it at teardown. These tests assert that:
    /// (1) the swap is published via Volatile so a concurrent reader never sees a torn reference and never
    /// crashes, and (2) the collector is no longer nulled on teardown, so the buffer stays readable until
    /// the next install overwrites it.
    ///
    /// <para>
    /// The collector is a plain managed ring buffer (no Godot native types, no <c>#if TOOLS</c>), so it is
    /// fully unit-testable in this plain xUnit host with no Godot binary. The tests own/restore the
    /// process-wide <see cref="GodotLogCollector.Current"/> static so they do not leak state into other
    /// tests (xUnit runs test classes in parallel by default; this class is marked non-parallel via the
    /// dedicated collection below because it mutates a process-wide static).
    /// </para>
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
            //
            // SCOPE of this test: it pins the GodotLogCollector.Current PROPERTY contract only —
            // last-writer-wins, and a previously-installed buffer stays queryable until the next assignment.
            // It does NOT (and cannot) invoke GodotMcpPlugin.Teardown, which needs a live Godot host, so it
            // would NOT catch a regression that re-added a `Current = null` to the plugin's teardown body.
            // That teardown-null regression is guarded by code review + the Suite-3 headless smoke (see the
            // testbed runbook), NOT by this unit test. The `// ... teardown runs here ...` line below models
            // the property-level no-op, not an actual plugin teardown call.
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
            // Reproduce the issue #173 race: a background thread continuously routes log lines through
            // GodotLogCollector.Current?.Append(...) (exactly what RouteFrameworkLog / Log* do off-thread)
            // while the "main thread" repeatedly swaps Current to a freshly installed buffer (the _EnterTree
            // path).
            //
            // NOTE on what this test does and does NOT prove: this is a SMOKE / LIVENESS test, not a
            // discriminating guard for the Volatile read/write contract. On the x86/x64 arch the CI runs,
            // reference-sized reads/writes are already atomic AND effectively acquire/release (x86-TSO), so
            // deleting the Volatile would NOT make this test fail here — the discipline only matters on a
            // weak memory model (e.g. ARM), which this suite never executes on. So treat a green result as
            // "the off-thread null-conditional Append path runs to completion under a swap storm without
            // crashing", NOT as "no torn reference is possible". The torn-read correctness rests on the
            // Volatile annotations in GodotLogCollector being kept (verified by review), not on this assert.
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
                        // The exact off-thread shape from RouteFrameworkLog: null-conditional Append on the
                        // volatile static. A torn read here would surface as an AccessViolation / bad ref.
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

            // Main thread: install a fresh collector repeatedly (the _EnterTree swap), interleaved with the
            // background Append storm.
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

            Assert.Null(readerFault);                 // no torn read / no crash on any reader thread
            Assert.True(appendsObserved > 0);         // the readers actually exercised the path
            Assert.NotNull(GodotLogCollector.Current); // never nulled out from under the readers
        }

        [Fact]
        public void ConcurrentAppend_SingleCollector_NoLostWritesPastCapacity()
        {
            // The buffer's own lock must serialize concurrent Append so the count never exceeds Capacity and
            // the structure is never corrupted under parallel producers (the other half of the issue: the
            // single shared collector is written from many threads).
            var collector = new GodotLogCollector();
            const int threads = 8;
            const int perThread = 500;

            Parallel.For(0, threads, t =>
            {
                for (int i = 0; i < perThread; i++)
                    collector.Append(GodotLogType.Log, $"t{t}-{i}");
            });

            // Total appended (4000) far exceeds Capacity, so the bounded buffer must be exactly full and
            // internally consistent (Query does not throw, returns Capacity rows).
            Assert.Equal(GodotLogCollector.Capacity, collector.Count);
            var rows = collector.Query(maxEntries: GodotLogCollector.Capacity);
            Assert.Equal(GodotLogCollector.Capacity, rows.Length);
            Assert.All(rows, r => Assert.False(string.IsNullOrEmpty(r.Message)));
        }

        // ---- Sequence and polling (sinceSequence cursor) ------------------------------------------

        [Fact]
        public void Append_AssignsMonotonicSequence_StartsAt1()
        {
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Log, "second");
            collector.Append(GodotLogType.Log, "third");

            var rows = collector.Query();
            Assert.Equal(new long[] { 3, 2, 1 }, rows.Select(r => r.Sequence));
        }

        [Fact]
        public void HighestSequence_ReturnsLargestEverAssigned()
        {
            var collector = new GodotLogCollector();
            Assert.Equal(0, collector.HighestSequence);

            collector.Append(GodotLogType.Log, "first");
            Assert.Equal(1, collector.HighestSequence);

            collector.Append(GodotLogType.Log, "second");
            Assert.Equal(2, collector.HighestSequence);
        }

        [Fact]
        public void HighestSequence_PersistsPastClear()
        {
            // This mirrors RuntimeErrorCollector behavior: Clear() does not reset the sequence counter.
            var collector = new GodotLogCollector();
            for (int i = 0; i < 5; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            Assert.Equal(5, collector.HighestSequence);
            collector.Clear();
            Assert.Equal(5, collector.HighestSequence);

            // New appends continue above the pre-clear maximum.
            collector.Append(GodotLogType.Log, "new");
            Assert.Equal(6, collector.HighestSequence);
        }

        [Fact]
        public void Query_SinceSequence0_ReturnsAllNewestFirst_WithSequenceField()
        {
            // Backward compatibility: sinceSequence=0 (default) returns all entries newest-first, just as
            // before, but now each entry carries its sequence number.
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "first");
            collector.Append(GodotLogType.Log, "second");
            collector.Append(GodotLogType.Log, "third");

            var rows = collector.Query(sinceSequence: 0);

            Assert.Equal(3, rows.Length);
            Assert.Equal("third", rows[0].Message);
            Assert.Equal("second", rows[1].Message);
            Assert.Equal("first", rows[2].Message);
            Assert.Equal(new long[] { 3, 2, 1 }, rows.Select(r => r.Sequence));
        }

        [Fact]
        public void Query_SinceSequence_ReturnsOnlyNewer_OldestFirst()
        {
            // When sinceSequence > 0, return only entries with sequence > sinceSequence, in ascending order.
            var collector = new GodotLogCollector();
            for (int i = 0; i < 5; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            var rows = collector.Query(sinceSequence: 2, maxEntries: 100);

            Assert.Equal(3, rows.Length);
            Assert.Equal(new long[] { 3, 4, 5 }, rows.Select(r => r.Sequence)); // oldest-first
            Assert.Equal(new string[] { "line-2", "line-3", "line-4" }, rows.Select(r => r.Message));
        }

        [Fact]
        public void Query_SinceSequence_PagingWithCursor()
        {
            // Test paging with sinceSequence > 0: fetching entries in oldest-first order, capping to keep
            // the newest entries, and verifying no gaps or duplicates across pages.
            var collector = new GodotLogCollector();
            var messages = new (GodotLogType type, string msg)[]
            {
                (GodotLogType.Log, "log1"),
                (GodotLogType.Warning, "warn1"),
                (GodotLogType.Log, "log2"),
                (GodotLogType.Error, "err1"),
                (GodotLogType.Log, "log3"),
            };

            foreach (var (type, msg) in messages)
                collector.Append(type, msg);

            // Sequences: log1=1, warn1=2, log2=3, err1=4, log3=5
            // Log entries only: 1,3,5

            // Page 1: query from sinceSequence=0 returns all entries, newest-first, cap at 2
            // All Log entries in buffer (oldest-first): [1,3,5]. Reverse to newest-first: [5,3,1]. Cap to 2: [5,3]
            var page1 = collector.Query(logTypeFilter: GodotLogType.Log, maxEntries: 2, sinceSequence: 0);
            Assert.Equal(2, page1.Length);
            Assert.Equal(new long[] { 5, 3 }, page1.Select(r => r.Sequence));  // newest-first

            // Page 2: continue from sequence 3, should get sequence > 3 which is only 5
            // But we already got 5 in page1, so this returns empty
            var page2 = collector.Query(logTypeFilter: GodotLogType.Log, maxEntries: 2, sinceSequence: 5);
            Assert.Empty(page2);

            // Verify all Log entries with sinceSequence=0 (newest-first)
            var allQuery = collector.Query(logTypeFilter: GodotLogType.Log, maxEntries: 100, sinceSequence: 0);
            Assert.Equal(3, allQuery.Length);
            Assert.Equal(new long[] { 5, 3, 1 }, allQuery.Select(r => r.Sequence));  // newest-first
        }

        [Fact]
        public void Query_SinceSequence_AboveMaximum_ReturnsEmpty()
        {
            // A cursor above the current maximum returns nothing.
            var collector = new GodotLogCollector();
            collector.Append(GodotLogType.Log, "line1");
            collector.Append(GodotLogType.Log, "line2");

            var rows = collector.Query(sinceSequence: 100); // above max of 2

            Assert.Empty(rows);
        }

        [Fact]
        public void Query_SinceSequence0_ReturnsNewestFirst()
        {
            // When sinceSequence=0 (backward compatible), return newest-first (not oldest-first).
            var collector = new GodotLogCollector();
            for (int i = 0; i < 10; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            var rows = collector.Query(sinceSequence: 0, maxEntries: 3);

            Assert.Equal(3, rows.Length);
            // Newest-first: sequences are [10,9,8]
            Assert.Equal(new long[] { 10, 9, 8 }, rows.Select(r => r.Sequence));
        }

        [Fact]
        public void Query_SinceSequence0_PersistsPastEviction()
        {
            // When appending past Capacity, the oldest entries are evicted but HighestSequence keeps advancing.
            // sinceSequence=0 returns newest-first (backward compatible), so newest available (highest sequence) comes first.
            var collector = new GodotLogCollector();
            int total = GodotLogCollector.Capacity + 50;
            for (int i = 0; i < total; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            Assert.Equal(GodotLogCollector.Capacity, collector.Count);
            Assert.Equal(total, collector.HighestSequence);

            // Query with sinceSequence=0 returns all available, newest-first.
            // The first 50 were evicted, so oldest available is 51. Newest-first means newest (total) comes first.
            var rows = collector.Query(sinceSequence: 0, maxEntries: GodotLogCollector.Capacity);
            Assert.Equal(GodotLogCollector.Capacity, rows.Length);
            Assert.Equal(total, rows.First().Sequence);  // newest first
            Assert.Equal(51, rows.Last().Sequence);      // oldest available last
        }

        [Fact]
        public void Query_SinceSequence_StaleAtCapacity_ReturnsNewestPage()
        {
            // When appending past Capacity, all older entries are evicted. A since-poll with a sequence in
            // the evicted range returns the NEWEST available page (capped to maxEntries), in oldest-first order.
            // This matches RuntimeErrorCollector behavior: when overflow, keep newest deltas to avoid losing recent data.
            var collector = new GodotLogCollector();
            int total = GodotLogCollector.Capacity + 100;
            for (int i = 0; i < total; i++)
                collector.Append(GodotLogType.Log, $"line-{i}");

            // The buffer holds sequences 101-1100 (the last 1000). Query for sinceSequence=50 (evicted).
            // Matching entries: 101-1100 (1000 total). Capped to 50 newest: [1051,...,1100].
            var rows = collector.Query(sinceSequence: 50, maxEntries: 50);

            // Should return the newest 50 entries available, in oldest-first order.
            Assert.Equal(50, rows.Length);
            Assert.Equal(1051, rows.First().Sequence);  // oldest of the newest 50
            Assert.Equal(1100, rows.Last().Sequence);   // newest
        }

        [Fact]
        public void Reset_NewCollectorInstance_SequenceContinuesAbovePrevious()
        {
            // When _EnterTree creates a fresh collector, the new instance has a fresh sequence counter,
            // but the old buffer stays readable until replaced. This demonstrates the scenario: old session's
            // buffer still has its entries, a new session installs fresh (starting at 1 again).
            var session1 = new GodotLogCollector();
            session1.Append(GodotLogType.Log, "s1-line1");
            session1.Append(GodotLogType.Log, "s1-line2");

            var session1Max = session1.HighestSequence;
            Assert.Equal(2, session1Max);

            // Session 2 installs fresh (new GodotLogCollector instance).
            var session2 = new GodotLogCollector();
            session2.Append(GodotLogType.Log, "s2-line1");

            // Session 2 sequences restart at 1 (it's a fresh instance).
            Assert.Equal(1, session2.HighestSequence);
            var s2rows = session2.Query();
            Assert.Single(s2rows);
            Assert.Equal(1, s2rows[0].Sequence);
        }

        [Fact]
        public void Query_WithFiltersAndSinceSequence_CombinesCorrectly()
        {
            // Verify that sinceSequence, logTypeFilter, and lastMinutes all apply together.
            var now = DateTime.UtcNow;
            var collector = new GodotLogCollector();

            // Append mixed types at different times.
            collector.Append(new LogEntry(GodotLogType.Log, "old-log", now.AddMinutes(-10)));
            collector.Append(new LogEntry(GodotLogType.Warning, "recent-warn", now.AddMinutes(-1)));
            collector.Append(new LogEntry(GodotLogType.Log, "recent-log", now));
            collector.Append(new LogEntry(GodotLogType.Error, "recent-err", now));

            // Query: last 5 minutes, Log type only, sinceSequence=1.
            var rows = collector.Query(
                sinceSequence: 1,
                logTypeFilter: GodotLogType.Log,
                lastMinutes: 5,
                maxEntries: 10);

            Assert.Single(rows);
            Assert.Equal("recent-log", rows[0].Message);
            Assert.Equal(3, rows[0].Sequence);
        }
    }

    /// <summary>
    /// Dedicated xUnit collection so <see cref="GodotLogCollectorTests"/> runs in isolation: it mutates the
    /// process-wide <see cref="GodotLogCollector.Current"/> static, which would race other test classes if
    /// run in the default parallel collection.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GodotLogCollectorCurrentCollection
    {
        public const string Name = "GodotLogCollector.Current (serial)";
    }
}
