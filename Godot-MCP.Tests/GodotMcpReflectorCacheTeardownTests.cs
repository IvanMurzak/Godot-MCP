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
using System.Reflection;
using com.IvanMurzak.ReflectorNet.Utils;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Regression tests pinning the ReflectorNet static-cache CLEAR contract that the Godot-MCP reload-safe
    /// teardown (<c>GodotMcpPlugin.Teardown</c>) depends on to fix godotengine/godot#78513.
    ///
    /// ReflectorNet is loaded into the NON-collectible default <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
    /// (the addon's assembly resolver puts it there), but its process-wide static reflection caches get populated,
    /// during tool registration / parameter-schema build, with entries that reference the COLLECTIBLE addon
    /// assembly's types:
    ///   • <see cref="TypeUtils"/> type-name caches hold resolved addon <see cref="System.Type"/> objects
    ///     (a Type roots its assembly's ALC);
    ///   • <see cref="TypeMemberUtils"/> field/property caches hold addon <see cref="FieldInfo"/>/<see cref="PropertyInfo"/>
    ///     (a member roots its DeclaringType's ALC).
    /// Either is a non-collectible→collectible strong static reference = a #78513 hot-reload pin. The teardown
    /// releases them by calling the clear APIs asserted here. If a future ReflectorNet bump renamed/removed any of
    /// these, the addon would silently start pinning the ALC again on every C# Build — this test catches that at CI.
    /// The functional pin-release itself is validated by the headless reload harness (<c>.scripts/godot_reload_test.py</c>).
    /// </summary>
    public class GodotMcpReflectorCacheTeardownTests
    {
        [Fact]
        public void TeardownClearApis_AreCallable_AfterCachesArePopulated_AndNeverThrow()
        {
            // Populate the member caches with entries whose DeclaringType lives in THIS assembly (a stand-in for the
            // collectible addon assembly) — exactly the shape that pins the ALC at runtime.
            _ = TypeMemberUtils.GetField(typeof(CacheProbe), BindingFlags.Public | BindingFlags.Instance, nameof(CacheProbe.Field));
            _ = TypeMemberUtils.GetProperty(typeof(CacheProbe), BindingFlags.Public | BindingFlags.Instance, nameof(CacheProbe.Property));

            // The exact set of clears the teardown invokes. Must never throw (they run inside the ALC-unload
            // handler) — assert the whole sequence completes cleanly.
            var ex = Record.Exception(() =>
            {
                TypeMemberUtils.ClearAllCaches();
                TypeUtils.ClearTypeCache();
                TypeUtils.ClearAssemblyTypeCache();
                TypeUtils.ClearExactAssemblyTypeCache();
                TypeUtils.ClearEnumerableItemTypeCache();
            });
            Assert.Null(ex);
        }

        [Fact]
        public void TeardownClearApis_AreIdempotent_OnAnAlreadyClearedCache()
        {
            // The teardown can be reached more than once (the reload hook AND a later _ExitTree), and a clear may
            // run with nothing cached. Calling them repeatedly / on an empty cache must be safe.
            var ex = Record.Exception(() =>
            {
                for (var i = 0; i < 3; i++)
                {
                    TypeMemberUtils.ClearAllCaches();
                    TypeUtils.ClearTypeCache();
                    TypeUtils.ClearAssemblyTypeCache();
                    TypeUtils.ClearExactAssemblyTypeCache();
                    TypeUtils.ClearEnumerableItemTypeCache();
                }
            });
            Assert.Null(ex);
        }

        sealed class CacheProbe
        {
            public int Field = 0;
            public int Property { get; set; }
        }
    }
}
