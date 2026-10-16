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
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Applies <c>node-modify</c> / <c>resource-modify</c> patches to one live <see cref="GodotObject"/>, counting
    /// a write as a change only when reading it back shows it landed. A Godot property name is set natively with
    /// <see cref="GodotObject.Set"/>; C# member paths go through ReflectorNet. The routing rules live in
    /// <see cref="PropertyPatchPath"/>.
    /// </summary>
    public sealed class GodotObjectPatcher
    {
        /// <summary>The patch path/value contract, shared by the modify tools' descriptions.</summary>
        public const string PathSyntaxHelp =
            "Path syntax: a Godot property name ('albedo_color', 'light_energy', a GDScript property) is set with " +
            "Godot's own setter, and its value may be copied verbatim from a get-data result (a bare scalar, a " +
            "{\"variantType\",\"value\"} envelope, a typed value, or a resource reference); or a C# member path: " +
            "'FieldName', 'nested/field' (snake_case segments map to C# names), 'arrayField/[i]', 'dictField/[key]'. " +
            "jsonPatch keys follow the same rules at every depth, and a JSON object given for a non-object property " +
            "merges into it ({\"position\":{\"x\":1}}); each key is applied on its own. A write counts as a change only " +
            "when reading it back shows it landed.";

        readonly Reflector reflector;
        readonly GodotObject target;
        readonly Logs logs;
        Dictionary<string, GodotProperty>? godotProperties;
        object? current;

        public GodotObjectPatcher(Reflector reflector, GodotObject target, Logs logs)
        {
            this.reflector = reflector;
            this.target = target;
            this.logs = logs;
            current = target;
        }

        /// <summary>
        /// The surface whose ReflectorNet call replaced the target with a fresh instance (a <c>$type</c>
        /// replacement) instead of mutating it in place, or <c>null</c>. The caller must not persist then.
        /// </summary>
        public string? DivergedSurface { get; private set; }

        /// <summary>
        /// Apply <paramref name="jsonPatch"/> first, then every entry of <paramref name="pathPatches"/>. A bad
        /// entry is logged and skipped, never aborting the rest. Returns <c>true</c> when anything changed.
        /// </summary>
        public bool Apply(string? jsonPatch, IReadOnlyList<NodePropertyPatch>? pathPatches)
        {
            var changed = false;
            if (!string.IsNullOrWhiteSpace(jsonPatch))
                changed |= Guarded("jsonPatch", () => ApplyJsonPatch(jsonPatch!));

            for (var i = 0; i < (pathPatches?.Count ?? 0); i++)
            {
                // Once a surface replaced the target, the caller discards the whole call; a native write
                // after that would still land on the live object behind a "left unchanged" report.
                if (DivergedSurface != null)
                {
                    logs.Error($"pathPatches[{i}..] skipped: {DivergedSurface} already replaced the target instance.");
                    break;
                }
                var patch = pathPatches![i];
                if (patch == null || string.IsNullOrEmpty(patch.Path))
                {
                    logs.Error($"pathPatches[{i}] with empty path skipped.");
                    continue;
                }
                var label = $"pathPatches[{i}] ('{patch.Path}')";
                if (patch.Value == null)
                {
                    logs.Error($"{label} with null value skipped.");
                    continue;
                }
                changed |= Guarded(label, () => ApplyPathPatch(patch, label));
            }
            return changed;
        }

        bool Guarded(string label, Func<bool> apply)
        {
            try
            {
                return apply();
            }
            catch (Exception ex)
            {
                logs.Error($"{label} threw and was skipped: {ex.Message}");
                return false;
            }
        }

        bool ApplyJsonPatch(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                return ApplyReflectorJson(json, "jsonPatch"); // TryPatch reports the parse error itself.
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("$type", out _))
                    return ApplyReflectorJson(json, "jsonPatch");

                var changed = false;
                var residual = new JsonObject();
                foreach (var entry in root.EnumerateObject())
                {
                    var isEnvelope = GodotVariantPayload.IsEnvelope(entry.Value);
                    var godotName = PropertyPatchPath.GodotPropertyFor(target.GetType(), entry.Name, isEnvelope, GodotProperties.Keys);
                    // A plain JSON object for a non-object property is a merge into its value ({"position":{"x":1}}),
                    // which only the ReflectorNet route can do — and only for a property with a C# member (a
                    // script-only one stays native, which explains the envelope it needs); an object property's
                    // JSON object is a reference.
                    var isMerge = entry.Value.ValueKind == JsonValueKind.Object && !isEnvelope;
                    if (godotName == null || !GodotProperties.TryGetValue(godotName, out var property)
                        || (isMerge && property.Type != Variant.Type.Object && PropertyPatchPath.FindMember(target.GetType(), entry.Name) != null))
                    {
                        residual[entry.Name] = JsonNode.Parse(entry.Value.GetRawText());
                        continue;
                    }
                    changed |= ApplyGodotProperty(godotName, property, SerializedMember.FromJson(property.ValueType, entry.Value), $"jsonPatch '{entry.Name}'");
                }

                PropertyPatchPath.CanonicalizeMergePatch(reflector, current, (current ?? target).GetType(), residual);
                // One TryPatch per key: TryPatch reports false when ANY key fails, even though the others were
                // already applied, which would hide those applied changes from the caller.
                var keys = residual.ToList();
                residual.Clear(); // detaches the nodes so each can move into its own single-key patch
                foreach (var (key, node) in keys)
                    changed |= ApplyReflectorJson(new JsonObject { [key] = node }.ToJsonString(), $"jsonPatch '{key}'");
                return changed;
            }
        }

        bool ApplyReflectorJson(string json, string label)
        {
            // ReflectorNet reports a failure (a throwing setter included) to Logs and returns false; its message
            // names the member, not the patch entry, so say which entry it belongs to.
            if (!reflector.TryPatch(ref current, json, logs: logs))
            {
                logs.Error($"{label} was NOT applied.");
                return false;
            }
            RecordDivergence(label);
            return true;
        }

        bool ApplyPathPatch(NodePropertyPatch patch, string label)
        {
            // A value carrying nothing settable (no 'value', no 'fields', no 'props' — e.g. a '{typeName}'-only
            // entry) cannot change anything; say so instead of letting the no-op pass silently.
            if (NodePropertyPatch.IsStructuralNoOp(patch.Value))
            {
                logs.Warning($"{label} has no value/fields/props to apply — nothing to set; skipped (no-op).");
                return false;
            }
            var value = patch.Value!;
            var valueIsVariant = PropertyPatchPath.IsVariantTypeName(value.typeName);

            var segment = PropertyPatchPath.SingleMemberSegment(patch.Path);
            if (segment != null)
            {
                var godotName = PropertyPatchPath.GodotPropertyFor(target.GetType(), segment, valueIsVariant, GodotProperties.Keys);
                if (godotName != null)
                    return ApplyGodotProperty(godotName, GodotProperties[godotName], value, label);
            }

            var resolved = PropertyPatchPath.Resolve(current ?? target, patch.Path);
            if (valueIsVariant && resolved.MemberType != null && resolved.MemberType != typeof(Variant))
            {
                logs.Error($"{label}: a 'Godot.Variant' value can only be applied to a top-level Godot property; " +
                    $"'{resolved.Path}' is a '{resolved.MemberType.GetTypeId()}' member — send the value with that typeName.");
                return false;
            }

            // A top-level C# member backed by a Godot property is read back like the native route.
            var readBack = segment == null ? null : PropertyPatchPath.GodotBackingProperty(target.GetType(), resolved.Path, GodotProperties.Keys);
            var before = readBack == null ? default : Snapshot.Of(target.Get(readBack));

            // ReflectorNet (5.4.3+) reports a failed write to Logs with a false result; only a false without a
            // logged reason is a silent no-op. Its message names the member, not the patch entry.
            var local = new Logs();
            var modified = reflector.TryModifyAt(ref current, resolved.Path, value, logs: local);
            foreach (var entry in local)
                logs.AddLast(entry);
            if (!modified)
            {
                if (ReflectionArgumentGuard.Failures(local).Count == 0)
                    logs.Warning($"{label} did not modify the target (no-op).");
                else
                    logs.Error($"{label}: the value was NOT applied.");
                return false;
            }
            // Applied, but a converter reported a problem on the way (e.g. an unresolved reference written as
            // null): the member did change, so it counts, and the entry is named so the error is not lost.
            if (ReflectionArgumentGuard.Failures(local).Count > 0)
                logs.Error($"{label}: applied, but with the error(s) above — check the value that was written.");
            RecordDivergence(label);

            if (readBack != null && before.IsSameAs(Snapshot.Of(target.Get(readBack))))
            {
                logs.Warning($"{label} read back unchanged (it already had this value); not counted as a change.");
                return false;
            }
            return true;
        }

        void RecordDivergence(string label)
        {
            if (DivergedSurface == null && !ReferenceEquals(current, target))
                DivergedSurface = label;
        }

        bool ApplyGodotProperty(string name, GodotProperty property, SerializedMember value, string label)
        {
            if (!TryToVariant(value, property, label, out var intended))
                return false;
            if (!PropertyPatchPath.IsAssignable(intended.VariantType, property.Type))
            {
                logs.Error($"{label}: a {intended.VariantType} value cannot be stored in '{name}' (a {property.Type} property); nothing changed.");
                return false;
            }

            var stored = target.Get(name);
            if (!TryKeepCollectionTyping(ref intended, stored))
            {
                logs.Error($"{label}: not every element fits the typed collection '{name}'; nothing changed.");
                return false;
            }
            var before = Snapshot.Of(stored);
            var requested = Snapshot.Of(intended);
            target.Set(name, intended);
            if (name == "script")
                godotProperties = null; // A new script brings its own properties; re-list them on next use.
            var after = Snapshot.Of(target.Get(name));
            var unchanged = before.IsSameAs(after);

            if (requested.IsHeldBy(after))
            {
                if (unchanged)
                {
                    logs.Warning($"{label}: '{name}' already holds {after}; not counted as a change.");
                    return false;
                }
                logs.Success($"{label}: '{name}' set from {before} to {after}.");
                return true;
            }
            if (unchanged)
            {
                logs.Error($"{label}: Godot did not apply the value to '{name}' (it may be read-only) — still {after}.");
                return false;
            }
            logs.Success($"{label}: '{name}' set from {before} to {after} (Godot adjusted the requested {requested}).");
            return true;
        }

        bool TryToVariant(SerializedMember value, GodotProperty property, string label, out Variant variant)
        {
            variant = default;
            var local = new Logs();
            try
            {
                var obj = reflector.Deserialize(value, fallbackType: property.ValueType, logs: local);
                if (ReflectionArgumentGuard.Failures(local).Count > 0)
                    return false;
                variant = ToVariant(obj);
                return true;
            }
            catch (Exception ex)
            {
                logs.Error($"{label}: the value could not be read as a Godot value: {(ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message}");
                return false;
            }
            finally
            {
                foreach (var entry in local)
                    logs.AddLast(entry);
            }
        }

        static readonly MethodInfo VariantFrom = typeof(Variant).GetMethod(nameof(Variant.From))!;

        static Variant ToVariant(object? obj) => obj switch
        {
            null => default,
            Variant v => v,
            _ => (Variant)VariantFrom.MakeGenericMethod(obj.GetType()).Invoke(null, new[] { obj })!,
        };

        /// <summary>
        /// A replacement for a typed <c>Array</c>/<c>Dictionary</c> (e.g. <c>Array[Material]</c>) must keep the
        /// property's typing, or Godot rejects it: rebuild the new elements inside an emptied copy of the
        /// current collection, which carries that typing (and none for an untyped one). Returns <c>false</c> when
        /// the typed copy refused an element (Godot drops it with an error), which would otherwise silently shrink
        /// the collection.
        /// </summary>
        static bool TryKeepCollectionTyping(ref Variant intended, Variant current)
        {
            if (intended.VariantType == Variant.Type.Array && current.VariantType == Variant.Type.Array)
            {
                var source = intended.AsGodotArray();
                var typed = current.AsGodotArray().Slice(0, 0); // empty, but carries the typing
                foreach (var item in source)
                    typed.Add(item);
                intended = typed;
                return typed.Count == source.Count;
            }
            if (intended.VariantType == Variant.Type.Dictionary && current.VariantType == Variant.Type.Dictionary)
            {
                var source = intended.AsGodotDictionary();
                var typed = current.AsGodotDictionary().Duplicate();
                typed.Clear();
                foreach (var entry in source)
                    typed[entry.Key] = entry.Value;
                intended = typed;
                return typed.Count == source.Count;
            }
            return true;
        }

        /// <summary>
        /// A value read from (or about to be written to) Godot, converted to its payload once so the read-back
        /// comparisons and log lines do not each walk a large collection again.
        /// </summary>
        readonly struct Snapshot
        {
            /// <summary>Largest value (in nested values) compared by payload; a bigger one is compared by its text form.</summary>
            const int MaxPayloadValues = 1 << 16;

            readonly Variant value;
            // null for Rid / Callable / Signal (no payload form) and for a collection over MaxPayloadValues
            // (or containing itself), which are compared by their text form instead.
            readonly GodotVariantPayload? payload;

            Snapshot(Variant value, GodotVariantPayload? payload)
            {
                this.value = value;
                this.payload = payload;
            }

            public static Snapshot Of(Variant value)
                => new Snapshot(value, Godot_Variant_ReflectionConverter.TryToPayload(value, MaxPayloadValues));

            /// <summary>True when <paramref name="stored"/> holds this requested value as Godot stores it (float32 rounding allowed).</summary>
            public bool IsHeldBy(Snapshot stored) => Equivalent(stored, allowFloatRounding: true);

            /// <summary>True when two values read back from Godot are the same (no rounding tolerance).</summary>
            public bool IsSameAs(Snapshot other) => Equivalent(other, allowFloatRounding: false);

            bool Equivalent(Snapshot other, bool allowFloatRounding)
                => payload != null && other.payload != null
                    ? payload.IsEquivalentTo(other.payload, allowFloatRounding)
                    : value.VariantType == other.value.VariantType && GD.VarToStr(value) == GD.VarToStr(other.value);

            public override string ToString()
            {
                if (value.VariantType == Variant.Type.Object)
                {
                    return value.AsGodotObject() switch
                    {
                        null => "null",
                        Resource { ResourcePath: { Length: > 0 } path } => $"'{path}'",
                        var obj => $"{obj.GetClass()}#{obj.GetInstanceId()}",
                    };
                }
                // Never serialize a whole collection into a log line.
                if (payload?.CollectionCount is { } count)
                    return $"{payload.Kind}({count})";
                if (value.VariantType is Variant.Type.Array or Variant.Type.Dictionary || GodotVariantPayload.PackedElementKind(value.VariantType) != null)
                    return Godot_Variant_ReflectionConverter.CollectionSummary(value);
                var text = GD.VarToStr(value);
                return text.Length > 120 ? text.Substring(0, 120) + "…" : text;
            }
        }

        /// <summary>The target's Godot properties by name (group/category pseudo-entries excluded), built once.</summary>
        Dictionary<string, GodotProperty> GodotProperties
        {
            get
            {
                if (godotProperties != null)
                    return godotProperties;

                godotProperties = new Dictionary<string, GodotProperty>(StringComparer.Ordinal);
                const PropertyUsageFlags pseudo = PropertyUsageFlags.Category | PropertyUsageFlags.Group | PropertyUsageFlags.Subgroup;
                foreach (var info in target.GetPropertyList())
                {
                    if ((info["usage"].As<PropertyUsageFlags>() & pseudo) != 0)
                        continue;
                    var name = info["name"].AsString();
                    if (name.Length > 0)
                        godotProperties[name] = new GodotProperty(info["type"].As<Variant.Type>(), info["class_name"].AsString());
                }
                return godotProperties;
            }
        }

        readonly struct GodotProperty
        {
            readonly string className;

            public GodotProperty(Variant.Type type, string className)
            {
                Type = type;
                this.className = className;
            }

            public Variant.Type Type { get; }

            /// <summary>
            /// The CLR type a value with no usable <c>typeName</c> is read as: the property's Godot class for an
            /// object property (so a <c>ResourceRef</c>/<c>NodeRef</c> resolves), else <c>Godot.Variant</c>.
            /// </summary>
            public Type ValueType => Type != Variant.Type.Object
                ? typeof(Variant)
                : TypeUtils.GetType("Godot." + className) ?? typeof(Resource);
        }
    }
}
