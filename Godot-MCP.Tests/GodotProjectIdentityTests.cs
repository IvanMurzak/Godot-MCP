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

        // === Hash-input diagnostic (auth-fixes h1 — OQ1 · k2) =========================================
        //
        // The connection logs the EXACT project-root string it hashes so the k2 matrix can CONFIRM the
        // path form each engine feeds into ProjectIdentity (open question OQ1) rather than assume it. On
        // Godot the source is GlobalizePath("res://") which already yields forward slashes, so pin v2 is a
        // no-op and the dual-hash is transition insurance — the line proves that empirically.

        [Fact]
        public void DescribeHashInput_SurfacesTheExactHashInputPathString_AndTheDerivedPinAndHash()
        {
            const string root = "/home/user/MyGame";
            var meta = GodotProjectIdentity.BuildInstanceMetadata(root, "MyGame", "id", "host");

            var line = GodotProjectIdentity.DescribeHashInput(root, meta);

            // The exact hash-input string is logged verbatim + quoted (OQ1: confirm the path form per engine).
            Assert.Contains($"projectRoot='{root}'", line);
            // The stable hash the routing pin is drawn from, and the 8-hex pin itself.
            Assert.Contains(meta.ProjectPathHash, line);
            Assert.Contains($"pin={meta.ProjectPathHash.Substring(0, 8)}", line);
            Assert.Contains("engine=godot", line);
        }

        [Fact]
        public void DescribeHashInput_EnumeratesEveryHandshakeHash_SoDualHashSurfacesWhenThePinCarriesIt()
        {
            const string root = "/home/user/MyGame";
            var meta = GodotProjectIdentity.BuildInstanceMetadata(root, "MyGame", "id", "host");

            var line = GodotProjectIdentity.DescribeHashInput(root, meta);

            // Every hash the handshake actually sends is surfaced: the released pin sends only
            // project_path_hash; once the dual-hash LIB lands, project_path_hash_legacy appears too — with
            // NO code change, because the line reads the query dict, not a named property.
            var hashKeys = meta.ToQuery()
                .Where(kvp => kvp.Key.IndexOf("hash", StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Assert.NotEmpty(hashKeys);
            foreach (var kvp in hashKeys)
                Assert.Contains($"{kvp.Key}={kvp.Value}", line);
        }

        [Fact]
        public void DescribeHashInput_NeverThrows_OnNullInputs()
        {
            // A diagnostic must never break the connection boot — a null metadata / root degrades to a
            // still-informative line rather than throwing on the connect path.
            var line = GodotProjectIdentity.DescribeHashInput(null, null);

            Assert.Contains("engine=godot", line);
            Assert.Contains("metadata unavailable", line);
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

        // === Local server BIND port — mcp-authorize g3 (server bind == written config == derived) ======
        //
        // Completes the Phase-4 8080 → derived-port migration for the LOCAL self-hosted server: the port
        // the server BINDS (GodotProjectIdentity.ResolveLocalServerBindPort, called by the connection panel's
        // Start Server action) must equal the port the shared b6 config writer writes into the AI-client
        // config (AgentConfiguratorSettings.PinnedHttpUrl / ResolvedPort). These pin the three-way equality
        // on the default local path, the D15 override precedence, and the b6-loopback-rule parity — all pure
        // over the shared McpPlugin 7.0 primitives, so they run in the plain xUnit host on the Linux CI runner.

        [Theory]
        [InlineData("C:/Games/MyGame", 29062)]
        [InlineData("/home/user/MyGame", 24998)]
        [InlineData("/home/user/Demo Project", 20832)]
        public void ResolveLocalServerBindPort_DefaultLoopbackHost_IsTheDerivedPort_NotFixed8080(string projectRoot, int expectedPort)
        {
            // The default local path (the baseline loopback host, no marker) binds the ProjectIdentity-derived
            // port — the whole point of g3: no fixed 8080 anywhere on the golden path.
            var port = GodotProjectIdentity.ResolveLocalServerBindPort(
                resolvedCustomHost: GodotMcpConfig.DefaultCustomHost, projectRoot, marker: null);

            Assert.Equal(expectedPort, port);
            Assert.NotEqual(8080, port);
            Assert.InRange(port, ProjectIdentity.MinPort, ProjectIdentity.MaxPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_MatchesTheB6WrittenConfigPort_OnTheDefaultPath()
        {
            // THE g3 guarantee: server bind port == written config port == the ProjectIdentity-derived port.
            const string root = "C:/Games/MyGame";
            var settings = LocalSettings(root, "http://localhost:8080/mcp");

            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(GodotMcpConfig.DefaultCustomHost, root, marker: null);
            var writtenPort = new Uri(settings.PinnedHttpUrl).Port;

            Assert.Equal(ProjectIdentity.DerivePort(root), bindPort); // == derived
            Assert.Equal(settings.ResolvedPort, bindPort);            // == the port b6 resolves
            Assert.Equal(writtenPort, bindPort);                      // == the port in the written URL
            Assert.Equal(29062, bindPort);                            // golden vector
            Assert.NotEqual(8080, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_MarkerPortOverride_WinsOnBothSides()
        {
            using var dir = new TempDir();
            new ProjectMarker { PortOverride = 24242 }.Write(dir.Path);
            var marker = ProjectMarker.Read(dir.Path);

            // The user's explicit marker portOverride (D15) wins for BOTH the server bind AND the written
            // config, so they still agree.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(GodotMcpConfig.DefaultCustomHost, dir.Path, marker);
            var settings = LocalSettings(dir.Path, "http://localhost:8080/mcp"); // reads the same marker at dir.Path

            Assert.Equal(24242, bindPort);
            Assert.Equal(settings.ResolvedPort, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_LoopbackExplicitPort_StillDerives_MatchingTheWriter()
        {
            const string root = "C:/Games/MyGame";

            // A user-typed loopback host with an explicit port: the b6 writer REWRITES a loopback URL's port
            // to the derived port, so the server binds the derived port too (a loopback port override goes
            // through the marker, not the URL) — server bind stays == written config, not the typed :9000.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort("http://localhost:9000", root, marker: null);
            var settings = LocalSettings(root, "http://localhost:9000/mcp");

            Assert.Equal(ProjectIdentity.DerivePort(root), bindPort);
            Assert.NotEqual(9000, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_NonLoopbackHost_HonorsExplicitPort_MatchingTheWriter()
        {
            const string root = "C:/Games/MyGame";
            const string host = "http://192.168.1.50:9000";

            // A non-loopback target: the b6 writer keeps its authority verbatim (:9000), so the server binds
            // that explicit port — an explicitly-set non-default custom host still wins, on both sides.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null);
            var settings = LocalSettings(root, host + "/mcp");

            Assert.Equal(9000, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        public void ResolveLocalServerBindPort_UnsetOrInvalidHost_FallsBackToDerived(string? host)
        {
            const string root = "C:/Games/MyGame";
            Assert.Equal(
                ProjectIdentity.DerivePort(root),
                GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null));
        }

        /// <summary>
        /// A shared local-mode <see cref="AgentConfiguratorSettings"/> for the three-way equality assertions:
        /// its <c>ResolvedPort</c> / <c>PinnedHttpUrl</c> derive the local port from <paramref name="projectRoot"/>
        /// (+ that root's marker) exactly as production does. The raw engine <c>port: 8080</c> is deliberately the
        /// stale value — b6 ignores it for the loopback rewrite — so a passing test proves the derivation, not the input.
        /// </summary>
        static AgentConfiguratorSettings LocalSettings(string projectRoot, string host) =>
            AgentConfiguratorSettings.CreateForHost(
                projectRootPath: projectRoot,
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: host,
                connectionMode: ConnectionMode.Local);

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
