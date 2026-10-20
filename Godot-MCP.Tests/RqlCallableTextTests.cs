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
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>The text form of a Callable ("target::method", CallableText).</summary>
    public class RqlCallableTextTests
    {
        [Theory]
        [InlineData("Player::_on_hit", "Player", "_on_hit")]
        [InlineData(" Level/Player :: hide ", "Level/Player", "hide")]
        [InlineData(".::queue_free", ".", "queue_free")]
        [InlineData("res://ui/menu.tres::Script_x1::_ready", "res://ui/menu.tres::Script_x1", "_ready")]
        [InlineData("Node3D#123456::show", "Node3D#123456", "show")]
        public void TargetAndMethodSplitAtTheLastSeparator(string text, string target, string method)
        {
            Assert.True(CallableText.TryParse(text, out var t, out var m, out var reason), reason);
            Assert.Equal(target, t);
            Assert.Equal(method, m);
            Assert.Equal(target + "::" + method, CallableText.Format(t, m));
        }

        [Theory]
        [InlineData("Player")]
        [InlineData("::hide")]
        [InlineData("Player::")]
        [InlineData("Player::not a method")]
        [InlineData("")]
        [InlineData(null)]
        public void OtherTextIsRejectedWithTheForm(string? text)
        {
            Assert.False(CallableText.TryParse(text, out _, out _, out var reason));
            Assert.False(string.IsNullOrEmpty(reason));
        }

        [Fact]
        public void ReferencesAreClassAndInstanceId()
        {
            var m = CallableText.Reference.Match("StaticBody3D#1598684208646");
            Assert.True(m.Success);
            Assert.Equal("StaticBody3D", m.Groups["type"].Value);
            Assert.Equal("1598684208646", m.Groups["id"].Value);
            Assert.False(CallableText.Reference.IsMatch("Level/Player"));
            Assert.False(CallableText.Reference.IsMatch("res://a.tres"));
        }
    }
}
