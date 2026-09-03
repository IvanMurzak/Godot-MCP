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

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>, no I/O of its own) resolver for the
    /// <c>GODOT_MCP_SERVER_PATH</c> override — the dev/CI escape hatch that makes the editor LAUNCH a
    /// caller-supplied <c>gamedev-mcp-server</c> executable instead of the release pinned by
    /// <see cref="GodotMcpServerView.ServerVersion"/>. Mirrors Unreal's <c>UNREAL_MCP_SERVER_PATH</c>
    /// (<c>UnrealMcpServerManager.cpp</c> <c>ResolveBinaryPath</c>) and Unity's <c>UNITY_MCP_SERVER_PATH</c>.
    ///
    /// <para>
    /// Built on the <see cref="DevControl.DevControlGate"/> pattern: the boot site
    /// (<see cref="GodotMcpServerManager"/>) does the env/file I/O and passes RAW strings plus a
    /// <c>fileExists</c> delegate here, so every decision the override drives is unit-testable in the
    /// plain-xUnit CI host with no Godot binary and no filesystem. The manager statics that would carry
    /// these decisions (<c>ExecutableFullPath</c>, <c>IsVersionMatches</c>, <c>KillOrphanedServerProcesses</c>)
    /// all reach <c>ProjectSettings.GlobalizePath</c> and therefore cannot run in that host at all — which is
    /// exactly why the decisions live here instead.
    /// </para>
    ///
    /// <para>
    /// Precedence is the addon's standard env layer — process environment &gt; project <c>res://.env</c>
    /// (<see cref="GodotMcpEnvFile.LookupRaw"/>) &gt; none — the same ORDER <c>GODOT_MCP_DEV_CONTROL</c> uses
    /// (<c>GodotMcpPlugin.StartDevControlIfEnabled</c>). One deliberate difference from that precedent: it
    /// picks the process layer on the RAW value being non-empty, whereas this picks it on the NORMALIZED
    /// value, so a process variable set to whitespace falls through to <c>.env</c> here instead of shadowing
    /// it. A value that is set but does NOT name an existing
    /// file is IGNORED (resolves to <c>null</c>) so the addon falls back to its normal download path rather
    /// than failing to boot — Unreal's rule.
    /// </para>
    ///
    /// <para>
    /// While the override is ACTIVE the addon deliberately changes three behaviours, all keyed off the single
    /// <see cref="IsActive"/> predicate: the release download is skipped, the cached-version match is bypassed
    /// (an arbitrary build carries no <c>version</c> marker), and orphaned-server cleanup is skipped. The last
    /// one is a correctness requirement, not a convenience: cleanup ownership
    /// (<see cref="GodotMcpServerOwnership.IsOwnedByThisProject"/>) matches on the SAME CONTAINING DIRECTORY,
    /// so with several projects pointed at one shared override binary an editor boot would kill a sibling
    /// process running that same executable.
    /// </para>
    /// </summary>
    public static class GodotMcpServerPathOverride
    {
        /// <summary>
        /// Normalize one raw <c>GODOT_MCP_SERVER_PATH</c> value exactly as the addon's <c>.env</c> layer
        /// normalizes file values (<c>GodotMcpEnvFile.Sanitize</c>): trim surrounding whitespace, strip a
        /// single pair of wrapping SINGLE quotes, then apply the shared env normalizer
        /// (<see cref="GodotMcpConfig.NormalizeEnv"/>, which is <c>Trim().Trim('"')</c> — whitespace plus ANY
        /// number of wrapping DOUBLE quotes, balanced or not). Returns
        /// <c>null</c> for null / empty / whitespace-only input.
        ///
        /// <para>
        /// The ORDER is load-bearing and matches the sibling: the single-quote strip runs BEFORE the shared
        /// normalizer, because the shared normalizer only knows about double quotes. Swapping the two changes
        /// the answer for a nested pair — <c>"'/srv/x'"</c> normalizes to <c>'/srv/x'</c> in this order and to
        /// <c>/srv/x</c> in the other — so the order is asserted, not merely described.
        /// </para>
        ///
        /// <para>
        /// The quote handling is load-bearing for the PROCESS-env half: <see cref="GodotMcpEnvFile.LookupRaw"/>
        /// already sanitizes what it reads out of <c>.env</c>, but nothing sanitizes a process variable, and a
        /// path exported as <c>GODOT_MCP_SERVER_PATH="C:/…/gamedev-mcp-server.exe"</c> (quotes preserved by the
        /// shell or a CI <c>env:</c> block) would otherwise be tested for existence WITH the literal quotes and
        /// silently ignored.
        /// </para>
        /// </summary>
        public static string? Normalize(string? rawValue)
        {
            if (rawValue == null)
                return null;

            var trimmed = rawValue.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[trimmed.Length - 1] == '\'')
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            return GodotMcpConfig.NormalizeEnv(trimmed);
        }

        /// <summary>
        /// Apply the addon's standard precedence to the two RAW values the boot site collected: the process
        /// environment wins whenever it normalizes to something non-blank, otherwise the project <c>.env</c>
        /// value is used, otherwise <c>null</c>. Pure — the caller performs both reads.
        /// </summary>
        public static string? SelectRaw(string? processValue, string? envFileValue)
            => Normalize(processValue) ?? Normalize(envFileValue);

        /// <summary>
        /// Resolve ONE raw value to an override path: the normalized value when
        /// <paramref name="fileExists"/> reports it names an existing file, otherwise <c>null</c>.
        /// The existence gate is what makes a stale or mistyped override fall THROUGH to the normal
        /// download path instead of breaking the editor.
        /// </summary>
        public static string? Resolve(string? rawValue, Func<string, bool> fileExists)
        {
            if (fileExists == null)
                throw new ArgumentNullException(nameof(fileExists));

            var normalized = Normalize(rawValue);
            if (normalized == null)
                return null;

            return fileExists(normalized) ? normalized : null;
        }

        /// <summary>
        /// Resolve the override from BOTH raw layers at once (process env &gt; project <c>.env</c>), then apply
        /// the existence gate — the whole resolution in one call, and the seam the precedence test pins.
        ///
        /// <para>
        /// The boot site does NOT use this overload: it needs the SELECTED-but-ungated value as well (to tell
        /// "set to a path that does not exist" from "not set" — see <see cref="IsIgnoredValue"/>), so it calls
        /// <see cref="SelectRaw"/> and the single-value <see cref="Resolve(string, Func{string, bool})"/> in
        /// turn. That composition is exactly this body, so the two paths cannot diverge.
        /// </para>
        /// </summary>
        public static string? Resolve(string? processValue, string? envFileValue, Func<string, bool> fileExists)
            => Resolve(SelectRaw(processValue, envFileValue), fileExists);

        /// <summary>
        /// The single "the override is in force" predicate. Every behaviour the override changes — download
        /// skip, version-match bypass, orphan-cleanup skip, launch path — DERIVES from this ONE decision;
        /// they are deliberately not independent flags, and nothing may reintroduce one.
        ///
        /// <para>
        /// The named siblings below (<see cref="VersionMatchesOrOverridden"/>, <see cref="ShouldKillOrphans"/>,
        /// <see cref="IsIgnoredValue"/>) are NOT such flags: each is a total function OF this predicate,
        /// spelled out so that the POLARITY the boot site applies to it is pinnable. That matters because the
        /// consuming call sites live in <c>GodotMcpServerManager</c>, which is <c>#if TOOLS</c> and cannot be
        /// compiled into the xUnit host at all — so an inverted <c>||</c> or a dropped <c>!</c> there would be
        /// caught by no automated gate. Pinning the predicate alone does not pin how it is read.
        /// </para>
        /// </summary>
        public static bool IsActive(string? resolvedOverride)
            => !string.IsNullOrEmpty(resolvedOverride);

        /// <summary>
        /// True when a value WAS supplied but did not survive the existence gate — i.e. the override is being
        /// IGNORED and the addon is silently falling back to the pinned release. The boot site logs a warning
        /// on this, because "set to a path that does not exist" is otherwise byte-indistinguishable from
        /// "not set at all": a CI or dev run whose whole purpose is to exercise its OWN server build would
        /// pass against the downloaded release with nothing in the log to say so.
        /// </summary>
        public static bool IsIgnoredValue(string? rawSelected, string? resolvedOverride)
            => !string.IsNullOrEmpty(rawSelected) && !IsActive(resolvedOverride);

        /// <summary>
        /// The manager's version-match verdict: unconditionally true while the override is active (an
        /// arbitrary caller-supplied build carries no <c>version</c> marker and is not expected to match the
        /// pin), otherwise whatever the cached-version comparison decides.
        ///
        /// <para>
        /// <paramref name="cachedVersionMatches"/> is a DELEGATE, not a <c>bool</c>, so the short-circuit is
        /// preserved: reading the cache's <c>version</c> marker is file I/O, and under an active override
        /// that cache folder routinely does not exist at all. Evaluating it eagerly would perform a read the
        /// original <c>||</c> expression never performed.
        /// </para>
        /// </summary>
        public static bool VersionMatchesOrOverridden(string? resolvedOverride, Func<bool> cachedVersionMatches)
        {
            if (cachedVersionMatches == null)
                throw new ArgumentNullException(nameof(cachedVersionMatches));

            return IsActive(resolvedOverride) || cachedVersionMatches();
        }

        /// <summary>
        /// Whether the boot site should run its orphaned-server sweep: NO while the override is active.
        /// Cleanup ownership (<see cref="GodotMcpServerOwnership.IsOwnedByThisProject"/>) claims every process
        /// whose executable sits in the same containing directory (or below it), and an override binary is
        /// shared by design, so sweeping would kill a live server belonging to another project or tool.
        /// </summary>
        public static bool ShouldKillOrphans(string? resolvedOverride)
            => !IsActive(resolvedOverride);

        /// <summary>
        /// The executable the manager must launch: the resolved override when active, else the per-platform
        /// cached binary the release download writes.
        /// </summary>
        public static string ExecutablePath(string? resolvedOverride, string cachedExecutablePath)
            => IsActive(resolvedOverride) ? resolvedOverride! : cachedExecutablePath;

        /// <summary>
        /// The child process's working directory: the directory CONTAINING the executable actually being
        /// launched (so an override binary runs beside its own sidecar files), falling back to
        /// <paramref name="fallbackDirectory"/> when the path carries no directory component. With no override
        /// this returns the cache platform folder — the historical value — because that IS the cached
        /// executable's directory.
        ///
        /// <para>
        /// The "beside its own sidecar files" property holds only for a path that HAS a directory component,
        /// which is what the documented contract (an ABSOLUTE path — see the <c>GODOT_MCP_SERVER_PATH</c> row
        /// in <c>README.md</c>) always supplies. A bare filename is still accepted by the existence gate — it
        /// resolves against the editor process's working directory — but then this returns
        /// <paramref name="fallbackDirectory"/>, so the child runs somewhere OTHER than beside its executable.
        /// </para>
        /// </summary>
        public static string WorkingDirectory(string? executablePath, string fallbackDirectory)
        {
            if (string.IsNullOrWhiteSpace(executablePath))
                return fallbackDirectory;

            var directory = Path.GetDirectoryName(executablePath);
            return string.IsNullOrEmpty(directory) ? fallbackDirectory : directory!;
        }
    }
}
