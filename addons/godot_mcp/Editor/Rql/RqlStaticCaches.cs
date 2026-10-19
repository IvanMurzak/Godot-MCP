/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Editor-only (RQL needs the editor-only com.IvanMurzak.Rql package); also compiled by the xUnit suite.
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Releases RQL's process-wide caches on plugin teardown so a C# hot reload can unload the addon's collectible
    /// AssemblyLoadContext.
    /// <para>
    /// <b>Why.</b> The RQL assemblies (<c>com.IvanMurzak.Rql</c>) are NuGet dependencies, which
    /// <c>GodotMcpAssemblyResolver</c> loads into the DEFAULT (non-collectible) load context. RQL keeps a few static caches
    /// keyed by <see cref="Type"/> / <see cref="MemberInfo"/> (the shared type model, compiled member getters, the
    /// <c>$type</c> subtype index, object-kind overrides). Once a query has touched a type of the addon assembly (the
    /// <c>editor</c> source's view objects, a user's C# node script), those entries root the collectible context and the
    /// reload fails (".NET: Failed to unload assemblies") — the same class of problem the addon already avoids for
    /// System.Text.Json (GodotMcpConfigJsonContext) and clears for ReflectorNet in <c>GodotMcpPlugin.Teardown</c>.
    /// </para>
    /// <para>
    /// RQL 0.1 has no public "clear caches" API, so this clears them by reflection, defensively: every static dictionary in
    /// the RQL assemblies whose key involves a type or member, plus the shared <see cref="TypeModel"/>'s per-type cache. It
    /// never throws (a renamed field in a later RQL version just means nothing is cleared) and reports how many caches it
    /// cleared. The dock-reload CI legs (test_godot_dock_reload.yml) are the proof that the reload still unloads.
    /// </para>
    /// </summary>
    public static class RqlStaticCaches
    {
        /// <summary>Clear the caches; returns the number of caches cleared (0 when none were found).</summary>
        public static int Clear()
        {
            var cleared = 0;
            foreach (var assembly in new[] { typeof(TypeModel).Assembly, typeof(global::Rql.RqlParser).Assembly })
            {
                foreach (var type in TypeModel.LoadableTypes(assembly))
                {
                    if (type.ContainsGenericParameters) continue;
                    foreach (var field in type.GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        if (TryClear(SafeGet(field, null))) cleared++;
                }
            }

            // The shared model (engines without adapters use it; TypeModel.Get(...) helpers read through it).
            var shared = TypeModel.Shared;
            foreach (var field in typeof(TypeModel).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
                if (TryClear(SafeGet(field, shared))) cleared++;
            return cleared;
        }

        private static object? SafeGet(FieldInfo field, object? target)
        {
            try { return field.GetValue(target); }
            catch { return null; }
        }

        // A dictionary keyed by Type / MemberInfo (or a tuple holding one) is a per-type cache: clear it. Lookup tables of
        // BCL types (aliases), regex caches and parser keyword tables are left alone.
        private static bool TryClear(object? value)
        {
            if (!(value is IDictionary dictionary) || dictionary.IsReadOnly) return false;
            var keyType = KeyType(value.GetType());
            if (keyType == null || !HoldsTypeOrMember(keyType)) return false;
            if (IsBclAliasTable(dictionary)) return false;
            // Teardown runs on the main thread with no query in flight; the caches are concurrent dictionaries.
            try
            {
                dictionary.Clear();
                return true;
            }
            catch { return false; }
        }

        private static Type? KeyType(Type dictionaryType)
        {
            for (var t = dictionaryType; t != null; t = t.BaseType)
                if (t.IsGenericType && t.GetGenericArguments().Length == 2) return t.GetGenericArguments()[0];
            foreach (var i in dictionaryType.GetInterfaces())
                if (i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>)) return i.GetGenericArguments()[0];
            return null;
        }

        private static bool HoldsTypeOrMember(Type key)
        {
            if (typeof(Type).IsAssignableFrom(key) || typeof(MemberInfo).IsAssignableFrom(key)) return true;
            if (key.IsGenericType && key.FullName?.StartsWith("System.ValueTuple", StringComparison.Ordinal) == true)
                foreach (var arg in key.GetGenericArguments())
                    if (HoldsTypeOrMember(arg)) return true;
            return false;
        }

        // TypeModel's display aliases map BCL types (int → "int"): static, immutable, never hold addon types.
        private static bool IsBclAliasTable(IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys)
                if (!(key is Type t) || t.Assembly != typeof(object).Assembly) return false;
            return dictionary.Count > 0;
        }
    }
}
#endif
