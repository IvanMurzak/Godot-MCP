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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.ReflectorNet;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the scene/node tool family: the structured result models
    /// (<see cref="NodeData"/>, <see cref="SceneData"/>), the property-patch model
    /// (<see cref="NodePropertyPatch"/>), the scene-tree path normalizer
    /// (<see cref="NodePathNormalizer"/>), and the ambient reflector accessor
    /// (<see cref="GodotMcpReflector"/>). None of these touch a live Godot SceneTree, so they run in the
    /// plain xUnit host with no Godot binary. The editor-driving handlers themselves (<c>#if TOOLS</c>)
    /// are verified by the headless Godot smoke (test.md Suite 3).
    /// </summary>
    public class SceneNodeToolTests
    {
        // ---- NodeData ----------------------------------------------------------------------------

        [Fact]
        public void NodeData_Serializes_WithExpectedJsonNames()
        {
            var data = new NodeData
            {
                InstanceId = 42ul,
                Name = "Player",
                Path = "/root/Main/Player",
                Type = "CharacterBody3D",
                ScriptResourcePath = "res://player.gd",
                ChildCount = 2,
            };

            var json = JsonSerializer.Serialize(data);

            Assert.Contains("\"instanceId\"", json);
            Assert.Contains("\"name\"", json);
            Assert.Contains("\"path\"", json);
            Assert.Contains("\"type\"", json);
            Assert.Contains("\"scriptResourcePath\"", json);
            Assert.Contains("\"childCount\"", json);

            var restored = JsonSerializer.Deserialize<NodeData>(json);
            Assert.NotNull(restored);
            Assert.Equal(42ul, restored!.InstanceId);
            Assert.Equal("Player", restored.Name);
            Assert.Equal("CharacterBody3D", restored.Type);
            Assert.Equal("res://player.gd", restored.ScriptResourcePath);
            Assert.Equal(2, restored.ChildCount);
            Assert.Null(restored.Children);
        }

        [Fact]
        public void NodeData_RoundTrips_WithChildrenHierarchy()
        {
            var data = new NodeData
            {
                Name = "Main",
                Type = "Node",
                ChildCount = 1,
                Children = new List<NodeData>
                {
                    new NodeData { Name = "Child", Type = "Node2D", ChildCount = 0 },
                },
            };

            var json = JsonSerializer.Serialize(data);
            var restored = JsonSerializer.Deserialize<NodeData>(json);

            Assert.NotNull(restored);
            Assert.NotNull(restored!.Children);
            Assert.Single(restored.Children!);
            Assert.Equal("Child", restored.Children![0].Name);
            Assert.Equal("Node2D", restored.Children[0].Type);
        }

        // ---- SceneData ---------------------------------------------------------------------------

        [Fact]
        public void SceneData_Serializes_WithExpectedJsonNames()
        {
            var data = new SceneData
            {
                ResourcePath = "res://levels/level_1.tscn",
                RootName = "Level1",
                RootInstanceId = 7ul,
                IsActive = true,
            };

            var json = JsonSerializer.Serialize(data);

            Assert.Contains("\"resourcePath\"", json);
            Assert.Contains("\"rootName\"", json);
            Assert.Contains("\"rootInstanceId\"", json);
            Assert.Contains("\"isActive\"", json);

            var restored = JsonSerializer.Deserialize<SceneData>(json);
            Assert.NotNull(restored);
            Assert.Equal("res://levels/level_1.tscn", restored!.ResourcePath);
            Assert.Equal("Level1", restored.RootName);
            Assert.Equal(7ul, restored.RootInstanceId);
            Assert.True(restored.IsActive);
        }

        [Fact]
        public void SceneData_AllowsNullPath_ForUnsavedScene()
        {
            var data = new SceneData { ResourcePath = null, RootName = "New", IsActive = true };
            var json = JsonSerializer.Serialize(data);
            var restored = JsonSerializer.Deserialize<SceneData>(json);
            Assert.NotNull(restored);
            Assert.Null(restored!.ResourcePath);
            Assert.Equal("New", restored.RootName);
        }

        // ---- NodePropertyPatch -------------------------------------------------------------------

        [Fact]
        public void NodePropertyPatch_RoundTrips_PathAndName()
        {
            var patch = new NodePropertyPatch { Path = "position/[0]" };
            var json = JsonSerializer.Serialize(patch);

            Assert.Contains("\"path\"", json);
            Assert.Contains("\"value\"", json);

            var restored = JsonSerializer.Deserialize<NodePropertyPatch>(json);
            Assert.NotNull(restored);
            Assert.Equal("position/[0]", restored!.Path);
        }

        // ---- NodePathNormalizer ------------------------------------------------------------------

        [Theory]
        // Absolute '/root/<Root>/...' forms reduce to a root-relative child path.
        [InlineData("/root/Main/Player", "Main", "Player")]
        [InlineData("/root/Main/Player/Weapon", "Main", "Player/Weapon")]
        // Leading single slash is stripped.
        [InlineData("/Main/Player", "Main", "Player")]
        // Root-prefixed (no /root/) forms strip the root segment.
        [InlineData("Main/Player", "Main", "Player")]
        // The root segment alone resolves to '.' (the root itself).
        [InlineData("/root/Main", "Main", ".")]
        [InlineData("Main", "Main", ".")]
        // A path that does not name the root is left root-relative as-is.
        [InlineData("Player/Weapon", "Main", "Player/Weapon")]
        // Whitespace is trimmed.
        [InlineData("  Main/Player  ", "Main", "Player")]
        public void NodePathNormalizer_Normalizes(string raw, string rootName, string expected)
        {
            Assert.Equal(expected, NodePathNormalizer.Normalize(raw, rootName));
        }

        [Fact]
        public void NodePathNormalizer_HandlesNullAndEmpty()
        {
            Assert.Equal(string.Empty, NodePathNormalizer.Normalize(string.Empty, "Main"));
            Assert.Equal(string.Empty, NodePathNormalizer.Normalize(null!, "Main"));
        }

        // ---- GodotMcpReflector -------------------------------------------------------------------

        [Fact]
        public void GodotMcpReflector_GetOrCreate_FallsBack_WhenNoCurrent()
        {
            var saved = GodotMcpReflector.Current;
            try
            {
                GodotMcpReflector.Current = null;
                var reflector = GodotMcpReflector.GetOrCreate();
                Assert.NotNull(reflector);
                // The fallback is NOT cached into Current (the connection remains the sole owner).
                Assert.Null(GodotMcpReflector.Current);
            }
            finally
            {
                GodotMcpReflector.Current = saved;
            }
        }

        [Fact]
        public void GodotMcpReflector_GetOrCreate_ReturnsCurrent_WhenSet()
        {
            var saved = GodotMcpReflector.Current;
            try
            {
                var mine = new Reflector();
                GodotMcpReflector.Current = mine;
                Assert.Same(mine, GodotMcpReflector.GetOrCreate());
            }
            finally
            {
                GodotMcpReflector.Current = saved;
            }
        }
    }
}
