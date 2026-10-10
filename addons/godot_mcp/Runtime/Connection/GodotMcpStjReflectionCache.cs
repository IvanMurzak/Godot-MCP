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
using System.Collections;
using System.Reflection;
using System.Text.Json;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Releases System.Text.Json's PROCESS-WIDE reflection caches on plugin teardown — a key root of
    /// godotengine/godot#78513.
    ///
    /// <para>
    /// <b>The pin (two independent cache families, both in the NON-collectible System.Text.Json assembly).</b>
    /// <list type="number">
    ///   <item><b>Member-accessor cache</b> — <c>ReflectionEmitCachingMemberAccessor.s_cache</c> holds compiled
    ///   getter/setter delegates keyed by <c>(string id, Type declaringType, MemberInfo)</c>. Any addon type
    ///   (de)serialized through the reflection path lands in it, pinning the collectible context.</item>
    ///   <item><b>Per-options type-info cache</b> — every live <see cref="JsonSerializerOptions"/> owns a
    ///   <c>CachingContext</c> whose <c>JsonTypeInfo</c>/<c>JsonPropertyInfo</c> objects reference resolved
    ///   addon <c>Type</c>s. <c>JsonSerializerOptions.Default</c> plus any long-lived options instance held
    ///   by ReflectorNet/McpPlugin is a static-lifetime root.</item>
    /// </list>
    /// Clearing only one family is NOT sufficient (verified: a collectible-ALC type serialized once stays
    /// pinned after a member-accessor-only clear).
    /// </para>
    ///
    /// <para>
    /// <b>The fix.</b> On the reload-safe teardown we invoke the runtime's OWN process-wide flush,
    /// <c>JsonSerializerOptionsUpdateHandler.ClearCache(Type[]?)</c> (internal; the entry point .NET itself
    /// calls from its metadata-update / hot-reload handler). It clears BOTH families:
    /// <c>foreach (options in JsonSerializerOptions.TrackedOptionsInstances.All) options.ClearCaches();</c>
    /// followed by the member-accessor clear. STJ rebuilds entries lazily on next use.
    /// </para>
    ///
    /// <para>
    /// <b>Version notes.</b> The update handler exists on .NET 8, 9 and 10. On .NET 8 the member-accessor
    /// clear is <c>ReflectionEmitCachingMemberAccessor.Clear()</c> — a <b>static</b> method (on .NET 9+ it is
    /// an instance override plus <c>DefaultJsonTypeInfoResolver.ClearMemberAccessorCaches()</c>). A fallback
    /// that only looks for an INSTANCE <c>Clear()</c> silently no-ops on .NET 8 (the runtime Godot 4.7
    /// ships with) — hence the layered implementation below.
    /// </para>
    ///
    /// <para>
    /// STJ internals are not public API, so the whole operation is reflection-based and fully defensive:
    /// any failure (a future STJ refactor, an AOT/no-emit runtime that never populated the cache) is
    /// swallowed, leaving behavior no worse than before. Pure-BCL (no Godot types) so it is CI-unit-testable.
    /// </para>
    /// </summary>
    public static class GodotMcpStjReflectionCache
    {
        /// <summary>Optional diagnostic sink (e.g. <c>GD.Print</c>); stays pure-BCL by taking a delegate.</summary>
        public static Action<string>? Log { get; set; }

        /// <summary>
        /// Clear System.Text.Json's process-wide reflection caches. Best-effort and never throws.
        /// Returns <c>true</c> if a cache was located and cleared, <c>false</c> otherwise (so a unit test can
        /// assert the reflection path still resolves against the running STJ build).
        /// </summary>
        public static bool Clear()
        {
            try
            {
                var stj = typeof(JsonSerializer).Assembly;

                // 1) Preferred (all supported runtimes): the runtime's own process-wide flush — the same entry
                //    point .NET's metadata-update/hot-reload handler uses. Clears every tracked options'
                //    type-info cache AND the static member-accessor cache in one call.
                var updateHandler = stj.GetType("System.Text.Json.JsonSerializerOptionsUpdateHandler");
                var clearCache = updateHandler?.GetMethod("ClearCache",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null, types: new[] { typeof(Type[]) }, modifiers: null);
                if (clearCache != null)
                {
                    clearCache.Invoke(null, new object?[] { null });
                    Log?.Invoke("[Godot-MCP] STJ clear: JsonSerializerOptionsUpdateHandler.ClearCache() invoked (all tracked options caches + member-accessor cache).");
                    return true;
                }

                var resolverType = stj.GetType("System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver");

                // 2) .NET 9+: the resolver's own static clear for the member-accessor family.
                var clearAll = resolverType?.GetMethod("ClearMemberAccessorCaches",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (clearAll != null)
                {
                    clearAll.Invoke(null, null);
                    Log?.Invoke("[Godot-MCP] STJ clear: DefaultJsonTypeInfoResolver.ClearMemberAccessorCaches() invoked.");
                    return true;
                }

                // 3) Manual fallback (.NET 8 shape): clear every tracked options' type-info cache, then the
                //    member-accessor cache. NOTE: on .NET 8 Clear() is STATIC on the caching accessor — an
                //    instance-only lookup misses it (the bug this fallback previously shipped with).
                bool clearedOptions = ClearTrackedOptionsCaches();
                bool clearedAccessor = ClearMemberAccessorCache(stj, resolverType);
                return clearedOptions || clearedAccessor;
            }
            catch (Exception ex)
            {
                // STJ internals changed / unavailable — fall back to pre-fix behavior (the reload may still
                // leak, but nothing breaks). Never throws into the ALC-unloading handler.
                try { Log?.Invoke($"[Godot-MCP] STJ clear failed (ignored): {ex.Message}"); } catch { }
                return false;
            }
        }

        /// <summary>
        /// Clear the <c>CachingContext</c> type-info cache of every live <see cref="JsonSerializerOptions"/>
        /// by walking <c>JsonSerializerOptions.TrackedOptionsInstances.All</c> (the internal
        /// <c>ConditionalWeakTable</c> the runtime itself enumerates) and invoking the internal instance
        /// <c>ClearCaches()</c> on each. Best-effort; returns <c>true</c> when at least one options cache was
        /// cleared. Never throws (callers are on the ALC-unloading path).
        /// </summary>
        static bool ClearTrackedOptionsCaches()
        {
            try
            {
                var optionsType = typeof(JsonSerializerOptions);
                var trackedType = optionsType.GetNestedType("TrackedOptionsInstances", BindingFlags.NonPublic);
                var all = trackedType?.GetProperty("All", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (all == null)
                {
                    Log?.Invoke("[Godot-MCP] STJ clear: TrackedOptionsInstances.All not found.");
                    return false;
                }

                var clearCaches = optionsType.GetMethod("ClearCaches",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (clearCaches == null)
                {
                    Log?.Invoke("[Godot-MCP] STJ clear: JsonSerializerOptions.ClearCaches() not found.");
                    return false;
                }

                int cleared = 0;
                foreach (var entry in (IEnumerable)all)
                {
                    // entry is a boxed KeyValuePair<JsonSerializerOptions, object?>.
                    if (entry?.GetType().GetProperty("Key")?.GetValue(entry) is JsonSerializerOptions options)
                    {
                        clearCaches.Invoke(options, null);
                        cleared++;
                    }
                }

                Log?.Invoke($"[Godot-MCP] STJ clear: cleared {cleared} tracked JsonSerializerOptions cache(s).");
                return cleared > 0;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Godot-MCP] STJ clear: tracked-options clear failed (ignored): {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Clear the reflection-emit member-accessor cache. Prefers the <b>static</b> <c>Clear()</c> on
        /// <c>ReflectionEmitCachingMemberAccessor</c> (the .NET 8 shape); falls back to an instance
        /// <c>Clear()</c> on the singleton accessor exposed by <c>DefaultJsonTypeInfoResolver</c> for
        /// runtimes with the other shape. Best-effort; never throws.
        /// </summary>
        static bool ClearMemberAccessorCache(Assembly stj, Type? resolverType)
        {
            try
            {
                var cachingType = stj.GetType("System.Text.Json.Serialization.Metadata.ReflectionEmitCachingMemberAccessor");
                var staticClear = cachingType?.GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (staticClear != null)
                {
                    staticClear.Invoke(null, null);
                    Log?.Invoke("[Godot-MCP] STJ clear: ReflectionEmitCachingMemberAccessor.Clear() (static) invoked.");
                    return true;
                }

                object? accessor =
                    resolverType?.GetProperty("MemberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    ?? resolverType?.GetField("s_memberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (accessor == null)
                {
                    Log?.Invoke("[Godot-MCP] STJ clear: member accessor not found.");
                    return false;
                }

                var instanceClear = accessor.GetType().GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (instanceClear == null)
                {
                    Log?.Invoke($"[Godot-MCP] STJ clear: no Clear() on {accessor.GetType().Name} (no-emit runtime?).");
                    return false;
                }

                instanceClear.Invoke(accessor, null);
                Log?.Invoke($"[Godot-MCP] STJ clear: {accessor.GetType().Name}.Clear() (instance) invoked.");
                return true;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"[Godot-MCP] STJ clear: member-accessor clear failed (ignored): {ex.Message}");
                return false;
            }
        }
    }
}
