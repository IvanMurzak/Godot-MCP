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
    /// (<see cref="PropertyPatchPath"/>, <see cref="ErrorCapturingLogger"/>). The native half — Godot's
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

        // ---- AlignPrimitiveTypeName -----------------------------------------------------------------

        [Theory]
        [InlineData("System.Double", typeof(float), true, "System.Single")]
        [InlineData("System.Int64", typeof(int), true, "System.Int32")]
        [InlineData("System.Single", typeof(float), false, "System.Single")]
        [InlineData("System.String", typeof(float), false, "System.String")]
        [InlineData("Godot.Variant", typeof(float), false, "Godot.Variant")]
        [InlineData("System.Double", typeof(string), false, "System.Double")]
        [InlineData("System.Int32", typeof(VariantType), true, "Godot.Variant-Type")]
        public void AlignPrimitiveTypeName(string sent, System.Type declared, bool changed, string expected)
        {
            var value = new SerializedMember { typeName = sent };
            Assert.Equal(changed, PropertyPatchPath.AlignPrimitiveTypeName(value, declared));
            Assert.Equal(expected, value.typeName);
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

        // ---- ErrorCapturingLogger + ReflectorNet ----------------------------------------------------

        class FloatHolder
        {
            public float Value { get; set; }
        }

        static SerializedMember Number(string typeName, double value)
            => new SerializedMember { typeName = typeName, valueJsonElement = JsonDocument.Parse(value.ToString(System.Globalization.CultureInfo.InvariantCulture)).RootElement };

        [Fact]
        public void TryModifyAt_MismatchedNumericType_ReportsSuccessButOnlyTheLoggerSeesTheFailure()
        {
            // The root cause of the silent drop: ReflectorNet deserializes to the sent type, the setter throws,
            // and the failure goes to the ILogger alone while TryModifyAt still returns true.
            object? target = new FloatHolder();
            var logs = new Logs();
            var logger = new ErrorCapturingLogger();

            var ok = new Reflector().TryModifyAt(ref target, "Value", Number("System.Double", 0.5), logs: logs, logger: logger);

            Assert.True(ok);
            Assert.Equal(0f, ((FloatHolder)target!).Value);
            Assert.Single(logger.Errors);
            Assert.Contains("Value", logger.Errors[0]);
        }

        [Fact]
        public void TryModifyAt_AlignedTypeName_Lands()
        {
            object? target = new FloatHolder();
            var value = Number("System.Double", 0.5);
            var logger = new ErrorCapturingLogger();
            Assert.True(PropertyPatchPath.AlignPrimitiveTypeName(value, typeof(float)));

            Assert.True(new Reflector().TryModifyAt(ref target, "Value", value, logs: new Logs(), logger: logger));

            Assert.Equal(0.5f, ((FloatHolder)target!).Value);
            Assert.Empty(logger.Errors);
        }

        [Fact]
        public void ErrorCapturingLogger_RecordsOnlyErrors()
        {
            var logger = new ErrorCapturingLogger();
            Assert.False(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Warning));
            Assert.True(logger.IsEnabled(Microsoft.Extensions.Logging.LogLevel.Critical));
            Microsoft.Extensions.Logging.LoggerExtensions.LogWarning(logger, "ignored");
            Microsoft.Extensions.Logging.LoggerExtensions.LogError(logger, "  kept  ");
            Assert.Equal(new[] { "kept" }, logger.Errors);
        }
    }
}
