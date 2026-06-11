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
    /// requirement). The downloaded binary is the SHARED GameDev-MCP-Server
    /// (https://github.com/IvanMurzak/GameDev-MCP-Server), pinned by <see cref="GodotMcpServerView.ServerVersion"/>
    /// — NOT the addon version. The editor download/launch/monitor side effects
    /// (<c>GodotMcpServerManager.cs</c>, <c>#if TOOLS</c>) are verified via the headless Godot smoke
    /// (test.md Suite 3) — NOT here.
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
            Assert.Equal("gamedev-mcp-server.exe", GodotMcpServerView.ExecutableFileName(OSPlatform.Windows));
            Assert.Equal("gamedev-mcp-server", GodotMcpServerView.ExecutableFileName(OSPlatform.Linux));
            Assert.Equal("gamedev-mcp-server", GodotMcpServerView.ExecutableFileName(OSPlatform.OSX));
        }

        [Fact]
        public void AssetZipName_IsExecutableDashRidDotZip()
        {
            Assert.Equal("gamedev-mcp-server-win-x64.zip",
                GodotMcpServerView.AssetZipName(OSPlatform.Windows, Architecture.X64));
            Assert.Equal("gamedev-mcp-server-linux-arm64.zip",
                GodotMcpServerView.AssetZipName(OSPlatform.Linux, Architecture.Arm64));
        }

        // --- Release tag (v-prefixed) ---

        [Theory]
        [InlineData("0.3.0", "v0.3.0")]
        [InlineData("1.2.3-beta", "v1.2.3-beta")]
        [InlineData(" 0.3.0 ", "v0.3.0")]
        public void ReleaseTag_PrependsV(string version, string expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ReleaseTag(version));
        }

        [Fact]
        public void ReleaseTag_AlreadyVPrefixed_IsNotDoublePrefixed()
        {
            // Defensive: a caller that already passed a v-tag must not get `vv0.3.0`.
            Assert.Equal("v0.3.0", GodotMcpServerView.ReleaseTag("v0.3.0"));
        }

        // --- Download URL ---

        [Fact]
        public void DownloadUrl_UsesVPrefixedReleaseTagAndRidAsset()
        {
            // GameDev-MCP-Server tags `v<version>` and attaches the server zips to THAT tag, so the URL
            // must carry the `v` prefix (a bare-version path 404s — issue #94).
            var url = GodotMcpServerView.DownloadUrl("8.0.0", OSPlatform.Windows, Architecture.X64);
            Assert.Equal(
                "https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v8.0.0/gamedev-mcp-server-win-x64.zip",
                url);
        }

        [Fact]
        public void DownloadUrl_VersionIsVerbatimUnderVTag_NoSemverNormalization()
        {
            // EXACT match like Unity — a pre-release version is used verbatim (no normalization), under the
            // v-prefixed release tag.
            var url = GodotMcpServerView.DownloadUrl("1.2.3-beta", OSPlatform.Linux, Architecture.X64);
            Assert.Contains("/releases/download/v1.2.3-beta/", url);
            Assert.EndsWith("gamedev-mcp-server-linux-x64.zip", url);
        }

        [Fact]
        public void DownloadUrl_DefaultOverload_UsesPinnedServerVersion()
        {
            // The production path (no version argument) MUST pin GodotMcpServerView.ServerVersion against
            // the shared GameDev-MCP-Server repo.
            var url = GodotMcpServerView.DownloadUrl(OSPlatform.Windows, Architecture.X64);
            Assert.Equal(
                GodotMcpServerView.DownloadUrl(GodotMcpServerView.ServerVersion, OSPlatform.Windows, Architecture.X64),
                url);
            Assert.StartsWith("https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/", url);
            Assert.Contains($"/releases/download/v{GodotMcpServerView.ServerVersion}/", url);
        }

        // --- Plugin-version source (parsed from plugin.cfg) ---

        [Fact]
        public void ParsePluginVersion_ReadsQuotedVersionFromPluginCfgText()
        {
            // The exact shape of addons/godot_mcp/plugin.cfg.
            const string cfg =
                "[plugin]\n\n" +
                "name=\"Godot-MCP\"\n" +
                "description=\"MCP integration for the Godot Editor.\"\n" +
                "author=\"Ivan Murzak\"\n" +
                "version=\"0.3.0\"\n" +
                "script=\"GodotMcpPlugin.cs\"\n";
            Assert.Equal("0.3.0", GodotMcpServerView.ParsePluginVersion(cfg));
        }

        [Theory]
        [InlineData("version=\"1.4.2\"\n", "1.4.2")]
        [InlineData("version = \"1.4.2\"\n", "1.4.2")]   // tolerant of spaces around '='
        [InlineData("version=1.4.2\n", "1.4.2")]         // tolerant of an unquoted value
        [InlineData("  version=\"1.4.2\"  \n", "1.4.2")] // tolerant of leading/trailing whitespace
        public void ParsePluginVersion_TolerantOfSpacingAndQuoting(string line, string expected)
        {
            Assert.Equal(expected, GodotMcpServerView.ParsePluginVersion("[plugin]\n" + line));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("[plugin]\nname=\"Godot-MCP\"\n")] // no version key
        [InlineData("version_extra=\"x\"\n")]          // a different key that merely starts with `version`
        public void ParsePluginVersion_MissingOrUnrelated_IsNull(string? cfg)
        {
            Assert.Null(GodotMcpServerView.ParsePluginVersion(cfg));
        }

        [Fact]
        public void DownloadUrl_UsesServerVersion_NotCommittedAddonVersion()
        {
            // The decoupling lock (the inverse of the old plugin.cfg-pin test): the addon version
            // (plugin.cfg, 0.x) and the shared GameDev-MCP-Server version (8.x) DIVERGE, and the download
            // URL must pin ServerVersion — NEVER the addon's own version (which has no release on the
            // shared repo and would 404).
            var cfgPath = FindRepoFile("addons/godot_mcp/plugin.cfg");
            Assert.True(cfgPath != null, "Could not locate addons/godot_mcp/plugin.cfg from the test assembly.");

            var addonVersion = GodotMcpServerView.ParsePluginVersion(System.IO.File.ReadAllText(cfgPath!));
            Assert.False(string.IsNullOrEmpty(addonVersion), "plugin.cfg must declare a non-empty version.");

            // The two version lines are decoupled by design (addon 0.x vs server 8.x).
            Assert.False(string.Equals(addonVersion, GodotMcpServerView.ServerVersion, System.StringComparison.Ordinal),
                "Addon version and pinned server version are expected to diverge — if they ever legitimately " +
                "coincide, revisit this lock rather than re-coupling the URL to the addon version.");

            var url = GodotMcpServerView.DownloadUrl(OSPlatform.Windows, Architecture.X64);
            Assert.Contains($"/releases/download/v{GodotMcpServerView.ServerVersion}/", url);
            Assert.DoesNotContain($"/releases/download/v{addonVersion}/", url);
        }

        /// <summary>
        /// Walk up from the test assembly location to find a repo-relative file, so the committed-plugin.cfg
        /// lock test does not depend on the test runner's working directory. Returns null when not found.
        /// </summary>
        static string? FindRepoFile(string relativePath)
        {
            var dir = new System.IO.DirectoryInfo(System.AppContext.BaseDirectory);
            for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = System.IO.Path.Combine(dir.FullName, relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                if (System.IO.File.Exists(candidate))
                    return candidate;
            }
            return null;
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

        // --- Orphan-process ownership (GodotMcpServerOwnership.IsOwnedByThisProject) ---
        // Driven with explicit string paths (no Path APIs, no filesystem) so the assertions are
        // deterministic and identical on Windows + Linux CI.

        [Fact]
        public void IsOwned_ExactSamePath_IsTrue()
        {
            var own = "C:/proj/.godot/mcp-server/win-x64/gamedev-mcp-server.exe";
            Assert.True(GodotMcpServerOwnership.IsOwnedByThisProject(own, own));
        }

        [Fact]
        public void IsOwned_SamePath_SeparatorAndCaseInsensitive()
        {
            // Windows backslash vs forward slash, mixed case — still our binary.
            var own = "C:/proj/.godot/mcp-server/win-x64/gamedev-mcp-server.exe";
            var candidate = @"c:\PROJ\.godot\mcp-server\win-x64\gamedev-mcp-server.exe";
            Assert.True(GodotMcpServerOwnership.IsOwnedByThisProject(candidate, own));
        }

        [Fact]
        public void IsOwned_SameCacheDir_IsTrue()
        {
            // A process whose exe sits in OUR cache platform folder is ours.
            var own = "/home/u/proj/.godot/mcp-server/linux-x64/gamedev-mcp-server";
            var candidate = "/home/u/proj/.godot/mcp-server/linux-x64/gamedev-mcp-server";
            Assert.True(GodotMcpServerOwnership.IsOwnedByThisProject(candidate, own));
        }

        [Fact]
        public void IsOwned_DifferentProjectCacheDir_IsFalse()
        {
            // The safety property: a DIFFERENT project's identically-named server binary is NEVER ours.
            var own = "/home/u/projA/.godot/mcp-server/linux-x64/gamedev-mcp-server";
            var other = "/home/u/projB/.godot/mcp-server/linux-x64/gamedev-mcp-server";
            Assert.False(GodotMcpServerOwnership.IsOwnedByThisProject(other, own));
        }

        [Fact]
        public void IsOwned_SiblingDirSharingNamePrefix_IsFalse()
        {
            // The trailing-slash bound prevents a sibling dir whose name merely shares a prefix from matching.
            var own = "/home/u/proj/.godot/mcp-server/linux-x64/gamedev-mcp-server";
            var sibling = "/home/u/proj/.godot/mcp-server/linux-x64-evil/gamedev-mcp-server";
            Assert.False(GodotMcpServerOwnership.IsOwnedByThisProject(sibling, own));
        }

        [Theory]
        [InlineData(null, "/x/gamedev-mcp-server")]
        [InlineData("", "/x/gamedev-mcp-server")]
        [InlineData("/x/gamedev-mcp-server", null)]
        [InlineData("/x/gamedev-mcp-server", "")]
        public void IsOwned_NullOrEmpty_FailsClosed(string? candidate, string? own)
        {
            Assert.False(GodotMcpServerOwnership.IsOwnedByThisProject(candidate, own));
        }
    }
}
