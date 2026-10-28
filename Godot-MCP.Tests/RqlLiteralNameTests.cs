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
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Godot property names in an object literal ({class: "StandardMaterial3D", albedo_color: "#d9a06b"}, wave 6) map to the C#
    /// members RQL builds Godot classes from. Reflection over GodotSharp metadata only (nothing native is created).
    /// </summary>
    public class RqlLiteralNameTests
    {
        [Theory]
        [InlineData(typeof(StandardMaterial3D), "albedo_color", "AlbedoColor")]
        [InlineData(typeof(Sky), "sky_material", "SkyMaterial")]
        [InlineData(typeof(GpuParticles3D), "draw_pass_1", "DrawPass1")]
        [InlineData(typeof(BoxMesh), "subdivide_width", "SubdivideWidth")]
        public void A_snake_case_name_finds_its_member(System.Type type, string snake, string member) =>
            Assert.Equal(member, RqlHostNames.CSharpMember(type, snake));

        [Theory]
        [InlineData(typeof(StandardMaterial3D), "AlbedoColor")] // no underscore: RQL matches it itself
        [InlineData(typeof(StandardMaterial3D), "no_such_thing")]
        [InlineData(typeof(StandardMaterial3D), "_")]
        public void Other_names_map_to_nothing(System.Type type, string name) => Assert.Null(RqlHostNames.CSharpMember(type, name));
    }
}
