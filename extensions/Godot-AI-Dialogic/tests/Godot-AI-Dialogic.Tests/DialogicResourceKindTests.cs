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
using com.IvanMurzak.Godot.MCP.Dialogic;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Dialogic.Tests
{
    /// <summary>Unit spec for the pure-managed resource-kind-by-extension classifier.</summary>
    public class DialogicResourceKindTests
    {
        [Theory]
        [InlineData("res://timelines/intro.dtl", DialogicResourceKind.Timeline)]
        [InlineData("res://characters/hero.DCH", DialogicResourceKind.Character)]
        public void FromPath_ClassifiesByExtension(string path, string expected)
        {
            Assert.Equal(expected, DialogicResourceKind.FromPath(path));
        }

        [Theory]
        [InlineData("res://x.tscn")]
        [InlineData("res://x")]
        [InlineData("")]
        [InlineData(null)]
        public void FromPath_RejectsUnsupported(string? path)
        {
            Assert.Throws<ArgumentException>(() => DialogicResourceKind.FromPath(path));
        }

        [Theory]
        [InlineData("a.dtl", "dtl")]
        [InlineData("a.DCH", "dch")]
        [InlineData("noext", "")]
        [InlineData("trailingdot.", "")]
        [InlineData(null, "")]
        public void ExtensionOf_LowerCasesAndStrips(string? path, string expected)
        {
            Assert.Equal(expected, DialogicResourceKind.ExtensionOf(path));
        }
    }
}
