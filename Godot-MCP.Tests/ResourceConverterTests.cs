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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for <see cref="Godot_Resource_ReflectionConverter"/>. These exercise only the pure-managed
    /// surface — type matching (by inheritance distance over <see cref="Type"/> tokens) and deserialization
    /// of the <see cref="ResourceRef"/> shape. Constructing a native Godot <c>Resource</c>/<c>Mesh</c> would
    /// fault the test host (P/Invoke with no Godot runtime), so the live <c>ResourceLoader.Load</c> path and
    /// the serialize-from-live-resource path are verified by the headless Godot smoke (test.md Suite 3), not
    /// here. <c>typeof(BoxMesh)</c> etc. are safe — a Type token triggers no native call.
    /// </summary>
    public class ResourceConverterTests
    {
        static Reflector NewReflector() => GodotReflectorFactory.CreateDefaultReflector();

        // The converter's SerializationPriority scores by inheritance distance from Godot.Resource, so it must
        // produce a positive score for the exact Resource type AND every Resource-derived type. A non-Resource
        // type must score 0 (cannot handle).
        [Theory]
        [InlineData(typeof(Resource))]
        [InlineData(typeof(Mesh))]
        [InlineData(typeof(BoxMesh))]
        [InlineData(typeof(Material))]
        [InlineData(typeof(Texture2D))]
        [InlineData(typeof(PackedScene))]
        public void Converter_Matches_ResourceAndDerivedTypes(System.Type type)
        {
            var converter = new Godot_Resource_ReflectionConverter();
            Assert.True(converter.SerializationPriority(type) > 0,
                $"Expected the Resource converter to match '{type.Name}'.");
        }

        [Theory]
        [InlineData(typeof(Vector3))]
        [InlineData(typeof(Color))]
        [InlineData(typeof(string))]
        [InlineData(typeof(int))]
        public void Converter_DoesNotMatch_NonResourceTypes(System.Type type)
        {
            var converter = new Godot_Resource_ReflectionConverter();
            Assert.Equal(0, converter.SerializationPriority(type));
        }

        // The exact Resource type must outscore a derived type, so a more-specific converter (if ever added)
        // can win by exact match while this one still covers the whole subtree.
        [Fact]
        public void Converter_ExactResource_OutscoresDerived()
        {
            var converter = new Godot_Resource_ReflectionConverter();
            Assert.True(converter.SerializationPriority(typeof(Resource)) >
                        converter.SerializationPriority(typeof(BoxMesh)));
        }

        // The registered reflector must actually pick THIS converter for a Resource-derived type — proving the
        // registration in GodotReflectorFactory routes Mesh/BoxMesh through the reference converter instead of
        // the generic instantiate-and-populate fallback that fails with "Instance creation failed".
        [Theory]
        [InlineData(typeof(Resource))]
        [InlineData(typeof(BoxMesh))]
        [InlineData(typeof(Material))]
        public void Reflector_SelectsResourceConverter_ForResourceTypes(System.Type type)
        {
            var reflector = NewReflector();
            var converter = reflector.Converters.GetConverter(type);
            Assert.IsType<Godot_Resource_ReflectionConverter>(converter);
        }

        // A null/absent value clears the property (assign null) without throwing.
        [Fact]
        public void Deserialize_NullValue_ReturnsNull()
        {
            var reflector = NewReflector();
            var data = new SerializedMember { typeName = typeof(BoxMesh).GetTypeId() };
            var result = reflector.Deserialize(data, fallbackType: typeof(BoxMesh));
            Assert.Null(result);
        }

        // An empty ref ({} with neither resourcePath nor instanceId) also clears the property.
        [Fact]
        public void Deserialize_EmptyRef_ReturnsNull()
        {
            var reflector = NewReflector();
            var emptyRefJson = JsonSerializer.SerializeToElement(new ResourceRef());
            var data = SerializedMember.FromJson(typeof(BoxMesh), emptyRefJson);
            var result = reflector.Deserialize(data, fallbackType: typeof(BoxMesh));
            Assert.Null(result);
        }

        // With NO resolver installed (the plain unit-test host), a non-empty ref does not throw — it degrades
        // to null. The live resolution is wired only under #if TOOLS and verified by the Suite-3 smoke.
        [Fact]
        public void Deserialize_NonEmptyRef_NoResolver_ReturnsNullWithoutThrowing()
        {
            // Ensure no resolver leaks in from another test.
            Godot_Resource_ReflectionConverter.ResourceResolver = null;

            var reflector = NewReflector();
            var refJson = JsonSerializer.SerializeToElement(new ResourceRef("res://box_mesh.tres"));
            var data = SerializedMember.FromJson(typeof(BoxMesh), refJson);

            var logs = new Logs();
            var result = reflector.Deserialize(data, fallbackType: typeof(BoxMesh), logs: logs);
            Assert.Null(result);
        }

        // With a resolver installed, a non-empty ref is handed to it and its result is returned. We stand in a
        // plain object for the "live resource" and pass typeof(object) as the target type so the converter's
        // assignability guard (targetType.IsInstanceOfType(resolved)) passes — this exercises the resolver
        // wiring end-to-end with NO native Godot type. The static delegate is shared across the converter's
        // closed generic types, so setting it on the base entry reaches the instance under test.
        [Fact]
        public void Deserialize_NonEmptyRef_WithResolver_ReturnsResolved()
        {
            ResourceRef? seenRef = null;
            var standIn = new object();
            Godot_Resource_ReflectionConverter.ResourceResolver = (ResourceRef r, out object? res, out string? err) =>
            {
                seenRef = r;
                res = standIn;
                err = null;
                return true;
            };
            try
            {
                var converter = new Godot_Resource_ReflectionConverter();
                var reflector = NewReflector();
                var refJson = JsonSerializer.SerializeToElement(new ResourceRef("res://wood.tres"));
                var data = SerializedMember.FromJson(typeof(object), refJson);

                // typeof(object) as the target makes the assignability guard accept the plain stand-in.
                var result = converter.Deserialize(reflector, data, fallbackType: typeof(object));

                Assert.Same(standIn, result);
                Assert.NotNull(seenRef);
                Assert.Equal("res://wood.tres", seenRef!.ResourcePath);
            }
            finally
            {
                Godot_Resource_ReflectionConverter.ResourceResolver = null;
            }
        }

        // ToResourceRef on a null resource yields an empty (invalid) ref — the pure-managed slice of the
        // serialize side (the live-resource branch needs a native Resource → Suite 3).
        [Fact]
        public void ToResourceRef_Null_YieldsEmptyRef()
        {
            var resourceRef = Godot_Resource_ReflectionConverter<Resource>.ToResourceRef(null);
            Assert.False(resourceRef.IsValid());
        }
    }
}
