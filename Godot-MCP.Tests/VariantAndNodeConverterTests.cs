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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Godot;
using Xunit;
using VariantType = Godot.Variant.Type;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for issue #292 — <c>reflection-method-call</c> silently deserializing a
    /// <c>Godot.Variant</c> parameter to <c>Nil</c> (and a <c>{"instanceId": N}</c> target object to
    /// <c>null</c>) while reporting success.
    ///
    /// <para>
    /// <b>What can and cannot be asserted here.</b> Materializing a non-<c>Nil</c>
    /// <see cref="global::Godot.Variant"/> calls <c>godotsharp_*</c> P/Invoke
    /// (<c>Variant.CreateFrom("x")</c> → <c>godotsharp_string_new_with_utf16_chars</c>), which faults with
    /// <c>AccessViolationException</c> and takes down the whole xUnit host when no Godot native library is
    /// loaded — the same constraint that keeps <c>NodePath</c> out of
    /// <see cref="ConverterRoundTripTests"/>. So this file asserts (a) the pure parser
    /// <see cref="GodotVariantPayload"/> end to end, (b) that the converters are actually SELECTED for their
    /// types, and (c) that every unreadable payload now THROWS instead of degrading to a silent default —
    /// which is the regression the issue is about. The live materialization round-trip is asserted against a
    /// real Godot (4.3 / 4.4 / 4.5) by <c>Godot-Tests/Harness/RuntimeHarness.cs</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Do not construct a non-Nil Variant in this project</b> — it crashes the test host, not just the
    /// test.
    /// </para>
    /// </summary>
    public class GodotVariantPayloadTests
    {
        static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();

        static GodotVariantPayload Parse(string json) => GodotVariantPayload.Parse(Json(json));

        // ---- Bare scalars (the shorthand the issue reporter tried first) --------------------------

        [Fact]
        public void Parse_NoElement_IsNil()
        {
            Assert.Equal(VariantType.Nil, GodotVariantPayload.Parse(null).Kind);
        }

        [Fact]
        public void Parse_JsonNull_IsNil()
        {
            Assert.Equal(VariantType.Nil, Parse("null").Kind);
        }

        [Fact]
        public void Parse_BareString_IsStringVariant()
        {
            // The exact payload from issue #292: {"typeName":"Godot.Variant","value":"res://X.tscn"}.
            var payload = Parse("\"res://CharacterCreator.tscn\"");

            Assert.Equal(VariantType.String, payload.Kind);
            Assert.Equal("res://CharacterCreator.tscn", payload.TextValue);
        }

        [Theory]
        [InlineData("true", true)]
        [InlineData("false", false)]
        public void Parse_BareBool_IsBoolVariant(string json, bool expected)
        {
            var payload = Parse(json);

            Assert.Equal(VariantType.Bool, payload.Kind);
            Assert.Equal(expected, payload.BoolValue);
        }

        [Fact]
        public void Parse_BareIntegralNumber_IsIntVariant()
        {
            var payload = Parse("42");

            Assert.Equal(VariantType.Int, payload.Kind);
            Assert.Equal(42L, payload.IntValue);
        }

        [Fact]
        public void Parse_BareFractionalNumber_IsFloatVariant()
        {
            var payload = Parse("1.5");

            Assert.Equal(VariantType.Float, payload.Kind);
            Assert.Equal(1.5, payload.FloatValue);
        }

        [Fact]
        public void Parse_BareArray_IsRejectedAsAmbiguous()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("[1,2,3]"));
            Assert.Contains("ambiguous", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        // ---- The declared {variantType, value} envelope -------------------------------------------

        [Fact]
        public void Parse_DeclaredString_RoundTripsTheValue()
        {
            var payload = Parse("{\"variantType\":\"String\",\"value\":\"res://a.tscn\"}");

            Assert.Equal(VariantType.String, payload.Kind);
            Assert.Equal("res://a.tscn", payload.TextValue);
        }

        [Fact]
        public void Parse_LegacyVariantTypeObjShape_IsAccepted()
        {
            // Godot.Variant's own properties are named VariantType / Obj, so an agent inspecting the type
            // emits this shape — it is exactly what issue #292 reported as silently producing Nil.
            var payload = Parse("{\"VariantType\":\"String\",\"Obj\":\"res://CharacterCreator.tscn\"}");

            Assert.Equal(VariantType.String, payload.Kind);
            Assert.Equal("res://CharacterCreator.tscn", payload.TextValue);
        }

        [Fact]
        public void Parse_FullyQualifiedTypeName_IsAccepted()
        {
            Assert.Equal(VariantType.Int, Parse("{\"variantType\":\"Variant.Type.Int\",\"value\":7}").Kind);
        }

        [Theory]
        [InlineData("StringName")]
        [InlineData("NodePath")]
        public void Parse_DeclaredTextTypes_KeepTheirKind(string typeName)
        {
            var payload = Parse($"{{\"variantType\":\"{typeName}\",\"value\":\"Main/Player\"}}");

            Assert.Equal(Enum.Parse<VariantType>(typeName), payload.Kind);
            Assert.Equal("Main/Player", payload.TextValue);
        }

        [Fact]
        public void Parse_DeclaredNil_IsNil()
        {
            Assert.Equal(VariantType.Nil, Parse("{\"variantType\":\"Nil\"}").Kind);
        }

        [Fact]
        public void Parse_DeclaredNilWithValue_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Nil\",\"value\":5}"));
            Assert.Contains("Nil carries no value", ex.Message);
        }

        [Fact]
        public void Parse_DeclaredTypeWithoutValue_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"String\"}"));
            Assert.Contains("no value was supplied", ex.Message);
        }

        [Fact]
        public void Parse_IntFromNumericString_IsAccepted()
        {
            Assert.Equal(-9L, Parse("{\"variantType\":\"Int\",\"value\":\"-9\"}").IntValue);
        }

        [Fact]
        public void Parse_IntFromFractionalNumber_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Int\",\"value\":1.5}"));
            Assert.Contains("expects a 64-bit integer", ex.Message);
        }

        // ---- Composite value types ----------------------------------------------------------------

        [Fact]
        public void Parse_Vector3FromArray_ReadsComponentsInOrder()
        {
            var payload = Parse("{\"variantType\":\"Vector3\",\"value\":[1,2,3.5]}");

            Assert.Equal(VariantType.Vector3, payload.Kind);
            Assert.Equal(new double[] { 1, 2, 3.5 }, payload.Components);
        }

        [Fact]
        public void Parse_Vector2FromNamedObject_ReadsComponentsInOrder()
        {
            var payload = Parse("{\"variantType\":\"Vector2\",\"value\":{\"y\":-4,\"x\":3}}");

            Assert.Equal(new double[] { 3, -4 }, payload.Components);
        }

        [Fact]
        public void Parse_ColorWithoutAlpha_DefaultsToOpaque()
        {
            var payload = Parse("{\"variantType\":\"Color\",\"value\":[0.1,0.2,0.3]}");

            Assert.Equal(new double[] { 0.1, 0.2, 0.3, 1.0 }, payload.Components);
        }

        [Fact]
        public void Parse_WrongComponentCount_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Vector3\",\"value\":[1,2]}"));
            Assert.Contains("expects 3 components; got 2", ex.Message);
        }

        [Fact]
        public void Parse_NonNumericComponent_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Vector2\",\"value\":[1,\"x\"]}"));
            Assert.Contains("must be a number", ex.Message);
        }

        [Fact]
        public void Parse_NamedObjectFormForAnUnnamedType_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(
                () => Parse("{\"variantType\":\"Transform3D\",\"value\":{\"origin\":[0,0,0]}}"));
            Assert.Contains("flat array of 12 numbers", ex.Message);
        }

        [Fact]
        public void Parse_IntegerVectorComponents_RoundToInt()
        {
            var payload = Parse("{\"variantType\":\"Vector2I\",\"value\":[3,-4]}");

            Assert.Equal(3, payload.ComponentInt(0));
            Assert.Equal(-4, payload.ComponentInt(1));
        }

        [Fact]
        public void Parse_IntegerVectorComponentOutOfRange_IsRejectedAtParseTime()
        {
            // Caught here rather than as an OverflowException from the cast during materialization, so the
            // caller gets the same actionable ArgumentException as every other bad payload.
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Vector3I\",\"value\":[1,2,1e20]}"));
            Assert.Contains("must fit in a 32-bit integer", ex.Message);
        }

        [Fact]
        public void Parse_FloatVectorAcceptsLargeComponents()
        {
            // 1e20 fits a 32-bit float, so it must be accepted — the finiteness check below must not be
            // over-eager and start rejecting ordinary large values.
            Assert.Equal(VariantType.Vector3, Parse("{\"variantType\":\"Vector3\",\"value\":[1,2,1e20]}").Kind);
        }

        [Fact]
        public void Parse_FloatVectorComponentBeyondSingleRange_IsRejected()
        {
            // 1e300 is a fine double but saturates to +Infinity as a 32-bit float — Godot's component type.
            // Silently storing Infinity is precisely the degradation this change removes.
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Vector3\",\"value\":[1,2,1e300]}"));
            Assert.Contains("must be a finite 32-bit float", ex.Message);
        }

        [Theory]
        // JsonElement.GetDouble() SATURATES an out-of-range literal to +/-Infinity rather than throwing, and
        // a non-finite Float variant has no JSON representation to be written back out.
        [InlineData("1e999")]
        [InlineData("-1e999")]
        [InlineData("{\"variantType\":\"Float\",\"value\":1e999}")]
        [InlineData("{\"variantType\":\"Float\",\"value\":\"NaN\"}")]
        [InlineData("{\"variantType\":\"Float\",\"value\":\"Infinity\"}")]
        public void Parse_NonFiniteFloat_IsRejected(string json)
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse(json));
            Assert.Contains("expects a finite number", ex.Message);
        }

        [Fact]
        public void Parse_ObjectFormMissingAComponent_NamesTheMissingOne()
        {
            // Reporting "expects 3 components; got 1" here would both under-count what the caller sent and
            // fail to say WHICH component is absent.
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Vector3\",\"value\":{\"x\":1,\"z\":3}}"));

            Assert.Contains("missing component 'y'", ex.Message);
            Assert.Contains("x, y, z", ex.Message);
        }

        [Fact]
        public void Parse_PlaneObjectForm_UsesGodotsOwnMemberNames()
        {
            // Godot.Plane exposes Normal.X/Y/Z + D — not the a/b/c/d of its constructor — so those are the
            // keys an agent reading the C# surface will send.
            var payload = Parse("{\"variantType\":\"Plane\",\"value\":{\"x\":0,\"y\":1,\"z\":0,\"d\":5}}");

            Assert.Equal(VariantType.Plane, payload.Kind);
            Assert.Equal(new double[] { 0, 1, 0, 5 }, payload.Components);
        }

        [Theory]
        // Enum.TryParse also accepts a flag LIST ("Bool,Int" == 3 == Float) and a bare ordinal, either of
        // which would silently build a DIFFERENT valid variant type.
        [InlineData("Bool,Int")]
        [InlineData("3")]
        [InlineData("0")]
        public void Parse_TypeNameThatIsNotASingleSymbolicName_IsRejected(string typeName)
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse($"{{\"variantType\":\"{typeName}\",\"value\":1}}"));
            Assert.Contains("is not a Godot Variant.Type name", ex.Message);
        }

        // ---- Rejections that used to be silent Nil ------------------------------------------------

        [Fact]
        public void Parse_ObjectWithoutADeclaredType_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"foo\":1}"));
            Assert.Contains("must declare the variant type", ex.Message);
        }

        [Fact]
        public void Parse_UnknownTypeName_IsRejected()
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"variantType\":\"Banana\",\"value\":1}"));
            Assert.Contains("is not a Godot Variant.Type name", ex.Message);
        }

        [Theory]
        [InlineData("Object")]
        [InlineData("Dictionary")]
        [InlineData("Array")]
        [InlineData("PackedStringArray")]
        [InlineData("Callable")]
        public void Parse_TypesWithNoJsonForm_AreRejectedExplicitly(string typeName)
        {
            var ex = Assert.Throws<ArgumentException>(() => Parse($"{{\"variantType\":\"{typeName}\",\"value\":1}}"));
            Assert.Contains("cannot be built from JSON", ex.Message);
        }

        [Fact]
        public void EveryRejection_CarriesTheWireFormatHelp()
        {
            // The whole point of #292 is that the caller must be told what to send instead of guessing.
            var ex = Assert.Throws<ArgumentException>(() => Parse("{\"foo\":1}"));
            Assert.Contains(GodotVariantPayload.WireFormatHelp, ex.Message);
        }
    }

    /// <summary>
    /// Converter REGISTRATION + the end-to-end "no silent default" contract through
    /// <see cref="Reflector.Deserialize"/>. Each of these fails against the pre-#292 code: the Variant one
    /// returned <c>default(Variant)</c> (Nil) and the Node one returned <c>null</c>, both reported as
    /// success.
    ///
    /// <para>
    /// Serialized into one class (and one xUnit collection) because the Node tests mutate the process-wide
    /// <see cref="Godot_Node_ReflectionConverter{T}.NodeResolver"/>.
    /// </para>
    /// </summary>
    [Collection(GodotConverterRegistrationTests.CollectionName)]
    public class GodotConverterRegistrationTests : IDisposable
    {
        public const string CollectionName = "godot-converter-statics";

        // Snapshot/restore the process-wide resolver once for the class instead of a try/finally per test
        // (xUnit builds a fresh instance per test). Enforced by TestIsolationGuardTests.
        readonly Godot_Node_ReflectionConverter.NodeResolverDelegate? _previousNodeResolver
            = Godot_Node_ReflectionConverter.NodeResolver;

        public void Dispose() => Godot_Node_ReflectionConverter.NodeResolver = _previousNodeResolver;

        static Reflector NewReflector() => GodotReflectorFactory.CreateDefaultReflector();

        static SerializedMember Member(Type type, string json, string name)
            => SerializedMember.FromJson(type, json, name);

        [Fact]
        public void VariantConverter_WinsSelectionFor_GodotVariant()
        {
            Assert.IsType<Godot_Variant_ReflectionConverter>(NewReflector().Converters.GetConverter(typeof(Variant)));
        }

        [Fact]
        public void NodeConverter_WinsSelectionFor_NodeAndDerivedTypes()
        {
            var reflector = NewReflector();

            Assert.IsType<Godot_Node_ReflectionConverter>(reflector.Converters.GetConverter(typeof(Node)));
            Assert.IsType<Godot_Node_ReflectionConverter>(reflector.Converters.GetConverter(typeof(Control)));
            Assert.IsType<Godot_Node_ReflectionConverter>(reflector.Converters.GetConverter(typeof(Node3D)));
        }

        [Fact]
        public void ResourceConverter_StillWinsFor_ResourceTypes()
        {
            // Node and Resource are siblings under GodotObject, so adding the Node converter must not
            // poach Resource-typed members from the converter that already handled them.
            var reflector = NewReflector();

            Assert.IsType<Godot_Resource_ReflectionConverter>(reflector.Converters.GetConverter(typeof(Resource)));
            Assert.IsType<Godot_Resource_ReflectionConverter>(reflector.Converters.GetConverter(typeof(PackedScene)));
        }

        [Fact]
        public void Deserialize_UnreadableVariantPayload_Throws_InsteadOfSilentNil()
        {
            // REGRESSION (#292): before the Variant converter existed, ReflectorNet's generic fallback
            // swallowed the JsonException this payload raises and returned default(Variant) == Nil, which
            // MethodWrapper.VerifyParameters then accepted as a valid Godot.Variant.
            var reflector = NewReflector();
            var member = Member(typeof(Variant), "{\"unexpected\":1}", "value");

            var ex = Assert.Throws<ArgumentException>(() => reflector.Deserialize(member, fallbackType: typeof(Variant)));
            Assert.Contains("Cannot read a Godot.Variant", ex.Message);
        }

        [Fact]
        public void Deserialize_AbsentVariantValue_IsNil()
        {
            // The ONLY ways to get Nil are explicit: no value, JSON null, or {"variantType":"Nil"}.
            var reflector = NewReflector();
            var result = reflector.Deserialize(SerializedMember.Null(typeof(Variant), "value"), fallbackType: typeof(Variant));

            var variant = Assert.IsType<Variant>(result);
            Assert.Equal(VariantType.Nil, variant.VariantType);
        }

        [Fact]
        public void Deserialize_NodeRef_WithNoResolverInstalled_WarnsWithoutFailingTheCall()
        {
            // No resolver is an ENVIRONMENT fact (no running editor), not a bad request — it must not be
            // promoted to a call failure by ReflectionArgumentGuard.
            Godot_Node_ReflectionConverter.NodeResolver = null;

            var logs = new Logs();
            var result = NewReflector().Deserialize(
                Member(typeof(Node), "{\"instanceId\":123}", "targetObject"), fallbackType: typeof(Node), logs: logs);

            Assert.Null(result);
            Assert.Empty(ReflectionArgumentGuard.Failures(logs));
            Assert.Contains(logs, entry => entry.Type == LogType.Warning && entry.Message.Contains("No Node resolver is installed"));
        }

        [Theory]
        // REGRESSION (#292): the ref never reached ANY resolver — ReflectorNet's generic converter swallowed
        // it and produced null with NO diagnostic, so every instance method was unreachable behind the
        // generic "'targetObject' deserialized instance is null". Now the ref arrives intact and the
        // resolver's own reason is recorded, which ReflectionArgumentGuard turns into a refused call.
        [InlineData("{\"instanceId\":123}", 123ul, null)]
        [InlineData("{\"path\":\"/root/Main/Player\"}", 0ul, "/root/Main/Player")]
        public void Deserialize_NodeRef_ReachesTheInstalledResolver_AndRecordsItsError(
            string json, ulong expectedInstanceId, string? expectedPath)
        {
            NodeRef? seen = null;
            Godot_Node_ReflectionConverter.NodeResolver = (NodeRef nodeRef, out object? node, out string? error) =>
            {
                seen = nodeRef;
                node = null;
                error = "No live object with that reference";
                return false;
            };

            var logs = new Logs();
            var result = NewReflector().Deserialize(Member(typeof(Node), json, "targetObject"), fallbackType: typeof(Node), logs: logs);

            Assert.Null(result);
            Assert.NotNull(seen);
            Assert.Equal(expectedInstanceId, seen!.InstanceId);
            Assert.Equal(expectedPath, seen.Path);
            Assert.Contains(ReflectionArgumentGuard.Failures(logs), m => m.Contains("No live object with that reference"));
        }

        [Fact]
        public void Deserialize_NodeRef_ResolverFailure_IsARefusedCall()
        {
            // The end-to-end contract: converter reports, ReflectionArgumentGuard (the call site) refuses.
            Godot_Node_ReflectionConverter.NodeResolver = (NodeRef nodeRef, out object? node, out string? error) =>
            {
                node = null;
                error = "No live object with instanceId '123'";
                return false;
            };

            var logs = new Logs();
            NewReflector().Deserialize(Member(typeof(Node), "{\"instanceId\":123}", "targetObject"), fallbackType: typeof(Node), logs: logs);

            var ex = Assert.Throws<ArgumentException>(
                () => ReflectionArgumentGuard.RequireNoErrors(logs, "targetObject", "Godot.Node"));
            Assert.Contains("the call was NOT made", ex.Message);
            Assert.Contains("No live object with instanceId '123'", ex.Message);
        }

        [Fact]
        public void Deserialize_EmptyNodeRef_IsAFailure()
        {
            var logs = new Logs();
            var result = NewReflector().Deserialize(
                Member(typeof(Node), "{\"instanceId\":0}", "targetObject"), fallbackType: typeof(Node), logs: logs);

            Assert.Null(result);
            Assert.Contains(ReflectionArgumentGuard.Failures(logs), m => m.Contains("Could not read a Node reference"));
        }

        // ToNodeRef on a null node yields an empty (invalid) ref — the pure-managed slice of the serialize
        // side (the live-node branch reads GetInstanceId/GetPath and needs a real Godot → Suite 3). Mirrors
        // ResourceConverterTests.ToResourceRef_Null_YieldsEmptyRef.
        [Fact]
        public void ToNodeRef_Null_YieldsEmptyRef()
        {
            var nodeRef = Godot_Node_ReflectionConverter<Node>.ToNodeRef(null);

            Assert.False(nodeRef.IsValid());
            Assert.Equal(0ul, nodeRef.InstanceId);
            Assert.Null(nodeRef.Path);
        }

        [Fact]
        public void Deserialize_ExplicitNullNode_IsNull()
        {
            // Clearing a Node-typed member stays legitimate — only a NON-EMPTY unresolvable ref is an error.
            var reflector = NewReflector();

            Assert.Null(reflector.Deserialize(SerializedMember.Null(typeof(Node), "value"), fallbackType: typeof(Node)));
        }
    }

    [CollectionDefinition(GodotConverterRegistrationTests.CollectionName, DisableParallelization = true)]
    public class GodotConverterRegistrationCollection { }
}
