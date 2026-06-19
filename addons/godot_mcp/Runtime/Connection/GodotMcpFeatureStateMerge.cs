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
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>, no McpPlugin dependency) merge/capture logic
    /// for the MCP-feature enable-map. Splitting this from the live <c>IToolManager</c>/<c>IPromptManager</c>/
    /// <c>IResourceManager</c> wiring keeps the effective-state and capture rules unit-testable in the
    /// plain-xUnit <c>Godot-MCP.Tests</c> host: the editor-only code (in <c>GodotMcpConnection</c>, behind
    /// <c>#if TOOLS</c>) just supplies the live item names + a setter delegate and lets this decide WHAT to do.
    ///
    /// <para>
    /// Two operations, mirroring the two directions the dock needs:
    /// <list type="bullet">
    ///   <item><b>Reapply</b> (boot): saved-map ⊕ live-item-names → the set of explicit
    ///   <c>SetEnabled(name, enabled)</c> calls to make. Only saved entries whose name still exists in the
    ///   live set are applied (UNKNOWN-NAME PRUNING — a renamed/removed tool's stale entry is ignored); a
    ///   live item with NO saved entry is left at its manager default (DEFAULT-EMPTY = ALL-ENABLED).</item>
    ///   <item><b>Capture</b> (after a user toggle / for round-trip): live-item-names + their current enabled
    ///   → the list of <see cref="GodotMcpFeatureState"/> entries to persist. Every live item is captured so
    ///   the on-disk map is a faithful snapshot the next reapply can restore.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class GodotMcpFeatureStateMerge
    {
        /// <summary>
        /// Compute the explicit enabled-state to apply for each LIVE item given the SAVED entries. The result
        /// maps each live item name to the enabled value the reapply step should push via the manager's
        /// <c>SetEnabled</c>:
        /// <list type="bullet">
        ///   <item>name present in <paramref name="saved"/> → that saved <see cref="GodotMcpFeatureState.Enabled"/>;</item>
        ///   <item>name NOT in <paramref name="saved"/> → omitted from the result (leave at live default —
        ///   DEFAULT-EMPTY = ALL-ENABLED, so the reapply makes no call for it).</item>
        /// </list>
        /// Saved entries whose name is not in <paramref name="liveItemNames"/> are dropped (UNKNOWN-NAME
        /// PRUNING). Last-wins on duplicate saved names (defensive against a hand-edited file). The returned
        /// dictionary's key order is not significant.
        /// </summary>
        public static IReadOnlyDictionary<string, bool> ComputeReapply(
            IEnumerable<string> liveItemNames,
            IReadOnlyList<GodotMcpFeatureState> saved)
        {
            var live = new HashSet<string>(liveItemNames);

            // Index saved by name (last-wins) so a duplicated / hand-edited entry resolves deterministically.
            var savedByName = new Dictionary<string, bool>();
            foreach (var entry in saved)
            {
                if (entry == null || string.IsNullOrEmpty(entry.Name))
                    continue;
                savedByName[entry.Name] = entry.Enabled;
            }

            var result = new Dictionary<string, bool>();
            foreach (var name in live)
            {
                // Only emit an explicit call when the user has a saved preference for this live item;
                // otherwise leave the manager default untouched (all-enabled by default).
                if (savedByName.TryGetValue(name, out var enabled))
                    result[name] = enabled;
            }

            return result;
        }

        /// <summary>
        /// Capture the current enabled-state of every LIVE item into a persistable list. <paramref name="liveItems"/>
        /// pairs each live item name with its current enabled flag (as the manager reports it). Every live item
        /// is captured (a faithful snapshot), so reapplying this exact map later restores the same state. Items
        /// with an empty name are skipped (defensive). Order follows enumeration order of <paramref name="liveItems"/>.
        /// </summary>
        public static List<GodotMcpFeatureState> Capture(IEnumerable<(string Name, bool Enabled)> liveItems)
        {
            var captured = new List<GodotMcpFeatureState>();
            var seen = new HashSet<string>();
            foreach (var (name, enabled) in liveItems)
            {
                if (string.IsNullOrEmpty(name) || !seen.Add(name))
                    continue;
                captured.Add(new GodotMcpFeatureState(name, enabled));
            }
            return captured;
        }

        /// <summary>
        /// Update (or insert) one item's saved entry in <paramref name="saved"/>, returning the same list for
        /// fluent use. Used when the user toggles a single item in the list window: the in-memory map is patched
        /// then persisted, without recapturing the whole live set. Last-wins if the name already exists.
        /// </summary>
        public static List<GodotMcpFeatureState> Upsert(List<GodotMcpFeatureState> saved, string name, bool enabled)
        {
            if (string.IsNullOrEmpty(name))
                return saved;

            for (int i = 0; i < saved.Count; i++)
            {
                if (saved[i] != null && saved[i].Name == name)
                {
                    saved[i].Enabled = enabled;
                    return saved;
                }
            }

            saved.Add(new GodotMcpFeatureState(name, enabled));
            return saved;
        }
    }
}
