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

        // === Local server BIND port — mcp-authorize g3 (server bind == written config) =================
        //
        // The port the server BINDS (GodotProjectIdentity.ResolveLocalServerBindPort, called by the
        // connection panel's Start Server action) must equal the port the shared config writer writes into
        // the AI-client config (AgentConfiguratorSettings.PinnedHttpUrl / ResolvedPort), so an agent always
        // dials the port the server is actually listening on.
        //
        // These assert the BINDER'S OWN CONTRACT — given a host + marker + project root, which port comes
        // out — under the three-level precedence both sides now share:
        //   1. marker portOverride  →  2. an explicit port typed into the host  →  3. deterministic derived.
        // Asserting the contract rather than diffing against whatever the currently-pinned McpPlugin build
        // writes is deliberate: coupling these to the writer's build is what let the binder and the writer
        // silently diverge in the first place. The one genuine cross-check against the live writer is
        // parked below until the pin reaches the release that carries the new precedence.
        //
        // All pure over the shared McpPlugin primitives, so they run in the plain xUnit host on Linux CI.

        [Theory]
        [InlineData("C:/Games/MyGame", 29062)]
        [InlineData("/home/user/MyGame", 24998)]
        [InlineData("/home/user/Demo Project", 20832)]
        public void ResolveLocalServerBindPort_SeededLocalHost_IsTheDerivedPort_NotFixed8080(string projectRoot, int expectedPort)
        {
            // The GOLDEN BOOT PATH: GodotMcpConnection.SeedDefaultLocalServerHost replaces the fixed
            // DefaultCustomHost baseline with the project's derived local URL before anything binds, so the
            // host the binder actually sees is http://localhost:{derived}. Levels 2 and 3 agree there — and
            // no fixed 8080 can reach either side, which is the whole point of g3.
            var seededHost = GodotProjectIdentity.ResolveDefaultLocalServerHost(projectRoot, marker: null);

            var port = GodotProjectIdentity.ResolveLocalServerBindPort(seededHost, projectRoot, marker: null);

            Assert.Equal(expectedPort, port);
            Assert.NotEqual(8080, port);
            Assert.InRange(port, ProjectIdentity.MinPort, ProjectIdentity.MaxPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_UnSeededDefaultCustomHost_BindsItsLiteralPort()
        {
            const string root = "C:/Games/MyGame";

            // The un-seeded DefaultCustomHost baseline ("http://localhost:8080") reaches the binder only on
            // degraded paths — an unresolvable project root, a marker that fails to parse, or an explicit
            // GODOT_MCP_HOST pointing there — because SeedDefaultLocalServerHost normally replaces it
            // before anything binds (see the golden-path test above).
            //
            // Pinning the outcome deliberately: 8080 is a port present in the host string, so level 2
            // binds it. That is NOT a regression to the retired fixed-8080 rule — the writer reads the same
            // host and resolves the same 8080, which is the property that matters (bind == written). The
            // binder must NOT special-case this string: a user who genuinely types :8080 gets it honoured
            // by the writer, so a special case here would reintroduce the very divergence the three-level
            // precedence removes.
            Assert.Equal(
                8080,
                GodotProjectIdentity.ResolveLocalServerBindPort(GodotMcpConfig.DefaultCustomHost, root, marker: null));
        }

        [Theory]
        [InlineData("http://localhost")]
        [InlineData("http://127.0.0.1")]
        [InlineData("http://localhost/mcp")]
        public void ResolveLocalServerBindPort_PortlessLoopbackHost_FallsBackToDerived_NotTheSchemeDefault(string host)
        {
            const string root = "C:/Games/MyGame";

            // Level 3. A portless host carries no user intent, so it must NOT be read as the scheme's
            // default port: Uri.Port would synthesize 80 here, which would bind 80 while the writer wrote
            // the derived port. The raw-authority parse (GodotMcpConfig.TryGetExplicitPort) is what keeps
            // this case on level 3 instead of level 2.
            Assert.Equal(
                ProjectIdentity.DerivePort(root),
                GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null));
        }

        [Fact]
        public void ResolveLocalServerBindPort_MatchesTheWrittenConfigPort_OnTheDefaultPath()
        {
            // THE g3 guarantee on the golden path: server bind port == written config port == derived.
            // The seeded host carries the derived port explicitly, so this holds under BOTH the old writer
            // (which rewrote a loopback port to ResolvedPort) and the new one (which honours the typed port
            // — here the same number). That is why this cross-check stays live across the pin bump.
            //
            // It also survives the writer's v1 → v2 root-normalization change, but only because Godot roots
            // come from GlobalizePath("res://") and are already forward-slashed, so v2's '\' → '/' step is a
            // no-op and both derivations yield the same port. A backslashed root would NOT be equivalent.
            const string root = "C:/Games/MyGame";
            var seededHost = GodotProjectIdentity.ResolveDefaultLocalServerHost(root, marker: null);
            var settings = LocalSettings(root, seededHost + "/mcp");

            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(seededHost, root, marker: null);
            var writtenPort = new Uri(settings.PinnedHttpUrl).Port;

            Assert.Equal(ProjectIdentity.DerivePort(root), bindPort); // == derived
            Assert.Equal(settings.ResolvedPort, bindPort);            // == the port the writer resolves
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

            // Level 1. The user's explicit marker portOverride wins for BOTH the server bind AND the
            // written config, so they still agree.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(GodotMcpConfig.DefaultCustomHost, dir.Path, marker);
            var settings = LocalSettings(dir.Path, "http://localhost:8080/mcp"); // reads the same marker at dir.Path

            Assert.Equal(24242, bindPort);
            Assert.Equal(settings.ResolvedPort, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_MarkerPortOverride_BeatsAnExplicitHostPort()
        {
            using var dir = new TempDir();
            new ProjectMarker { PortOverride = 24242 }.Write(dir.Path);
            var marker = ProjectMarker.Read(dir.Path);

            // Level 1 BEATS level 2: portOverride is a deliberate per-project pin, so it outranks an
            // incidental port in the host string. Same ordering as the writer's own port resolution.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort("http://localhost:9000", dir.Path, marker);

            Assert.Equal(24242, bindPort);
            Assert.NotEqual(9000, bindPort);
        }

        [Theory]
        [InlineData("http://localhost:9000", 9000)]
        [InlineData("http://localhost:9000/mcp", 9000)]
        [InlineData("http://127.0.0.1:27618", 27618)]
        public void ResolveLocalServerBindPort_LoopbackExplicitPort_HonorsTheTypedPort(string host, int expectedPort)
        {
            const string root = "C:/Games/MyGame";

            // Level 2 — a port the user typed into a loopback host is BOUND, not overwritten with the
            // derived port. This assertion is the inverse of the rule that stood here before: mirroring
            // the retired writer (which rewrote a loopback URL's port) would make the server listen on the
            // derived port while the written config pointed at the typed one. Unity's binder already
            // resolves a typed port this way.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null);

            Assert.Equal(expectedPort, bindPort);
            Assert.NotEqual(ProjectIdentity.DerivePort(root), bindPort);
        }

        [Fact(Skip = "Un-skip when Godot-MCP.csproj pins com.IvanMurzak.McpPlugin >= 7.3.0 (issue #304) — see body.")]
        public void ResolveLocalServerBindPort_LoopbackExplicitPort_MatchesTheWrittenConfigPort()
        {
            const string root = "C:/Games/MyGame";
            const string host = "http://localhost:9000";

            // The live cross-check for level 2: the binder's typed-port result must equal the port the
            // shared writer emits for the same host. It CANNOT pass on the currently-pinned McpPlugin
            // 7.2.0, whose PinnedHttpUrl still rewrites a loopback port to the derived one — that old
            // writer is precisely what this change stopped mirroring. The binder-contract assertions above
            // (ResolveLocalServerBindPort_LoopbackExplicitPort_HonorsTheTypedPort) carry the behaviour in
            // the meantime; this one re-arms the two-sided guarantee once the pin lands in the release
            // cascade. Do not delete it and do not weaken it — un-skip it with the pin bump.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null);
            var settings = LocalSettings(root, host + "/mcp");

            Assert.Equal(9000, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_NonLoopbackHost_HonorsExplicitPort_MatchingTheWriter()
        {
            const string root = "C:/Games/MyGame";
            const string host = "http://192.168.1.50:9000";

            // A non-loopback target: the writer keeps its authority verbatim (:9000), so the server binds
            // that explicit port — an explicitly-set non-default custom host still wins, on both sides.
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(host, root, marker: null);
            var settings = LocalSettings(root, host + "/mcp");

            Assert.Equal(9000, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort);
        }

        [Fact]
        public void ResolveLocalServerBindPort_NonLoopbackHost_BeatsMarkerPortOverride()
        {
            using var dir = new TempDir();
            new ProjectMarker { PortOverride = 24242 }.Write(dir.Path);
            var marker = ProjectMarker.Read(dir.Path);

            // Host CLASS is decided before the port ladder: a marker portOverride is a LOCAL pin and does
            // NOT override a remote authority, which the writer keeps verbatim. This is the one cell where
            // the flat "portOverride wins outright" reading would give the wrong answer, so it is pinned
            // here — do not "restore" level-1 primacy by hoisting the override check above the loopback
            // branch in ResolveLocalServerBindPort.
            const string host = "http://192.168.1.50:9000";
            var bindPort = GodotProjectIdentity.ResolveLocalServerBindPort(host, dir.Path, marker);
            var settings = LocalSettings(dir.Path, host + "/mcp");

            Assert.Equal(9000, bindPort);
            Assert.NotEqual(24242, bindPort);
            Assert.Equal(new Uri(settings.PinnedHttpUrl).Port, bindPort); // the writer agrees
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

        // --- level-2 input: the raw-authority explicit-port parse -------------------------------------
        //
        // GodotMcpConfig.TryGetExplicitPort is the Godot mirror of the writer's internal
        // AgentConfiguratorSettings.TryGetExplicitPort. "No explicit port" MUST be distinguishable from
        // "the scheme default", or the binder silently binds 80 where the writer wrote the derived port.

        [Theory]
        [InlineData("http://localhost:9000", 9000)]
        [InlineData("http://localhost:9000/mcp", 9000)]
        [InlineData("https://example.com:443/p/abc?x=1", 443)]
        [InlineData("http://user:pass@localhost:9000", 9000)] // userinfo colon is not a port separator
        [InlineData("http://[::1]:8080", 8080)]               // IPv6 literal — port follows the bracket
        [InlineData("localhost:9000", 9000)]                  // scheme-less authority
        public void TryGetExplicitPort_ReadsAPortTheUserTyped(string url, int expected)
            => Assert.Equal(expected, GodotMcpConfig.TryGetExplicitPort(url));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("http://localhost")]        // portless — must NOT read as the scheme default 80
        [InlineData("http://localhost/mcp")]
        [InlineData("http://[::1]")]
        [InlineData("http://localhost:")]       // empty port
        [InlineData("http://localhost:abc")]    // non-numeric
        [InlineData("http://localhost:-1")]     // signed — rejected by NumberStyles.None
        [InlineData("http://localhost: 90")]    // whitespace — likewise
        [InlineData("http://localhost:0")]      // out of range (low)
        [InlineData("http://localhost:65536")]  // out of range (high)
        [InlineData("http://localhost:99999999999")] // overflow
        public void TryGetExplicitPort_ReturnsNull_WhenThereIsNoUsableTypedPort(string? url)
            => Assert.Null(GodotMcpConfig.TryGetExplicitPort(url));

        /// <summary>
        /// A shared local-mode <see cref="AgentConfiguratorSettings"/> for the three-way equality assertions:
        /// its <c>ResolvedPort</c> / <c>PinnedHttpUrl</c> derive the local port from <paramref name="projectRoot"/>
        /// (+ that root's marker) exactly as production does. The raw engine <c>port: 8080</c> is deliberately the
        /// stale value — the writer resolves the port from the root/marker and the host, never from this field — so a
        /// passing test proves the derivation, not the input.
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
