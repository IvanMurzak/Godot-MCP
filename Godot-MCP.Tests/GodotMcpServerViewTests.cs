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
using System.Runtime.InteropServices;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed local-server hosting logic (<see cref="GodotMcpServerView"/>): the platform→RID
    /// /asset-name mapping, the EXACT version-match, the GitHub release download-URL construction, the
    /// launch-argument builder, the status→button-text/label/circle-state mappings, and the configured-host→
    /// port extraction. Every assertion is a deterministic string/enum transform with NO platform divergence,
    /// so the SAME assertions hold on the Linux CI runner and on a Windows dev box (the issue's cross-platform
    /// requirement). The editor download/launch/monitor side effects (<c>GodotMcpServerManager.cs</c>,
    /// <c>#if TOOLS</c>) are verified via the headless Godot smoke (test.md Suite 3) and are operator-pending
    /// until a real GitHub release publishes the server binaries — NOT here.
    /// </summary>
    public class GodotMcpServerViewTests
    {
        // --- Platform mapping (os + arch -> RID), driven explicitly so the tests are platform-independent ---

        [Theory]
        [InlineData("Windows", "win")]
        [InlineData("OSX", "osx")]
        [InlineData("Linux", "linux")]
        public void OsToken_MapsEachOs(string osName, string expected)
        {
            var os = osName switch
            {
                "Windows" => OSPlatform.Windows,
                "OSX" => OSPlatform.OSX,
                _ => OSPlatform.Linux
            };
            Assert.Equal(expected, GodotMcpServerView.OsToken(os));
        }

        [Fact]
        public void OsToken_UnknownOs_IsUnknown()
        {
            Assert.Equal("unknown", GodotMcpServerView.OsToken(OSPlatform.Create("freebsd")));
        }

        [Theory]
        [InlineData(Architecture.X86, "x86")]
        [InlineData(Architecture.X64, "x64")]
        [InlineData(Architecture.Arm, "arm")]
        [InlineData(Architecture.Arm64, "arm64")]
        public void ArchToken_MapsEachArch(Architecture arch, string expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ArchToken(arch));
        }

        [Fact]
        public void PlatformName_IsOsDashArch()
        {
            Assert.Equal("win-x64", GodotMcpServerView.PlatformName(OSPlatform.Windows, Architecture.X64));
            Assert.Equal("osx-arm64", GodotMcpServerView.PlatformName(OSPlatform.OSX, Architecture.Arm64));
            Assert.Equal("linux-x64", GodotMcpServerView.PlatformName(OSPlatform.Linux, Architecture.X64));
        }

        [Fact]
        public void PlatformName_CoversAllSevenReleaseRids()
        {
            // The release workflow (build-all.sh) emits exactly these 7 RIDs; the asset names must match 1:1.
            Assert.Equal("win-x64", GodotMcpServerView.PlatformName(OSPlatform.Windows, Architecture.X64));
            Assert.Equal("win-x86", GodotMcpServerView.PlatformName(OSPlatform.Windows, Architecture.X86));
            Assert.Equal("win-arm64", GodotMcpServerView.PlatformName(OSPlatform.Windows, Architecture.Arm64));
            Assert.Equal("linux-x64", GodotMcpServerView.PlatformName(OSPlatform.Linux, Architecture.X64));
            Assert.Equal("linux-arm64", GodotMcpServerView.PlatformName(OSPlatform.Linux, Architecture.Arm64));
            Assert.Equal("osx-x64", GodotMcpServerView.PlatformName(OSPlatform.OSX, Architecture.X64));
            Assert.Equal("osx-arm64", GodotMcpServerView.PlatformName(OSPlatform.OSX, Architecture.Arm64));
        }

        // --- Executable / asset names ---

        [Fact]
        public void ExecutableFileName_HasExeSuffixOnWindowsOnly()
        {
            Assert.Equal("godot-mcp-server.exe", GodotMcpServerView.ExecutableFileName(OSPlatform.Windows));
            Assert.Equal("godot-mcp-server", GodotMcpServerView.ExecutableFileName(OSPlatform.Linux));
            Assert.Equal("godot-mcp-server", GodotMcpServerView.ExecutableFileName(OSPlatform.OSX));
        }

        [Fact]
        public void AssetZipName_IsExecutableDashRidDotZip()
        {
            Assert.Equal("godot-mcp-server-win-x64.zip",
                GodotMcpServerView.AssetZipName(OSPlatform.Windows, Architecture.X64));
            Assert.Equal("godot-mcp-server-linux-arm64.zip",
                GodotMcpServerView.AssetZipName(OSPlatform.Linux, Architecture.Arm64));
        }

        // --- Download URL ---

        [Fact]
        public void DownloadUrl_UsesExactVersionAndRidAsset()
        {
            var url = GodotMcpServerView.DownloadUrl("0.1.0", OSPlatform.Windows, Architecture.X64);
            Assert.Equal(
                "https://github.com/IvanMurzak/Godot-MCP/releases/download/0.1.0/godot-mcp-server-win-x64.zip",
                url);
        }

        [Fact]
        public void DownloadUrl_VersionIsVerbatim_NoSemverNormalization()
        {
            // EXACT match like Unity — a 'v'-prefixed or pre-release version is used verbatim, not normalized.
            var url = GodotMcpServerView.DownloadUrl("1.2.3-beta", OSPlatform.Linux, Architecture.X64);
            Assert.Contains("/releases/download/1.2.3-beta/", url);
            Assert.EndsWith("godot-mcp-server-linux-x64.zip", url);
        }

        // --- Version match (EXACT) ---

        [Fact]
        public void VersionMatches_ExactEqual_IsTrue()
        {
            Assert.True(GodotMcpServerView.VersionMatches("0.1.0", "0.1.0"));
        }

        [Fact]
        public void VersionMatches_TrimsSurroundingWhitespace()
        {
            // The cached `version` file may carry a trailing newline; that still matches.
            Assert.True(GodotMcpServerView.VersionMatches("0.1.0\n", "0.1.0"));
        }

        [Theory]
        [InlineData("0.1.0", "0.2.0")]
        [InlineData("0.1.0", "0.1.1")]
        [InlineData(null, "0.1.0")]
        [InlineData("", "0.1.0")]
        public void VersionMatches_DifferentOrMissing_IsFalse(string? cached, string addon)
        {
            Assert.False(GodotMcpServerView.VersionMatches(cached, addon));
        }

        // --- Launch argument builder ---

        [Fact]
        public void BuildLaunchArguments_NoAuth_OmitsToken()
        {
            var args = GodotMcpServerView.BuildLaunchArguments(port: 8080, pluginTimeoutMs: 10000, authRequired: false, token: "secret");
            Assert.Equal("port=8080 plugin-timeout=10000 client-transport=streamableHttp authorization=none", args);
            Assert.DoesNotContain("token=", args);
            Assert.DoesNotContain("secret", args);
        }

        [Fact]
        public void BuildLaunchArguments_AuthRequiredWithToken_AppendsToken()
        {
            var args = GodotMcpServerView.BuildLaunchArguments(port: 5300, pluginTimeoutMs: 12000, authRequired: true, token: "abc123");
            Assert.Equal("port=5300 plugin-timeout=12000 client-transport=streamableHttp authorization=required token=abc123", args);
        }

        [Fact]
        public void BuildLaunchArguments_AuthRequiredButNoToken_OmitsTokenArg()
        {
            var args = GodotMcpServerView.BuildLaunchArguments(port: 5300, pluginTimeoutMs: 10000, authRequired: true, token: null);
            Assert.Equal("port=5300 plugin-timeout=10000 client-transport=streamableHttp authorization=required", args);
            Assert.DoesNotContain("token=", args);
        }

        [Fact]
        public void BuildLaunchArguments_AlwaysStreamableHttp_NeverStdio()
        {
            // The transport is always streamableHttp — we deliberately do NOT build a stdio launch path
            // the plugin's SignalR client cannot consume.
            var args = GodotMcpServerView.BuildLaunchArguments(port: 8080, pluginTimeoutMs: 10000, authRequired: false, token: null);
            Assert.Contains($"client-transport={McpServerConsts.TransportMethod.streamableHttp}", args);
            Assert.DoesNotContain(McpServerConsts.TransportMethod.stdio.ToString(), args);
        }

        [Fact]
        public void BuildLaunchArguments_UsesCanonicalArgKeys()
        {
            // The keys must be the McpPlugin canonical arg names the server parses.
            var args = GodotMcpServerView.BuildLaunchArguments(port: 9, pluginTimeoutMs: 1, authRequired: true, token: "t");
            Assert.StartsWith($"{McpServerConsts.Args.Port}=9 ", args);
            Assert.Contains($" {McpServerConsts.Args.PluginTimeout}=1 ", args);
            Assert.Contains($" {McpServerConsts.Args.ClientTransportMethod}=", args);
            Assert.Contains($" {McpServerConsts.Args.Authorization}={McpServerConsts.AuthOption.required}", args);
            Assert.EndsWith($" {McpServerConsts.Args.Token}=t", args);
        }

        // --- Status -> button text / disabled ---

        [Theory]
        [InlineData(GodotMcpServerStatus.Stopped, GodotMcpServerView.ButtonTextStart)]
        [InlineData(GodotMcpServerStatus.Starting, GodotMcpServerView.ButtonTextStarting)]
        [InlineData(GodotMcpServerStatus.Running, GodotMcpServerView.ButtonTextStop)]
        [InlineData(GodotMcpServerStatus.Stopping, GodotMcpServerView.ButtonTextStopping)]
        [InlineData(GodotMcpServerStatus.External, GodotMcpServerView.ButtonTextExternal)]
        public void ServerButtonText_MapsEachStatus(GodotMcpServerStatus status, string expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ServerButtonText(status));
        }

        [Theory]
        [InlineData(GodotMcpServerStatus.Stopped, false)]
        [InlineData(GodotMcpServerStatus.Starting, true)]
        [InlineData(GodotMcpServerStatus.Running, false)]
        [InlineData(GodotMcpServerStatus.Stopping, true)]
        [InlineData(GodotMcpServerStatus.External, true)]
        public void ServerButtonDisabled_DisabledDuringTransientsAndExternal(GodotMcpServerStatus status, bool disabled)
        {
            Assert.Equal(disabled, GodotMcpServerView.ServerButtonDisabled(status));
        }

        // --- Status -> label ---

        [Fact]
        public void ServerStatusLabel_HasDistinctTextPerStatus()
        {
            Assert.Equal("Local server: Stopped", GodotMcpServerView.ServerStatusLabel(GodotMcpServerStatus.Stopped));
            Assert.Equal("Local server: Running", GodotMcpServerView.ServerStatusLabel(GodotMcpServerStatus.Running));
            Assert.Contains("Starting", GodotMcpServerView.ServerStatusLabel(GodotMcpServerStatus.Starting));
            Assert.Contains("Stopping", GodotMcpServerView.ServerStatusLabel(GodotMcpServerStatus.Stopping));
            Assert.Contains("External", GodotMcpServerView.ServerStatusLabel(GodotMcpServerStatus.External));
        }

        // --- Status -> timeline circle state ---

        [Theory]
        [InlineData(GodotMcpServerStatus.Running, ConnectionPanelView.TimelinePointState.Online)]
        [InlineData(GodotMcpServerStatus.External, ConnectionPanelView.TimelinePointState.Online)]
        [InlineData(GodotMcpServerStatus.Starting, ConnectionPanelView.TimelinePointState.Connecting)]
        [InlineData(GodotMcpServerStatus.Stopping, ConnectionPanelView.TimelinePointState.Connecting)]
        [InlineData(GodotMcpServerStatus.Stopped, ConnectionPanelView.TimelinePointState.Disconnected)]
        public void ServerPointState_MapsEachStatus(GodotMcpServerStatus status, ConnectionPanelView.TimelinePointState expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ServerPointState(status));
        }

        [Fact]
        public void ServerStatusColor_ReusesConnectionPalette()
        {
            Assert.Equal(ConnectionPanelView.ColorConnected, GodotMcpServerView.ServerStatusColor(GodotMcpServerStatus.Running));
            Assert.Equal(ConnectionPanelView.ColorConnecting, GodotMcpServerView.ServerStatusColor(GodotMcpServerStatus.Starting));
            Assert.Equal(ConnectionPanelView.ColorDisconnected, GodotMcpServerView.ServerStatusColor(GodotMcpServerStatus.Stopped));
        }

        // --- Port extraction from the configured Custom host URL ---

        [Theory]
        [InlineData("http://localhost:5300", 5300)]
        [InlineData("http://127.0.0.1:8080", 8080)]
        [InlineData("https://example.com:443", 443)]
        public void ResolveServerPort_ParsesExplicitPort(string url, int expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ResolveServerPort(url, defaultPort: 9999));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not a url")]
        public void ResolveServerPort_MissingOrUnparseable_FallsBackToDefault(string? url)
        {
            Assert.Equal(9999, GodotMcpServerView.ResolveServerPort(url, defaultPort: 9999));
        }

        [Fact]
        public void ResolveServerPort_NoExplicitPort_UsesSchemeDefaultWhenAbsolute()
        {
            // An absolute http URL with no explicit port resolves to the scheme's default (80), not the
            // fallback — Uri fills it in. (Documents the behavior so the manager's port choice is predictable.)
            Assert.Equal(80, GodotMcpServerView.ResolveServerPort("http://localhost", defaultPort: 9999));
        }
    }
}
