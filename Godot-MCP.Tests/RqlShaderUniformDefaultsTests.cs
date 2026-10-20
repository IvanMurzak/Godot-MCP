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
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>Shader uniform defaults read from the code (ShaderUniformDefaults): literal forms, hints, comments, typing.</summary>
    public class RqlShaderUniformDefaultsTests
    {
        private const string Code =
            "shader_type spatial;\n" +
            "// uniform float commented = 9.0;\n" +
            "/* uniform float blocked = 8.0; */\n" +
            "uniform vec4 tint : source_color = vec4(1.0, 0.5, 0.25, 1.0);\n" +
            "uniform float speed : hint_range(0.0, 10.0, 0.1) = 2.5;\n" +
            "uniform highp int count = -3;\n" +
            "uniform uint mask = 7u;\n" +
            "uniform bool on = true;\n" +
            "uniform vec3 up = vec3(1.0);\n" +
            "uniform ivec2 cell = ivec2(2, 3);\n" +
            "uniform float half_ = .5f;\n" +
            "uniform sampler2D tex : source_color;\n" +
            "uniform float computed = 1.0 + 2.0;\n" +
            "uniform vec4 nested = vec4(vec3(1.0), 1.0);\n" +
            "uniform mat4 m;\n" +
            "uniform vec2 short_ = vec2(1.0, 2.0, 3.0);\n" +
            "void fragment() { float uniform_like = 1.0; }\n";

        [Fact]
        public void LiteralDefaultsAreRead()
        {
            var d = ShaderUniformDefaults.Parse(Code);
            Assert.Equal(new[] { 1.0, 0.5, 0.25, 1.0 }, d["tint"]);
            Assert.Equal(new[] { 2.5 }, d["speed"]);
            Assert.Equal(new[] { -3.0 }, d["count"]);
            Assert.Equal(new[] { 7.0 }, d["mask"]);
            Assert.Equal(new[] { 1.0 }, d["on"]);
            Assert.Equal(new[] { 1.0, 1.0, 1.0 }, d["up"]);
            Assert.Equal(new[] { 2.0, 3.0 }, d["cell"]);
            Assert.Equal(new[] { 0.5 }, d["half_"]);
        }

        [Fact]
        public void CommentsComputedValuesAndUninitializedUniformsHaveNoDefault()
        {
            var d = ShaderUniformDefaults.Parse(Code);
            foreach (var name in new[] { "commented", "blocked", "tex", "computed", "nested", "m", "short_", "uniform_like" })
                Assert.False(d.ContainsKey(name), name);
            Assert.Empty(ShaderUniformDefaults.Parse(null));
            Assert.Empty(ShaderUniformDefaults.Parse(""));
        }

        [Fact]
        public void ValuesTakeTheUniformsType()
        {
            Assert.Equal(new Color(1f, 0.5f, 0.25f, 1f), ShaderUniformDefaults.ToValue(Variant.Type.Color, new[] { 1.0, 0.5, 0.25, 1.0 }));
            Assert.Equal(new Color(1f, 0f, 0f), ShaderUniformDefaults.ToValue(Variant.Type.Color, new[] { 1.0, 0.0, 0.0 }));
            Assert.Equal(new Vector4(1, 0.5f, 0.25f, 1), ShaderUniformDefaults.ToValue(Variant.Type.Vector4, new[] { 1.0, 0.5, 0.25, 1.0 }));
            Assert.Equal(2.5, ShaderUniformDefaults.ToValue(Variant.Type.Float, new[] { 2.5 }));
            Assert.Equal(-3L, ShaderUniformDefaults.ToValue(Variant.Type.Int, new[] { -3.0 }));
            Assert.Equal(true, ShaderUniformDefaults.ToValue(Variant.Type.Bool, new[] { 1.0 }));
            Assert.Equal(new Vector2I(2, 3), ShaderUniformDefaults.ToValue(Variant.Type.Vector2I, new[] { 2.0, 3.0 }));
            Assert.Equal(new Vector3(1, 1, 1), ShaderUniformDefaults.ToValue(Variant.Type.Vector3, new[] { 1.0, 1.0, 1.0 }));
            Assert.Null(ShaderUniformDefaults.ToValue(Variant.Type.Vector3, new[] { 1.0, 1.0 }));
            Assert.Null(ShaderUniformDefaults.ToValue(Variant.Type.Projection, new[] { 1.0 }));
        }
    }
}
