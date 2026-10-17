/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Linq;
using Godot;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.CSG.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes the CSG types: an RQL engine discovers it, registers its guide, and the member
    /// tables of the primitives carry the validated members (replacing the C# properties of the same name and type) and
    /// <c>rootShape</c>. Metadata only: no Godot object is created.
    /// </summary>
    public class CsgModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(CsgModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(CsgModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == CsgGuide.Topic);
            Assert.Equal(CsgGuide.Markdown, guide.Value);
        }

        [Theory]
        [InlineData(typeof(CsgBox3D), "Size", typeof(Vector3))]
        [InlineData(typeof(CsgSphere3D), "Radius", typeof(float))]
        [InlineData(typeof(CsgSphere3D), "RadialSegments", typeof(int))]
        [InlineData(typeof(CsgSphere3D), "Rings", typeof(int))]
        [InlineData(typeof(CsgCylinder3D), "Radius", typeof(float))]
        [InlineData(typeof(CsgCylinder3D), "Height", typeof(float))]
        [InlineData(typeof(CsgCylinder3D), "Sides", typeof(int))]
        [InlineData(typeof(CsgTorus3D), "Sides", typeof(int))]
        [InlineData(typeof(CsgTorus3D), "RingSides", typeof(int))]
        public void PrimitivesHaveValidatedMembers(Type type, string name, Type memberType)
        {
            var member = Assert.Single(Engine().Types.Info(type).Members, m => m.Name == name);
            Assert.Null(member.Member); // the adapter's validated member replaced the C# property
            Assert.Equal(memberType, member.Type);
            Assert.True(member.CanWrite);
            Assert.False(string.IsNullOrEmpty(member.Description));
        }

        [Theory]
        [InlineData(typeof(CsgBox3D))]
        [InlineData(typeof(CsgSphere3D))]
        [InlineData(typeof(CsgCylinder3D))]
        [InlineData(typeof(CsgTorus3D))]
        [InlineData(typeof(CsgCombiner3D))]
        [InlineData(typeof(CsgPolygon3D))]
        public void EveryShapeHasOperationAndRootShape(Type type)
        {
            var info = Engine().Types.Info(type);
            Assert.Contains(info.Members, m => m.Name == "Operation" && m.CanWrite);
            var root = Assert.Single(info.Members, m => m.Name == "rootShape");
            Assert.Equal(typeof(bool), root.Type);
            Assert.False(root.CanWrite);
        }

        [Fact]
        public void EveryAdapterNamesTheExtension()
        {
            var adapters = new RqlTypeAdapter[]
            {
                new Godot_CsgShape3D_Adapter(), new Godot_CsgBox3D_Adapter(), new Godot_CsgSphere3D_Adapter(),
                new Godot_CsgCylinder3D_Adapter(), new Godot_CsgTorus3D_Adapter(),
            };
            Assert.All(adapters, a => Assert.Contains("(CSG extension)", a.Description));
        }
    }
}
