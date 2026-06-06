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
        /// The server executable base name (matches the <c>AssemblyName</c> in
        /// <c>com.IvanMurzak.Godot.MCP.Server.csproj</c>). On Windows the on-disk file is
        /// <c>godot-mcp-server.exe</c>; on Unix it is <c>godot-mcp-server</c>.
        /// </summary>
        public const string ExecutableName = "godot-mcp-server";

        /// <summary>
        /// The GitHub release-asset / .NET RID prefix. The release workflow zips each self-contained build
        /// as <c>godot-mcp-server-&lt;rid&gt;.zip</c> (see <c>Godot-MCP-Server/build-all.sh</c>), so the asset
        /// stem is the executable name plus the platform RID.
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
        /// The on-disk executable file name for an os: <c>godot-mcp-server.exe</c> on Windows, else
        /// <c>godot-mcp-server</c>. Mirrors Unity's <c>ExecutableFullName</c>.
        /// </summary>
        public static string ExecutableFileName(OSPlatform os) =>
            os == OSPlatform.Windows ? ExecutableName + ".exe" : ExecutableName;

        /// <summary>The on-disk executable file name for the current OS.</summary>
        public static string CurrentExecutableFileName() => ExecutableFileName(CurrentOsPlatform());

        /// <summary>
        /// The GitHub release zip asset name for a platform: <c>godot-mcp-server-&lt;os&gt;-&lt;arch&gt;.zip</c>
        /// (e.g. <c>godot-mcp-server-win-x64.zip</c>). Matches <c>build-all.sh</c>'s <c>ZIP_NAME</c>.
        /// </summary>
        public static string AssetZipName(OSPlatform os, Architecture arch) =>
            $"{AssetPrefix}-{PlatformName(os, arch)}.zip";

        // --- Release URL construction (exact version, no semver) ---

        /// <summary>
        /// The download URL for the version-matched server zip:
        /// <c>https://github.com/IvanMurzak/Godot-MCP/releases/download/&lt;version&gt;/godot-mcp-server-&lt;os&gt;-&lt;arch&gt;.zip</c>.
        /// The <paramref name="addonVersion"/> is used VERBATIM (an EXACT plugin-version → server-version
        /// match, like Unity — no semver range). Mirrors Unity's <c>ExecutableZipUrl</c>.
        /// </summary>
        public static string DownloadUrl(string addonVersion, OSPlatform os, Architecture arch) =>
            $"https://github.com/IvanMurzak/Godot-MCP/releases/download/{addonVersion}/{AssetZipName(os, arch)}";

        // --- Version match (EXACT, like Unity) ---

        /// <summary>
        /// True only when a cached binary's recorded version EXACTLY equals the addon version (ordinal,
        /// trimmed). A null/empty cached version (no <c>version</c> file) never matches. Mirrors Unity's
        /// <c>IsVersionMatches</c> (which is a plain <c>==</c>).
        /// </summary>
        public static bool VersionMatches(string? cachedVersion, string addonVersion)
        {
            if (string.IsNullOrEmpty(cachedVersion))
                return false;

            return string.Equals(cachedVersion!.Trim(), (addonVersion ?? string.Empty).Trim(), StringComparison.Ordinal);
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
}
