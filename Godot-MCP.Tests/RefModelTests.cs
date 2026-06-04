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
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// <see cref="NodeRef"/> / <see cref="ResourceRef"/> are plain data models (no Godot API), so they
    /// (de)serialize through System.Text.Json directly. These tests pin the JSON property names and the
    /// validity rules the tool layer relies on.
    /// </summary>
    public class RefModelTests
    {
        [Fact]
        public void NodeRef_RoundTrips_ByPath()
        {
            var original = new NodeRef("/root/Main/Player");

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<NodeRef>(json);

            Assert.NotNull(restored);
            Assert.Equal(original.Path, restored!.Path);
            Assert.Equal(0ul, restored.InstanceId);
            Assert.Contains("\"path\"", json);
        }

        [Fact]
        public void NodeRef_RoundTrips_ByInstanceId()
        {
            var original = new NodeRef(123456ul);

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<NodeRef>(json);

            Assert.NotNull(restored);
            Assert.Equal(123456ul, restored!.InstanceId);
            Assert.Contains("\"instanceId\"", json);
        }

        [Theory]
        [InlineData("/root/Main", 0ul, true)]
        [InlineData(null, 42ul, true)]
        [InlineData(null, 0ul, false)]
        [InlineData("", 0ul, false)]
        public void NodeRef_IsValid(string? path, ulong instanceId, bool expected)
        {
            var nodeRef = new NodeRef { Path = path, InstanceId = instanceId };
            Assert.Equal(expected, nodeRef.IsValid());
        }

        [Fact]
        public void ResourceRef_RoundTrips_ByResourcePath()
        {
            var original = new ResourceRef("res://materials/wood.tres");

            var json = JsonSerializer.Serialize(original);
            var restored = JsonSerializer.Deserialize<ResourceRef>(json);

            Assert.NotNull(restored);
            Assert.Equal(original.ResourcePath, restored!.ResourcePath);
            Assert.Contains("\"resourcePath\"", json);
        }

        [Theory]
        [InlineData("res://a.tres", 0ul, true)]
        [InlineData(null, 7ul, true)]
        [InlineData(null, 0ul, false)]
        [InlineData("", 0ul, false)]
        public void ResourceRef_IsValid(string? resourcePath, ulong instanceId, bool expected)
        {
            var resourceRef = new ResourceRef { ResourcePath = resourcePath, InstanceId = instanceId };
            Assert.Equal(expected, resourceRef.IsValid());
        }
    }
}
