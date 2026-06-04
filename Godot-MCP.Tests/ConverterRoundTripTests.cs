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
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Round-trip coverage for the core Godot value-type converters registered by
    /// <see cref="GodotReflectorFactory"/>. None of these touch a SceneTree, so they run in a
    /// plain xUnit process with no Godot editor/runtime.
    /// </summary>
    public class ConverterRoundTripTests
    {
        static Reflector NewReflector() => GodotReflectorFactory.CreateDefaultReflector();

        [Fact]
        public void Factory_CreatesReflector()
        {
            Assert.NotNull(NewReflector());
        }

        [Fact]
        public void Vector2_RoundTrips()
        {
            var reflector = NewReflector();
            var original = new Vector2(1.5f, -2.25f);

            var serialized = reflector.Serialize(original, typeof(Vector2));
            var result = reflector.Deserialize(serialized, fallbackType: typeof(Vector2));

            var restored = Assert.IsType<Vector2>(result);
            Assert.Equal(original.X, restored.X);
            Assert.Equal(original.Y, restored.Y);
        }

        [Fact]
        public void Vector3_RoundTrips()
        {
            var reflector = NewReflector();
            var original = new Vector3(3.0f, 4.0f, -5.5f);

            var serialized = reflector.Serialize(original, typeof(Vector3));
            var result = reflector.Deserialize(serialized, fallbackType: typeof(Vector3));

            var restored = Assert.IsType<Vector3>(result);
            Assert.Equal(original.X, restored.X);
            Assert.Equal(original.Y, restored.Y);
            Assert.Equal(original.Z, restored.Z);
        }

        [Fact]
        public void Color_RoundTrips()
        {
            var reflector = NewReflector();
            var original = new Color(0.1f, 0.2f, 0.3f, 0.4f);

            var serialized = reflector.Serialize(original, typeof(Color));
            var result = reflector.Deserialize(serialized, fallbackType: typeof(Color));

            var restored = Assert.IsType<Color>(result);
            Assert.Equal(original.R, restored.R);
            Assert.Equal(original.G, restored.G);
            Assert.Equal(original.B, restored.B);
            Assert.Equal(original.A, restored.A);
        }

        // NOTE on NodePath: unlike Vector2/3/Color (pure managed structs), Godot's NodePath is a managed
        // wrapper over a NATIVE object — even `new NodePath("...")` calls godotsharp_node_path_new_from_string
        // via P/Invoke, which faults with AccessViolationException when the native Godot library is not loaded
        // (i.e. in a plain xUnit host with no Godot runtime). The GodotNodePathJsonConverter is therefore
        // exercised in the headless Godot smoke (see test.md Suite 3 / the Godot-Test-Project testbed), not
        // here. Constructing a NodePath in this process would crash the whole test host, so it is omitted.
    }
}
