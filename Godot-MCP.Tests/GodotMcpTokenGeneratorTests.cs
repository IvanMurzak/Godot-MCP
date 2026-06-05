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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the URL-safe bearer-token generator. The output is cryptographically random, so the tests
    /// assert FORMAT invariants (charset, url-safety, length, uniqueness) rather than a fixed value —
    /// mirroring the Unity reference's <c>UnityMcpPlugin.GenerateToken()</c> contract.
    /// </summary>
    public class GodotMcpTokenGeneratorTests
    {
        [Fact]
        public void Generate_IsNonEmpty()
        {
            Assert.False(string.IsNullOrEmpty(GodotMcpTokenGenerator.Generate()));
        }

        [Fact]
        public void Generate_UsesOnlyUrlSafeChars()
        {
            // URL-safe Base64 alphabet: A-Z a-z 0-9 '-' '_'. No '+', '/', or '=' padding.
            var token = GodotMcpTokenGenerator.Generate();
            Assert.All(token, c =>
                Assert.True(
                    char.IsLetterOrDigit(c) || c == '-' || c == '_',
                    $"unexpected char '{c}' in token"));
        }

        [Theory]
        [InlineData('+')]
        [InlineData('/')]
        [InlineData('=')]
        public void Generate_StripsNonUrlSafeChars(char forbidden)
        {
            // Run several times: the random input could otherwise hide an unstripped char by chance.
            for (var i = 0; i < 50; i++)
                Assert.DoesNotContain(forbidden, GodotMcpTokenGenerator.Generate());
        }

        [Fact]
        public void Generate_HasExpectedLength()
        {
            // 32 bytes -> Base64 is ceil(32/3)*4 = 44 chars, minus the two trailing '=' padding chars
            // that the generator strips => 43 chars. Charset substitutions don't change the length.
            var token = GodotMcpTokenGenerator.Generate();
            Assert.Equal(43, token.Length);
        }

        [Fact]
        public void Generate_ProducesUniqueValues()
        {
            // Random => practically-zero collision probability across a small sample.
            var tokens = new HashSet<string>(Enumerable.Range(0, 100).Select(_ => GodotMcpTokenGenerator.Generate()));
            Assert.Equal(100, tokens.Count);
        }
    }
}
