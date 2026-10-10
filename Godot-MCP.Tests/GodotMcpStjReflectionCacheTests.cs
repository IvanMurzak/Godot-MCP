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
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Text.Json;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    // This suite mutates PROCESS-WIDE System.Text.Json caches, so it must not run concurrently with other
    // tests (a concurrent serialization would evict cache entries on its own and mask the pinned/unpinned
    // signal the ALC test reads).
    [CollectionDefinition("STJ reflection cache", DisableParallelization = true)]
    public sealed class StjReflectionCacheCollection { }

    /// <summary>
    /// Regression tests for <see cref="GodotMcpStjReflectionCache"/> — the teardown hook that releases
    /// System.Text.Json's process-wide reflection caches, a root of godotengine/godot#78513 (the caches hold
    /// resolved addon types / compiled accessor delegates over collectible-ALC types, pinning the context on a
    /// hot-reload).
    ///
    /// <para>
    /// Three contracts are pinned here:
    /// <list type="number">
    ///   <item><b>No-throw / idempotent</b> — the hook runs inside the ALC-unloading handler (both the reload
    ///   hook and a later <c>_ExitTree</c> reach it), so it may never throw and must tolerate repeat calls.</item>
    ///   <item><b>The clear actually happens after the reflection path was exercised</b> — this is the
    ///   regression that shipped: on .NET 8 (the runtime Godot 4.x uses) the member-accessor clear is a
    ///   STATIC <c>ReflectionEmitCachingMemberAccessor.Clear()</c>, so a fallback that only looked for an
    ///   INSTANCE <c>Clear()</c> silently returned false and the ALC stayed pinned.</item>
    ///   <item><b>The pin is really released</b> — a type from a collectible <see cref="AssemblyLoadContext"/>
    ///   is serialized once (populating both STJ cache families), the context is unloaded, and the context
    ///   must become collectible after <see cref="GodotMcpStjReflectionCache.Clear"/>. This is the unit-level
    ///   mirror of the live-editor symptom; the full end-to-end reload remains covered by the headless reload
    ///   harness (<c>scripts/dock_reload_harness.py</c>).</item>
    /// </list>
    /// </para>
    /// </summary>
    [Collection("STJ reflection cache")]
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
        public void Clear_ReturnsTrue_AfterReflectionSerialization()
        {
            // THE REGRESSION: after the reflection path has been used, the clear MUST locate + clear the
            // caches. Pre-fix on .NET 8 this returned false (static-vs-instance Clear mismatch) and the
            // collectible ALC stayed pinned on every hot-reload.
            _ = JsonSerializer.Serialize(new Probe { Value = 7 });

            Assert.True(GodotMcpStjReflectionCache.Clear());
        }

        [Fact]
        public void Clear_ReleasesCollectibleAlcPinnedBySerialization()
        {
            // Mirror of the live symptom: a type defined in a COLLECTIBLE AssemblyLoadContext is serialized
            // through the default reflection path, which populates STJ's process-wide caches with that
            // context's Type/MemberInfo (type-info cache + member-accessor cache). Without a cache flush the
            // context stays pinned exactly like the Godot addon's ALC on a hot-reload.
            //
            // The context is created + populated in a NoInlining helper so no strong reference to it survives
            // into this frame (a live local would keep it pinned regardless of the cache flush).
            var alcRef = CreatePinnedCollectibleContext();

            // The flush must release the context (bounded GC — the runtime needs a few collections).
            Assert.True(GodotMcpStjReflectionCache.Clear());
            Assert.True(WaitForCollection(alcRef), "the collectible AssemblyLoadContext stayed pinned after Clear()");
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

            // Shared-framework/BCL assemblies resolve from the default context, mirroring Godot's plugin ALC.
            alc.Resolving += (_, name) =>
            {
                try { return AssemblyLoadContext.Default.LoadFromAssemblyName(name); }
                catch { return null; }
            };

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

        /// <summary>Bounded forced-GC loop; returns true when the weak reference died (context collected).</summary>
        static bool WaitForCollection(WeakReference reference, int attempts = 20)
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
