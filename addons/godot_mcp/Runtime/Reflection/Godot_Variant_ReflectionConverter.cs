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
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Converter;
using com.IvanMurzak.ReflectorNet.Model;
using Godot;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using VariantType = global::Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// ReflectorNet converter for <see cref="global::Godot.Variant"/> — the fix for issue #292.
    ///
    /// <para>
    /// <b>The defect.</b> <c>Godot.Variant</c> is a struct whose only two properties (<c>VariantType</c>,
    /// <c>Obj</c>) are get-only, and nothing was registered to convert it. ReflectorNet therefore fell
    /// through to <c>GenericReflectionConverter&lt;object&gt;</c>, which tried to read the wire value as a
    /// nested <c>SerializedMember</c>, hit an unknown-property <c>JsonException</c>, <b>swallowed it</b>, and
    /// returned <c>GetDefaultValue(typeof(Variant))</c> = <c>default(Variant)</c> (i.e. <c>Nil</c>).
    /// <c>MethodWrapper.VerifyParameters</c> cannot catch that either — a boxed <c>default(Variant)</c> is a
    /// perfectly valid <c>Godot.Variant</c> instance, so <c>IsInstanceOfType</c> passes. Net effect:
    /// <c>reflection-method-call</c> invoked e.g. <c>ProjectSettings.SetSetting(name, Nil)</c> and reported
    /// <c>System.Void</c> success while writing nothing.
    /// </para>
    ///
    /// <para>
    /// <b>The fix.</b> Registering this converter makes <c>Godot.Variant</c> win converter selection
    /// (exact-type distance 0 =&gt; priority 10000, vs 9998 for the <c>object</c> fallback), and it either
    /// materializes a REAL variant from a documented wire shape or throws
    /// <see cref="ArgumentException"/> with an actionable message. There is no path left that yields a silent
    /// <c>Nil</c> — the sole way to obtain <c>Nil</c> is to send JSON <c>null</c> / no value at all /
    /// <c>{"variantType":"Nil"}</c>, all of which are explicit.
    /// </para>
    ///
    /// <para>
    /// <b>#if TOOLS / testability split.</b> Everything decidable — parsing, validation, error text — lives
    /// in the pure-managed <see cref="GodotVariantPayload"/> and is unit-tested in the plain xUnit host. Only
    /// <see cref="ToVariant"/> / <see cref="ToPayload"/> touch native Godot marshalling (which faults the
    /// test host without a Godot native library), and they are reached only from a live editor/game process.
    /// Mirrors the pure-ref / injected-resolver split of
    /// <see cref="Godot_Resource_ReflectionConverter{T}"/>.
    /// </para>
    /// </summary>
    public class Godot_Variant_ReflectionConverter : GenericReflectionConverter<global::Godot.Variant>
    {
        // Taken from the PARSER's accepted-alias lists rather than restated, so the shape this writes can
        // never drift from the shape GodotVariantPayload.Parse reads (a one-way wire break).

        /// <summary>JSON key naming the Godot variant type on the wire.</summary>
        public static readonly string VariantTypeKey = GodotVariantPayload.TypeKeys[0];

        /// <summary>JSON key carrying the value on the wire.</summary>
        public static readonly string ValueKey = GodotVariantPayload.ValueKeys[0];

        /// <summary>JSON key carrying an explanatory note for a variant that cannot be written back.</summary>
        public const string NoteKey = "note";

        // A Variant is a leaf on the wire (a scalar, or a {variantType, value} envelope) — there is no nested
        // object graph to descend into. Turning cascade off routes the raw JsonElement (scalar OR object) to
        // DeserializeValueAsJsonElement instead of ReflectorNet's "value must be a SerializedMember-shaped
        // object" structural walk, which is what lets a bare "res://x.tscn" work.
        public override bool AllowCascadeSerialization => false;
        public override bool AllowSetValue => true;

        // Treat the wire envelope as atomic on the merge-patch (Reflector.TryPatch) path so it routes through
        // this converter instead of being descended key-by-key. Mirrors the Resource converter.
        public override bool TreatJsonObjectAsAtomicValue(Type type) => true;

        public override object? Deserialize(
            Reflector reflector,
            SerializedMember data,
            Type? fallbackType = null,
            string? fallbackName = null,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null,
            DeserializationContext? context = null)
        {
            return FromJson(data.valueJsonElement);
        }

        protected override object? DeserializeValueAsJsonElement(
            Reflector reflector,
            SerializedMember data,
            Type type,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null)
        {
            return FromJson(data.valueJsonElement);
        }

        protected override SerializedMember InternalSerialize(
            Reflector reflector,
            object? obj,
            Type type,
            string? name = null,
            bool recursive = true,
            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            int depth = 0,
            Logs? logs = null,
            ILogger? logger = null,
            SerializationContext? context = null)
        {
            var variant = obj is global::Godot.Variant v ? v : default;
            // FromJson(Type, string, string) parses + clones internally (SerializedMember.SetJsonValue).
            return SerializedMember.FromJson(type, ToWireNode(variant).ToJsonString(), name);
        }

        /// <summary>
        /// Parse the wire JSON and materialize a live <see cref="global::Godot.Variant"/>. Throws
        /// <see cref="ArgumentException"/> (never returns a silent <c>Nil</c>) when the payload is
        /// unreadable.
        /// </summary>
        static object FromJson(JsonElement? valueJsonElement)
            => ToVariant(GodotVariantPayload.Parse(valueJsonElement));

        // --- Native materialization (NOT unit-testable: godotsharp_* P/Invoke) --------------------------

        /// <summary>
        /// Build a live <see cref="global::Godot.Variant"/> from a validated payload. Every branch is
        /// total — <see cref="GodotVariantPayload.Parse"/> has already rejected any type this cannot build.
        /// </summary>
        public static global::Godot.Variant ToVariant(GodotVariantPayload payload)
        {
            switch (payload.Kind)
            {
                case VariantType.Nil:
                    return default;
                case VariantType.Bool:
                    return global::Godot.Variant.CreateFrom(payload.BoolValue);
                case VariantType.Int:
                    return global::Godot.Variant.CreateFrom(payload.IntValue);
                case VariantType.Float:
                    return global::Godot.Variant.CreateFrom(payload.FloatValue);
                case VariantType.String:
                    return global::Godot.Variant.CreateFrom(payload.TextValue);
                case VariantType.StringName:
                    return global::Godot.Variant.CreateFrom(new StringName(payload.TextValue));
                case VariantType.NodePath:
                    return global::Godot.Variant.CreateFrom(new NodePath(payload.TextValue));

                case VariantType.Vector2:
                    return global::Godot.Variant.CreateFrom(new Vector2(payload.Component(0), payload.Component(1)));
                case VariantType.Vector2I:
                    return global::Godot.Variant.CreateFrom(new Vector2I(payload.ComponentInt(0), payload.ComponentInt(1)));
                case VariantType.Vector3:
                    return global::Godot.Variant.CreateFrom(ToVector3(payload, 0));
                case VariantType.Vector3I:
                    return global::Godot.Variant.CreateFrom(new Vector3I(payload.ComponentInt(0), payload.ComponentInt(1), payload.ComponentInt(2)));
                case VariantType.Vector4:
                    return global::Godot.Variant.CreateFrom(ToVector4(payload, 0));
                case VariantType.Vector4I:
                    return global::Godot.Variant.CreateFrom(new Vector4I(
                        payload.ComponentInt(0), payload.ComponentInt(1), payload.ComponentInt(2), payload.ComponentInt(3)));

                case VariantType.Quaternion:
                    return global::Godot.Variant.CreateFrom(new Quaternion(
                        payload.Component(0), payload.Component(1), payload.Component(2), payload.Component(3)));
                case VariantType.Plane:
                    return global::Godot.Variant.CreateFrom(new Plane(
                        payload.Component(0), payload.Component(1), payload.Component(2), payload.Component(3)));
                case VariantType.Color:
                    return global::Godot.Variant.CreateFrom(new Color(
                        payload.Component(0), payload.Component(1), payload.Component(2), payload.Component(3)));

                case VariantType.Rect2:
                    return global::Godot.Variant.CreateFrom(new Rect2(
                        payload.Component(0), payload.Component(1), payload.Component(2), payload.Component(3)));
                case VariantType.Rect2I:
                    return global::Godot.Variant.CreateFrom(new Rect2I(
                        payload.ComponentInt(0), payload.ComponentInt(1), payload.ComponentInt(2), payload.ComponentInt(3)));
                case VariantType.Aabb:
                    return global::Godot.Variant.CreateFrom(new Aabb(
                        payload.Component(0), payload.Component(1), payload.Component(2),
                        payload.Component(3), payload.Component(4), payload.Component(5)));

                case VariantType.Transform2D:
                    return global::Godot.Variant.CreateFrom(new Transform2D(
                        payload.Component(0), payload.Component(1),
                        payload.Component(2), payload.Component(3),
                        payload.Component(4), payload.Component(5)));
                case VariantType.Basis:
                    return global::Godot.Variant.CreateFrom(ToBasis(payload, 0));
                case VariantType.Transform3D:
                    return global::Godot.Variant.CreateFrom(new Transform3D(ToBasis(payload, 0), ToVector3(payload, 9)));
                case VariantType.Projection:
                    return global::Godot.Variant.CreateFrom(new Projection(
                        ToVector4(payload, 0), ToVector4(payload, 4), ToVector4(payload, 8), ToVector4(payload, 12)));

                case VariantType.Object:
                    return ResolveObject(payload) is { } resolved ? global::Godot.Variant.CreateFrom(resolved) : default;
                case VariantType.Array:
                    var array = new global::Godot.Collections.Array();
                    foreach (var item in payload.Items)
                        array.Add(ToVariant(item));
                    return global::Godot.Variant.CreateFrom(array);
                case VariantType.Dictionary:
                    var dictionary = new global::Godot.Collections.Dictionary();
                    foreach (var entry in payload.Entries)
                        dictionary[ToVariant(entry.Key)] = ToVariant(entry.Value);
                    return global::Godot.Variant.CreateFrom(dictionary);
                case VariantType.PackedByteArray:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => (byte)i.IntValue).ToArray());
                case VariantType.PackedInt32Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => (int)i.IntValue).ToArray());
                case VariantType.PackedInt64Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => i.IntValue).ToArray());
                case VariantType.PackedFloat32Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => (float)i.FloatValue).ToArray());
                case VariantType.PackedFloat64Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => i.FloatValue).ToArray());
                case VariantType.PackedStringArray:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => i.TextValue).ToArray());
                case VariantType.PackedVector2Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => new Vector2(i.Component(0), i.Component(1))).ToArray());
                case VariantType.PackedVector3Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => ToVector3(i, 0)).ToArray());
                case VariantType.PackedVector4Array:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => ToVector4(i, 0)).ToArray());
                case VariantType.PackedColorArray:
                    return global::Godot.Variant.CreateFrom(payload.Items.Select(i => new Color(i.Component(0), i.Component(1), i.Component(2), i.Component(3))).ToArray());

                default:
                    // Unreachable: GodotVariantPayload.Parse rejects every type not handled above.
                    throw new ArgumentException(
                        $"Cannot build a Godot.Variant of type '{payload.Kind}'. {GodotVariantPayload.WireFormatHelp}");
            }
        }

        /// <summary>Resolve an <c>Object</c> payload to the live object (a saved resource is loaded by path).</summary>
        static GodotObject? ResolveObject(GodotVariantPayload payload)
        {
            if (payload.IsNullObject)
                return null;
            var resolved = payload.InstanceId != 0
                ? GodotObject.InstanceFromId(payload.InstanceId)
                : ResourceLoader.Load(payload.ResourcePath!);
            return resolved ?? throw new ArgumentException(
                $"Cannot read a Godot.Variant: no object found for {(payload.InstanceId != 0 ? "instanceId " + payload.InstanceId : "'" + payload.ResourcePath + "'")}.");
        }

        static Vector3 ToVector3(GodotVariantPayload p, int offset)
            => new Vector3(p.Component(offset), p.Component(offset + 1), p.Component(offset + 2));

        static Vector4 ToVector4(GodotVariantPayload p, int offset)
            => new Vector4(p.Component(offset), p.Component(offset + 1), p.Component(offset + 2), p.Component(offset + 3));

        static Basis ToBasis(GodotVariantPayload p, int offset)
            => new Basis(
                p.Component(offset + 0), p.Component(offset + 1), p.Component(offset + 2),
                p.Component(offset + 3), p.Component(offset + 4), p.Component(offset + 5),
                p.Component(offset + 6), p.Component(offset + 7), p.Component(offset + 8));

        /// <summary>
        /// Describe a live variant as the wire payload (the inverse of <see cref="ToVariant"/> for every
        /// round-trippable type). Reads native variant storage, so it runs only in a live Godot process.
        /// </summary>
        public static GodotVariantPayload ToPayload(global::Godot.Variant variant)
        {
            switch (variant.VariantType)
            {
                case VariantType.Nil:
                    return GodotVariantPayload.Nil();
                case VariantType.Bool:
                    return GodotVariantPayload.FromBool(variant.AsBool());
                case VariantType.Int:
                    return GodotVariantPayload.FromInt(variant.AsInt64());
                case VariantType.Float:
                    return GodotVariantPayload.FromFloat(variant.AsDouble());
                case VariantType.String:
                case VariantType.StringName:
                case VariantType.NodePath:
                    return GodotVariantPayload.FromText(variant.VariantType, variant.AsString());

                case VariantType.Vector2:
                    var v2 = variant.AsVector2();
                    return Components(variant.VariantType, v2.X, v2.Y);
                case VariantType.Vector2I:
                    var v2i = variant.AsVector2I();
                    return Components(variant.VariantType, v2i.X, v2i.Y);
                case VariantType.Vector3:
                    return Components(variant.VariantType, Flatten(variant.AsVector3()));
                case VariantType.Vector3I:
                    var v3i = variant.AsVector3I();
                    return Components(variant.VariantType, v3i.X, v3i.Y, v3i.Z);
                case VariantType.Vector4:
                    return Components(variant.VariantType, Flatten(variant.AsVector4()));
                case VariantType.Vector4I:
                    var v4i = variant.AsVector4I();
                    return Components(variant.VariantType, v4i.X, v4i.Y, v4i.Z, v4i.W);

                case VariantType.Quaternion:
                    var q = variant.AsQuaternion();
                    return Components(variant.VariantType, q.X, q.Y, q.Z, q.W);
                case VariantType.Plane:
                    var plane = variant.AsPlane();
                    return Components(variant.VariantType, plane.Normal.X, plane.Normal.Y, plane.Normal.Z, plane.D);
                case VariantType.Color:
                    var c = variant.AsColor();
                    return Components(variant.VariantType, c.R, c.G, c.B, c.A);

                case VariantType.Rect2:
                    var r2 = variant.AsRect2();
                    return Components(variant.VariantType, r2.Position.X, r2.Position.Y, r2.Size.X, r2.Size.Y);
                case VariantType.Rect2I:
                    var r2i = variant.AsRect2I();
                    return Components(variant.VariantType, r2i.Position.X, r2i.Position.Y, r2i.Size.X, r2i.Size.Y);
                case VariantType.Aabb:
                    var aabb = variant.AsAabb();
                    return Components(variant.VariantType,
                        aabb.Position.X, aabb.Position.Y, aabb.Position.Z,
                        aabb.Size.X, aabb.Size.Y, aabb.Size.Z);

                case VariantType.Transform2D:
                    var t2 = variant.AsTransform2D();
                    return Components(variant.VariantType, t2.X.X, t2.X.Y, t2.Y.X, t2.Y.Y, t2.Origin.X, t2.Origin.Y);
                case VariantType.Basis:
                    return Components(variant.VariantType, FlattenBasis(variant.AsBasis()));
                case VariantType.Transform3D:
                    var t3 = variant.AsTransform3D();
                    return Components(variant.VariantType, FlattenBasis(t3.Basis).Concat(Flatten(t3.Origin)).ToArray());
                case VariantType.Projection:
                    var proj = variant.AsProjection();
                    return Components(variant.VariantType, Flatten(proj.X)
                        .Concat(Flatten(proj.Y)).Concat(Flatten(proj.Z)).Concat(Flatten(proj.W)).ToArray());

                case VariantType.Object:
                    var obj = variant.AsGodotObject();
                    return GodotVariantPayload.FromObject((obj as Resource)?.ResourcePath, obj?.GetInstanceId() ?? 0);
                case VariantType.Array:
                    return GodotVariantPayload.FromItems(VariantType.Array, variant.AsGodotArray().Select(ToPayload).ToArray());
                case VariantType.Dictionary:
                    return GodotVariantPayload.FromEntries(variant.AsGodotDictionary()
                        .Select(e => new KeyValuePair<GodotVariantPayload, GodotVariantPayload>(ToPayload(e.Key), ToPayload(e.Value))).ToArray());
                case VariantType.PackedByteArray:
                    return Items(variant.VariantType, variant.AsByteArray().Select(b => GodotVariantPayload.FromInt(b)));
                case VariantType.PackedInt32Array:
                    return Items(variant.VariantType, variant.AsInt32Array().Select(i => GodotVariantPayload.FromInt(i)));
                case VariantType.PackedInt64Array:
                    return Items(variant.VariantType, variant.AsInt64Array().Select(GodotVariantPayload.FromInt));
                case VariantType.PackedFloat32Array:
                    return Items(variant.VariantType, variant.AsFloat32Array().Select(f => GodotVariantPayload.FromFloat(f)));
                case VariantType.PackedFloat64Array:
                    return Items(variant.VariantType, variant.AsFloat64Array().Select(GodotVariantPayload.FromFloat));
                case VariantType.PackedStringArray:
                    return Items(variant.VariantType, variant.AsStringArray().Select(t => GodotVariantPayload.FromText(VariantType.String, t)));
                case VariantType.PackedVector2Array:
                    return Items(variant.VariantType, variant.AsVector2Array().Select(v => Components(VariantType.Vector2, v.X, v.Y)));
                case VariantType.PackedVector3Array:
                    return Items(variant.VariantType, variant.AsVector3Array().Select(v => Components(VariantType.Vector3, Flatten(v))));
                case VariantType.PackedVector4Array:
                    return Items(variant.VariantType, variant.AsVector4Array().Select(v => Components(VariantType.Vector4, Flatten(v))));
                case VariantType.PackedColorArray:
                    return Items(variant.VariantType, variant.AsColorArray().Select(c => Components(VariantType.Color, c.R, c.G, c.B, c.A)));

                default:
                    throw new NotSupportedException($"Godot Variant.Type '{variant.VariantType}' has no JSON payload form.");
            }
        }

        static GodotVariantPayload Items(VariantType kind, IEnumerable<GodotVariantPayload> items)
            => GodotVariantPayload.FromItems(kind, items.ToArray());

        static GodotVariantPayload Components(VariantType kind, params double[] components)
            => GodotVariantPayload.FromComponents(kind, components);

        static double[] Flatten(Vector3 v) => new double[] { v.X, v.Y, v.Z };

        static double[] Flatten(Vector4 v) => new double[] { v.X, v.Y, v.Z, v.W };

        static double[] FlattenBasis(Basis b) => new double[]
        {
            b.Row0.X, b.Row0.Y, b.Row0.Z,
            b.Row1.X, b.Row1.Y, b.Row1.Z,
            b.Row2.X, b.Row2.Y, b.Row2.Z,
        };

        /// <summary>
        /// Build the wire JSON for a live variant. Two kinds of variant cannot be written back as an
        /// argument — types with no JSON form (Object / Array / Dictionary / Packed*Array / Rid / Callable /
        /// Signal), and any variant holding a non-finite float (JSON has no literal for NaN/±Infinity, and
        /// <c>JsonValue.Create(double.NaN).ToJsonString()</c> throws). Both are reported honestly, as the
        /// declared <c>variantType</c> plus Godot's own string rendering and a note, rather than being
        /// silently emitted as something that would not round-trip — and, critically, rather than turning a
        /// SUCCESSFUL call into an opaque serialization failure.
        /// </summary>
        internal static JsonObject ToWireNode(global::Godot.Variant variant)
        {
            var node = new JsonObject { [VariantTypeKey] = variant.VariantType.ToString() };

            GodotVariantPayload? payload = null;
            string? reason = null;
            try
            {
                payload = ToPayload(variant);
                if (!IsJsonWritable(payload))
                    reason = "it holds a non-finite float (NaN or +/-Infinity), which JSON cannot represent";
                else if (payload.NodeCount > MaxInlineNodes)
                    reason = $"it holds {payload.NodeCount} values, more than the {MaxInlineNodes} inlined in a reply";
            }
            catch (NotSupportedException)
            {
                reason = $"Godot Variant.Type '{variant.VariantType}' has no JSON form";
            }

            if (reason != null)
            {
                node[ValueKey] = variant.ToString();
                node[NoteKey] =
                    $"Rendered as Godot's own string form because {reason}; it cannot be sent back as a " +
                    "Godot.Variant argument. " + GodotVariantPayload.WireFormatHelp;
                return node;
            }

            node[ValueKey] = ToWireValue(payload!);
            return node;
        }

        /// <summary>Largest collection (in nested values) written out in full; bigger ones get a note instead.</summary>
        const int MaxInlineNodes = 4096;

        /// <summary>True when every number in <paramref name="payload"/> can be written as a JSON literal.</summary>
        static bool IsJsonWritable(GodotVariantPayload payload)
        {
            if (payload.Kind == VariantType.Float)
                return !double.IsNaN(payload.FloatValue) && !double.IsInfinity(payload.FloatValue);

            foreach (var component in payload.Components)
            {
                if (double.IsNaN(component) || double.IsInfinity(component))
                    return false;
            }
            return payload.Items.All(IsJsonWritable) && payload.Entries.All(e => IsJsonWritable(e.Key) && IsJsonWritable(e.Value));
        }

        /// <summary>
        /// The wire form of a collection element or dictionary key/value: Bool/Int/String/Nil as bare JSON
        /// (they parse back to the same type), everything else as an envelope — a bare 1.0 would come back
        /// as an Int.
        /// </summary>
        static JsonNode? ToElementWire(GodotVariantPayload payload) => payload.Kind switch
        {
            VariantType.Nil or VariantType.Bool or VariantType.Int or VariantType.String => ToWireValue(payload),
            _ => new JsonObject { [VariantTypeKey] = payload.Kind.ToString(), [ValueKey] = ToWireValue(payload) },
        };

        static JsonNode? ToWireValue(GodotVariantPayload payload)
        {
            switch (payload.Kind)
            {
                case VariantType.Nil:
                    return null;
                case VariantType.Bool:
                    return JsonValue.Create(payload.BoolValue);
                case VariantType.Int:
                    return JsonValue.Create(payload.IntValue);
                case VariantType.Float:
                    return JsonValue.Create(payload.FloatValue);
                case VariantType.String:
                case VariantType.StringName:
                case VariantType.NodePath:
                    return JsonValue.Create(payload.TextValue);
                case VariantType.Object:
                    if (payload.IsNullObject)
                        return null;
                    return payload.ResourcePath != null
                        ? new JsonObject { ["resourcePath"] = payload.ResourcePath, ["instanceId"] = payload.InstanceId }
                        : new JsonObject { ["instanceId"] = payload.InstanceId };
                case VariantType.Array:
                    return new JsonArray(payload.Items.Select(ToElementWire).ToArray());
                case VariantType.Dictionary:
                    if (payload.Entries.All(e => e.Key.Kind == VariantType.String))
                    {
                        var map = new JsonObject();
                        foreach (var entry in payload.Entries)
                            map[entry.Key.TextValue] = ToElementWire(entry.Value);
                        return map;
                    }
                    return new JsonArray(payload.Entries
                        .Select(e => (JsonNode)new JsonObject { ["key"] = ToElementWire(e.Key), ["value"] = ToElementWire(e.Value) })
                        .ToArray());
                case var kind when GodotVariantPayload.PackedElementKind(kind) != null:
                    return new JsonArray(payload.Items.Select(ToWireValue).ToArray());
                default:
                    var array = new JsonArray();
                    foreach (var component in payload.Components)
                        array.Add(JsonValue.Create(component));
                    return array;
            }
        }
    }
}
