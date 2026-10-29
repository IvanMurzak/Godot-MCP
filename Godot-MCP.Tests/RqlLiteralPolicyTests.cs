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
    /// <summary>The literal kinds and refusal advice only (no native type is instantiated); the live smoke builds the literals.</summary>
    public class RqlLiteralPolicyTests
    {
        [Theory]
        [InlineData(typeof(Resource))]
        [InlineData(typeof(BoxMesh))]
        [InlineData(typeof(StandardMaterial3D))]
        [InlineData(typeof(BoxShape3D))]
        [InlineData(typeof(global::Godot.Environment))]
        [InlineData(typeof(ParticleProcessMaterial))]
        public void Resources_are_values(Type type) => Assert.Equal(LiteralKind.Value, RqlLiteralPolicy.KindOf(type));

        [Theory]
        [InlineData(typeof(Node))]
        [InlineData(typeof(Node3D))]
        [InlineData(typeof(MeshInstance3D))]
        [InlineData(typeof(GodotObject))]
        [InlineData(typeof(string))]
        [InlineData(typeof(Vector3))]
        public void Other_types_keep_the_default_kind(Type type) => Assert.Null(RqlLiteralPolicy.KindOf(type));

        [Theory]
        [InlineData(typeof(Node), RqlLiteralPolicy.NodeAdvice)]
        [InlineData(typeof(MeshInstance3D), RqlLiteralPolicy.NodeAdvice)]
        [InlineData(typeof(GodotObject), RqlLiteralPolicy.ObjectAdvice)]
        [InlineData(typeof(string), null)]
        public void Refusals_name_godots_references(Type type, string? expected) => Assert.Equal(expected, RqlLiteralPolicy.RefusalHint(type));

        [Fact]
        public void Node_advice_names_the_finder_and_the_creation_calls()
        {
            Assert.Contains("godot.find(", RqlLiteralPolicy.NodeAdvice);
            Assert.Contains("editor.CreateNode", RqlLiteralPolicy.NodeAdvice);
            Assert.EndsWith(RqlLiteralPolicy.NodeAdvice, RqlLiteralPolicy.Refusal("MeshInstance3D", isNode: true));
            Assert.EndsWith(RqlLiteralPolicy.ObjectAdvice, RqlLiteralPolicy.Refusal("RefCounted", isNode: false));
        }

        // The literal kind replaces the CanConstructFromLiteral Resource override the addon set for RQL 2877579.
        [Fact]
        public void Configure_installs_the_kind_and_the_advice_only()
        {
            var engine = new RqlEngine();
            RqlLiteralPolicy.Configure(engine.Mutations);
            Assert.Equal(LiteralKind.Value, engine.Mutations.LiteralKindOf!(typeof(BoxMesh)));
            Assert.Null(engine.Mutations.LiteralKindOf!(typeof(Node3D)));
            Assert.Equal(RqlLiteralPolicy.NodeAdvice, engine.Mutations.LiteralRefusalHint!(typeof(Node3D)));
            Assert.Null(engine.Mutations.CanConstructFromLiteral);
            Assert.Null(engine.Mutations.CreateForLiteral);
        }
    }
}
