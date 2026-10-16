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
            "jsonPatch top-level keys follow the same rules. A write counts as a change only when reading it back " +
            "shows it landed.";

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
                return ApplyReflectorJson(json); // TryPatch reports the parse error itself.
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object || root.TryGetProperty("$type", out _))
                    return ApplyReflectorJson(json);

                var changed = false;
                var residual = new JsonObject();
                foreach (var entry in root.EnumerateObject())
                {
                    var godotName = PropertyPatchPath.GodotPropertyFor(target.GetType(), entry.Name,
                        GodotVariantPayload.IsEnvelope(entry.Value), GodotProperties.Keys);
                    if (godotName == null)
                    {
                        residual[entry.Name] = JsonNode.Parse(entry.Value.GetRawText());
                        continue;
                    }
                    var property = GodotProperties[godotName];
                    changed |= ApplyGodotProperty(godotName, property, SerializedMember.FromJson(property.ValueType, entry.Value), $"jsonPatch '{entry.Name}'");
                }

                if (residual.Count > 0)
                    changed |= ApplyReflectorJson(residual.ToJsonString());
                return changed;
            }
        }

        bool ApplyReflectorJson(string json)
        {
            if (!reflector.TryPatch(ref current, json, logs: logs))
                return false;
            RecordDivergence("jsonPatch");
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
            if (resolved.MemberType != null)
            {
                if (valueIsVariant && resolved.MemberType != typeof(Variant))
                {
                    logs.Error($"{label}: a 'Godot.Variant' value can only be applied to a top-level Godot property; " +
                        $"'{resolved.Path}' is a '{resolved.MemberType.GetTypeId()}' member — send the value with that typeName.");
                    return false;
                }
                var sentTypeName = value.typeName;
                if (PropertyPatchPath.AlignPrimitiveTypeName(value, resolved.MemberType))
                    logs.Info($"{label}: value typeName '{sentTypeName}' aligned to '{value.typeName}'.");
            }

            // A top-level C# member backed by a Godot property is read back like the native route.
            var readBack = segment == null ? null : PropertyPatchPath.GodotBackingProperty(target.GetType(), resolved.Path, GodotProperties.Keys);
            var before = readBack == null ? default : target.Get(readBack);

            // ReflectorNet 5.4 sends a failed member setter only to its ILogger and still returns true.
            var logger = new ErrorCapturingLogger();
            if (!reflector.TryModifyAt(ref current, resolved.Path, value, logs: logs, logger: logger))
            {
                logs.Warning($"{label} did not modify the target (no-op).");
                return false;
            }
            if (logger.Errors.Count > 0)
            {
                foreach (var error in logger.Errors)
                    logs.Error($"{label}: {error} — the value was NOT applied.");
                return false;
            }
            RecordDivergence(label);

            if (readBack != null && Same(before, target.Get(readBack)))
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

            var before = target.Get(name);
            target.Set(name, intended);
            if (name == "script")
                godotProperties = null; // A new script brings its own properties; re-list them on next use.
            var after = target.Get(name);
            var unchanged = Same(before, after);

            if (Matches(intended, after))
            {
                if (unchanged)
                {
                    logs.Warning($"{label}: '{name}' already holds {Describe(after)}; not counted as a change.");
                    return false;
                }
                logs.Success($"{label}: '{name}' set from {Describe(before)} to {Describe(after)}.");
                return true;
            }
            if (unchanged)
            {
                logs.Error($"{label}: Godot did not apply the value to '{name}' (it may be read-only) — still {Describe(after)}.");
                return false;
            }
            logs.Success($"{label}: '{name}' set from {Describe(before)} to {Describe(after)} (Godot adjusted the requested {Describe(intended)}).");
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
        /// True when <paramref name="actual"/> holds <paramref name="expected"/> as Godot stores it: numbers
        /// compare across Bool/Int/Float (a requested double also matches its 32-bit <c>float</c> rounding),
        /// text across String/StringName/NodePath, objects by instance id; anything else by type and text form.
        /// </summary>
        static bool Matches(Variant expected, Variant actual) => Compare(expected, actual, allowFloatRounding: true);

        /// <summary>
        /// True when two values read back from Godot are the same: <see cref="Matches"/> without the
        /// <c>float</c>-rounding tolerance, so a real one-ulp change is still a change.
        /// </summary>
        static bool Same(Variant before, Variant after) => Compare(before, after, allowFloatRounding: false);

        static bool Compare(Variant expected, Variant actual, bool allowFloatRounding)
        {
            var e = expected.VariantType;
            var a = actual.VariantType;
            if (IsNumeric(e) && IsNumeric(a))
            {
                if (e != Variant.Type.Float && a != Variant.Type.Float)
                    return ToInt64(expected) == ToInt64(actual);
                double x = ToDouble(expected), y = ToDouble(actual);
                return x == y || (allowFloatRounding && (float)x == (float)y);
            }
            if (IsText(e) && IsText(a))
                return expected.AsString() == actual.AsString();
            if (e is Variant.Type.Object or Variant.Type.Nil && a is Variant.Type.Object or Variant.Type.Nil)
                return InstanceId(expected) == InstanceId(actual);
            return e == a && GD.VarToStr(expected) == GD.VarToStr(actual);
        }

        static bool IsNumeric(Variant.Type type) => type is Variant.Type.Bool or Variant.Type.Int or Variant.Type.Float;

        static bool IsText(Variant.Type type) => type is Variant.Type.String or Variant.Type.StringName or Variant.Type.NodePath;

        static double ToDouble(Variant v) => v.VariantType == Variant.Type.Float ? v.AsDouble() : ToInt64(v);

        static long ToInt64(Variant v) => v.VariantType == Variant.Type.Bool ? (v.AsBool() ? 1 : 0) : v.AsInt64();

        static ulong InstanceId(Variant v)
            => v.VariantType == Variant.Type.Object ? v.AsGodotObject()?.GetInstanceId() ?? 0 : 0;

        static string Describe(Variant v)
        {
            switch (v.VariantType)
            {
                case Variant.Type.Object:
                    return v.AsGodotObject() switch
                    {
                        null => "null",
                        Resource { ResourcePath: { Length: > 0 } path } => $"'{path}'",
                        var obj => $"{obj.GetClass()}#{obj.GetInstanceId()}",
                    };
                case Variant.Type.Array:
                case Variant.Type.Dictionary:
                case >= Variant.Type.PackedByteArray and < Variant.Type.Max:
                    return v.VariantType.ToString(); // Never serialize a whole collection into a log line.
                default:
                    var text = GD.VarToStr(v);
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
