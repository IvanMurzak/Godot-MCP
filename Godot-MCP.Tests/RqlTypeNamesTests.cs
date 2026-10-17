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
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>TypeNames: short, unique display names for the types one tool describes (ported from Unity-MCP).</summary>
    public class RqlTypeNamesTests
    {
        [Fact]
        public void UniqueTypesGetTheirShortName()
        {
            var names = TypeNames.Assign(new[] { "com.IvanMurzak.ReflectorNet.Model.SerializedMember", "com.IvanMurzak.Godot.MCP.Data.NodeRef", "com.X.Tool_Screenshot-CameraView" });
            Assert.Equal("SerializedMember", names["com.IvanMurzak.ReflectorNet.Model.SerializedMember"]);
            Assert.Equal("NodeRef", names["com.IvanMurzak.Godot.MCP.Data.NodeRef"]);
            Assert.Equal("CameraView", names["com.X.Tool_Screenshot-CameraView"]);
        }

        [Fact]
        public void ClashingShortNamesKeepJustEnoughNamespaceOnEveryType()
        {
            var a = new[] { "Game.Alpha.Settings", "Game.Beta.Settings", "Game.Item" };
            var forward = TypeNames.Assign(a);
            var backward = TypeNames.Assign(new[] { a[2], a[1], a[0] });
            Assert.Equal("Alpha.Settings", forward["Game.Alpha.Settings"]);
            Assert.Equal("Beta.Settings", forward["Game.Beta.Settings"]);
            Assert.Equal("Item", forward["Game.Item"]);
            Assert.Equal(forward, backward);
        }

        [Fact]
        public void ClashesNeedingDeeperNamespacesResolve()
        {
            var names = TypeNames.Assign(new[] { "A.Core.Settings", "B.Core.Settings" });
            Assert.Equal("A.Core.Settings", names["A.Core.Settings"]);
            Assert.Equal("B.Core.Settings", names["B.Core.Settings"]);
        }

        [Fact]
        public void NestedAndIdenticalNamesStayDistinct()
        {
            var names = TypeNames.Assign(new[] { "Ns.Outer+Settings", "Ns.Settings" });
            Assert.NotEqual(names["Ns.Outer+Settings"], names["Ns.Settings"]);
            Assert.Equal("Outer.Settings", names["Ns.Outer+Settings"]);
        }

        [Theory]
        [InlineData("Game.Wrapper(Game.Item)", "Wrapper<Item>")]
        [InlineData("System.Collections.Generic.Dictionary(System.String,Game.Item)", "Dictionary<String, Item>")]
        [InlineData("Game.Box(Game.Wrapper(Game.Item))", "Box<Wrapper<Item>>")]
        public void GenericNamesAreShortenedInside(string key, string expected) =>
            Assert.Equal(expected, TypeNames.Assign(new[] { key })[key]);

        [Fact]
        public void ClrTypesUseTheReflectorNetForm()
        {
            Assert.Equal("System.Collections.Generic.List(System.Collections.Generic.List(System.String))", TypeNames.FullName(typeof(List<List<string>>)));
            Assert.Equal("List<List<String>>", TypeNames.Display(TypeNames.FullName(typeof(List<List<string>>)), 1));
        }
    }
}
