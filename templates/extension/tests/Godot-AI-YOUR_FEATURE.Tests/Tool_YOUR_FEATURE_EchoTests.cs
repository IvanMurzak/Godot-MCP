/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.YOUR_FEATURE;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE.Tests
{
    /// <summary>
    /// One-test-per-tool unit scaffold (the Unity/Unreal template parity pattern). Pins the
    /// PURE-MANAGED sample tool <c>YOUR_TOOL_PREFIX-echo</c> by constructing the tool family and
    /// invoking the method directly — no Godot binary, no MCP server. Copy this class per pure-managed
    /// tool you add; verify editor-only tools (#if TOOLS) via the headless-Godot E2E instead.
    /// </summary>
    public class Tool_YOUR_FEATURE_EchoTests
    {
        [Fact]
        public void Echo_WithMessage_ReturnsItVerbatim()
        {
            var tool = new Tool_YOUR_FEATURE();
            Assert.Equal("hello-godot", tool.Echo("hello-godot"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void Echo_WithoutMessage_ReturnsReadySentinel(string? message)
        {
            var tool = new Tool_YOUR_FEATURE();
            Assert.Equal("YOUR_TOOL_PREFIX-ready", tool.Echo(message));
        }

        [Fact]
        public void EchoToolId_IsStable()
        {
            // The id the dock / godot-cli / catalog reference must not drift silently.
            Assert.Equal("YOUR_TOOL_PREFIX-echo", Tool_YOUR_FEATURE.EchoToolId);
        }
    }
}
