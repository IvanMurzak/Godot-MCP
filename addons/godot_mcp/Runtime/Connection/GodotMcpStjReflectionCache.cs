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
using System.Reflection;
using System.Reflection.Metadata;
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
    /// that only looks for an INSTANCE <c>Clear()</c> silently no-ops on .NET 8 (the runtime the Godot 4.x
    /// .NET editor uses) — hence the layered implementation below.
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
        const string LogPrefix = "[Godot-MCP] STJ clear: ";

        /// <summary>Optional diagnostic sink (e.g. <c>GD.Print</c>); stays pure-BCL by taking a delegate.</summary>
        public static Action<string>? Log { get; set; }

        /// <summary>
        /// Clear System.Text.Json's process-wide reflection caches. Best-effort and never throws.
        /// Returns <c>true</c> when the runtime's update handler ran (it clears both families itself) or, on
        /// the manual fallback, when BOTH families were cleared (so a unit test can assert the reflection path
        /// still resolves against the running STJ build).
        /// </summary>
        public static bool Clear()
        {
            try
            {
                // Locate the handler through the PUBLIC [assembly: MetadataUpdateHandler] contract rather than its
                // internal type name, so a rename of JsonSerializerOptionsUpdateHandler does not silently drop us
                // onto the manual fallback.
                foreach (var handler in typeof(JsonSerializer).Assembly.GetCustomAttributes<MetadataUpdateHandlerAttribute>())
                {
                    if (TryInvoke(FindMethod(handler.HandlerType, "ClearCache", BindingFlags.Static, typeof(Type[])), null,
                            $"{handler.HandlerType.Name}.ClearCache() invoked (all tracked options caches + member-accessor cache).",
                            new object?[] { null }))
                        return true;
                }
            }
            catch (Exception ex)
            {
                SafeLog($"update-handler flush failed, trying manual fallback: {Describe(ex)}");
            }

            return ClearManually();
        }

        /// <summary>
        /// Manual fallback for a runtime without the update handler; <c>true</c> only when both families were
        /// cleared. Internal so tests can exercise it — <see cref="Clear"/> never reaches it on a supported runtime.
        /// </summary>
        internal static bool ClearManually()
        {
            // Run both even if the first fails: a partial clear still releases what it can.
            bool clearedOptions = ClearTrackedOptionsCaches();
            bool clearedAccessor = ClearMemberAccessorCache();
            return clearedOptions && clearedAccessor;
        }

        /// <summary>
        /// Invoke the internal <c>ClearCaches()</c> on every options instance in
        /// <c>JsonSerializerOptions.TrackedOptionsInstances.All</c> (the <c>ConditionalWeakTable</c> the runtime
        /// itself enumerates). <c>true</c> when the table was walked. Never throws.
        /// </summary>
        static bool ClearTrackedOptionsCaches()
        {
            try
            {
                var optionsType = typeof(JsonSerializerOptions);
                var trackedType = optionsType.GetNestedType("TrackedOptionsInstances", BindingFlags.NonPublic);
                var all = trackedType?.GetProperty("All", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    as IEnumerable<KeyValuePair<JsonSerializerOptions, object?>>;
                if (all == null)
                {
                    SafeLog("TrackedOptionsInstances.All not found.");
                    return false;
                }

                var clearCaches = FindMethod(optionsType, "ClearCaches", BindingFlags.Instance);
                if (clearCaches == null)
                {
                    SafeLog("JsonSerializerOptions.ClearCaches() not found.");
                    return false;
                }

                // Per-entry guard: one options instance whose ClearCaches() throws must not leave every later
                // instance's type-info cache (and the collectible types it roots) uncleared.
                int cleared = 0, failed = 0;
                foreach (var entry in all)
                {
                    try
                    {
                        clearCaches.Invoke(entry.Key, null);
                        cleared++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        SafeLog($"tracked-options entry clear failed (continuing): {Describe(ex)}");
                    }
                }

                SafeLog($"cleared {cleared} tracked JsonSerializerOptions cache(s){(failed > 0 ? $", {failed} failed" : "")}.");
                return failed == 0;
            }
            catch (Exception ex)
            {
                SafeLog($"tracked-options clear failed (ignored): {Describe(ex)}");
                return false;
            }
        }

        /// <summary>
        /// Clear the reflection-emit member-accessor cache, trying each known runtime shape in turn:
        /// <c>DefaultJsonTypeInfoResolver.ClearMemberAccessorCaches()</c> (.NET 9+), the <b>static</b>
        /// <c>ReflectionEmitCachingMemberAccessor.Clear()</c> (.NET 8), then an instance <c>Clear()</c> on the
        /// resolver's singleton accessor. Never throws.
        /// </summary>
        static bool ClearMemberAccessorCache()
        {
            try
            {
                var stj = typeof(JsonSerializer).Assembly;
                var resolverType = stj.GetType("System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver");
                var cachingType = stj.GetType("System.Text.Json.Serialization.Metadata.ReflectionEmitCachingMemberAccessor");

                if (TryInvoke(FindMethod(resolverType, "ClearMemberAccessorCaches", BindingFlags.Static), null,
                        "DefaultJsonTypeInfoResolver.ClearMemberAccessorCaches() invoked.")
                    || TryInvoke(FindMethod(cachingType, "Clear", BindingFlags.Static), null,
                        "ReflectionEmitCachingMemberAccessor.Clear() (static) invoked."))
                    return true;

                object? accessor =
                    resolverType?.GetProperty("MemberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    ?? resolverType?.GetField("s_memberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (accessor == null)
                {
                    SafeLog("member accessor not found.");
                    return false;
                }

                var name = accessor.GetType().Name;
                if (TryInvoke(FindMethod(accessor.GetType(), "Clear", BindingFlags.Instance), accessor,
                        $"{name}.Clear() (instance) invoked."))
                    return true;

                SafeLog($"no Clear() on {name} (no-emit runtime?).");
                return false;
            }
            catch (Exception ex)
            {
                SafeLog($"member-accessor clear failed (ignored): {Describe(ex)}");
                return false;
            }
        }

        /// <summary>Public-or-internal method lookup by exact signature; <paramref name="scope"/> is Static or Instance.</summary>
        static MethodInfo? FindMethod(Type? type, string name, BindingFlags scope, params Type[] parameters) =>
            type?.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | scope,
                binder: null, types: parameters, modifiers: null);

        /// <summary>Invoke <paramref name="method"/> if it was found and log <paramref name="message"/>; <c>false</c> when absent.</summary>
        static bool TryInvoke(MethodInfo? method, object? target, string message, object?[]? args = null)
        {
            if (method == null)
                return false;
            method.Invoke(target, args);
            SafeLog(message);
            return true;
        }

        /// <summary>
        /// Invoke <see cref="Log"/> without letting a throwing sink (e.g. <c>GD.Print</c> on a disposed engine
        /// object during unload) turn a successful clear into a reported failure.
        /// </summary>
        static void SafeLog(string message)
        {
            try { Log?.Invoke(LogPrefix + message); } catch { /* never throw into the ALC-unloading handler */ }
        }

        /// <summary>
        /// <see cref="MethodBase.Invoke(object, object[])"/> wraps the real STJ failure in a
        /// <see cref="TargetInvocationException"/> whose message is generic; log the inner exception instead.
        /// </summary>
        static string Describe(Exception ex)
        {
            var inner = (ex as TargetInvocationException)?.InnerException ?? ex;
            return $"{inner.GetType().Name}: {inner.Message}";
        }
    }
}
