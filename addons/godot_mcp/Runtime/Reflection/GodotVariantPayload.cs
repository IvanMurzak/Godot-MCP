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
using System.Globalization;
using System.Linq;
using System.Text.Json;
using VariantType = global::Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// The PURE-MANAGED half of <c>Godot.Variant</c> (de)serialization (issue #292): the wire JSON is parsed
    /// into this description, and only then does <see cref="Godot_Variant_ReflectionConverter"/> materialize
    /// a real <see cref="global::Godot.Variant"/> from it.
    ///
    /// <para>
    /// <b>Why the split.</b> Constructing a <c>Godot.Variant</c> that holds anything but <c>Nil</c> calls
    /// <c>godotsharp_*</c> P/Invoke (e.g. <c>Variant.CreateFrom("x")</c> →
    /// <c>godotsharp_string_new_with_utf16_chars</c>), which faults with
    /// <c>AccessViolationException</c> and kills the whole host process when no Godot native library is
    /// loaded — i.e. in the plain xUnit CI host. Reading <c>default(Variant).VariantType</c> and the
    /// <see cref="VariantType"/> enum itself are pure managed operations. So ALL parsing, validation and
    /// error messaging lives here and is fully unit-tested, while the (untestable) native materialization is
    /// one small switch in the converter. Same shape as
    /// <see cref="Godot_Resource_ReflectionConverter{T}"/>'s pure-ref / injected-resolver split.
    /// </para>
    ///
    /// <para>
    /// <b>The bug this closes.</b> With no converter registered for <c>Godot.Variant</c>, ReflectorNet's
    /// generic fallback tried to read the wire object as a nested <c>SerializedMember</c>, threw a
    /// <c>JsonException</c> on the unknown key, swallowed it, and returned <c>default(Variant)</c> — reported
    /// as SUCCESS. So <c>reflection-method-call</c> on e.g. <c>ProjectSettings.SetSetting</c> wrote
    /// <c>Nil</c> and told the agent it worked. Everything here either produces a real value or throws
    /// <see cref="ArgumentException"/> with an actionable message; it never silently degrades to
    /// <c>Nil</c>.
    /// </para>
    /// </summary>
    public sealed class GodotVariantPayload
    {
        /// <summary>Wire key (and accepted aliases) naming the Godot variant type.</summary>
        public static readonly string[] TypeKeys = { "variantType", "type" };

        /// <summary>
        /// Wire key (and accepted aliases) carrying the value. <c>obj</c> is accepted because
        /// <c>Godot.Variant</c>'s own read-only properties are named <c>VariantType</c> / <c>Obj</c>, so an
        /// agent inspecting the type naturally emits that shape (exactly what issue #292 reported).
        /// </summary>
        public static readonly string[] ValueKeys = { "value", "obj" };

        /// <summary>Human-readable description of the accepted wire shapes — reused in tool descriptions AND in every parse error.</summary>
        public const string WireFormatHelp =
            "A Godot.Variant is written either as a bare JSON scalar (null => Nil, true/false => Bool, " +
            "integral number => Int, fractional number => Float, string => String) or as " +
            "{\"variantType\":\"<Type>\",\"value\":<json>} where <Type> is a Godot Variant.Type name. " +
            "Composite types take a flat array of numbers in Godot's own constructor order (or, for " +
            "Vector2/Vector2I/Vector3/Vector3I/Vector4/Vector4I/Quaternion/Plane/Color, an object with the " +
            "named components): Vector2/Vector2I [x,y]; Vector3/Vector3I [x,y,z]; Vector4/Vector4I and " +
            "Quaternion [x,y,z,w]; Color [r,g,b(,a)]; Plane [x,y,z,d] (the normal's xyz, then D); " +
            "Rect2/Rect2I [x,y,width,height]; " +
            "Aabb [x,y,z,width,height,depth]; Transform2D [xx,xy,yx,yy,ox,oy]; " +
            "Basis [xx,yx,zx,xy,yy,zy,xz,yz,zz]; Transform3D = Basis then [ox,oy,oz]; " +
            "Projection = four Vector4 columns flattened (16). String/StringName/NodePath take a string. " +
            "Note: a parameter DECLARED as a concrete type (e.g. 'Godot.Vector3') keeps its own " +
            "member-wise shape ({\"x\":1,\"y\":2,\"z\":3}); only a 'Godot.Variant' parameter uses this " +
            "envelope — and its named-component object form matches that member-wise shape, so the same " +
            "value can be written either way.";

        GodotVariantPayload(VariantType kind)
        {
            Kind = kind;
            TextValue = string.Empty;
            Components = Array.Empty<double>();
        }

        /// <summary>The Godot variant type this payload materializes into.</summary>
        public VariantType Kind { get; }

        /// <summary>Set for <see cref="VariantType.Bool"/>.</summary>
        public bool BoolValue { get; init; }

        /// <summary>Set for <see cref="VariantType.Int"/>.</summary>
        public long IntValue { get; init; }

        /// <summary>Set for <see cref="VariantType.Float"/>.</summary>
        public double FloatValue { get; init; }

        /// <summary>Set for <see cref="VariantType.String"/>, <see cref="VariantType.StringName"/> and <see cref="VariantType.NodePath"/>.</summary>
        public string TextValue { get; init; }

        /// <summary>Flat numeric components, in Godot constructor order, for the composite value types.</summary>
        public IReadOnlyList<double> Components { get; init; }

        /// <summary>Convenience accessor for a component as <see cref="float"/>.</summary>
        public float Component(int index) => (float)Components[index];

        /// <summary>
        /// Convenience accessor for a component as <see cref="int"/> (the integer vector/rect types). The
        /// range is validated at parse time (see <see cref="ComponentLayout.Integral"/>), so this cannot
        /// overflow on a parsed payload.
        /// </summary>
        public int ComponentInt(int index) => (int)Math.Round(Components[index], MidpointRounding.AwayFromZero);

        public static GodotVariantPayload Nil() => new GodotVariantPayload(VariantType.Nil);

        public static GodotVariantPayload FromBool(bool value) => new GodotVariantPayload(VariantType.Bool) { BoolValue = value };

        public static GodotVariantPayload FromInt(long value) => new GodotVariantPayload(VariantType.Int) { IntValue = value };

        public static GodotVariantPayload FromFloat(double value) => new GodotVariantPayload(VariantType.Float) { FloatValue = value };

        public static GodotVariantPayload FromText(VariantType kind, string value)
            => new GodotVariantPayload(kind) { TextValue = value };

        public static GodotVariantPayload FromComponents(VariantType kind, IReadOnlyList<double> components)
            => new GodotVariantPayload(kind) { Components = components };

        public override string ToString() => Kind switch
        {
            VariantType.Nil => "Variant(Nil)",
            VariantType.Bool => $"Variant(Bool {BoolValue})",
            VariantType.Int => $"Variant(Int {IntValue})",
            VariantType.Float => $"Variant(Float {FloatValue.ToString(CultureInfo.InvariantCulture)})",
            VariantType.String or VariantType.StringName or VariantType.NodePath => $"Variant({Kind} '{TextValue}')",
            _ => $"Variant({Kind} [{string.Join(", ", Components.Select(c => c.ToString(CultureInfo.InvariantCulture)))}])",
        };

        // --- Component layout table -------------------------------------------------------------------

        /// <summary>
        /// Composite value types, their component COUNT (Godot constructor order) and — where the components
        /// have canonical short names — the accepted object-form keys. A type absent from this table is not
        /// buildable from plain JSON and is rejected with an explicit message rather than silently defaulted.
        /// </summary>
        static readonly IReadOnlyDictionary<VariantType, ComponentLayout> Layouts = new Dictionary<VariantType, ComponentLayout>
        {
            [VariantType.Vector2] = new ComponentLayout(2, "x", "y"),
            [VariantType.Vector2I] = new ComponentLayout(2, "x", "y") { Integral = true },
            [VariantType.Vector3] = new ComponentLayout(3, "x", "y", "z"),
            [VariantType.Vector3I] = new ComponentLayout(3, "x", "y", "z") { Integral = true },
            [VariantType.Vector4] = new ComponentLayout(4, "x", "y", "z", "w"),
            [VariantType.Vector4I] = new ComponentLayout(4, "x", "y", "z", "w") { Integral = true },
            [VariantType.Quaternion] = new ComponentLayout(4, "x", "y", "z", "w"),
            // Named after Godot.Plane's own members (Normal.X/Y/Z + D), not its constructor's (a,b,c,d) —
            // an agent reading the C# surface sees X/Y/Z/D.
            [VariantType.Plane] = new ComponentLayout(4, "x", "y", "z", "d"),
            // Color accepts 3 (opaque) or 4 components; the optional alpha defaults to 1 in Fill below.
            [VariantType.Color] = new ComponentLayout(4, "r", "g", "b", "a") { MinCount = 3, DefaultTail = 1.0 },
            [VariantType.Rect2] = new ComponentLayout(4),
            [VariantType.Rect2I] = new ComponentLayout(4) { Integral = true },
            [VariantType.Aabb] = new ComponentLayout(6),
            [VariantType.Transform2D] = new ComponentLayout(6),
            [VariantType.Basis] = new ComponentLayout(9),
            [VariantType.Transform3D] = new ComponentLayout(12),
            [VariantType.Projection] = new ComponentLayout(16),
        };

        sealed class ComponentLayout
        {
            public ComponentLayout(int count, params string[] names)
            {
                Count = count;
                Names = names;
                MinCount = count;
            }

            public int Count { get; }
            public string[] Names { get; }

            /// <summary>
            /// Fewest components the caller must supply; anything short of <see cref="Count"/> is padded
            /// with <see cref="DefaultTail"/>. Only <c>Color</c> uses this today (alpha is optional and
            /// defaults to opaque); every other layout requires all of them.
            /// </summary>
            public int MinCount { get; init; }

            /// <summary>Value used to pad the components between <see cref="MinCount"/> and <see cref="Count"/>.</summary>
            public double DefaultTail { get; init; }

            /// <summary>The <c>*I</c> types, whose components must fit in an <see cref="int"/>.</summary>
            public bool Integral { get; init; }

            public bool HasNames => Names.Length == Count;
        }

        // Fixed at type load, and consulted on EVERY object-form parse — build it once rather than
        // re-allocating an array plus a Concat iterator per call. Declared after Layouts so the static
        // initializers run in the right order.
        static readonly VariantType[] SupportedTypeList = new[]
        {
            VariantType.Nil, VariantType.Bool, VariantType.Int, VariantType.Float,
            VariantType.String, VariantType.StringName, VariantType.NodePath,
        }.Concat(Layouts.Keys).ToArray();

        /// <summary>Every <see cref="VariantType"/> this payload can represent, for error messages.</summary>
        public static IEnumerable<VariantType> SupportedTypes => SupportedTypeList;

        // --- Parsing ----------------------------------------------------------------------------------

        /// <summary>
        /// Parse the wire JSON for a <c>Godot.Variant</c>. An absent element (or JSON <c>null</c>) is
        /// <see cref="VariantType.Nil"/> — the only way to legitimately obtain Nil. Every other input either
        /// yields a fully-specified payload or throws <see cref="ArgumentException"/>; there is deliberately
        /// no "give up and return Nil" branch, because a silent Nil is the defect this closes (#292).
        /// </summary>
        public static GodotVariantPayload Parse(JsonElement? element)
        {
            if (element == null || element.Value.ValueKind == JsonValueKind.Null || element.Value.ValueKind == JsonValueKind.Undefined)
                return Nil();

            var json = element.Value;
            return json.ValueKind switch
            {
                JsonValueKind.True => FromBool(true),
                JsonValueKind.False => FromBool(false),
                JsonValueKind.String => FromText(VariantType.String, json.GetString() ?? string.Empty),
                JsonValueKind.Number => ParseBareNumber(json),
                JsonValueKind.Object => ParseObject(json),
                JsonValueKind.Array => throw Bad(
                    "a bare JSON array is ambiguous for a Godot.Variant — declare the type explicitly, e.g. " +
                    "{\"variantType\":\"Vector3\",\"value\":[1,2,3]}"),
                _ => throw Bad($"unsupported JSON value kind '{json.ValueKind}'"),
            };
        }

        static GodotVariantPayload ParseBareNumber(JsonElement json)
        {
            // An integral literal is an Int; anything with a fraction/exponent (or beyond long) is a Float.
            // Godot itself distinguishes the two, and picking the wrong one is a real behaviour change for
            // e.g. ProjectSettings entries — so key off the literal's own text, not off a lossy conversion.
            var raw = json.GetRawText();
            if (raw.IndexOf('.') < 0 && raw.IndexOf('e') < 0 && raw.IndexOf('E') < 0 && json.TryGetInt64(out var i))
                return FromInt(i);

            // GetDouble() saturates an out-of-range literal (e.g. 1e999) to ±Infinity instead of throwing,
            // and a non-finite Float variant cannot be written back out as JSON — reject it here.
            return FromFloat(RequireFinite(VariantType.Float, json.GetDouble(), json));
        }

        static GodotVariantPayload ParseObject(JsonElement json)
        {
            if (!TryFindProperty(json, TypeKeys, out var typeElement, out var matchedTypeKey))
                throw Bad(
                    $"a JSON object must declare the variant type under '{TypeKeys[0]}' (alias '{TypeKeys[1]}'); " +
                    $"got keys [{DescribeKeys(json)}]");

            if (typeElement.ValueKind != JsonValueKind.String)
                throw Bad($"'{matchedTypeKey}' must be a string naming a Godot Variant.Type; got {typeElement.ValueKind}");

            var typeName = typeElement.GetString() ?? string.Empty;
            var kind = ParseVariantType(typeName);

            TryFindProperty(json, ValueKeys, out var valueElement, out _);
            var hasValue = valueElement.ValueKind != JsonValueKind.Undefined && valueElement.ValueKind != JsonValueKind.Null;

            if (kind == VariantType.Nil)
            {
                if (hasValue)
                    throw Bad($"'{TypeKeys[0]}' is 'Nil' but a non-null value was supplied — Nil carries no value");
                return Nil();
            }

            if (!hasValue)
                throw Bad(
                    $"'{TypeKeys[0]}' is '{kind}' but no value was supplied under '{ValueKeys[0]}' " +
                    $"(alias '{ValueKeys[1]}')");

            return Build(kind, valueElement);
        }

        static VariantType ParseVariantType(string typeName)
        {
            var trimmed = typeName.Trim();
            // Tolerate the fully-qualified spellings an agent may copy out of the C# API surface.
            const string prefix = "Variant.Type.";
            if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                trimmed = trimmed.Substring(prefix.Length);

            // Enum.TryParse also accepts a comma-separated flag LIST ("Bool,Int" parses as 3 == Float) and a
            // bare ordinal ("3"), either of which would silently yield a DIFFERENT valid type — the exact
            // class of failure this file exists to remove. Only a single symbolic name is a type name.
            if (trimmed.IndexOf(',') >= 0 || long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)
                || !Enum.TryParse<VariantType>(trimmed, ignoreCase: true, out var kind)
                || !Enum.IsDefined(kind))
                throw Bad($"'{typeName}' is not a Godot Variant.Type name. Supported: {DescribeSupported()}");

            if (Array.IndexOf(SupportedTypeList, kind) < 0)
                throw Bad(
                    $"Godot Variant.Type '{kind}' cannot be built from JSON by this tool. Supported: " +
                    $"{DescribeSupported()}. For an object/resource/node argument, declare the parameter with " +
                    "its concrete type (e.g. 'Godot.Resource' or 'Godot.Node') instead of 'Godot.Variant'.");

            return kind;
        }

        static GodotVariantPayload Build(VariantType kind, JsonElement value)
        {
            switch (kind)
            {
                case VariantType.Bool:
                    return value.ValueKind switch
                    {
                        JsonValueKind.True => FromBool(true),
                        JsonValueKind.False => FromBool(false),
                        _ => throw Bad($"'{kind}' expects a JSON boolean; got {Describe(value)}"),
                    };

                case VariantType.Int:
                    if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var i))
                        return FromInt(i);
                    if (value.ValueKind == JsonValueKind.String &&
                        long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedInt))
                        return FromInt(parsedInt);
                    throw Bad($"'{kind}' expects a 64-bit integer; got {Describe(value)}");

                case VariantType.Float:
                    if (value.ValueKind == JsonValueKind.Number)
                        return FromFloat(RequireFinite(kind, value.GetDouble(), value));
                    if (value.ValueKind == JsonValueKind.String &&
                        double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFloat))
                        return FromFloat(RequireFinite(kind, parsedFloat, value));
                    throw Bad($"'{kind}' expects a number; got {Describe(value)}");

                case VariantType.String:
                case VariantType.StringName:
                case VariantType.NodePath:
                    if (value.ValueKind != JsonValueKind.String)
                        throw Bad($"'{kind}' expects a JSON string; got {Describe(value)}");
                    return FromText(kind, value.GetString() ?? string.Empty);

                default:
                    return FromComponents(kind, ReadComponents(kind, value));
            }
        }

        static double[] ReadComponents(VariantType kind, JsonElement value)
        {
            if (!Layouts.TryGetValue(kind, out var layout))
                // Unreachable: ParseVariantType already rejects anything outside SupportedTypes.
                throw Bad($"'{kind}' has no known component layout");

            if (value.ValueKind == JsonValueKind.Array)
            {
                var numbers = new List<double>(layout.Count);
                var index = 0;
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Number)
                        throw Bad($"'{kind}' component [{index}] must be a number; got {Describe(item)}");
                    numbers.Add(item.GetDouble());
                    index++;
                }
                return Fill(kind, layout, numbers);
            }

            if (value.ValueKind == JsonValueKind.Object)
            {
                if (!layout.HasNames)
                    throw Bad(
                        $"'{kind}' must be given as a flat array of {layout.Count} numbers " +
                        "(Godot constructor order); the named-component object form is not defined for it");

                var numbers = new List<double>(layout.Count);
                foreach (var name in layout.Names)
                {
                    if (!TryFindProperty(value, name, out var component, out _))
                    {
                        // Name the ABSENT component. Falling through to Fill's count check instead would
                        // report the number collected so far, which under-counts whatever the caller
                        // actually sent past the gap ({"x":1,"z":3} would be reported as "got 1").
                        if (numbers.Count >= layout.MinCount)
                            break; // an optional tail component (Color's alpha) — Fill supplies the default
                        throw Bad(
                            $"'{kind}' is missing component '{name}'; it needs [{string.Join(", ", layout.Names)}] " +
                            $"(or a flat array of {layout.Count} numbers), got keys [{DescribeKeys(value)}]");
                    }
                    if (component.ValueKind != JsonValueKind.Number)
                        throw Bad($"'{kind}' component '{name}' must be a number; got {Describe(component)}");
                    numbers.Add(component.GetDouble());
                }
                return Fill(kind, layout, numbers);
            }

            throw Bad(
                $"'{kind}' expects an array of {layout.Count} numbers" +
                (layout.HasNames ? $" or an object with [{string.Join(", ", layout.Names)}]" : string.Empty) +
                $"; got {Describe(value)}");
        }

        static double[] Fill(VariantType kind, ComponentLayout layout, List<double> numbers)
        {
            if (numbers.Count < layout.MinCount || numbers.Count > layout.Count)
            {
                var expected = layout.MinCount == layout.Count
                    ? layout.Count.ToString(CultureInfo.InvariantCulture)
                    : $"{layout.MinCount}-{layout.Count}";
                throw Bad($"'{kind}' expects {expected} components; got {numbers.Count}");
            }

            while (numbers.Count < layout.Count)
                numbers.Add(layout.DefaultTail);

            // Validate the narrowing HERE rather than at materialization time, so an out-of-range component
            // is an ArgumentException carrying the wire-format help — not an OverflowException from a cast
            // (the integer types) and not a silent ±Infinity (the float types, whose components are
            // 32-bit floats in Godot). A silently saturated component is exactly the class of failure this
            // whole change removes.
            if (layout.Integral)
            {
                for (var i = 0; i < numbers.Count; i++)
                {
                    var rounded = Math.Round(numbers[i], MidpointRounding.AwayFromZero);
                    if (double.IsNaN(rounded) || rounded < int.MinValue || rounded > int.MaxValue)
                        throw Bad($"'{kind}' component [{i}] must fit in a 32-bit integer; got {Number(numbers[i])}");
                }
            }
            else
            {
                for (var i = 0; i < numbers.Count; i++)
                {
                    if (!IsFiniteAsSingle(numbers[i]))
                        throw Bad($"'{kind}' component [{i}] must be a finite 32-bit float; got {Number(numbers[i])}");
                }
            }

            return numbers.ToArray();
        }

        // --- Helpers ----------------------------------------------------------------------------------

        /// <summary>Case-insensitive property lookup — JSON keys from an LLM are not reliably cased.</summary>
        static bool TryFindProperty(JsonElement json, string name, out JsonElement value, out string matched)
        {
            foreach (var property in json.EnumerateObject())
            {
                if (!string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                    continue;
                value = property.Value;
                matched = property.Name;
                return true;
            }

            value = default;
            matched = string.Empty;
            return false;
        }

        /// <summary>
        /// Try each accepted alias IN PRIORITY ORDER, so 'variantType' beats 'type' and 'value' beats 'obj'
        /// when a payload carries both. Do not invert the loops — that would let key order decide instead.
        /// </summary>
        static bool TryFindProperty(JsonElement json, IReadOnlyList<string> names, out JsonElement value, out string matched)
        {
            foreach (var name in names)
            {
                if (TryFindProperty(json, name, out value, out matched))
                    return true;
            }

            value = default;
            matched = string.Empty;
            return false;
        }

        /// <summary>
        /// True when <paramref name="value"/> survives the narrowing to a 32-bit float — the component type
        /// of every Godot composite value type. Deliberately STRICTER than a plain
        /// <c>double.IsFinite</c>: a finite double such as 1e300 becomes +Infinity as a float, and silently
        /// storing that is the degradation this whole file exists to prevent. (Contrast
        /// <c>Godot_Variant_ReflectionConverter.IsJsonWritable</c>, which asks the double-precision question
        /// about a variant that already exists.)
        /// </summary>
        static bool IsFiniteAsSingle(double value) => !double.IsNaN(value) && !float.IsInfinity((float)value);

        /// <summary>Godot's scalar Float variant is a double, so this is the double-precision check.</summary>
        static double RequireFinite(VariantType kind, double value, JsonElement source)
            => double.IsNaN(value) || double.IsInfinity(value)
                ? throw Bad($"'{kind}' expects a finite number; got {Describe(source)}")
                : value;

        static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);

        static string DescribeKeys(JsonElement json)
            => string.Join(", ", json.EnumerateObject().Select(p => $"'{p.Name}'"));

        static string Describe(JsonElement value)
            => value.ValueKind == JsonValueKind.Undefined ? "nothing" : $"{value.ValueKind} ({value.GetRawText()})";

        // Constant for the life of the process, and rebuilding it means ~23 reflection-backed Enum.ToString
        // calls — do it once.
        static readonly string SupportedTypeNames = string.Join(", ", SupportedTypeList.Select(t => t.ToString()));

        static string DescribeSupported() => SupportedTypeNames;

        static ArgumentException Bad(string reason)
            => new ArgumentException($"Cannot read a Godot.Variant: {reason}. {WireFormatHelp}");
    }
}
