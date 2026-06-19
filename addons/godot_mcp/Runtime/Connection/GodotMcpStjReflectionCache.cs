/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak)              │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Reflection;
using System.Text.Json;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Releases System.Text.Json's PROCESS-WIDE reflection-emit member-accessor cache on plugin teardown —
    /// a key root of godotengine/godot#78513.
    ///
    /// <para>
    /// <b>The pin.</b> The default (reflection-based) STJ path compiles per-type member getters/setters with
    /// reflection-emit and caches the resulting delegates in a static
    /// <c>ReflectionEmitCachingMemberAccessor</c> living in the NON-collectible <c>System.Text.Json</c>
    /// assembly. Whenever the addon (de)serializes one of ITS OWN types — config, device-auth DTOs, tool
    /// argument/result models routed through ReflectorNet, … — that cache gains a delegate over a type
    /// defined in Godot's <b>collectible plugin AssemblyLoadContext</b>, which roots the type and pins the
    /// context open on a "Build Project" hot-reload (".NET: Failed to unload assemblies"). This is a known
    /// .NET 8 limitation (fixed in .NET 9 by making the cache ALC-aware; the Godot 4.5 mono runtime is .NET 8).
    /// Per-instance/per-call <see cref="JsonSerializerOptions"/> do NOT help — the member-accessor cache is a
    /// single process-wide static, keyed by member, shared across all options.
    /// </para>
    ///
    /// <para>
    /// <b>The fix.</b> On the reload-safe teardown we clear that cache, dropping every cached delegate (and so
    /// every reference into the collectible assembly) right before the unload; STJ simply rebuilds entries
    /// lazily on next use. This is a single systematic hook — it covers EVERY STJ-(de)serialized addon type
    /// at once, including the ReflectorNet-owned serialization path that must remain reflection-based for
    /// Unity compatibility (so it cannot be source-generated). Cheap addon-side, non-source-generated types
    /// (e.g. <c>GodotMcpConfig</c>) additionally avoid the cache entirely via source-gen — this clear is the
    /// safety net for everything else.
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
        /// <summary>
        /// Clear System.Text.Json's reflection-emit member-accessor cache. Best-effort and never throws.
        /// Returns <c>true</c> if a cache was located and cleared, <c>false</c> otherwise (so a unit test can
        /// assert the reflection path still resolves against the running STJ build).
        /// </summary>
        /// <summary>Optional diagnostic sink (e.g. <c>GD.Print</c>); stays pure-BCL by taking a delegate.</summary>
        public static Action<string>? Log { get; set; }

        public static bool Clear()
        {
            try
            {
                var stj = typeof(JsonSerializer).Assembly;
                var resolverType = stj.GetType("System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver");
                if (resolverType == null)
                {
                    Log?.Invoke("[Godot-MCP] STJ clear: DefaultJsonTypeInfoResolver not found.");
                    return false;
                }

                // 1) Preferred: the resolver's own static clear-all (System.Text.Json >= .NET 9). This is the
                //    method the runtime itself calls to evict the reflection-emit member-accessor caches.
                var clearAll = resolverType.GetMethod("ClearMemberAccessorCaches",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (clearAll != null)
                {
                    clearAll.Invoke(null, null);
                    Log?.Invoke("[Godot-MCP] STJ clear: ClearMemberAccessorCaches() invoked.");
                    return true;
                }

                // 2) Fallback (.NET 8): clear the singleton member accessor's cache directly. The reflection-
                //    emit caching accessor exposes Clear(); the plain no-emit accessor has none (nothing to do).
                object? accessor =
                    resolverType.GetProperty("MemberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
                    ?? resolverType.GetField("s_memberAccessor", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null);
                if (accessor == null)
                {
                    Log?.Invoke("[Godot-MCP] STJ clear: member accessor not found.");
                    return false;
                }

                var clear = accessor.GetType().GetMethod("Clear",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    binder: null, types: Type.EmptyTypes, modifiers: null);
                if (clear == null)
                {
                    Log?.Invoke($"[Godot-MCP] STJ clear: no Clear() on {accessor.GetType().Name} (no-emit runtime?).");
                    return false;
                }

                clear.Invoke(accessor, null);
                Log?.Invoke($"[Godot-MCP] STJ clear: {accessor.GetType().Name}.Clear() invoked.");
                return true;
            }
            catch (Exception ex)
            {
                // STJ internals changed / unavailable — fall back to pre-fix behavior (the reload may still
                // leak, but nothing breaks). Never throws into the ALC-unloading handler.
                try { Log?.Invoke($"[Godot-MCP] STJ clear failed (ignored): {ex.Message}"); } catch { }
                return false;
            }
        }
    }
}
