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
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    // This suite flushes PROCESS-WIDE System.Text.Json caches and swaps the static Log sink, so it must not run
    // concurrently with other tests (a flush from elsewhere would release the ALC on its own and mask the
    // pinned/unpinned signal the ALC test reads).
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class StjReflectionCacheCollection
    {
        public const string Name = "STJ reflection cache (serial)";
    }

    /// <summary>
    /// Regression tests for <see cref="GodotMcpStjReflectionCache"/> (rationale lives there). Contracts:
    /// <list type="number">
    ///   <item><b>No-throw / idempotent</b> — the hook runs inside the ALC-unloading handler, possibly twice.</item>
    ///   <item><b>A throwing log sink does not turn a successful flush into a reported failure.</b></item>
    ///   <item><b>The pin is really released</b> — by <see cref="GodotMcpStjReflectionCache.Clear"/> AND by the
    ///   manual fallback <see cref="GodotMcpStjReflectionCache.ClearManually"/> it never reaches on a supported
    ///   runtime (so the fallback cannot rot unnoticed). Pre-fix on .NET 8 the clear returned false and the
    ///   collectible ALC stayed pinned. The end-to-end reload is covered by <c>scripts/dock_reload_harness.py</c>.</item>
    /// </list>
    /// </summary>
    [Collection(StjReflectionCacheCollection.Name)]
    public class GodotMcpStjReflectionCacheTests
    {
        [Fact]
        public void Clear_NeverThrows_AndIsIdempotent_EvenAfterSerialization()
        {
            // SAFETY CONTRACT: the hook must never throw and must be safe to call repeatedly. It is also
            // called both before and after STJ has been exercised.
            _ = JsonSerializer.Serialize(new Probe { Value = 7 });

            var ex = Record.Exception(() =>
            {
                GodotMcpStjReflectionCache.Clear();
                GodotMcpStjReflectionCache.Clear();
            });
            Assert.Null(ex);
        }

        [Fact]
        public void Clear_DoesNotReportFailure_WhenLogSinkThrows()
        {
            // A sink that throws on the unload path (e.g. GD.Print on a disposed engine object) must not turn
            // a successful flush into a reported failure.
            var previous = GodotMcpStjReflectionCache.Log;
            GodotMcpStjReflectionCache.Log = _ => throw new InvalidOperationException("sink down");
            try
            {
                _ = JsonSerializer.Serialize(new Probe { Value = 7 });
                Assert.True(GodotMcpStjReflectionCache.Clear());
                Assert.True(GodotMcpStjReflectionCache.ClearManually());
            }
            finally
            {
                GodotMcpStjReflectionCache.Log = previous;
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void Clear_ReleasesCollectibleAlcPinnedBySerialization(bool manualFallback)
        {
            // Mirror of the live symptom: a type defined in a COLLECTIBLE AssemblyLoadContext is serialized
            // through the default reflection path, which populates STJ's process-wide caches with that
            // context's Type/MemberInfo (type-info cache + member-accessor cache). Without a cache flush the
            // context stays pinned exactly like the Godot addon's ALC on a hot-reload.
            //
            // The context is created + populated in a NoInlining helper so no strong reference to it survives
            // into this frame (a live local would keep it pinned regardless of the cache flush).
            var alcRef = CreatePinnedCollectibleContext();

            // Negative control: without the flush the context really is pinned. Without this the test would
            // also pass if something else (e.g. an unload hook in the loaded copy) released the context.
            Assert.False(WaitForCollection(alcRef, attempts: 5), "the collectible AssemblyLoadContext was not pinned by STJ serialization; the test cannot discriminate");

            // The flush must release the context (bounded GC — the runtime needs a few collections).
            Assert.True(manualFallback ? GodotMcpStjReflectionCache.ClearManually() : GodotMcpStjReflectionCache.Clear());
            Assert.True(WaitForCollection(alcRef), "the collectible AssemblyLoadContext stayed pinned after the STJ cache clear");
        }

        /// <summary>
        /// Create a collectible context, serialize a type loaded into it (populating both STJ cache
        /// families), unload the context and hand back only a weak reference. NoInlining so the strong
        /// locals here are unreachable once the method returns.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference CreatePinnedCollectibleContext()
        {
            var alc = new AssemblyLoadContext("stj-pin-alc-test", isCollectible: true);
            var alcRef = new WeakReference(alc, trackResurrection: true);

            // No Resolving hook needed: a custom context whose Load() returns null already falls back to the
            // default context for shared-framework/BCL/test-host dependencies, mirroring Godot's plugin ALC.
            SerializeTypeFromCollectibleContext(alc);
            alc.Unload();
            return alcRef;
        }

        /// <summary>
        /// Load a second copy of THIS test assembly into <paramref name="alc"/> and serialize its public
        /// <see cref="AlcProbe"/> through the default reflection path. Kept out-of-line (NoInlining) so no
        /// strong local reference to the loaded assembly survives this frame and masks the pin.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static void SerializeTypeFromCollectibleContext(AssemblyLoadContext alc)
        {
            var collectibleAssembly = alc.LoadFromAssemblyPath(typeof(AlcProbe).Assembly.Location);
            var probeType = collectibleAssembly.GetType(typeof(AlcProbe).FullName!, throwOnError: true)!;
            var probe = Activator.CreateInstance(probeType)!;
            probeType.GetProperty(nameof(AlcProbe.Name))!.SetValue(probe, "pinned");
            probeType.GetProperty(nameof(AlcProbe.Value))!.SetValue(probe, 42.5);
            _ = JsonSerializer.Serialize(probe, probeType);
        }

        /// <summary>
        /// Bounded forced-GC loop; returns true when the weak reference died (context collected). ALC unload
        /// completes asynchronously through the LoaderAllocator finalizer chain, so the budget (~10 s) is sized
        /// for loaded CI runners; a healthy run exits after the first few iterations.
        /// </summary>
        static bool WaitForCollection(WeakReference reference, int attempts = 200)
        {
            for (int i = 0; i < attempts; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                if (!reference.IsAlive)
                    return true;
                Thread.Sleep(50);
            }
            return !reference.IsAlive;
        }

        sealed class Probe
        {
            public int Value { get; set; }
        }

        /// <summary>Public probe serialized from a second, collectible copy of this test assembly.</summary>
        public sealed class AlcProbe
        {
            public string Name { get; set; } = "";
            public double Value { get; set; }
        }
    }
}
