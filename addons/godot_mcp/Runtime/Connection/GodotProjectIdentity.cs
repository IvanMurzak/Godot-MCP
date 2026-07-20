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
using System.Linq;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Godot-side wiring of the shared McpPlugin 7.0 project-identity primitives (mcp-authorize e1,
    /// PR 3 — design <c>04-pairing-and-routing.md</c> / <c>06-engine-plugins.md</c>). This is a thin,
    /// pure-managed adapter over the library's canonical derivation — it deliberately does NOT
    /// reimplement any hashing / port math, so the pin and derived port match the committed golden
    /// vectors byte-for-byte (the derivation lives in <see cref="ProjectIdentity"/>; this only supplies
    /// the Godot <c>engine</c> tag and the resolution glue). No Godot native types and no
    /// <c>#if TOOLS</c>, so the whole surface is unit-testable in the plain-xUnit host; the Godot
    /// project-root / project-name resolution stays in <see cref="GodotMcpConnection"/> (verified via
    /// the headless smoke).
    ///
    /// <para>
    /// Three seams live here:
    /// <list type="bullet">
    ///   <item><b>Handshake payload</b> — <see cref="BuildInstanceMetadata"/> builds the
    ///   <see cref="ConnectionInstanceMetadata"/> the SignalR client sends on connect so the server can
    ///   route/dedup by account + project + instance (design 04).</item>
    ///   <item><b>ProjectIdentity</b> — <see cref="Derive"/> resolves the project's <c>{pin, port}</c>
    ///   (honoring a marker <c>portOverride</c>), and <see cref="ResolveDefaultLocalServerHost"/> turns
    ///   that into the default local Custom-mode server URL (<c>http://localhost:&lt;port&gt;</c>) — the
    ///   mcp-authorize e1 · PR 4 replacement for the fixed <c>8080</c> local default (design 06 · D15).</item>
    ///   <item><b>Server-target resolution</b> — <see cref="ResolveServerTarget"/> maps the project
    ///   marker's enrolled <c>serverTarget</c> to a connection decision so the plugin boots pointed at
    ///   the right hub (hosted vs local; design 06 / 09 · 1A).</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class GodotProjectIdentity
    {
        /// <summary>
        /// The engine tag stamped into the instance metadata (design 04 — <c>"unity" | "godot" |
        /// "unreal"</c>). The server's account registry keys the instance on this + project-path-hash +
        /// machine name.
        /// </summary>
        public const string Engine = "godot";

        /// <summary>
        /// The instance id minted once per editor session (design 04 — a GUID per editor session). Sent
        /// in the handshake so the server can dedup a reconnect of the same editor without orphaning the
        /// prior entry. Stable for the life of the loaded addon assembly; a fresh editor session (or a
        /// C# hot-reload that reloads this assembly) mints a new one — the server evicts the stale entry
        /// after the SignalR client-timeout (design 04 "Dedup").
        /// </summary>
        public static readonly string SessionInstanceId = Guid.NewGuid().ToString();

        /// <summary>
        /// Build the connection instance metadata for the hub handshake. A thin forward to
        /// <see cref="ConnectionInstanceMetadata.Create"/> that fixes the engine tag to
        /// <see cref="Engine"/> and centralizes the argument order; the library computes the stable
        /// <c>ProjectPathHash</c> (SHA256 of the normalized project root — the pin is its first 8 hex).
        /// Null inputs degrade to safe empties / the session id so a partial boot never throws here.
        /// </summary>
        /// <param name="projectRoot">The project root the server hashes into <c>ProjectPathHash</c> —
        /// the same normalized path the configurator hashes into the routing pin, so an agent session in
        /// this project folder routes strictly to this instance.</param>
        /// <param name="projectName">Human-facing project name (e.g. Godot <c>application/config/name</c>).</param>
        /// <param name="instanceId">Per-session instance id (normally <see cref="SessionInstanceId"/>).</param>
        /// <param name="machineName">Host machine name (audit / cross-machine disambiguation).</param>
        public static ConnectionInstanceMetadata BuildInstanceMetadata(
            string projectRoot,
            string projectName,
            string instanceId,
            string machineName)
            => ConnectionInstanceMetadata.Create(
                engine: Engine,
                projectName: projectName ?? string.Empty,
                projectRootPath: projectRoot ?? string.Empty,
                instanceId: string.IsNullOrEmpty(instanceId) ? SessionInstanceId : instanceId,
                machineName: machineName ?? string.Empty);

        /// <summary>
        /// Build the diagnostic line for the instance-metadata hash input (auth-fixes h1, design 01 §7 /
        /// OQ1 · k2). It surfaces the <b>exact project-root string</b> fed into the hash derivation — the
        /// empirical answer to "what path form does each engine actually hash?" (open question OQ1, verified
        /// in the k2 matrix) — plus every hash the handshake carries. On Godot the path source is
        /// <c>GlobalizePath("res://")</c>, which already yields forward slashes (01 §7), so the pin v2
        /// separator-normalization is a no-op here and the dual-hash (v2 + legacy v1) is carried purely as
        /// transition insurance — this line is how a smoke/CI run PROVES that form rather than assuming it.
        ///
        /// <para>
        /// The hashes are read from <see cref="ConnectionInstanceMetadata.ToQuery"/> rather than named
        /// properties, so the SINGLE hash of the currently-pinned package AND the additional
        /// <c>project_path_hash_legacy</c> that a dual-hash LIB adds both surface with no compile-time
        /// dependency on a field the pinned package may not yet expose (the pin bump lands at release · k3).
        /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) and never throws — a null/partial input
        /// degrades to a still-informative line so a diagnostic can never break the connection boot.
        /// </para>
        /// </summary>
        /// <param name="projectRoot">The exact hash-input string (the globalized, trailing-sep-trimmed
        /// <c>res://</c> root) the metadata was built from — logged verbatim, quoted so trailing/again-case
        /// differences are visible.</param>
        /// <param name="metadata">The metadata built for this connection (via <see cref="BuildInstanceMetadata"/>).</param>
        public static string DescribeHashInput(string? projectRoot, ConnectionInstanceMetadata? metadata)
        {
            var root = projectRoot ?? string.Empty;
            if (metadata == null)
                return $"[Godot-MCP] instance-metadata hash input: engine={Engine} projectRoot='{root}' (metadata unavailable).";

            var hash = metadata.ProjectPathHash ?? string.Empty;
            // The pin is the first ProjectIdentity.PinLength hex of the path hash — reuse the library's
            // canonical constant so this diagnostic can never drift from the real routing-pin width.
            var pin = hash.Length >= ProjectIdentity.PinLength ? hash.Substring(0, ProjectIdentity.PinLength) : hash;

            // Enumerate EVERY handshake hash key so the line stays correct across the dual-hash transition:
            // the released pin sends only `project_path_hash`; a dual-hash LIB additionally sends
            // `project_path_hash_legacy`. Both surface here without naming a property the pin may lack.
            var hashes = string.Join(", ", metadata.ToQuery()
                .Where(kvp => kvp.Key.IndexOf("hash", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));
            if (string.IsNullOrEmpty(hashes))
                hashes = "<none>";

            return $"[Godot-MCP] instance-metadata hash input: engine={metadata.Engine} " +
                   $"projectRoot='{root}' pin={pin}; handshake hashes: {hashes}.";
        }

        /// <summary>
        /// Resolve the project's <see cref="ProjectIdentity"/> (<c>{pin, port}</c>) from its root,
        /// consulting the marker's <c>portOverride</c> when a marker is present (the
        /// <see cref="ProjectMarker"/> overload) and otherwise deriving the default port. The derivation
        /// itself is the library's golden-vector-pinned canonical math — this only picks the right
        /// overload. The resolved port is the local Custom-mode server default (see
        /// <see cref="ResolveDefaultLocalServerHost"/>): a marker <c>portOverride</c> wins, else the
        /// deterministic hash-derived port (mcp-authorize e1 · PR 4 — the 8080 → derived-port migration).
        /// </summary>
        public static ProjectIdentity Derive(string projectRoot, ProjectMarker? marker)
            => marker != null
                ? ProjectIdentity.Derive(projectRoot, marker)
                : ProjectIdentity.Derive(projectRoot, (int?)null);

        /// <summary>
        /// The loopback scheme+host every local Custom-mode server URL is built on. The port is appended
        /// by <see cref="ResolveDefaultLocalServerHost"/>; there is no fixed default port baked in here
        /// (mcp-authorize e1 · PR 4 retired the <c>8080</c> literal on the connect path).
        /// </summary>
        public const string LocalLoopbackHost = "http://localhost";

        /// <summary>
        /// Resolve the DEFAULT local Custom-mode server URL for <paramref name="projectRoot"/> — the
        /// mcp-authorize e1 · PR 4 replacement for the fixed <c>http://localhost:8080</c> default (design
        /// 06 · D15). The port precedence is: the project marker's explicit user <c>portOverride</c> (when
        /// present it always wins) → the deterministic <see cref="ProjectIdentity"/> hash-derived port.
        /// There is NO hardcoded 8080 fallback. Deterministic and probe-free — the same project path always
        /// yields the same URL, so it matches the pin/port a configurator writes for the same root.
        /// </summary>
        public static string ResolveDefaultLocalServerHost(string projectRoot, ProjectMarker? marker)
            => $"{LocalLoopbackHost}:{Derive(projectRoot, marker).Port}";

        /// <summary>
        /// Resolve the port the LOCAL self-hosted server must BIND so it MATCHES the port the shared
        /// config writer (<see cref="AgentConfiguratorSettings.PinnedHttpUrl"/> /
        /// <see cref="AgentConfiguratorSettings.ResolvedPort"/>) writes into the AI-client config — the
        /// mcp-authorize g3 guarantee that <b>server bind port == written config port</b>, so an agent
        /// always dials the port the server is actually listening on.
        ///
        /// <para>
        /// Host CLASS is decided first, then the port. A <b>NON-loopback</b> host is a remote target the
        /// writer keeps verbatim, so it binds that host's own port unconditionally — a marker
        /// <c>portOverride</c> does NOT override a remote authority, on either side.
        /// </para>
        ///
        /// <para>
        /// For a <b>loopback / unset</b> host the port follows the shared writer's THREE-LEVEL precedence
        /// (MCP-Plugin-dotnet #174 / #176), applied identically here:
        /// <list type="number">
        ///   <item>the project marker's <c>portOverride</c> — a deliberate per-project pin, and it
        ///   SUPPRESSES level 2 entirely;</item>
        ///   <item>an explicit port the user typed into the Custom host — read off the RAW authority via
        ///   <see cref="GodotMcpConfig.TryGetExplicitPort"/>, so a portless host is NOT mistaken for the
        ///   scheme default (80/443);</item>
        ///   <item>the deterministic hash-derived <see cref="ProjectIdentity"/> port — the fallback when
        ///   the host carries no explicit port.</item>
        /// </list>
        /// Levels 1 and 3 are inherited from <see cref="Derive"/> (the golden-vector-pinned math), which
        /// folds an override into <see cref="ProjectIdentity.Port"/>; only level 2 is resolved here, and
        /// only from the host string.
        /// </para>
        ///
        /// <para><b>Why a loopback host's own port is honoured.</b> A port the user typed is user intent:
        /// "the Configure button must use exactly that port". The shared writer resolves it the same way,
        /// so both sides land on the same number. This method previously ignored it and always bound the
        /// derived port, mirroring an older writer that rewrote a loopback URL's port — mirroring that
        /// retired rule is what would make the two sides disagree. Unity's binder
        /// (<c>UnityMcpPluginEditor.Port</c>) already resolves a typed port this way.</para>
        ///
        /// <para><b>⚠ Transitional window — read before debugging a port mismatch.</b> The precedence
        /// above is the writer's behaviour from McpPlugin <b>7.3.0</b> onward. While this addon is pinned
        /// BELOW that (see <c>Godot-MCP.csproj</c>), the pinned writer still rewrites a loopback URL's
        /// port to the derived one, so a user-typed loopback port makes the binder and the written config
        /// disagree until the pin lands. The golden boot path is unaffected —
        /// <c>GodotMcpConnection.SeedDefaultLocalServerHost</c> seeds the Custom host as
        /// <c>http://localhost:{derived}</c>, so levels 2 and 3 agree there and no fixed 8080 reaches
        /// either side. The parked cross-check
        /// <c>ResolveLocalServerBindPort_LoopbackExplicitPort_MatchesTheWrittenConfigPort</c> re-arms the
        /// two-sided guarantee at the pin bump.</para>
        ///
        /// <para>A NON-loopback host (a real remote / self-hosted target the writer keeps verbatim) still
        /// binds that host's own port — that branch was already correct and is untouched.</para>
        ///
        /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>). The loopback / URL-validity
        /// predicates are the same ones <see cref="GodotMcpConfig"/> and the writer use, so a marker, an
        /// env/UI custom host, and a terminal-written config all classify identically.
        /// </summary>
        /// <param name="resolvedCustomHost">The active Custom-mode host (already env/persist-resolved via
        /// <see cref="GodotMcpConfig.ResolveCustomHost"/>).</param>
        /// <param name="projectRoot">The normalized project root — the same string the config writer hashes.</param>
        /// <param name="marker">The project marker (its <c>portOverride</c> wins outright), or null.</param>
        public static int ResolveLocalServerBindPort(string? resolvedCustomHost, string projectRoot, ProjectMarker? marker)
        {
            var identity = Derive(projectRoot, marker);

            // NOT necessarily the hash-derived port: Derive folds a marker portOverride into Port, so this
            // is the override when one is set. That is exactly what makes it the right level-3 fallback AND
            // the right level-1 answer — hence the deliberately neutral name.
            var identityPort = identity.Port;

            var host = GodotMcpConfig.NormalizeUrl(resolvedCustomHost);

            // An unset / unparseable host carries no intent at all: level 3. This guard is load-bearing
            // for level 2 below — TryGetExplicitPort parses a bare authority ("localhost:9000") that
            // IsValidHttpUrl rejects, so without it a non-URL string could still yield a port.
            if (string.IsNullOrEmpty(host) || !GodotMcpConfig.IsValidHttpUrl(host!))
                return identityPort;

            if (!GodotMcpConfig.IsLoopbackUrl(host))
            {
                // Non-loopback target: the writer keeps its authority verbatim — regardless of the marker,
                // which is why this branch precedes the level-1 check below. Bind that host's own port. A
                // portless non-loopback host resolves to the URI scheme's default (e.g. 80 for http), the
                // same value the written authority reports, so both sides still agree. The identityPort
                // fallback is reached only by an authority whose port is explicitly 0.
                return GodotMcpServerView.ResolveServerPort(host!, identityPort);
            }

            // Loopback. Level 1 — a marker portOverride is a deliberate per-project pin, so it SUPPRESSES
            // the level-2 lookup rather than merely outranking its result (identical ordering to the
            // writer's own port resolution). Level 2 — the port the user typed, read from the RAW
            // authority (not Uri.Port) so a portless host is not mistaken for the synthesized scheme
            // default. Level 3 — neither applies: identityPort, which here IS the derived port.
            var typedPort = identity.PortIsOverridden ? null : GodotMcpConfig.TryGetExplicitPort(host);

            return typedPort ?? identityPort;
        }

        /// <summary>
        /// Resolve the enrolled server target from the project marker into a connection decision, or
        /// <c>null</c> when the marker is absent / carries no valid <c>serverTarget</c> (the common case —
        /// then the caller keeps its existing Cloud/Custom resolution untouched). A loopback target maps
        /// to <see cref="GodotMcpConnectionMode.Custom"/> pointed at that host; any other valid http(s)
        /// target maps to <see cref="GodotMcpConnectionMode.Cloud"/> (hosted). Pure — the loopback / URL
        /// predicates are the same ones <see cref="GodotMcpConfig"/> uses, so a marker and a manual
        /// custom-host entry classify identically.
        /// </summary>
        public static (GodotMcpConnectionMode Mode, string? CustomHost, string ServerTarget)? ResolveServerTarget(ProjectMarker? marker)
        {
            var target = GodotMcpConfig.NormalizeUrl(marker?.ServerTarget);
            if (string.IsNullOrEmpty(target) || !GodotMcpConfig.IsValidHttpUrl(target!))
                return null;

            return GodotMcpConfig.IsLoopbackUrl(target)
                ? (GodotMcpConnectionMode.Custom, target, target!)
                : (GodotMcpConnectionMode.Cloud, (string?)null, target!);
        }
    }
}
