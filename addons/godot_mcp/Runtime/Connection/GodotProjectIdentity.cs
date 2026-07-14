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
