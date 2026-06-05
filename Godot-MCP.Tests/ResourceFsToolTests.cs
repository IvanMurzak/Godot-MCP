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
using System.Collections.Generic;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the resource/filesystem tool family: the structured result models
    /// (<see cref="FileSystemEntry"/>, <see cref="FileSystemListing"/>, <see cref="ResourceInfo"/>,
    /// <see cref="ResourceFindResult"/>) and the res:// path normalizer (<see cref="ResPathNormalizer"/>).
    /// None of these touch a live Godot filesystem, so they run in the plain xUnit host with no Godot
    /// binary. The editor-driving handlers themselves (<c>#if TOOLS</c>) are verified by the headless
    /// Godot smoke (test.md Suite 3).
    /// </summary>
    public class ResourceFsToolTests
    {
        // ---- FileSystemEntry / FileSystemListing -------------------------------------------------

        [Fact]
        public void FileSystemEntry_Serializes_WithExpectedJsonNames()
        {
            var entry = new FileSystemEntry
            {
                Name = "wood.tres",
                Path = "res://materials/wood.tres",
                IsDirectory = false,
                ResourceType = "StandardMaterial3D",
                Uid = "uid://abc123",
            };

            var json = JsonSerializer.Serialize(entry);

            Assert.Contains("\"name\"", json);
            Assert.Contains("\"path\"", json);
            Assert.Contains("\"isDirectory\"", json);
            Assert.Contains("\"resourceType\"", json);
            Assert.Contains("\"uid\"", json);

            var restored = JsonSerializer.Deserialize<FileSystemEntry>(json);
            Assert.NotNull(restored);
            Assert.Equal("wood.tres", restored!.Name);
            Assert.Equal("res://materials/wood.tres", restored.Path);
            Assert.False(restored.IsDirectory);
            Assert.Equal("StandardMaterial3D", restored.ResourceType);
            Assert.Equal("uid://abc123", restored.Uid);
        }

        [Fact]
        public void FileSystemEntry_Directory_AllowsNullTypeAndUid()
        {
            var entry = new FileSystemEntry { Name = "materials", Path = "res://materials/", IsDirectory = true };
            var json = JsonSerializer.Serialize(entry);
            var restored = JsonSerializer.Deserialize<FileSystemEntry>(json);

            Assert.NotNull(restored);
            Assert.True(restored!.IsDirectory);
            Assert.Null(restored.ResourceType);
            Assert.Null(restored.Uid);
        }

        [Fact]
        public void FileSystemListing_RoundTrips_WithEntries()
        {
            var listing = new FileSystemListing
            {
                Path = "res://",
                DirectoryCount = 1,
                FileCount = 1,
                Entries = new List<FileSystemEntry>
                {
                    new FileSystemEntry { Name = "materials", Path = "res://materials/", IsDirectory = true },
                    new FileSystemEntry { Name = "icon.svg", Path = "res://icon.svg", IsDirectory = false, ResourceType = "Texture2D" },
                },
            };

            var json = JsonSerializer.Serialize(listing);
            Assert.Contains("\"path\"", json);
            Assert.Contains("\"directoryCount\"", json);
            Assert.Contains("\"fileCount\"", json);
            Assert.Contains("\"entries\"", json);

            var restored = JsonSerializer.Deserialize<FileSystemListing>(json);
            Assert.NotNull(restored);
            Assert.Equal("res://", restored!.Path);
            Assert.Equal(1, restored.DirectoryCount);
            Assert.Equal(1, restored.FileCount);
            Assert.Equal(2, restored.Entries.Count);
            Assert.True(restored.Entries[0].IsDirectory);
            Assert.Equal("Texture2D", restored.Entries[1].ResourceType);
        }

        // ---- ResourceInfo / ResourceFindResult ---------------------------------------------------

        [Fact]
        public void ResourceInfo_Serializes_WithExpectedJsonNames()
        {
            var info = new ResourceInfo
            {
                ResourcePath = "res://materials/wood.tres",
                Uid = "uid://abc123",
                Type = "StandardMaterial3D",
            };

            var json = JsonSerializer.Serialize(info);
            Assert.Contains("\"resourcePath\"", json);
            Assert.Contains("\"uid\"", json);
            Assert.Contains("\"type\"", json);

            var restored = JsonSerializer.Deserialize<ResourceInfo>(json);
            Assert.NotNull(restored);
            Assert.Equal("res://materials/wood.tres", restored!.ResourcePath);
            Assert.Equal("uid://abc123", restored.Uid);
            Assert.Equal("StandardMaterial3D", restored.Type);
        }

        [Fact]
        public void ResourceInfo_AllowsNullUidAndType()
        {
            var info = new ResourceInfo { ResourcePath = "res://x.tres" };
            var json = JsonSerializer.Serialize(info);
            var restored = JsonSerializer.Deserialize<ResourceInfo>(json);
            Assert.NotNull(restored);
            Assert.Null(restored!.Uid);
            Assert.Null(restored.Type);
        }

        [Fact]
        public void ResourceFindResult_RoundTrips_CountAndResources()
        {
            var result = new ResourceFindResult
            {
                Count = 2,
                Resources = new List<ResourceInfo>
                {
                    new ResourceInfo { ResourcePath = "res://a.tres", Type = "Resource" },
                    new ResourceInfo { ResourcePath = "res://b.tres", Type = "Curve" },
                },
            };

            var json = JsonSerializer.Serialize(result);
            Assert.Contains("\"count\"", json);
            Assert.Contains("\"resources\"", json);

            var restored = JsonSerializer.Deserialize<ResourceFindResult>(json);
            Assert.NotNull(restored);
            Assert.Equal(2, restored!.Count);
            Assert.Equal(2, restored.Resources.Count);
            Assert.Equal("res://a.tres", restored.Resources[0].ResourcePath);
            Assert.Equal("Curve", restored.Resources[1].Type);
        }

        // ---- ResPathNormalizer.NormalizeDir ------------------------------------------------------

        [Theory]
        // Empty/null/'res://' all map to the project root.
        [InlineData(null, "res://")]
        [InlineData("", "res://")]
        [InlineData("res://", "res://")]
        // A directory path gets a trailing slash.
        [InlineData("res://materials", "res://materials/")]
        [InlineData("res://materials/", "res://materials/")]
        // Nested.
        [InlineData("res://a/b/c", "res://a/b/c/")]
        // Whitespace trimmed.
        [InlineData("  res://materials  ", "res://materials/")]
        public void NormalizeDir_Normalizes(string? raw, string expected)
        {
            Assert.Equal(expected, ResPathNormalizer.NormalizeDir(raw));
        }

        [Fact]
        public void NormalizeDir_Rejects_NonResPath()
        {
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.NormalizeDir("/abs/path"));
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.NormalizeDir("user://x"));
        }

        // ---- ResPathNormalizer.IsResPath / RequireResFilePath ------------------------------------

        [Theory]
        [InlineData("res://x.tres", true)]
        [InlineData("res://", true)]
        [InlineData("user://x", false)]
        [InlineData("/abs", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsResPath_Detects(string? path, bool expected)
        {
            Assert.Equal(expected, ResPathNormalizer.IsResPath(path));
        }

        [Fact]
        public void RequireResFilePath_AcceptsResFile_TrimsWhitespace()
        {
            Assert.Equal("res://materials/wood.tres",
                ResPathNormalizer.RequireResFilePath("  res://materials/wood.tres  ", "p"));
        }

        [Fact]
        public void RequireResFilePath_Rejects_NonResAndDirectory()
        {
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.RequireResFilePath("/abs/x.tres", "p"));
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.RequireResFilePath("res://materials/", "p"));
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.RequireResFilePath(null, "p"));
        }

        [Fact]
        public void RequireResFilePath_BareScheme_RejectsWithRootSpecificMessage()
        {
            // 'res://' passes IsResPath (it IS a res:// path) but names the project root, not a file. The
            // message must say so accurately rather than the misleading "not a directory".
            var ex = Assert.Throws<ArgumentException>(() => ResPathNormalizer.RequireResFilePath("res://", "p"));
            Assert.Contains("project root", ex.Message);
            Assert.DoesNotContain("not a directory", ex.Message);
        }

        // ---- ResPathNormalizer.ParentDir (mkdir-parents derivation) ------------------------------

        [Theory]
        [InlineData("res://scenes/level.tscn", "res://scenes/")]
        [InlineData("res://a/b/c.tres", "res://a/b/")]
        [InlineData("res://thing.tres", "res://")] // file directly under the project root
        [InlineData("  res://scenes/level.tscn  ", "res://scenes/")] // trims
        public void ParentDir_DerivesParentDirectory(string filePath, string expected)
        {
            Assert.Equal(expected, ResPathNormalizer.ParentDir(filePath));
        }

        [Fact]
        public void ParentDir_Rejects_NonResAndDirectory()
        {
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.ParentDir("/abs/level.tscn"));
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.ParentDir("res://scenes/")); // a dir, not a file
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.ParentDir(null));
        }

        [Fact]
        public void RequireResFilePath_Rejects_ParentTraversalSegment()
        {
            // A '..' segment would let res://a/../a/b.tres and res://a/b.tres compare unequal while denoting
            // the same file — reject it so the src==dst path-equality guards hold.
            var ex = Assert.Throws<ArgumentException>(
                () => ResPathNormalizer.RequireResFilePath("res://a/../a/b.tres", "p"));
            Assert.Contains("..", ex.Message);
        }

        [Fact]
        public void NormalizeDir_Rejects_ParentTraversalSegment()
        {
            Assert.Throws<ArgumentException>(() => ResPathNormalizer.NormalizeDir("res://a/../b"));
        }
    }
}
