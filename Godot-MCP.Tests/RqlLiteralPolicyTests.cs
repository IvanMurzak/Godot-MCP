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
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Godot;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// RQL 2877579 never builds an engine object (a type with an identity / liveness adapter, i.e. every GodotObject) from an
    /// object literal unless the host says so: Resources (embedded meshes, shapes, materials …) must stay constructible,
    /// Nodes keep the refusal. The live CI smoke builds such literals in the editor; here only the decision (no native type
    /// is instantiated).
    /// </summary>
    public class RqlLiteralPolicyTests
    {
        [Theory]
        [InlineData(typeof(Resource))]
        [InlineData(typeof(BoxMesh))]
        [InlineData(typeof(StandardMaterial3D))]
        [InlineData(typeof(BoxShape3D))]
        [InlineData(typeof(global::Godot.Environment))]
        [InlineData(typeof(ParticleProcessMaterial))]
        public void Resources_are_built_from_literals(Type type) => Assert.True(RqlLiteralPolicy.CanConstruct(type));

        [Theory]
        [InlineData(typeof(Node))]
        [InlineData(typeof(Node3D))]
        [InlineData(typeof(MeshInstance3D))]
        [InlineData(typeof(GodotObject))]
        [InlineData(typeof(string))]
        [InlineData(typeof(Vector3))]
        public void Other_types_keep_the_default(Type type) => Assert.Null(RqlLiteralPolicy.CanConstruct(type));

        [Fact]
        public void Configure_installs_the_hook()
        {
            var engine = new RqlEngine();
            Assert.Null(engine.Mutations.CanConstructFromLiteral);
            RqlLiteralPolicy.Configure(engine.Mutations);
            Assert.True(engine.Mutations.CanConstructFromLiteral!(typeof(BoxMesh)));
            Assert.Null(engine.Mutations.CanConstructFromLiteral!(typeof(Node3D)));
        }
    }
}
