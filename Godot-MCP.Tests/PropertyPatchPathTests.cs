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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Xunit;
using VariantType = Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for the pure-managed rules behind <c>node-modify</c> / <c>resource-modify</c>
    /// (<see cref="PropertyPatchPath"/>) and the pinned ReflectorNet's write-back contract. The native half — Godot's
    /// <c>Set</c>/<c>Get</c> read-back — is covered by <c>scripts/smoke_property_patches.py</c> against a
    /// running editor.
    /// </summary>
    public class PropertyPatchPathTests
    {
        class Inner
        {
            public float Ratio = 1f;
        }

        class Sample
        {
            public float LightEnergy { get; set; } = 1f;
            public int Count { get; set; }
            public string Title { get; set; } = string.Empty;
            public Inner Child { get; set; } = new Inner();
            public object Boxed { get; set; } = new Inner();
            public List<Inner> Items { get; set; } = new List<Inner> { new Inner() };
            public Dictionary<string, int> Map { get; set; } = new Dictionary<string, int>();
            public global::Godot.Variant Any { get; set; }
            public float this[int index] => index;
#pragma warning disable CS0169 // Only reached by reflection.
            float _omni_range;
#pragma warning restore CS0169
        }

        class Derived : Sample
        {
            public new int Count { get; set; }
        }

        static readonly IReadOnlyCollection<string> GodotNames = new HashSet<string> { "light_energy", "omni_range", "count" };

        // ---- SingleMemberSegment --------------------------------------------------------------------

        [Theory]
        [InlineData("light_energy", "light_energy")]
        [InlineData("#/LightEnergy", "LightEnergy")]
        [InlineData("Child/Ratio", null)]
        [InlineData("[0]", null)]
        [InlineData("[]", null)]
        [InlineData("[a/b]", null)]
        [InlineData("", null)]
        public void SingleMemberSegment(string path, string? expected)
        {
            Assert.Equal(expected, PropertyPatchPath.SingleMemberSegment(path));
        }

        // ---- FindMember / Resolve -------------------------------------------------------------------

        [Theory]
        [InlineData("LightEnergy", "LightEnergy")]
        [InlineData("light_energy", "LightEnergy")]
        [InlineData("lightenergy", "LightEnergy")]
        [InlineData("missing", null)]
        public void FindMember_MatchesExactThenSnakeCase(string name, string? expected)
        {
            Assert.Equal(expected, PropertyPatchPath.FindMember(typeof(Sample), name)?.Name);
        }

        [Fact]
        public void FindMember_SkipsIndexers()
        {
            Assert.Null(PropertyPatchPath.FindMember(typeof(Sample), "Item"));
        }

        [Fact]
        public void FindMember_NewHiddenProperty_PrefersMostDerived_AndDoesNotThrow()
        {
            var member = PropertyPatchPath.FindMember(typeof(Derived), "Count");
            Assert.Equal(typeof(Derived), member?.DeclaringType);
        }

        [Fact]
        public void Resolve_TopLevel_CanonicalizesAndReportsMember()
        {
            var r = PropertyPatchPath.Resolve(new Sample(), "light_energy");
            Assert.Equal("LightEnergy", r.Path);
            Assert.Equal(typeof(float), r.MemberType);
        }

        [Fact]
        public void Resolve_Nested_UsesRuntimeTypeOfDeclaredBaseMember()
        {
            // 'Boxed' is declared object; its runtime Inner still resolves 'ratio'.
            var r = PropertyPatchPath.Resolve(new Sample(), "boxed/ratio");
            Assert.Equal("Boxed/Ratio", r.Path);
            Assert.Equal(typeof(float), r.MemberType);
        }

        [Fact]
        public void Resolve_Brackets_UseElementAndValueTypes()
        {
            Assert.Equal(typeof(float), PropertyPatchPath.Resolve(new Sample(), "items/[0]/ratio").MemberType);
            Assert.Equal("Items/[0]/Ratio", PropertyPatchPath.Resolve(new Sample(), "items/[0]/ratio").Path);
            Assert.Equal(typeof(int), PropertyPatchPath.Resolve(new Sample(), "Map/[k]").MemberType);
        }

        [Theory]
        // Same segmentation as ReflectorNet 5.4.3's ParsePath: '/' inside brackets belongs to the key, escapes stay.
        [InlineData("map/[a/b]", "Map/[a/b]", "a/b")]
        [InlineData("map/[a\\]b]", "Map/[a\\]b]", "a]b")]
        [InlineData("map/[]", "Map/[]", "")]
        public void Resolve_BracketKeys_FollowReflectorNetParsing(string path, string expected, string key)
        {
            var r = PropertyPatchPath.Resolve(new Sample(), path);
            Assert.Equal(expected, r.Path);
            Assert.Equal(typeof(int), r.MemberType);

            // The resolved path must address the same key in ReflectorNet's own parser.
            object? target = new Sample();
            Assert.True(new Reflector().TryModifyAt(ref target, r.Path, Number("System.Int32", 7), logs: new Logs()));
            Assert.Equal(7, ((Sample)target!).Map[key]);
        }

        [Fact]
        public void Resolve_UnknownSegment_KeepsRestAndHasNoType()
        {
            var r = PropertyPatchPath.Resolve(new Sample(), "child/nope/deeper");
            Assert.Equal("Child/nope/deeper", r.Path);
            Assert.Null(r.MemberType);
        }

        // ---- GodotPropertyFor -----------------------------------------------------------------------

        [Theory]
        // A Godot name that is not a C# member goes native.
        [InlineData("light_energy", false, "light_energy")]
        [InlineData("omni_range", false, "omni_range")]
        // A C# name stays on the CLR route...
        [InlineData("LightEnergy", false, null)]
        // ...unless it carries a Variant envelope, which the CLR route cannot assign to a float.
        [InlineData("LightEnergy", true, "light_energy")]
        // An exact C# member wins over a Godot property of the same spelling.
        [InlineData("Count", false, null)]
        // Not a Godot property at all.
        [InlineData("Title", true, null)]
        [InlineData("unknown", false, null)]
        public void GodotPropertyFor_ChoosesRoute(string segment, bool valueIsVariant, string? expected)
        {
            Assert.Equal(expected, PropertyPatchPath.GodotPropertyFor(typeof(Sample), segment, valueIsVariant, GodotNames));
        }

        [Theory]
        // An exact Godot name, or a public C# property's snake_case spelling, is read back...
        [InlineData("light_energy", "light_energy")]
        [InlineData("LightEnergy", "light_energy")]
        [InlineData("Count", "count")]
        // ...but a private field never borrows an unrelated Godot property's read-back.
        [InlineData("_omni_range", null)]
        [InlineData("Title", null)]
        public void GodotBackingProperty(string clrName, string? expected)
        {
            Assert.Equal(expected, PropertyPatchPath.GodotBackingProperty(typeof(Sample), clrName, GodotNames));
        }

        [Theory]
        [InlineData("light_energy", "light_energy")]
        [InlineData("LightEnergy", "light_energy")]
        [InlineData("OMNI_RANGE", "omni_range")]
        [InlineData("Title", null)]
        public void MatchGodotProperty(string name, string? expected)
        {
            Assert.Equal(expected, PropertyPatchPath.MatchGodotProperty(name, GodotNames));
        }

        [Theory]
        [InlineData("{\"variantType\":\"Float\",\"value\":1}", true)]
        [InlineData("{\"type\":\"Color\",\"value\":[1,0,0]}", true)]
        [InlineData("{\"VariantType\":\"Int\",\"value\":1}", true)]
        [InlineData("{\"resourcePath\":\"res://a.tres\"}", false)]
        [InlineData("0.5", false)]
        public void GodotVariantPayload_IsEnvelope(string json, bool expected)
        {
            Assert.Equal(expected, GodotVariantPayload.IsEnvelope(JsonDocument.Parse(json).RootElement));
        }

        [Fact]
        public void GodotPropertyFor_VariantIntoVariantMember_StaysClr()
        {
            var names = new HashSet<string> { "any" };
            Assert.Null(PropertyPatchPath.GodotPropertyFor(typeof(Sample), "Any", valueIsVariant: true, names));
        }

        // ---- CanonicalizeMergePatch -----------------------------------------------------------------

        class Holder
        {
            public global::Godot.Vector3 Position { get; set; }
            public Inner Child { get; set; } = new Inner();
            public List<Inner> Items { get; set; } = new List<Inner> { new Inner() };
            public global::Godot.Variant Any { get; set; }
            public Dictionary<string, Inner> Map { get; set; } = new Dictionary<string, Inner> { ["count"] = new Inner() };
            public object Boxed { get; set; } = new Inner();
        }

        class Replacement
        {
            public float ratio = 0;
        }

        static string Canonical(object target, string json)
        {
            var patch = (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(json)!;
            PropertyPatchPath.CanonicalizeMergePatch(GodotReflectorFactory.CreateDefaultReflector(), target, target.GetType(), patch);
            return patch.ToJsonString();
        }

        [Theory]
        [InlineData("{\"position\":{\"x\":1}}", "{\"Position\":{\"X\":1}}")]
        [InlineData("{\"child\":{\"ratio\":0.5}}", "{\"Child\":{\"Ratio\":0.5}}")]
        [InlineData("{\"items\":{\"[0]\":{\"ratio\":2}}}", "{\"Items\":{\"[0]\":{\"Ratio\":2}}}")]
        [InlineData("{\"unknown_key\":1}", "{\"unknown_key\":1}")]
        [InlineData("{\"$type\":\"X\",\"child\":{}}", "{\"$type\":\"X\",\"Child\":{}}")]
        // A Variant member's object is an envelope, never a merge: its keys stay as sent.
        [InlineData("{\"any\":{\"variant_type\":\"Int\"}}", "{\"Any\":{\"variant_type\":\"Int\"}}")]
        // Only a bracket addresses a collection's element; a bare key is never renamed to a collection member.
        [InlineData("{\"map\":{\"count\":{}}}", "{\"Map\":{\"count\":{}}}")]
        [InlineData("{\"map\":{\"[count]\":{\"ratio\":3}}}", "{\"Map\":{\"[count]\":{\"Ratio\":3}}}")]
        // The runtime value's type is followed, and two sent keys are never folded into one.
        [InlineData("{\"boxed\":{\"ratio\":4}}", "{\"Boxed\":{\"Ratio\":4}}")]
        [InlineData("{\"child\":{\"ratio\":1},\"CHILD\":{\"ratio\":2}}", "{\"Child\":{\"Ratio\":1},\"CHILD\":{\"Ratio\":2}}")]
        public void CanonicalizeMergePatch_RewritesKeysToCSharpMembers(string json, string expected)
        {
            Assert.Equal(expected, Canonical(new Holder(), json));
        }

        [Fact]
        public void CanonicalizeMergePatch_NestedTypeReplacement_ResolvesAgainstTheNewType()
        {
            var typeId = typeof(Replacement).GetTypeId();
            var json = "{\"boxed\":{\"$type\":\"" + typeId + "\",\"ratio\":1}}";
            Assert.Equal("{\"Boxed\":{\"$type\":\"" + typeId + "\",\"ratio\":1}}", Canonical(new Holder(), json));
        }

        [Fact]
        public void IsVariantTypeName()
        {
            Assert.True(PropertyPatchPath.IsVariantTypeName("Godot.Variant"));
            Assert.False(PropertyPatchPath.IsVariantTypeName("System.Single"));
            Assert.False(PropertyPatchPath.IsVariantTypeName(null));
        }

        // ---- IsAssignable ---------------------------------------------------------------------------

        [Theory]
        [InlineData(VariantType.Float, VariantType.Float, true)]
        [InlineData(VariantType.Int, VariantType.Float, true)]
        [InlineData(VariantType.Bool, VariantType.Int, true)]
        [InlineData(VariantType.String, VariantType.StringName, true)]
        [InlineData(VariantType.Vector3I, VariantType.Vector3, true)]
        [InlineData(VariantType.Array, VariantType.PackedVector3Array, true)]
        [InlineData(VariantType.Nil, VariantType.Object, true)]
        [InlineData(VariantType.Color, VariantType.Nil, true)]
        [InlineData(VariantType.String, VariantType.Float, false)]
        [InlineData(VariantType.Array, VariantType.Float, false)]
        [InlineData(VariantType.Nil, VariantType.Float, false)]
        [InlineData(VariantType.Vector2, VariantType.Vector3, false)]
        [InlineData(VariantType.Color, VariantType.Vector4, false)]
        public void IsAssignable(VariantType value, VariantType property, bool expected)
        {
            Assert.Equal(expected, PropertyPatchPath.IsAssignable(value, property));
        }

        // ---- Pinned ReflectorNet write-back contract (5.4.3+) -----------------------------------------

        class FloatHolder
        {
            public float Value { get; set; }

            public int Count { get; set; }

            public VariantType Kind { get; set; }

            public int Guarded
            {
                get => 0;
                set => throw new System.InvalidOperationException("guarded");
            }
        }

        static SerializedMember Number(string typeName, double value)
            => new SerializedMember { typeName = typeName, valueJsonElement = JsonDocument.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement };

        [Theory]
        [InlineData("Value", "System.Double", 0.5, 0.5f)]
        [InlineData("Count", "System.Int64", 3, 3)]
        [InlineData("Kind", "System.Int32", 3, VariantType.Float)]
        public void TryModifyAt_MismatchedNumericType_LandsAsTheDeclaredType(string member, string typeName, double sent, object expected)
        {
            // 5.4.2 parsed the value as the sent 'System.Double', the float setter threw, and the failure was hidden
            // (#370). The patcher relies on 5.4.3+ reading a numeric typeName as the member's declared numeric or
            // enum type instead.
            object? target = new FloatHolder();
            var logs = new Logs();

            Assert.True(new Reflector().TryModifyAt(ref target, member, Number(typeName, sent), logs: logs));
            Assert.Equal(expected, typeof(FloatHolder).GetProperty(member)!.GetValue(target));
            Assert.Empty(ReflectionArgumentGuard.Failures(logs));
        }

        [Fact]
        public void TryModifyAt_ThrowingSetter_FailsAndLogsTheReason()
        {
            // The patcher reports a write as failed only through TryModifyAt's result and Logs (no ILogger capture).
            object? target = new FloatHolder();
            var logs = new Logs();

            Assert.False(new Reflector().TryModifyAt(ref target, "Guarded", Number("System.Int32", 2), logs: logs));
            Assert.Contains(ReflectionArgumentGuard.Failures(logs), m => m.Contains("guarded"));
        }

        [Fact]
        public void TryPatch_ThrowingSetter_FailsAndLogsTheReason()
        {
            // The jsonPatch route relies on the same contract from TryPatch.
            object? target = new FloatHolder();
            var logs = new Logs();

            Assert.False(new Reflector().TryPatch(ref target, "{\"Guarded\":2}", logs: logs));
            Assert.Contains(ReflectionArgumentGuard.Failures(logs), m => m.Contains("guarded"));
        }
    }
}
