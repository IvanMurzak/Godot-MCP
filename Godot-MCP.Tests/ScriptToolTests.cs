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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the script tool family: the <see cref="ScriptInfo"/> result model and the
    /// <see cref="ScriptLang_"/> extension/path helper. Neither touches a live Godot scripting runtime, so
    /// they run in the plain xUnit host with no Godot binary. The editor-driving handlers themselves
    /// (<c>Tool_Script.*.cs</c>, behind <c>#if TOOLS</c>) — including the GDScript parse check and the C#
    /// build settle — are verified by the headless Godot smoke (test.md Suite 3).
    /// </summary>
    public class ScriptToolTests
    {
        // ---- ScriptLang_.TryGetLang / IsScriptExtension ------------------------------------------

        [Theory]
        [InlineData("res://scripts/Enemy.cs", true, ScriptLang.CSharp)]
        [InlineData("res://scripts/player.gd", true, ScriptLang.GDScript)]
        // Case-insensitive extension match.
        [InlineData("res://scripts/Enemy.CS", true, ScriptLang.CSharp)]
        [InlineData("res://scripts/Player.GD", true, ScriptLang.GDScript)]
        // Non-script extensions.
        [InlineData("res://scenes/main.tscn", false, ScriptLang.CSharp)]
        [InlineData("res://materials/wood.tres", false, ScriptLang.CSharp)]
        [InlineData("res://notes.txt", false, ScriptLang.CSharp)]
        public void TryGetLang_DetectsByExtension(string path, bool expected, ScriptLang expectedLang)
        {
            var ok = ScriptLang_.TryGetLang(path, out var lang);
            Assert.Equal(expected, ok);
            Assert.Equal(expected, ScriptLang_.IsScriptExtension(path));
            if (ok)
                Assert.Equal(expectedLang, lang);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void TryGetLang_EmptyOrNull_ReturnsFalse(string? path)
        {
            Assert.False(ScriptLang_.TryGetLang(path, out _));
            Assert.False(ScriptLang_.IsScriptExtension(path));
        }

        [Theory]
        [InlineData(ScriptLang.CSharp, ".cs")]
        [InlineData(ScriptLang.GDScript, ".gd")]
        public void ExtensionFor_ReturnsCanonicalExtension(ScriptLang lang, string expected)
        {
            Assert.Equal(expected, ScriptLang_.ExtensionFor(lang));
        }

        // ---- ScriptLang_.RequireScriptResPath ----------------------------------------------------

        [Fact]
        public void RequireScriptResPath_AcceptsCSharp_TrimsWhitespace_DetectsLang()
        {
            var p = ScriptLang_.RequireScriptResPath("  res://scripts/Enemy.cs  ", "scriptPath", out var lang);
            Assert.Equal("res://scripts/Enemy.cs", p);
            Assert.Equal(ScriptLang.CSharp, lang);
        }

        [Fact]
        public void RequireScriptResPath_AcceptsGDScript()
        {
            var p = ScriptLang_.RequireScriptResPath("res://player.gd", "scriptPath", out var lang);
            Assert.Equal("res://player.gd", p);
            Assert.Equal(ScriptLang.GDScript, lang);
        }

        [Fact]
        public void RequireScriptResPath_Rejects_NonScriptExtension()
        {
            // It is a valid res:// file path, but not a .cs/.gd — must be rejected with a language-specific
            // message (the shared ResPathNormalizer would have accepted it).
            var ex = Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("res://scenes/main.tscn", "scriptPath", out _));
            Assert.Contains(".cs", ex.Message);
            Assert.Contains(".gd", ex.Message);
        }

        [Fact]
        public void RequireScriptResPath_Rejects_NonResPath()
        {
            Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("/abs/Enemy.cs", "scriptPath", out _));
            Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("user://Enemy.cs", "scriptPath", out _));
        }

        [Fact]
        public void RequireScriptResPath_Rejects_DirectoryAndBareScheme()
        {
            // Directory (trailing slash) and the bare 'res://' root are not files — rejected by the shared
            // res:// guards before the extension check runs.
            Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("res://scripts/", "scriptPath", out _));
            Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("res://", "scriptPath", out _));
            Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath(null, "scriptPath", out _));
        }

        [Fact]
        public void RequireScriptResPath_Rejects_ParentTraversalSegment()
        {
            // The shared '..' guard applies — res://a/../a/b.cs must be rejected so path-equality guards hold.
            var ex = Assert.Throws<ArgumentException>(
                () => ScriptLang_.RequireScriptResPath("res://a/../a/Enemy.cs", "scriptPath", out _));
            Assert.Contains("..", ex.Message);
        }

        // ---- ScriptInfo serialization ------------------------------------------------------------

        [Fact]
        public void ScriptInfo_Serializes_WithExpectedJsonNames()
        {
            var info = new ScriptInfo
            {
                ResourcePath = "res://scripts/player.gd",
                Language = "GDScript",
                Content = "extends Node\n",
                LineCount = 2,
                Status = "Script read.",
            };

            var json = JsonSerializer.Serialize(info);
            Assert.Contains("\"resourcePath\"", json);
            Assert.Contains("\"language\"", json);
            Assert.Contains("\"content\"", json);
            Assert.Contains("\"lineCount\"", json);
            Assert.Contains("\"status\"", json);

            var restored = JsonSerializer.Deserialize<ScriptInfo>(json);
            Assert.NotNull(restored);
            Assert.Equal("res://scripts/player.gd", restored!.ResourcePath);
            Assert.Equal("GDScript", restored.Language);
            Assert.Equal("extends Node\n", restored.Content);
            Assert.Equal(2, restored.LineCount);
            Assert.Equal("Script read.", restored.Status);
        }

        [Fact]
        public void ScriptInfo_IdentityOnly_AllowsNullContentAndStatus()
        {
            var info = new ScriptInfo { ResourcePath = "res://scripts/Enemy.cs", Language = "CSharp" };
            var json = JsonSerializer.Serialize(info);
            var restored = JsonSerializer.Deserialize<ScriptInfo>(json);

            Assert.NotNull(restored);
            Assert.Null(restored!.Content);
            Assert.Null(restored.Status);
            Assert.Equal(0, restored.LineCount);
        }
    }
}
