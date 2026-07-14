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
using System.IO;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.McpPlugin.AgentConfig;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the mcp-authorize e1 (PR 3) project-identity wiring: the instance-metadata handshake
    /// payload, the <see cref="ProjectIdentity"/> derivation (golden-vector parity — the pin + derived
    /// port MUST match the committed cross-language vectors byte-for-byte), and the project-marker
    /// read/write + <c>serverTarget</c> → connection-mode resolution. All pure-managed over McpPlugin
    /// 7.0's shared primitives (no Godot native types, no <c>#if TOOLS</c>), so they run in the plain
    /// xUnit host on the Linux CI runner. The editor-side Godot project-root/name resolution lives in
    /// <c>GodotMcpConnection</c> and is verified via the headless smoke (test.md Suite 3).
    /// </summary>
    public class GodotProjectIdentityTests
    {
        // === Instance-metadata handshake payload (design 04) =========================================

        [Fact]
        public void BuildInstanceMetadata_StampsGodotEngine_AndCarriesTheSuppliedFields()
        {
            var meta = GodotProjectIdentity.BuildInstanceMetadata(
                projectRoot: "C:/Games/MyGame",
                projectName: "MyGame",
                instanceId: "11111111-2222-3333-4444-555555555555",
                machineName: "DESKTOP-X");

            Assert.Equal("godot", meta.Engine);
            Assert.Equal("MyGame", meta.ProjectName);
            Assert.Equal("11111111-2222-3333-4444-555555555555", meta.InstanceId);
            Assert.Equal("DESKTOP-X", meta.MachineName);
        }

        [Fact]
        public void BuildInstanceMetadata_ProjectPathHash_IsTheStablePinSource()
        {
            var meta = GodotProjectIdentity.BuildInstanceMetadata(
                "C:/Games/MyGame", "MyGame", "id", "host");

            // The metadata hash is exactly the ProjectIdentity project-path hash (same normalization),
            // and the routing pin is its first 8 hex chars (design 04 — "pin = first 8 hex of the
            // ProjectIdentity SHA256"). This is what ties an agent session's pin to this instance.
            Assert.Equal(ProjectIdentity.DeriveProjectPathHash("C:/Games/MyGame"), meta.ProjectPathHash);
            Assert.Equal(64, meta.ProjectPathHash.Length);
            Assert.Equal(ProjectIdentity.DerivePin("C:/Games/MyGame"), meta.ProjectPathHash.Substring(0, 8));
        }

        [Fact]
        public void BuildInstanceMetadata_ToQuery_CarriesTheFiveHandshakeKeys()
        {
            var meta = GodotProjectIdentity.BuildInstanceMetadata(
                "C:/Games/MyGame", "MyGame", "the-instance-id", "DESKTOP-X");

            var query = meta.ToQuery();

            Assert.Equal("the-instance-id", query["instance_id"]);
            Assert.Equal("godot", query["engine"]);
            Assert.Equal("MyGame", query["project_name"]);
            Assert.Equal(meta.ProjectPathHash, query["project_path_hash"]);
            Assert.Equal("DESKTOP-X", query["machine_name"]);
        }

        [Fact]
        public void BuildInstanceMetadata_AppendToUrl_UsesTheRightQuerySeparator()
        {
            var meta = GodotProjectIdentity.BuildInstanceMetadata(
                "C:/Games/MyGame", "MyGame", "id", "host");

            // A query-less URL gets a '?'; a URL that already has a query gets an '&'. Either way the
            // five handshake params are appended (the client encodes the metadata into the hub URL).
            Assert.Contains("?instance_id=id&", meta.AppendToUrl("https://ai-game.dev/mcp"));
            Assert.Contains("&instance_id=id&", meta.AppendToUrl("http://localhost:8080/hub/mcp-server?x=1"));
        }

        [Fact]
        public void SessionInstanceId_IsAStableNonEmptyGuidForTheSession()
        {
            Assert.False(string.IsNullOrWhiteSpace(GodotProjectIdentity.SessionInstanceId));
            Assert.True(Guid.TryParse(GodotProjectIdentity.SessionInstanceId, out _));
            // Stable within the session (same value on repeated reads — minted once per loaded assembly).
            Assert.Equal(GodotProjectIdentity.SessionInstanceId, GodotProjectIdentity.SessionInstanceId);
        }

        // === ProjectIdentity derivation — golden-vector parity =======================================
        //
        // These pin the committed cross-language vectors. The derivation is McpPlugin 7.0's canonical
        // ProjectIdentity (Normalize = trim trailing separators + ToLowerInvariant, NO separator
        // conversion; SHA256; pin = first 8 hex; port = MinPort + LE-uint32(hash) % PortRange). If a
        // McpPlugin bump ever changes the math, these fail — that is the intended tripwire (the Godot
        // pin/port must stay byte-identical to the Unity/CLI derivation).

        [Theory]
        [InlineData("C:/Games/MyGame", "360f52fb", 29062)]
        [InlineData("/home/user/MyGame", "465fe842", 24998)]
        [InlineData("/home/user/Demo Project", "705ccffb", 20832)]
        public void Derive_MatchesGoldenVectors(string projectRoot, string expectedPin, int expectedPort)
        {
            var identity = GodotProjectIdentity.Derive(projectRoot, marker: null);

            Assert.Equal(expectedPin, identity.Pin);
            Assert.Equal(expectedPort, identity.Port);
            Assert.False(identity.PortIsOverridden);
            // The derived port always lands in the shared 20000–29999 band (D14/D15).
            Assert.InRange(identity.Port, ProjectIdentity.MinPort, ProjectIdentity.MaxPort);
        }

        [Theory]
        [InlineData("C:/Games/MyGame/")] // trailing separator trimmed
        [InlineData("c:/games/mygame")]  // ToLowerInvariant
        [InlineData("C:/Games/MyGame")]  // canonical
        public void Derive_IsTrailingSlashAndCaseInsensitive(string projectRoot)
        {
            var identity = GodotProjectIdentity.Derive(projectRoot, marker: null);

            Assert.Equal("360f52fb", identity.Pin);
            Assert.Equal(29062, identity.Port);
        }

        [Fact]
        public void Derive_DoesNotConvertSeparators_SoBackslashDiffersFromForwardSlash()
        {
            // The normalization ToLowerInvariant-s but does NOT convert '\' <-> '/', so a backslash path
            // and its forward-slash twin hash differently. Godot globalizes res:// to forward slashes, so
            // the forward-slash vector above is the one the plugin actually produces; this pins the rule.
            var forward = GodotProjectIdentity.Derive("C:/Games/MyGame", marker: null);
            var backslash = GodotProjectIdentity.Derive(@"C:\Games\MyGame", marker: null);

            Assert.NotEqual(forward.Pin, backslash.Pin);
        }

        [Fact]
        public void Derive_HonorsMarkerPortOverride_ButKeepsThePin()
        {
            var marker = new ProjectMarker { PortOverride = 25000 };

            var identity = GodotProjectIdentity.Derive("C:/Games/MyGame", marker);

            Assert.Equal("360f52fb", identity.Pin);      // pin is path-derived, unaffected by the override
            Assert.Equal(25000, identity.Port);          // port comes from the marker override
            Assert.True(identity.PortIsOverridden);
        }

        // === Default local server host — mcp-authorize e1, PR 4 (8080 → derived-port migration) ========
        //
        // The plugin's connect/start default host is now http://localhost:<derived-port> — the fixed 8080
        // literal is gone from the golden path. These pin: (1) the derived port is used when there is no
        // override; (2) it is NOT the fixed 8080; (3) a marker portOverride wins; (4) the URL's port tracks
        // the same golden-vector port as Derive (one derivation, no second copy of the port math).

        [Theory]
        [InlineData("C:/Games/MyGame", "http://localhost:29062")]
        [InlineData("/home/user/MyGame", "http://localhost:24998")]
        [InlineData("/home/user/Demo Project", "http://localhost:20832")]
        public void ResolveDefaultLocalServerHost_UsesDerivedPort_WhenNoOverride(string projectRoot, string expectedUrl)
        {
            Assert.Equal(expectedUrl, GodotProjectIdentity.ResolveDefaultLocalServerHost(projectRoot, marker: null));
        }

        [Fact]
        public void ResolveDefaultLocalServerHost_IsNotTheFixed8080_OnTheGoldenPath()
        {
            // The whole point of PR 4: the local default is no longer the fixed 8080 literal.
            var url = GodotProjectIdentity.ResolveDefaultLocalServerHost("C:/Games/MyGame", marker: null);
            Assert.Equal("http://localhost:29062", url);
            Assert.DoesNotContain(":8080", url);
        }

        [Fact]
        public void ResolveDefaultLocalServerHost_MarkerPortOverride_Wins()
        {
            var marker = new ProjectMarker { PortOverride = 25000 };

            // The user's explicit portOverride always wins over the derived default (D15 precedence).
            Assert.Equal(
                "http://localhost:25000",
                GodotProjectIdentity.ResolveDefaultLocalServerHost("C:/Games/MyGame", marker));
        }

        [Fact]
        public void ResolveDefaultLocalServerHost_TracksTheDeriveGoldenVectorPort()
        {
            // The URL's port is exactly ProjectIdentity's derived port for the same root — golden-vector
            // parity is inherited from Derive, and the port stays inside the shared 20000–29999 band.
            const string root = "/home/user/MyGame";
            var identity = GodotProjectIdentity.Derive(root, marker: null);

            Assert.Equal(
                $"http://localhost:{identity.Port}",
                GodotProjectIdentity.ResolveDefaultLocalServerHost(root, marker: null));
            Assert.InRange(identity.Port, ProjectIdentity.MinPort, ProjectIdentity.MaxPort);
        }

        // === Project marker read/write + server-target resolution =====================================

        [Fact]
        public void ProjectMarker_RoundTrips_ServerTargetAndPortOverride()
        {
            using var dir = new TempDir();

            new ProjectMarker { ServerTarget = "https://ai-game.dev", PortOverride = 24242 }.Write(dir.Path);

            // The marker lands at <project>/.ai-game-dev/project.json (design 06).
            Assert.True(File.Exists(ProjectMarker.PathFor(dir.Path)));
            Assert.True(File.Exists(Path.Combine(dir.Path, ".ai-game-dev", "project.json")));

            var read = ProjectMarker.Read(dir.Path);
            Assert.NotNull(read);
            Assert.Equal("https://ai-game.dev", read!.ServerTarget);
            Assert.Equal(24242, read.PortOverride);

            // The marker portOverride flows through the identity derivation.
            var identity = GodotProjectIdentity.Derive(dir.Path, read);
            Assert.Equal(24242, identity.Port);
            Assert.True(identity.PortIsOverridden);
        }

        [Fact]
        public void ProjectMarker_Read_ReturnsNull_WhenAbsent()
        {
            using var dir = new TempDir();

            Assert.Null(ProjectMarker.Read(dir.Path));
            // An absent marker resolves to no server-target decision → caller keeps its own config.
            Assert.Null(GodotProjectIdentity.ResolveServerTarget(ProjectMarker.Read(dir.Path)));
        }

        [Fact]
        public void ResolveServerTarget_MapsLoopbackToCustom_AndHostedToCloud()
        {
            var local = GodotProjectIdentity.ResolveServerTarget(
                new ProjectMarker { ServerTarget = "http://localhost:20000" });
            Assert.NotNull(local);
            Assert.Equal(GodotMcpConnectionMode.Custom, local!.Value.Mode);
            Assert.Equal("http://localhost:20000", local.Value.CustomHost);

            var hosted = GodotProjectIdentity.ResolveServerTarget(
                new ProjectMarker { ServerTarget = "https://ai-game.dev" });
            Assert.NotNull(hosted);
            Assert.Equal(GodotMcpConnectionMode.Cloud, hosted!.Value.Mode);
            Assert.Null(hosted.Value.CustomHost);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        public void ResolveServerTarget_ReturnsNull_ForMissingOrInvalidTargets(string? serverTarget)
        {
            Assert.Null(GodotProjectIdentity.ResolveServerTarget(
                new ProjectMarker { ServerTarget = serverTarget }));
        }

        [Fact]
        public void ResolveServerTarget_ReturnsNull_ForNullMarker()
        {
            Assert.Null(GodotProjectIdentity.ResolveServerTarget(marker: null));
        }

        /// <summary>Self-cleaning temp directory for the marker round-trip tests.</summary>
        sealed class TempDir : IDisposable
        {
            public string Path { get; }

            public TempDir()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), "godot-mcp-marker-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                try { Directory.Delete(Path, recursive: true); }
                catch { /* best-effort cleanup */ }
            }
        }
    }
}
