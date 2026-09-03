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
    /// (<see cref="GodotMcpEnvFile.LookupRaw"/>) &gt; none — the same order <c>GODOT_MCP_DEV_CONTROL</c> uses
    /// (<c>GodotMcpPlugin.StartDevControlIfEnabled</c>). A value that is set but does NOT name an existing
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
        /// (<see cref="GodotMcpConfig.NormalizeEnv"/>: whitespace + wrapping DOUBLE quotes). Returns
        /// <c>null</c> for null / empty / whitespace-only input.
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
        /// the existence gate. This is the seam the boot site calls and the one the precedence test pins.
        /// </summary>
        public static string? Resolve(string? processValue, string? envFileValue, Func<string, bool> fileExists)
            => Resolve(SelectRaw(processValue, envFileValue), fileExists);

        /// <summary>
        /// The single "the override is in force" predicate. Every behaviour the override changes — download
        /// skip, version-match bypass, orphan-cleanup skip, launch path — is this ONE decision read at four
        /// call sites; they are deliberately not independent flags.
        /// </summary>
        public static bool IsActive(string? resolvedOverride)
            => !string.IsNullOrEmpty(resolvedOverride);

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
