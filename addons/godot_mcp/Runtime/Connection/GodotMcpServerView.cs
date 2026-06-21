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
using System.Runtime.InteropServices;
using System.Text;
using com.IvanMurzak.Godot.MCP.UI;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The lifecycle status of the locally-hosted Godot-MCP server process, mirroring Unity-MCP's
    /// <c>McpServerStatus</c>. Drives the MCP-server timeline point's Start/Stop button + status circle in
    /// the dock (the editor maps it to a <see cref="ConnectionPanelView.TimelinePointState"/> /
    /// button-text via the pure-managed helpers below).
    /// </summary>
    public enum GodotMcpServerStatus
    {
        /// <summary>Not running — the Start button launches it.</summary>
        Stopped,

        /// <summary>Process spawned, in the ~5s startup-verification window (button shows a busy "Starting…").</summary>
        Starting,

        /// <summary>Verified running — the Stop button terminates it.</summary>
        Running,

        /// <summary>Terminate signal sent, awaiting exit.</summary>
        Stopping,

        /// <summary>A server we did not launch is already on the port (we connect to it but cannot stop it).</summary>
        External
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) logic for the locally-hosted Godot-MCP
    /// server manager — the Godot analog of the deterministic pieces of Unity-MCP's <c>McpServerManager</c>
    /// (binary metadata) + <c>MainWindowEditor.McpServer</c> (status presentation). Keeping these here
    /// (rather than inline in the <c>#if TOOLS</c> <see cref="GodotMcpServerManager"/>) makes every decision
    /// unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host with no Godot binary and — critically —
    /// no platform divergence: every method below is a deterministic string/enum transform, so the same
    /// assertions hold on the Linux CI runner and on a Windows dev box.
    ///
    /// <para>
    /// The download/cache/unzip/launch/monitor side effects live in the editor-only
    /// <see cref="GodotMcpServerManager"/>; this class only computes the asset name, the version match, the
    /// release-zip URL, the launch argument string, and the status→button-text/label/circle-color mappings.
    /// </para>
    /// </summary>
    public static class GodotMcpServerView
    {
        /// <summary>
        /// The PINNED version of the shared <c>GameDev-MCP-Server</c> this addon downloads and runs. The
        /// addon version (<c>plugin.cfg</c>, 0.x) and the shared server version (8.x) DIVERGE — the server
        /// is released from its own repo (https://github.com/IvanMurzak/GameDev-MCP-Server) on its own
        /// cadence — so the download URL must NEVER be derived from the addon version. Bumping the consumed
        /// server is an explicit addon change: update THIS constant (and make sure the corresponding
        /// <c>v&lt;ServerVersion&gt;</c> release with all 7 RID zips exists on GameDev-MCP-Server BEFORE
        /// cutting an addon release that pins it — otherwise the download 404s, the issue-#94 class of bug).
        /// </summary>
        public const string ServerVersion = "8.0.1";

        /// <summary>
        /// The server executable base name (the shared <c>GameDev-MCP-Server</c> binary). On Windows the
        /// on-disk file is <c>gamedev-mcp-server.exe</c>; on Unix it is <c>gamedev-mcp-server</c>.
        /// </summary>
        public const string ExecutableName = "gamedev-mcp-server";

        /// <summary>
        /// The GitHub release-asset / .NET RID prefix. The GameDev-MCP-Server release workflow zips each
        /// self-contained build as <c>gamedev-mcp-server-&lt;rid&gt;.zip</c>, so the asset stem is the
        /// executable name plus the platform RID.
        /// </summary>
        public const string AssetPrefix = ExecutableName;

        // --- Platform mapping (os + arch -> .NET RID), deterministic + injectable for cross-platform tests ---

        /// <summary>
        /// Map an <see cref="OSPlatform"/>-style os token to the .NET RID os segment Unity uses
        /// (<c>win</c>/<c>osx</c>/<c>linux</c>). Unknown → <c>unknown</c>. Mirrors Unity's
        /// <c>McpServerManager.OperationSystem</c>.
        /// </summary>
        public static string OsToken(OSPlatform os) =>
            os == OSPlatform.Windows ? "win" :
            os == OSPlatform.OSX ? "osx" :
            os == OSPlatform.Linux ? "linux" :
            "unknown";

        /// <summary>
        /// Map a process <see cref="Architecture"/> to the RID arch segment (<c>x86</c>/<c>x64</c>/
        /// <c>arm</c>/<c>arm64</c>). Unknown → <c>unknown</c>. Mirrors Unity's <c>McpServerManager.CpuArch</c>.
        /// </summary>
        public static string ArchToken(Architecture arch) => arch switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm => "arm",
            Architecture.Arm64 => "arm64",
            _ => "unknown"
        };

        /// <summary>The RID-style platform name <c>&lt;os&gt;-&lt;arch&gt;</c> (e.g. <c>win-x64</c>).</summary>
        public static string PlatformName(OSPlatform os, Architecture arch) =>
            $"{OsToken(os)}-{ArchToken(arch)}";

        /// <summary>The live platform name for the current process. Thin wrapper over <see cref="RuntimeInformation"/>.</summary>
        public static string CurrentPlatformName() =>
            PlatformName(CurrentOsPlatform(), RuntimeInformation.ProcessArchitecture);

        /// <summary>The current <see cref="OSPlatform"/> (Windows/OSX/Linux), or <see cref="OSPlatform.Create"/>("unknown").</summary>
        public static OSPlatform CurrentOsPlatform() =>
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? OSPlatform.Windows :
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? OSPlatform.OSX :
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? OSPlatform.Linux :
            OSPlatform.Create("unknown");

        /// <summary>
        /// The on-disk executable file name for an os: <c>gamedev-mcp-server.exe</c> on Windows, else
        /// <c>gamedev-mcp-server</c>. Mirrors Unity's <c>ExecutableFullName</c>.
        /// </summary>
        public static string ExecutableFileName(OSPlatform os) =>
            os == OSPlatform.Windows ? ExecutableName + ".exe" : ExecutableName;

        /// <summary>The on-disk executable file name for the current OS.</summary>
        public static string CurrentExecutableFileName() => ExecutableFileName(CurrentOsPlatform());

        /// <summary>
        /// The GitHub release zip asset name for a platform: <c>gamedev-mcp-server-&lt;os&gt;-&lt;arch&gt;.zip</c>
        /// (e.g. <c>gamedev-mcp-server-win-x64.zip</c>). Matches the GameDev-MCP-Server release assets.
        /// </summary>
        public static string AssetZipName(OSPlatform os, Architecture arch) =>
            $"{AssetPrefix}-{PlatformName(os, arch)}.zip";

        // --- Plugin-version source (parsed from plugin.cfg, so the tag can never drift) ---

        /// <summary>
        /// Parse the addon <c>version="x.y.z"</c> value out of the raw text of
        /// <c>addons/godot_mcp/plugin.cfg</c> (a Godot INI file). Returns the trimmed, unquoted version, or
        /// null when no <c>version=</c> key is present. This mirrors the release workflow's OWN version read
        /// (<c>.github/workflows/release.yml</c>'s <c>get_version</c> step greps the same <c>^version=</c>
        /// line and tags the release <c>v&lt;version&gt;</c>), so the server tag the addon downloads can never
        /// drift from the tag the release was actually cut on (issue #94). Pure string parse — unit-testable
        /// with no Godot binary.
        /// </summary>
        public static string? ParsePluginVersion(string? pluginCfgText)
        {
            if (string.IsNullOrEmpty(pluginCfgText))
                return null;

            foreach (var rawLine in pluginCfgText!.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                var eq = line.IndexOf('=');
                if (eq < 0)
                    continue;

                // The key (left of '=') must be exactly `version` — not e.g. a future `version_extra`.
                if (!string.Equals(line.Substring(0, eq).Trim(), "version", StringComparison.Ordinal))
                    continue;

                // Mirror release.yml:91's sed (`s/^version="?([^"]*)"?.*/\1/`): the value is what sits
                // inside the quotes, and ANY trailing content (an inline `; comment`, stray tokens) after
                // the closing quote is dropped. Stopping at the closing quote — rather than just trimming
                // end-quotes — is what keeps this parse byte-for-byte aligned with the workflow read.
                var rhs = line.Substring(eq + 1).Trim();
                string value;
                if (rhs.StartsWith("\"", StringComparison.Ordinal))
                {
                    var close = rhs.IndexOf('"', 1);
                    value = close < 0 ? rhs.Substring(1) : rhs.Substring(1, close - 1);
                }
                else
                {
                    value = rhs;
                }
                value = value.Trim();
                return value.Length == 0 ? null : value;
            }

            return null;
        }

        // --- Release URL construction (exact version, no semver; v-prefixed tag) ---

        /// <summary>
        /// The Git release TAG for a server version: the version with a leading <c>v</c> (e.g.
        /// <c>8.0.0</c> → <c>v8.0.0</c>). GameDev-MCP-Server tags every release <c>v&lt;version&gt;</c>
        /// and the per-platform server zips are attached to THAT tag — so the download path MUST use the
        /// v-prefixed tag, never the bare version (a bare-version path 404s). Already-v-prefixed input is
        /// passed through unchanged so a caller cannot accidentally double-prefix.
        /// </summary>
        public static string ReleaseTag(string serverVersion)
        {
            var version = (serverVersion ?? string.Empty).Trim();
            return version.StartsWith("v", StringComparison.Ordinal) ? version : "v" + version;
        }

        /// <summary>
        /// The download URL for an explicit server version's zip:
        /// <c>https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v&lt;serverVersion&gt;/gamedev-mcp-server-&lt;os&gt;-&lt;arch&gt;.zip</c>.
        /// The <paramref name="serverVersion"/> selects an EXACT server release (no semver range); it is
        /// mapped to the release tag via <see cref="ReleaseTag"/> (the <c>v</c>-prefixed tag the shared
        /// repo cuts), then used verbatim. Mirrors Unity's <c>ExecutableZipUrl</c> shape. Production
        /// callers must use the parameterless <see cref="DownloadUrl(OSPlatform, Architecture)"/> overload,
        /// which pins <see cref="ServerVersion"/> — NEVER the addon version (the two diverge).
        /// </summary>
        public static string DownloadUrl(string serverVersion, OSPlatform os, Architecture arch) =>
            $"https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/{ReleaseTag(serverVersion)}/{AssetZipName(os, arch)}";

        /// <summary>
        /// The download URL for the PINNED <see cref="ServerVersion"/> server zip for a platform — the one
        /// production path the manager uses. Deliberately takes NO version parameter so a caller cannot
        /// accidentally feed the addon version into the URL (addon 0.x and server 8.x diverge).
        /// </summary>
        public static string DownloadUrl(OSPlatform os, Architecture arch) =>
            DownloadUrl(ServerVersion, os, arch);

        // --- SHA256SUMS integrity manifest (download-verify-before-execute, fail-closed) ---

        /// <summary>
        /// The name of the integrity manifest asset attached to every GameDev-MCP-Server release: a standard
        /// coreutils <c>sha256sum</c> output file listing one <c>&lt;hex&gt;␠␠&lt;filename&gt;</c> line per
        /// per-RID server zip. LIVE on the pinned <c>v8.0.0</c> release (and every future release).
        /// </summary>
        public const string Sha256SumsAssetName = "SHA256SUMS";

        /// <summary>
        /// The URL of the release's <c>SHA256SUMS</c> manifest — the SIBLING of the per-RID zip
        /// <see cref="DownloadUrl(string, OSPlatform, Architecture)"/> under the SAME <c>v&lt;serverVersion&gt;</c>
        /// release tag:
        /// <c>https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v&lt;serverVersion&gt;/SHA256SUMS</c>.
        /// The downloaded zip's SHA256 is verified against this manifest BEFORE extraction/execution
        /// (fail-closed). Pure string build — unit-testable with no Godot binary.
        /// </summary>
        public static string Sha256SumsUrl(string serverVersion) =>
            $"https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/{ReleaseTag(serverVersion)}/{Sha256SumsAssetName}";

        /// <summary>
        /// The <c>SHA256SUMS</c> manifest URL for the PINNED <see cref="ServerVersion"/> — the production path,
        /// taking NO version parameter so the manifest tag can never drift from the zip tag.
        /// </summary>
        public static string Sha256SumsUrl() => Sha256SumsUrl(ServerVersion);

        /// <summary>
        /// Parse a coreutils <c>sha256sum</c> manifest into a <c>{filename → lowercase-hex-digest}</c> map.
        /// The exact LIVE format is one line per file: a 64-character lowercase hex digest, then TWO spaces
        /// (the coreutils text-mode separator), then the file name —
        /// <c>844d4ad8…53319␠␠gamedev-mcp-server-linux-x64.zip</c>. Tolerances applied (so a hand-edited or
        /// CRLF manifest still parses, while a malformed one yields no usable entry):
        /// <list type="bullet">
        /// <item>CRLF and bare-LF line endings; blank lines skipped.</item>
        /// <item>Leading/trailing whitespace on each line trimmed.</item>
        /// <item>The coreutils binary-mode <c>'*'</c> marker before the filename (<c>&lt;hex&gt; *&lt;name&gt;</c>)
        /// is stripped.</item>
        /// <item>A line whose first token is NOT a 64-char hex string, or which has no filename, is SKIPPED
        /// (it never produces a spurious entry — fail-closed at the lookup layer).</item>
        /// </list>
        /// Digests are normalized to lowercase; filenames are kept verbatim (case-sensitive, matching the
        /// asset names). On a duplicate filename the LAST entry wins. Never throws — a null/empty/garbage
        /// input yields an empty map. Pure managed; unit-testable with no Godot binary.
        /// </summary>
        public static System.Collections.Generic.IReadOnlyDictionary<string, string> ParseSha256Sums(string? sha256SumsText)
        {
            var map = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.Ordinal);
            if (string.IsNullOrEmpty(sha256SumsText))
                return map;

            foreach (var rawLine in sha256SumsText!.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0)
                    continue;

                // Split into the digest token and the remainder (the filename). The coreutils separator is two
                // spaces, but we split on the FIRST run of whitespace so a single-space or tab variant still
                // parses — the digest token is fixed-width 64 hex, the filename is everything after.
                var sepIndex = -1;
                for (var i = 0; i < line.Length; i++)
                {
                    if (line[i] == ' ' || line[i] == '\t')
                    {
                        sepIndex = i;
                        break;
                    }
                }
                if (sepIndex <= 0 || sepIndex >= line.Length - 1)
                    continue;

                var digestToken = line.Substring(0, sepIndex);
                if (!IsHex64(digestToken))
                    continue;

                var fileName = line.Substring(sepIndex + 1).TrimStart(' ', '\t');
                // coreutils binary-mode marker: `<hex> *<name>`. Strip a single leading '*'.
                if (fileName.StartsWith("*", System.StringComparison.Ordinal))
                    fileName = fileName.Substring(1);
                fileName = fileName.Trim();
                if (fileName.Length == 0)
                    continue;

                map[fileName] = digestToken.ToLowerInvariant();
            }

            return map;
        }

        /// <summary>True when <paramref name="value"/> is exactly 64 ASCII hex characters (a SHA256 hex digest).</summary>
        static bool IsHex64(string value)
        {
            if (value.Length != 64)
                return false;
            foreach (var c in value)
            {
                var isHex = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!isHex)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Look up the expected SHA256 digest for <paramref name="assetZipName"/> (e.g.
        /// <c>gamedev-mcp-server-win-x64.zip</c>) in a parsed <see cref="ParseSha256Sums"/> map. Returns the
        /// lowercase hex digest, or null when the manifest has no entry for that asset (the MISSING-entry
        /// fail-closed case). Pure managed.
        /// </summary>
        public static string? LookupDigest(
            System.Collections.Generic.IReadOnlyDictionary<string, string> parsedSha256Sums,
            string assetZipName)
        {
            if (parsedSha256Sums == null || string.IsNullOrEmpty(assetZipName))
                return null;
            return parsedSha256Sums.TryGetValue(assetZipName, out var digest) ? digest : null;
        }

        /// <summary>
        /// Case-insensitive hex-digest equality (both sides trimmed). A null/empty digest on either side is
        /// NEVER a match (fail-closed: an unknown digest must not pass). Pure managed.
        /// </summary>
        public static bool DigestMatches(string? expectedHexDigest, string? actualHexDigest)
        {
            if (string.IsNullOrWhiteSpace(expectedHexDigest) || string.IsNullOrWhiteSpace(actualHexDigest))
                return false;
            return string.Equals(expectedHexDigest!.Trim(), actualHexDigest!.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The verdict of verifying a downloaded zip against a release <c>SHA256SUMS</c> manifest.
        /// </summary>
        public enum ChecksumVerdict
        {
            /// <summary>The manifest parsed, contained this asset's entry, and the digest matched. SAFE to extract/execute.</summary>
            Verified,

            /// <summary>The manifest text was missing/empty/unparsable (no usable entries). Fail-closed.</summary>
            ManifestUnparsable,

            /// <summary>The manifest parsed but had no line for this asset's zip name. Fail-closed.</summary>
            MissingEntry,

            /// <summary>The manifest's entry for this asset did NOT match the downloaded zip's digest. Fail-closed.</summary>
            DigestMismatch
        }

        /// <summary>
        /// The single fail-closed integrity decision the editor manager calls BEFORE
        /// <c>ZipFile.ExtractToDirectory</c> / <c>Process.Start</c>: parse the release's <c>SHA256SUMS</c>,
        /// find the entry for <paramref name="assetZipName"/>, and compare it (case-insensitive hex) against
        /// the locally-computed SHA256 of the downloaded zip (<paramref name="actualZipHexDigest"/>). Returns
        /// <see cref="ChecksumVerdict.Verified"/> ONLY when the manifest parsed, contained the asset, and the
        /// digest matched; every other outcome is a distinct fail-closed verdict the caller MUST treat as
        /// "do NOT extract, do NOT launch". Keeping this here (not inline in the <c>#if TOOLS</c> manager)
        /// makes the entire decision unit-testable with no Godot binary and no real download. Never throws.
        /// </summary>
        /// <param name="sha256SumsText">The raw downloaded <c>SHA256SUMS</c> manifest text.</param>
        /// <param name="assetZipName">This RID's zip name, e.g. <c>gamedev-mcp-server-win-x64.zip</c>.</param>
        /// <param name="actualZipHexDigest">The SHA256 of the downloaded zip, as lowercase/any-case hex.</param>
        public static ChecksumVerdict VerifyZipChecksum(string? sha256SumsText, string assetZipName, string? actualZipHexDigest)
        {
            var parsed = ParseSha256Sums(sha256SumsText);
            if (parsed.Count == 0)
                return ChecksumVerdict.ManifestUnparsable;

            var expected = LookupDigest(parsed, assetZipName);
            if (expected == null)
                return ChecksumVerdict.MissingEntry;

            return DigestMatches(expected, actualZipHexDigest)
                ? ChecksumVerdict.Verified
                : ChecksumVerdict.DigestMismatch;
        }

        /// <summary>
        /// A short, actionable human-readable reason for a non-<see cref="ChecksumVerdict.Verified"/> verdict,
        /// for the editor manager's fail-closed log line. Pure string transform.
        /// </summary>
        public static string ChecksumFailureReason(ChecksumVerdict verdict, string assetZipName) => verdict switch
        {
            ChecksumVerdict.ManifestUnparsable =>
                $"the downloaded {Sha256SumsAssetName} manifest was empty or unparsable",
            ChecksumVerdict.MissingEntry =>
                $"the {Sha256SumsAssetName} manifest has no entry for '{assetZipName}'",
            ChecksumVerdict.DigestMismatch =>
                $"the downloaded '{assetZipName}' SHA256 did not match the {Sha256SumsAssetName} manifest entry",
            _ => "the checksum was verified"
        };

        // --- Version match (EXACT, like Unity) ---

        /// <summary>
        /// True only when a cached binary's recorded version EXACTLY equals the expected version — the
        /// pinned <see cref="ServerVersion"/> in production — (ordinal, trimmed). A null/empty cached
        /// version (no <c>version</c> file) never matches. Mirrors Unity's <c>IsVersionMatches</c>
        /// (which is a plain <c>==</c>).
        /// </summary>
        public static bool VersionMatches(string? cachedVersion, string expectedVersion)
        {
            if (string.IsNullOrEmpty(cachedVersion))
                return false;

            return string.Equals(cachedVersion!.Trim(), (expectedVersion ?? string.Empty).Trim(), StringComparison.Ordinal);
        }

        // --- Launch argument builder (matches Unity's BuildArguments shape) ---

        /// <summary>
        /// Build the server process argument string:
        /// <c>port=&lt;p&gt; plugin-timeout=&lt;t&gt; client-transport=streamableHttp authorization=&lt;a&gt; [token=&lt;tok&gt;]</c>.
        /// The transport is ALWAYS <c>streamableHttp</c> — the only transport the plugin's own SignalR
        /// client can connect to (we deliberately do NOT build a <c>stdio</c> launch path the plugin cannot
        /// consume). The <paramref name="token"/> is appended ONLY when <paramref name="authRequired"/> is
        /// true AND the token is non-empty; it is the secret and is NEVER otherwise emitted. Mirrors Unity's
        /// <c>McpServerManager.BuildArguments</c>.
        /// </summary>
        /// <param name="port">The TCP port the server should listen on (the plugin connects to this port).</param>
        /// <param name="pluginTimeoutMs">The plugin-timeout argument in milliseconds.</param>
        /// <param name="authRequired">Whether the connection requires a bearer token.</param>
        /// <param name="token">The bearer token (secret); appended only when <paramref name="authRequired"/> and non-empty.</param>
        public static string BuildLaunchArguments(int port, int pluginTimeoutMs, bool authRequired, string? token)
        {
            var authValue = authRequired ? McpServerConsts.AuthOption.required : McpServerConsts.AuthOption.none;

            var sb = new StringBuilder();
            sb.Append(McpServerConsts.Args.Port).Append('=').Append(port).Append(' ');
            sb.Append(McpServerConsts.Args.PluginTimeout).Append('=').Append(pluginTimeoutMs).Append(' ');
            sb.Append(McpServerConsts.Args.ClientTransportMethod).Append('=').Append(McpServerConsts.TransportMethod.streamableHttp).Append(' ');
            sb.Append(McpServerConsts.Args.Authorization).Append('=').Append(authValue);

            if (authRequired && !string.IsNullOrEmpty(token))
                sb.Append(' ').Append(McpServerConsts.Args.Token).Append('=').Append(token);

            return sb.ToString();
        }

        // --- Status presentation (status -> button text / label / circle state), pure + unit-tested ---

        /// <summary>Button label shown when the local server is stopped (clicking starts it).</summary>
        public const string ButtonTextStart = "Start Server";

        /// <summary>Button label shown while the local server is starting (the button is disabled).</summary>
        public const string ButtonTextStarting = "Starting…";

        /// <summary>Button label shown when the local server is running (clicking stops it).</summary>
        public const string ButtonTextStop = "Stop Server";

        /// <summary>Button label shown while the local server is stopping (the button is disabled).</summary>
        public const string ButtonTextStopping = "Stopping…";

        /// <summary>Button label shown when an external server we did not launch owns the port (the button is disabled).</summary>
        public const string ButtonTextExternal = "External";

        /// <summary>The Start/Stop button text for a given server status.</summary>
        public static string ServerButtonText(GodotMcpServerStatus status) => status switch
        {
            GodotMcpServerStatus.Stopped => ButtonTextStart,
            GodotMcpServerStatus.Starting => ButtonTextStarting,
            GodotMcpServerStatus.Running => ButtonTextStop,
            GodotMcpServerStatus.Stopping => ButtonTextStopping,
            GodotMcpServerStatus.External => ButtonTextExternal,
            _ => ButtonTextStart
        };

        /// <summary>
        /// True when the Start/Stop button should be disabled — during the transient
        /// <see cref="GodotMcpServerStatus.Starting"/> / <see cref="GodotMcpServerStatus.Stopping"/> states
        /// (neither start nor stop is a clean action mid-transition) and when the port is owned by an
        /// <see cref="GodotMcpServerStatus.External"/> server we cannot control.
        /// </summary>
        public static bool ServerButtonDisabled(GodotMcpServerStatus status) =>
            status == GodotMcpServerStatus.Starting ||
            status == GodotMcpServerStatus.Stopping ||
            status == GodotMcpServerStatus.External;

        /// <summary>The "MCP server: …" status line text for a given server status.</summary>
        public static string ServerStatusLabel(GodotMcpServerStatus status) => status switch
        {
            GodotMcpServerStatus.Stopped => "Local server: Stopped",
            GodotMcpServerStatus.Starting => "Local server: Starting…",
            GodotMcpServerStatus.Running => "Local server: Running",
            GodotMcpServerStatus.Stopping => "Local server: Stopping…",
            GodotMcpServerStatus.External => "Local server: External (already running)",
            _ => "Local server: Stopped"
        };

        /// <summary>
        /// Map the server status to the timeline circle's <see cref="ConnectionPanelView.TimelinePointState"/>
        /// so the MCP-server point's circle reflects the LOCAL server's lifecycle: a verified
        /// <see cref="GodotMcpServerStatus.Running"/> (or an <see cref="GodotMcpServerStatus.External"/>
        /// server occupying the port) is the filled-green <c>Online</c> disc; the transient
        /// Starting/Stopping states are the green <c>Connecting</c> ring; Stopped is the orange
        /// <c>Disconnected</c> disc. The editor paints the returned state 1:1.
        /// </summary>
        public static ConnectionPanelView.TimelinePointState ServerPointState(GodotMcpServerStatus status) => status switch
        {
            GodotMcpServerStatus.Running => ConnectionPanelView.TimelinePointState.Online,
            GodotMcpServerStatus.External => ConnectionPanelView.TimelinePointState.Online,
            GodotMcpServerStatus.Starting => ConnectionPanelView.TimelinePointState.Connecting,
            GodotMcpServerStatus.Stopping => ConnectionPanelView.TimelinePointState.Connecting,
            _ => ConnectionPanelView.TimelinePointState.Disconnected
        };

        /// <summary>The status-dot RGB for a given server status (reuses the connection palette).</summary>
        public static (float R, float G, float B) ServerStatusColor(GodotMcpServerStatus status) => status switch
        {
            GodotMcpServerStatus.Running => ConnectionPanelView.ColorConnected,
            GodotMcpServerStatus.External => ConnectionPanelView.ColorConnected,
            GodotMcpServerStatus.Starting => ConnectionPanelView.ColorConnecting,
            GodotMcpServerStatus.Stopping => ConnectionPanelView.ColorConnecting,
            _ => ConnectionPanelView.ColorDisconnected
        };

        // --- Local-server port extraction from the configured Custom host URL ---

        /// <summary>
        /// Resolve the port the locally-hosted server should listen on, parsed from the configured Custom
        /// host URL (e.g. <c>http://localhost:5300</c> → <c>5300</c>). When the URL has no explicit port,
        /// or is not parseable, falls back to <paramref name="defaultPort"/> (pass
        /// <c>Consts.Hub.DefaultPort</c>). Deterministic string/URI parse — no platform divergence.
        /// </summary>
        public static int ResolveServerPort(string? customHostUrl, int defaultPort)
        {
            if (string.IsNullOrWhiteSpace(customHostUrl))
                return defaultPort;

            if (Uri.TryCreate(customHostUrl!.Trim(), UriKind.Absolute, out var uri) && uri.Port > 0)
                return uri.Port;

            return defaultPort;
        }
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) ownership test used by the orphan-process
    /// cleanup to decide whether a running <c>gamedev-mcp-server</c> process belongs to THIS project — so the
    /// cleanup can never cross-kill another Godot project's hosted server. The decision is a deterministic,
    /// cross-platform path comparison (no <c>Path</c> APIs, no <c>netstat</c>/<c>lsof</c>), so the same
    /// assertions hold on the Linux CI runner and on a Windows dev box.
    /// </summary>
    public static class GodotMcpServerOwnership
    {
        /// <summary>
        /// True when <paramref name="candidateExecutablePath"/> (a running process's executable path) is THIS
        /// project's cached server binary — i.e. it resolves to the same file as
        /// <paramref name="ownExecutablePath"/> (the path returned by the manager's <c>ExecutableFullPath()</c>),
        /// OR it lives in the same containing directory. Both paths are produced by the manager from the same
        /// cache root, so a same-directory match is the load-bearing signal that the process is ours.
        ///
        /// <para>
        /// Comparison normalizes backslash/forward-slash separators and is case-insensitive (Windows paths are
        /// case-insensitive; the macOS default volume is too; on the rare case-sensitive Linux volume this only
        /// ever broadens "is it ours" slightly toward our OWN cache dir — it never matches a DIFFERENT
        /// project, which is the safety property that matters). A null/empty candidate is never owned (fail
        /// closed: a process we cannot attribute is never killed).
        /// </para>
        /// </summary>
        public static bool IsOwnedByThisProject(string? candidateExecutablePath, string? ownExecutablePath)
        {
            if (string.IsNullOrEmpty(candidateExecutablePath) || string.IsNullOrEmpty(ownExecutablePath))
                return false;

            var candidate = NormalizePath(candidateExecutablePath!);
            var own = NormalizePath(ownExecutablePath!);

            // Exact same binary file.
            if (string.Equals(candidate, own, StringComparison.OrdinalIgnoreCase))
                return true;

            // Same containing directory (our cache platform folder) — the candidate must start with our
            // directory prefix (bounded by the trailing slash so a sibling dir with a shared name prefix
            // cannot match).
            var ownDir = DirectoryPrefix(own);
            if (ownDir.Length == 0)
                return false;

            return candidate.StartsWith(ownDir, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Replace backslashes with forward slashes so the comparison is separator-agnostic.</summary>
        static string NormalizePath(string path) => path.Replace('\\', '/');

        /// <summary>
        /// The directory prefix of a normalized path, INCLUDING the trailing slash (so a <c>StartsWith</c>
        /// match is bounded to the directory and cannot match a sibling whose name merely shares a prefix).
        /// Returns empty when the path has no separator.
        /// </summary>
        static string DirectoryPrefix(string normalizedPath)
        {
            var idx = normalizedPath.LastIndexOf('/');
            return idx < 0 ? string.Empty : normalizedPath.Substring(0, idx + 1);
        }
    }
}
