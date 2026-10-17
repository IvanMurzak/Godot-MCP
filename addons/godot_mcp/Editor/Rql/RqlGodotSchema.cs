/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Compact schema for agents (rql-schema), the Godot counterpart of Unity-MCP's RqlUnitySchema. RQL's generic schema
    /// walks every reachable type, which for Godot means hundreds of classes; the overview lists the sources, the functions
    /// and the node classes the edited scene actually contains, and a type name shows one type's members plus its Godot
    /// properties (ClassDB) — the <c>props["…"]</c> keys, which have no C# member. Lines use the core format
    /// (AgentFormat.TypeSchema).
    /// </summary>
    public static class RqlGodotSchema
    {
        public static string Render(RqlEngine engine, string? typeName)
        {
            if (typeName != null) return RenderType(engine, typeName);

            var sb = new StringBuilder();
            sb.Append("sources:\n");
            foreach (var s in engine.Sources.Values)
                sb.Append("  ").Append(s.Name).Append(s.Description != null ? " — " + s.Description : "").Append('\n');
            sb.Append("functions:\n");
            foreach (var f in engine.Functions.Keys.OrderBy(k => k, StringComparer.Ordinal))
                sb.Append("  ").Append(engine.FunctionDescriptions.TryGetValue(f, out var d) ? d : f + "(…)").Append('\n');

            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var node in RqlGodot.AllNodes())
            {
                var cls = node.GetClass();
                counts[cls] = counts.TryGetValue(cls, out var n) ? n + 1 : 1;
            }
            sb.Append(counts.Count == 0
                ? "edited scene: none (open one with rql-mutate editor | call OpenScene(\"res://…\"))\n"
                : "node classes in the edited scene (rql-schema \"<class>\" for members and props): ");
            if (counts.Count > 0)
                sb.Append(string.Join(", ", counts.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key, StringComparer.Ordinal)
                    .Select(kv => kv.Key + " ×" + kv.Value))).Append('\n');
            sb.Append(AgentFormat.TypeSchema(engine, typeof(Node), markExplicitOnly: true));
            return sb.ToString();
        }

        /// <summary>A source name ("editor", "files") or the type of a source's items, e.g. RqlEditorApi or RqlFile.</summary>
        private static IEnumerable<Type> SourceTypes(RqlEngine engine, string name)
        {
            foreach (var s in engine.Sources.Values)
            {
                var t = SourceItemType(engine, s);
                if (t == null) continue;
                if (string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase) || string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase))
                    yield return t;
            }
        }

        // A source's item type: from the getter's declared return type (method-group sources such as RqlGodot.AllNodes),
        // otherwise learned by calling the getter once; cached per engine and source (a source that returned nothing is
        // asked again next time).
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<RqlEngine, Dictionary<string, Type>> ItemTypes =
            new System.Runtime.CompilerServices.ConditionalWeakTable<RqlEngine, Dictionary<string, Type>>();

        private static Type? SourceItemType(RqlEngine engine, SourceRegistration source)
        {
            var cache = ItemTypes.GetOrCreateValue(engine);
            if (cache.TryGetValue(source.Name, out var known)) return known;
            Type? t = source.Getter.Method.ReturnType;
            if (t == typeof(object) || t == typeof(void))
            {
                try { t = source.Getter()?.GetType(); } catch { return null; }
            }
            if (t == null) return null;
            var info = engine.Types.Info(t);
            if ((info.IsList || info.IsMap) && info.ElementType != null) t = info.ElementType;
            return cache[source.Name] = t;
        }

        private static string RenderType(RqlEngine engine, string typeName)
        {
            var candidates = SourceTypes(engine, typeName).Concat(GodotTypes.Candidates(typeName)).Distinct().Take(3).ToList();
            var godotClass = GodotClassName(typeName, candidates);
            if (candidates.Count == 0 && godotClass == null)
            {
                // Types only reachable through sources (e.g. an extension's view objects): RQL's own schema walk finds them.
                var reachable = AgentFormat.Schema(engine, typeName);
                return reachable.StartsWith("error unknown-type", StringComparison.Ordinal)
                    ? AgentFormat.UnknownTypeError(typeName, GodotTypes.ShortNames())
                    : reachable;
            }
            var sb = new StringBuilder();
            foreach (var t in candidates)
            {
                if (sb.Length > 0) sb.Append('\n');
                // Node subclasses list their own members; the shared Node / GodotObject ones are summarised once.
                var shared = typeof(Node).IsAssignableFrom(t) && t != typeof(Node) ? typeof(Node)
                    : typeof(Resource).IsAssignableFrom(t) && t != typeof(Resource) ? typeof(Resource) : null;
                sb.Append(AgentFormat.TypeSchema(engine, t, inheritedFrom: shared, markExplicitOnly: true));
                if (shared != null)
                {
                    var own = new HashSet<string>(engine.Types.Info(t).Members.Select(m => m.Name), StringComparer.Ordinal);
                    var inherited = engine.Types.Info(shared).Members.Select(m => m.Name).Where(own.Contains).ToList();
                    if (inherited.Count > 0) sb.Append("\n  (+ inherited: ").Append(string.Join(", ", inherited)).Append(')');
                }
            }
            if (godotClass != null)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(GodotProperties(godotClass));
            }
            return sb.ToString();
        }

        /// <summary>The ClassDB class a name (or its C# type) stands for, or null.</summary>
        private static string? GodotClassName(string typeName, IReadOnlyList<Type> candidates)
        {
            if (ClassDB.ClassExists(typeName)) return typeName;
            var classes = GodotTypes.GodotClasses();
            string? Match(string name) => classes.FirstOrDefault(c => string.Equals(c, name, StringComparison.OrdinalIgnoreCase));
            foreach (var t in candidates)
                if (typeof(GodotObject).IsAssignableFrom(t) && Match(t.Name) is { } match) return match;
            return Match(typeName);
        }

        /// <summary>The class's own Godot properties (props["…"] keys) with their types, then its parent chain.</summary>
        private static string GodotProperties(string godotClass)
        {
            var sb = new StringBuilder("props of Godot class ").Append(godotClass);
            var parent = ClassDB.GetParentClass(godotClass).ToString();
            if (parent.Length > 0) sb.Append(" (extends ").Append(parent).Append(')');
            sb.Append(" — props[\"name\"]:");
            var any = false;
            foreach (var info in ClassDB.ClassGetPropertyList(godotClass, noInheritance: true))
            {
                if (!GodotVariants.IsExposed((PropertyUsageFlags)info["usage"].AsInt64(), storageOnly: false)) continue;
                var type = (Variant.Type)info["type"].AsInt64();
                var hint = info["hint_string"].AsString();
                sb.Append("\n  ").Append(info["name"].AsString()).Append(' ').Append(TypeText(type, (PropertyHint)info["hint"].AsInt64(), hint));
                any = true;
            }
            if (!any) sb.Append(" (none of its own)");
            var chain = new List<string>();
            for (var p = parent; p.Length > 0; p = ClassDB.GetParentClass(p).ToString()) chain.Add(p);
            if (chain.Count > 0) sb.Append("\n  (+ inherited props of ").Append(string.Join(" → ", chain)).Append(": rql-schema \"<class>\")");
            return sb.ToString();
        }

        private static string TypeText(Variant.Type type, PropertyHint hint, string hintString) => type switch
        {
            Variant.Type.Object when hintString.Length > 0 => hintString,
            Variant.Type.Int when hint == PropertyHint.Enum && hintString.Length > 0 => "int (" + hintString + ")",
            Variant.Type.Nil => "Variant",
            _ => type.ToString(),
        };
    }
}
#endif
