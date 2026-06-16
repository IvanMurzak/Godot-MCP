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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Connection.DevControl;
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed routing + parsing core of the DEV-ONLY inject/control bridge
    /// (<see cref="DevControlRouter"/>): the <c>(method, path)</c> → command-id route table, the connection /
    /// server status-string parsers, and the click-target vocabulary. The editor HttpListener transport
    /// (<c>DevControlServer.cs</c>, <c>#if TOOLS</c>) is verified live (curl the editor) — NOT here.
    /// </summary>
    public class DevControlRouterTests
    {
        // --- Route table -------------------------------------------------------------------------------------

        [Theory]
        [InlineData("GET", "/health", DevControlRouter.Command.Health)]
        [InlineData("GET", "/state", DevControlRouter.Command.State)]
        [InlineData("POST", "/inject/connection-status", DevControlRouter.Command.InjectConnectionStatus)]
        [InlineData("POST", "/inject/server-status", DevControlRouter.Command.InjectServerStatus)]
        [InlineData("POST", "/control/server-url", DevControlRouter.Command.ControlServerUrl)]
        [InlineData("POST", "/control/select-agent", DevControlRouter.Command.ControlSelectAgent)]
        [InlineData("POST", "/control/click", DevControlRouter.Command.ControlClick)]
        [InlineData("POST", "/control/set-segment", DevControlRouter.Command.ControlSetSegment)]
        [InlineData("POST", "/control/cloud-authorize", DevControlRouter.Command.ControlCloudAuthorize)]
        public void Route_MapsKnownRoutes(string method, string path, DevControlRouter.Command expected)
        {
            Assert.Equal(expected, DevControlRouter.Route(method, path));
        }

        [Theory]
        [InlineData("get", "/health")]
        [InlineData("GeT", "/state")]
        [InlineData("post", "/control/click")]
        public void Route_MethodIsCaseInsensitive(string method, string path)
        {
            Assert.NotEqual(DevControlRouter.Command.Unknown, DevControlRouter.Route(method, path));
        }

        [Theory]
        [InlineData("POST", "/health")]              // wrong method
        [InlineData("GET", "/control/click")]        // wrong method
        [InlineData("GET", "/nope")]                 // unknown path
        [InlineData("GET", "/HEALTH")]               // path is case-sensitive
        [InlineData("GET", "")]                      // empty path
        public void Route_UnknownReturnsUnknown(string method, string path)
        {
            Assert.Equal(DevControlRouter.Command.Unknown, DevControlRouter.Route(method, path));
        }

        [Fact]
        public void Route_NullArgs_ReturnUnknown()
        {
            Assert.Equal(DevControlRouter.Command.Unknown, DevControlRouter.Route(null!, "/health"));
            Assert.Equal(DevControlRouter.Command.Unknown, DevControlRouter.Route("GET", null!));
        }

        // --- Connection-status parser ------------------------------------------------------------------------

        [Theory]
        [InlineData("Connected", ConnectionStatus.Connected)]
        [InlineData("connecting", ConnectionStatus.Connecting)]
        [InlineData("DISCONNECTED", ConnectionStatus.Disconnected)]
        [InlineData("  Connected  ", ConnectionStatus.Connected)]
        public void TryParseConnectionStatus_ParsesKnown(string value, ConnectionStatus expected)
        {
            Assert.True(DevControlRouter.TryParseConnectionStatus(value, out var status));
            Assert.Equal(expected, status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("bogus")]
        public void TryParseConnectionStatus_RejectsUnknown(string? value)
        {
            Assert.False(DevControlRouter.TryParseConnectionStatus(value, out _));
        }

        // --- Server-status parser ----------------------------------------------------------------------------

        [Theory]
        [InlineData("Stopped", GodotMcpServerStatus.Stopped)]
        [InlineData("starting", GodotMcpServerStatus.Starting)]
        [InlineData("RUNNING", GodotMcpServerStatus.Running)]
        [InlineData("Stopping", GodotMcpServerStatus.Stopping)]
        [InlineData("external", GodotMcpServerStatus.External)]
        public void TryParseServerStatus_ParsesKnown(string value, GodotMcpServerStatus expected)
        {
            Assert.True(DevControlRouter.TryParseServerStatus(value, out var status));
            Assert.Equal(expected, status);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("paused")]
        public void TryParseServerStatus_RejectsUnknown(string? value)
        {
            Assert.False(DevControlRouter.TryParseServerStatus(value, out _));
        }

        // --- Click-target vocabulary -------------------------------------------------------------------------

        [Theory]
        [InlineData("configure", "configure")]
        [InlineData("Reconfigure", "reconfigure")]
        [InlineData("REMOVE", "remove")]
        [InlineData("connect", "connect")]
        [InlineData("start-server", "start-server")]
        [InlineData("  generate ", "generate")]
        [InlineData("Authorize", "authorize")]
        [InlineData("REVOKE", "revoke")]
        [InlineData("reveal", "reveal")]
        [InlineData("Copy", "copy")]
        [InlineData("generate-token", "generate-token")]
        public void TryNormalizeClickTarget_NormalizesKnown(string target, string expected)
        {
            Assert.True(DevControlRouter.TryNormalizeClickTarget(target, out var normalized));
            Assert.Equal(expected, normalized);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("explode")]
        [InlineData("start server")]
        public void TryNormalizeClickTarget_RejectsUnknown(string? target)
        {
            Assert.False(DevControlRouter.TryNormalizeClickTarget(target, out var normalized));
            Assert.Equal(string.Empty, normalized);
        }

        // --- Segmented-control vocabulary --------------------------------------------------------------------

        [Theory]
        [InlineData("mode", "custom", "mode", 0)]
        [InlineData("mode", "Cloud", "mode", 1)]
        [InlineData("MODE", "  cloud ", "mode", 1)]
        [InlineData("auth", "none", "auth", 0)]
        [InlineData("Auth", "REQUIRED", "auth", 1)]
        public void TryNormalizeSegment_NormalizesKnown(string control, string option, string expectedControl, int expectedIndex)
        {
            Assert.True(DevControlRouter.TryNormalizeSegment(control, option, out var normControl, out var index));
            Assert.Equal(expectedControl, normControl);
            Assert.Equal(expectedIndex, index);
        }

        [Theory]
        [InlineData(null, "custom")]      // null control
        [InlineData("mode", null)]        // null option
        [InlineData("", "")]              // empty
        [InlineData("transport", "http")] // unknown control
        [InlineData("mode", "offline")]   // unknown option for a known control
        [InlineData("auth", "cloud")]     // option from the wrong control
        public void TryNormalizeSegment_RejectsUnknown(string? control, string? option)
        {
            Assert.False(DevControlRouter.TryNormalizeSegment(control, option, out var normControl, out var index));
            Assert.Equal(string.Empty, normControl);
            Assert.Equal(-1, index);
        }
    }
}
