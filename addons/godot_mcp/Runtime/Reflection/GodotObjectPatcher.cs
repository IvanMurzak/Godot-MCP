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
    /// Applies <c>node-modify</c> / <c>resource-modify</c> patches to one live <see cref="GodotObject"/> and
    /// reports a write as a change only when reading it back proves it landed.
    ///
    /// <para>
    /// <b>Two routes.</b> A top-level segment naming a Godot property (snake_case, as
    /// <c>resource-get-data</c> returns it, or a GDScript property) is applied natively with
    /// <see cref="GodotObject.Set"/>: the value goes through the reflector's own converters (so a
    /// <c>Godot.Variant</c> envelope, a bare scalar, a <c>ResourceRef</c> or a CLR-typed value all work) and
    /// Godot performs its usual numeric coercion. Everything else — CLR (PascalCase) names and nested
    /// paths — stays on ReflectorNet's <c>TryModifyAt</c>/<c>TryPatch</c>, with snake_case segments mapped
    /// to their CLR name, a mismatched primitive <c>typeName</c> aligned to the declared type, and the
    /// setter failure ReflectorNet only sends to its <c>ILogger</c> surfaced as an error. The routing rules
    /// are pure-managed and unit-tested in <see cref="PropertyPatchPath"/>.
    /// </para>
    /// </summary>
    public sealed class GodotObjectPatcher
    {
        /// <summary>The patch path/value contract, shared by the modify tools' descriptions.</summary>
        public const string PathSyntaxHelp =
            "Path syntax: a Godot property name ('albedo_color', 'light_energy', a GDScript property) is set with " +
            "Godot's own setter, and its value may be copied verbatim from a get-data result (a bare scalar, a " +
            "{\"variantType\",\"value\"} envelope, a typed value, or a resource reference); or a C# member path: " +
            "'FieldName', 'nested/field' (snake_case segments map to C# names), 'arrayField/[i]', 'dictField/[key]'. " +
            "A write counts as a change only when reading it back shows it landed.";

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

        /// <summary>Apply a JSON Merge Patch. Returns <c>true</c> when at least one member changed.</summary>
        public bool ApplyJsonPatch(string json)
        {
            JsonObject? patch;
            try
            {
                patch = JsonNode.Parse(json) as JsonObject;
            }
            catch (JsonException)
            {
                patch = null; // TryPatch reports the parse error itself.
            }
            if (patch == null || patch.ContainsKey("$type"))
                return ApplyReflectorJson(json, "jsonPatch");

            var changed = false;
            var residual = new JsonObject();
            foreach (var (key, node) in patch.ToList())
            {
                var isEnvelope = node is JsonObject o && o.ContainsKey(GodotVariantPayload.TypeKeys[0]);
                var godotName = PropertyPatchPath.GodotPropertyFor(target.GetType(), key, isEnvelope, GodotProperties.Keys);
                if (godotName == null)
                {
                    patch.Remove(key);
                    residual[key] = node;
                    continue;
                }

                var property = GodotProperties[godotName];
                var value = SerializedMember.FromJson(property.ValueType, node?.ToJsonString() ?? "null");
                changed |= ApplyGodotProperty(godotName, value, $"jsonPatch '{key}'");
            }

            if (residual.Count > 0)
                changed |= ApplyReflectorJson(residual.ToJsonString(), "jsonPatch");
            return changed;
        }

        /// <summary>Apply one path patch. Returns <c>true</c> when the member changed.</summary>
        public bool ApplyPathPatch(NodePropertyPatch patch, string label)
        {
            // A value carrying nothing settable (no 'value', no 'fields', no 'props' — e.g. a '{typeName}'-only
            // entry) cannot change anything; say so instead of letting the no-op pass silently.
            if (NodePropertyPatch.IsStructuralNoOp(patch.Value))
            {
                logs.Warning($"{label} has no value/fields/props to apply — nothing to set; skipped (no-op).");
                return false;
            }
            var value = patch.Value!;

            var segment = PropertyPatchPath.SingleMemberSegment(patch.Path);
            if (segment != null)
            {
                var godotName = PropertyPatchPath.GodotPropertyFor(target.GetType(), segment,
                    PropertyPatchPath.IsVariantTypeName(value.typeName), GodotProperties.Keys);
                if (godotName != null)
                    return ApplyGodotProperty(godotName, value, label);
            }

            var resolved = PropertyPatchPath.Resolve(current ?? target, patch.Path);
            if (resolved.MemberType != null)
            {
                if (PropertyPatchPath.IsVariantTypeName(value.typeName) && resolved.MemberType != typeof(Variant))
                {
                    logs.Error($"{label}: a 'Godot.Variant' value can only be applied to a top-level Godot property; " +
                        $"'{resolved.Path}' is a '{resolved.MemberType.GetTypeId()}' member — send the value with that typeName.");
                    return false;
                }
                var sentTypeName = value.typeName;
                if (PropertyPatchPath.AlignPrimitiveTypeName(value, resolved.MemberType))
                    logs.Info($"{label}: value typeName '{sentTypeName}' aligned to '{value.typeName}'.");
            }

            var readBack = resolved.TopLevelMember as PropertyInfo;
            if (readBack != null && !readBack.PropertyType.IsValueType && readBack.PropertyType != typeof(string))
                readBack = null;
            var before = readBack?.GetValue(target);

            var logger = new ErrorCapturingLogger();
            if (!reflector.TryModifyAt(ref current, resolved.Path, value, logs: logs, logger: logger))
            {
                logs.Warning($"{label} did not modify the target (no-op).");
                return false;
            }
            if (!Settled(logger, label))
                return false;

            if (readBack != null && Equals(before, readBack.GetValue(target)))
            {
                logs.Warning($"{label} read back unchanged (it already had this value); not counted as a change.");
                return false;
            }
            return true;
        }

        bool ApplyReflectorJson(string json, string label)
        {
            var logger = new ErrorCapturingLogger();
            return reflector.TryPatch(ref current, json, logs: logs, logger: logger) && Settled(logger, label);
        }

        /// <summary>
        /// After a ReflectorNet call reported success: surface the errors it only logged (a swallowed setter
        /// failure) and record a <c>$type</c> replacement. Returns <c>false</c> when the write did not land.
        /// </summary>
        bool Settled(ErrorCapturingLogger logger, string label)
        {
            if (logger.Errors.Count > 0)
            {
                foreach (var error in logger.Errors)
                    logs.Error($"{label}: {error} — the value was NOT applied.");
                return false;
            }
            if (DivergedSurface == null && !ReferenceEquals(current, target))
                DivergedSurface = label;
            return true;
        }

        bool ApplyGodotProperty(string name, SerializedMember value, string label)
        {
            var property = GodotProperties[name];
            if (!TryToVariant(value, property, label, out var intended))
                return false;
            if (!PropertyPatchPath.IsAssignable(intended.VariantType, property.Type))
            {
                logs.Error($"{label}: a {intended.VariantType} value cannot be stored in '{name}' (a {property.Type} property); nothing changed.");
                return false;
            }

            var before = target.Get(name);
            target.Set(name, intended);
            var after = target.Get(name);

            if (Matches(intended, after))
            {
                if (Matches(before, after) && before.VariantType == after.VariantType)
                {
                    logs.Warning($"{label}: '{name}' already holds {Describe(after)}; not counted as a change.");
                    return false;
                }
                logs.Success($"{label}: '{name}' set from {Describe(before)} to {Describe(after)}.");
                return true;
            }
            if (Matches(before, after))
            {
                logs.Error($"{label}: Godot did not apply a {intended.VariantType} value to '{name}' " +
                    $"(a {property.Type} property; it may be read-only or reject this type) — still {Describe(after)}.");
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
                foreach (var entry in local)
                    logs.AddLast(entry);
                if (local.Any(e => e.Type == LogType.Error))
                    return false;
                variant = ToVariant(obj);
                return true;
            }
            catch (Exception ex)
            {
                foreach (var entry in local)
                    logs.AddLast(entry);
                logs.Error($"{label}: the value could not be read as a Godot value: {(ex as TargetInvocationException)?.InnerException?.Message ?? ex.Message}");
                return false;
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
        /// compare across Bool/Int/Float within <c>float</c> precision, text across String/StringName/NodePath,
        /// objects by instance id; anything else by type and text form.
        /// </summary>
        static bool Matches(Variant expected, Variant actual)
        {
            var e = expected.VariantType;
            var a = actual.VariantType;
            if (IsNumeric(e) && IsNumeric(a))
            {
                double x = ToDouble(expected), y = ToDouble(actual);
                return Math.Abs(x - y) <= 1e-6 * Math.Max(1.0, Math.Abs(x));
            }
            if (IsText(e) && IsText(a))
                return expected.AsString() == actual.AsString();
            if (e is Variant.Type.Object or Variant.Type.Nil || a is Variant.Type.Object or Variant.Type.Nil)
                return InstanceId(expected) == InstanceId(actual);
            return e == a && GD.VarToStr(expected) == GD.VarToStr(actual);
        }

        static bool IsNumeric(Variant.Type type) => type is Variant.Type.Bool or Variant.Type.Int or Variant.Type.Float;

        static bool IsText(Variant.Type type) => type is Variant.Type.String or Variant.Type.StringName or Variant.Type.NodePath;

        static double ToDouble(Variant v) => v.VariantType switch
        {
            Variant.Type.Bool => v.AsBool() ? 1 : 0,
            Variant.Type.Int => v.AsInt64(),
            _ => v.AsDouble(),
        };

        static ulong InstanceId(Variant v)
            => v.VariantType == Variant.Type.Object ? v.AsGodotObject()?.GetInstanceId() ?? 0 : 0;

        static string Describe(Variant v)
        {
            if (v.VariantType == Variant.Type.Object)
            {
                var obj = v.AsGodotObject();
                return obj switch
                {
                    null => "null",
                    Resource { ResourcePath: { Length: > 0 } path } => $"'{path}'",
                    _ => $"{obj.GetClass()}#{obj.GetInstanceId()}",
                };
            }
            var text = GD.VarToStr(v);
            return text.Length > 120 ? text.Substring(0, 120) + "…" : text;
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
            public GodotProperty(Variant.Type type, string className)
            {
                Type = type;
                ValueType = type == Variant.Type.Object
                    ? TypeUtils.GetType("Godot." + className) ?? typeof(Resource)
                    : typeof(Variant);
            }

            public Variant.Type Type { get; }

            /// <summary>
            /// The CLR type a value with no usable <c>typeName</c> is read as: the property's Godot class for an
            /// object property (so a <c>ResourceRef</c>/<c>NodeRef</c> resolves), else <c>Godot.Variant</c>.
            /// </summary>
            public Type ValueType { get; }
        }
    }
}
