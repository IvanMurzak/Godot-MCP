/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
// Alias the BCL types that collide with same-named Godot types (Godot.Environment / Godot.HttpClient).
using SystemEnvironment = System.Environment;
using HttpClient = System.Net.Http.HttpClient;
using HttpCompletionOption = System.Net.Http.HttpCompletionOption;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Editor-only (<c>#if TOOLS</c>) lifecycle manager for the locally-hosted <c>gamedev-mcp-server</c>
    /// binary — the shared, engine-agnostic MCP server released from
    /// https://github.com/IvanMurzak/GameDev-MCP-Server (the Godot analog of Unity-MCP's
    /// <c>McpServerManager</c>). It downloads the server zip pinned by
    /// <see cref="GodotMcpServerView.ServerVersion"/> from the GameDev-MCP-Server GitHub release, caches it
    /// under the project's <c>.godot/mcp-server/&lt;rid&gt;/</c>
    /// folder (gitignored), unzips it, marks it executable on Unix, launches it as a child process, monitors
    /// the ~5s startup window, persists the PID so an editor recompile can reconnect to a still-running
    /// server, and stops it gracefully (then force-kills) on request or editor quit.
    ///
    /// <para>
    /// This makes Godot-MCP able to HOST its own MCP server (not just connect to an external one). All the
    /// deterministic, cross-platform decisions (asset name, version match, download URL, launch-arg string,
    /// status→button/label/circle mappings, port extraction) live in the pure-managed
    /// <see cref="GodotMcpServerView"/> so they are unit-tested in the plain-xUnit host with no Godot binary;
    /// this class only performs the side effects and marshals status changes onto the editor main thread.
    /// </para>
    ///
    /// <para>
    /// Version pin note: the consumed server version is the <see cref="GodotMcpServerView.ServerVersion"/>
    /// constant — NOT the addon version (addon 0.x and shared server 8.x diverge). Bumping the consumed
    /// server = changing that constant; the corresponding GameDev-MCP-Server release must already exist.
    /// </para>
    /// </summary>
    public sealed class GodotMcpServerManager : IDisposable
    {
        /// <summary>~5s startup-verification window, matching Unity's <c>verificationDelaySeconds</c>.</summary>
        const double StartupVerificationSeconds = 5.0;

        readonly Action<string> _log;
        readonly Action<string> _logWarning;
        readonly Action<string> _logError;
        readonly object _processMutex = new();

        Process? _serverProcess;
        GodotMcpServerStatus _status = GodotMcpServerStatus.Stopped;

        /// <summary>
        /// Raised whenever the server status changes. ALWAYS raised on the editor main thread (the manager
        /// marshals via <see cref="MainThreadDispatcher"/> when a process event fires off
        /// a thread-pool thread), so subscribers — the dock's <c>ConnectionPanel</c> — may touch Controls
        /// directly. Mirrors the <c>ConnectionStatusChanged</c> contract.
        /// </summary>
        public event Action<GodotMcpServerStatus>? StatusChanged;

        /// <summary>The current server status (last value pushed to <see cref="StatusChanged"/>).</summary>
        public GodotMcpServerStatus Status
        {
            get { lock (_processMutex) { return _status; } }
        }

        public GodotMcpServerManager(
            Action<string> log,
            Action<string> logWarning,
            Action<string> logError)
        {
            _log = log;
            _logWarning = logWarning;
            _logError = logError;
        }

        // --- Cache paths ---------------------------------------------------------------------------------

        /// <summary>
        /// The per-project cache ROOT: <c>&lt;projectRoot&gt;/.godot/mcp-server</c> (globalized from
        /// <c>user://</c>'s sibling — we use the project's <c>res://.godot</c> so the binary lives next to the
        /// project and is wiped with the editor cache; <c>.godot/</c> is gitignored). The per-platform binary
        /// lives in a <c>&lt;rid&gt;/</c> subfolder beneath this.
        /// </summary>
        public static string CacheRootPath()
            => ProjectSettings.GlobalizePath("res://.godot/mcp-server");

        /// <summary>The per-platform cache folder: <c>&lt;cacheRoot&gt;/&lt;rid&gt;</c> (e.g. <c>.../win-x64</c>).</summary>
        public static string CachePlatformPath()
            => Path.Combine(CacheRootPath(), GodotMcpServerView.CurrentPlatformName());

        /// <summary>Full path to the executable inside the per-platform cache folder.</summary>
        public static string ExecutableFullPath()
            => Path.Combine(CachePlatformPath(), GodotMcpServerView.CurrentExecutableFileName());

        /// <summary>Full path to the <c>version</c> marker file inside the per-platform cache folder.</summary>
        public static string VersionFilePath()
            => Path.Combine(CachePlatformPath(), "version");

        // --- CI detection --------------------------------------------------------------------------------

        /// <summary>
        /// True when running under CI / a headless smoke (the standard <c>CI</c> env var, which GitHub
        /// Actions and most CI providers set, plus <c>GITHUB_ACTIONS</c>). In CI the download is SKIPPED —
        /// there is no release to pull and no need to host a server. Mirrors Unity's <c>EnvironmentUtils.IsCi</c>.
        /// </summary>
        public static bool IsCi()
        {
            static bool Truthy(string? v) =>
                !string.IsNullOrEmpty(v) && !string.Equals(v, "false", StringComparison.OrdinalIgnoreCase) && v != "0";

            return Truthy(SystemEnvironment.GetEnvironmentVariable("CI"))
                || Truthy(SystemEnvironment.GetEnvironmentVariable("GITHUB_ACTIONS"));
        }

        // --- Binary lifecycle ----------------------------------------------------------------------------

        /// <summary>True when the cached executable exists on disk.</summary>
        public static bool IsBinaryExists() => File.Exists(ExecutableFullPath());

        /// <summary>The version recorded in the cache's <c>version</c> file, or null when absent.</summary>
        public static string? GetCachedVersion()
        {
            var path = VersionFilePath();
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// True when the cached binary exists AND its recorded version EXACTLY matches the pinned
        /// <see cref="GodotMcpServerView.ServerVersion"/>.
        /// </summary>
        public bool IsVersionMatches()
            => GodotMcpServerView.VersionMatches(GetCachedVersion(), GodotMcpServerView.ServerVersion);

        /// <summary>
        /// Download + unpack the version-matched server binary when it is missing or stale. No-op (returns
        /// true) when the cached binary already matches. SKIPPED in CI (returns false). Mirrors Unity's
        /// <c>DownloadServerBinaryIfNeeded</c>.
        /// </summary>
        public async Task<bool> DownloadServerBinaryIfNeeded()
        {
            if (IsCi())
            {
                _log("[Godot-MCP] skipping MCP server download in CI environment.");
                return false;
            }

            if (IsBinaryExists() && IsVersionMatches())
                return true;

            return await DownloadAndUnpackBinary();
        }

        /// <summary>
        /// Download the release zip for this platform, replace the per-platform cache folder, unzip it,
        /// mark the binary executable on Unix, and write the <c>version</c> marker. Returns true only when
        /// the binary exists and the version matches afterward. Never throws — failures are logged and
        /// reported as <c>false</c>. The token is not involved here (download is anonymous).
        /// </summary>
        public async Task<bool> DownloadAndUnpackBinary()
        {
            var os = GodotMcpServerView.CurrentOsPlatform();
            var arch = RuntimeInformation.ProcessArchitecture;
            var serverVersion = GodotMcpServerView.ServerVersion;
            var url = GodotMcpServerView.DownloadUrl(os, arch);
            var platformFolder = CachePlatformPath();
            var executablePath = ExecutableFullPath();

            _log($"[Godot-MCP] downloading GameDev-MCP-Server binary from: {url}");

            try
            {
                // Replace any stale per-platform folder so a partial/old extract can't linger.
                if (Directory.Exists(platformFolder))
                    TryDeleteDirectory(platformFolder);
                Directory.CreateDirectory(platformFolder);

                var archivePath = Path.Combine(Path.GetTempPath(),
                    $"{GodotMcpServerView.AssetPrefix}-{GodotMcpServerView.PlatformName(os, arch)}-{serverVersion}.zip");

                using (var client = new HttpClient())
                {
                    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    await using var src = await response.Content.ReadAsStreamAsync();
                    await using var dst = File.Create(archivePath);
                    await src.CopyToAsync(dst);
                }

                // FAIL-CLOSED INTEGRITY GATE (verify-before-execute). The zip is on disk but UNTRUSTED. Before
                // extracting or launching it, download the release's SHA256SUMS manifest (sibling of the zip
                // URL under the same v<ServerVersion> tag), compute the downloaded zip's SHA256 (pure BCL), and
                // compare against the manifest entry for THIS RID. On MISMATCH / MISSING entry / unparsable
                // manifest we delete the temp zip and return the existing download-failure path WITHOUT
                // extracting or launching — an unverified binary must NEVER be executed (a compromised release
                // asset or a trusted-CA MITM would otherwise yield arbitrary code execution; issue #192).
                var assetZipName = GodotMcpServerView.AssetZipName(os, arch);
                if (!await VerifyDownloadedArchive(archivePath, serverVersion, assetZipName))
                {
                    try { File.Delete(archivePath); } catch { /* best effort */ }
                    return false;
                }

                _log($"[Godot-MCP] unpacking GameDev-MCP-Server binary to: {platformFolder}");
                // The shared GameDev-MCP-Server release zips are NOT layout-uniform: the win zips are FLAT
                // (gamedev-mcp-server.exe at the zip root) while the osx/linux zips wrap the binary in a
                // <rid>/ folder. Extract to a throwaway staging folder, FIND the binary wherever it landed,
                // then move its containing folder's files into the per-platform cache folder — so BOTH
                // layouts (and any future re-arrangement) resolve correctly.
                var stagingFolder = Path.Combine(Path.GetTempPath(),
                    $"{GodotMcpServerView.AssetPrefix}-extract-{Guid.NewGuid():N}");
                try
                {
                    ZipFile.ExtractToDirectory(archivePath, stagingFolder, overwriteFiles: true);

                    var extractedBinary = FindExtractedBinary(stagingFolder, GodotMcpServerView.CurrentExecutableFileName());
                    if (extractedBinary == null)
                    {
                        _logError($"[Godot-MCP] server binary '{GodotMcpServerView.CurrentExecutableFileName()}' not found inside the downloaded zip.");
                        return false;
                    }

                    // Move the binary plus any sidecar files beside it (none today; defensive) into the cache.
                    var sourceFolder = Path.GetDirectoryName(extractedBinary)!;
                    foreach (var file in Directory.GetFiles(sourceFolder))
                    {
                        var destination = Path.Combine(platformFolder, Path.GetFileName(file));
                        if (File.Exists(destination))
                            File.Delete(destination);
                        File.Move(file, destination);
                    }
                }
                finally
                {
                    TryDeleteDirectory(stagingFolder);
                    try { File.Delete(archivePath); } catch { /* best effort */ }
                }

                if (!File.Exists(executablePath))
                {
                    _logError($"[Godot-MCP] server binary not found after unpack at: {executablePath}");
                    return false;
                }

                if (os != OSPlatform.Windows)
                    TrySetExecutable(executablePath);

                File.WriteAllText(VersionFilePath(), serverVersion);

                var success = IsBinaryExists() && IsVersionMatches();
                _log(success
                    ? $"[Godot-MCP] server binary ready: {executablePath} (version {serverVersion})"
                    : "[Godot-MCP] server binary download completed but verification failed.");
                return success;
            }
            catch (Exception ex)
            {
                _logError($"[Godot-MCP] failed to download/unpack server binary: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// The number of attempts for the SHA256SUMS manifest fetch (1 initial + retries) before we
        /// fail-closed. A TRANSIENT network error on the manifest fetch is retried (the binary is already
        /// downloaded; only the integrity manifest is missing) — but a persistent failure NEVER falls through
        /// to executing an unverified binary.
        /// </summary>
        const int Sha256SumsFetchAttempts = 3;

        /// <summary>Backoff between SHA256SUMS fetch attempts.</summary>
        static readonly TimeSpan Sha256SumsRetryDelay = TimeSpan.FromSeconds(1.0);

        /// <summary>
        /// Fail-closed verify-before-execute gate. Downloads the release's <c>SHA256SUMS</c> manifest (with a
        /// bounded transient-retry), computes the downloaded zip's SHA256 (pure BCL
        /// <see cref="SHA256.HashData(System.ReadOnlySpan{byte})"/> — no new deps), and compares against the
        /// manifest entry for <paramref name="assetZipName"/> via the pure-managed
        /// <see cref="GodotMcpServerView.VerifyZipChecksum"/>. Returns true ONLY when the digest matched the
        /// manifest. Every failure path — a manifest we could not fetch after all retries, an unparsable
        /// manifest, a missing entry, or a digest mismatch — returns false with a clear, actionable error so
        /// the caller skips extraction/launch. Never throws.
        /// </summary>
        async Task<bool> VerifyDownloadedArchive(string archivePath, string serverVersion, string assetZipName)
        {
            var sumsUrl = GodotMcpServerView.Sha256SumsUrl(serverVersion);

            // 1) Fetch the integrity manifest (bounded transient-retry). A null result means every attempt
            //    failed — fail-closed (do NOT execute an unverified binary).
            var sha256SumsText = await FetchSha256SumsText(sumsUrl);
            if (sha256SumsText == null)
            {
                _logError(
                    $"[Godot-MCP] refusing to launch server: could not download the {GodotMcpServerView.Sha256SumsAssetName} " +
                    $"integrity manifest from {sumsUrl} after {Sha256SumsFetchAttempts} attempt(s). " +
                    "The downloaded binary was NOT verified and will not be executed (fail-closed).");
                return false;
            }

            // 2) Compute the downloaded zip's SHA256 (pure BCL).
            string actualHexDigest;
            try
            {
                await using var zipStream = File.OpenRead(archivePath);
                var hashBytes = await SHA256.HashDataAsync(zipStream);
                actualHexDigest = Convert.ToHexString(hashBytes); // upper-case hex; compare is case-insensitive
            }
            catch (Exception ex)
            {
                _logError($"[Godot-MCP] refusing to launch server: failed to compute the downloaded zip's SHA256: {ex.Message}");
                return false;
            }

            // 3) Parse + compare via the pure-managed verifier (unit-tested with no Godot binary).
            var verdict = GodotMcpServerView.VerifyZipChecksum(sha256SumsText, assetZipName, actualHexDigest);
            if (verdict != GodotMcpServerView.ChecksumVerdict.Verified)
            {
                _logError(
                    $"[Godot-MCP] refusing to launch server: {GodotMcpServerView.ChecksumFailureReason(verdict, assetZipName)}. " +
                    "The binary will not be extracted or executed (fail-closed).");
                return false;
            }

            _log($"[Godot-MCP] verified '{assetZipName}' against {GodotMcpServerView.Sha256SumsAssetName} (SHA256 OK).");
            return true;
        }

        /// <summary>
        /// Download the <c>SHA256SUMS</c> manifest text with a bounded transient-retry. Returns the manifest
        /// body, or null when every attempt failed (the fail-closed signal). The manifest is small text — read
        /// it fully into a string. Never throws.
        /// </summary>
        async Task<string?> FetchSha256SumsText(string sumsUrl)
        {
            for (var attempt = 1; attempt <= Sha256SumsFetchAttempts; attempt++)
            {
                try
                {
                    using var client = new HttpClient();
                    using var response = await client.GetAsync(sumsUrl);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsStringAsync();
                }
                catch (Exception ex)
                {
                    if (attempt < Sha256SumsFetchAttempts)
                    {
                        _logWarning(
                            $"[Godot-MCP] {GodotMcpServerView.Sha256SumsAssetName} fetch attempt {attempt}/{Sha256SumsFetchAttempts} " +
                            $"failed ({ex.Message}); retrying…");
                        try { await Task.Delay(Sha256SumsRetryDelay); } catch { /* ignore */ }
                    }
                    else
                    {
                        _logWarning(
                            $"[Godot-MCP] {GodotMcpServerView.Sha256SumsAssetName} fetch attempt {attempt}/{Sha256SumsFetchAttempts} " +
                            $"failed ({ex.Message}).");
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Locate the extracted server binary under the staging folder, wherever the zip layout put it —
        /// at the root (the FLAT win zips) or nested in a <c>&lt;rid&gt;/</c> folder (the osx/linux zips).
        /// Prefers the SHALLOWEST match so a hypothetical nested duplicate cannot shadow the real binary.
        /// Returns null when the zip contains no file with the expected name.
        /// </summary>
        static string? FindExtractedBinary(string stagingFolder, string executableFileName)
        {
            string? best = null;
            var bestDepth = int.MaxValue;
            foreach (var candidate in Directory.GetFiles(stagingFolder, executableFileName, SearchOption.AllDirectories))
            {
                var relative = Path.GetRelativePath(stagingFolder, candidate);
                var depth = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length;
                if (depth < bestDepth)
                {
                    best = candidate;
                    bestDepth = depth;
                }
            }
            return best;
        }

        // --- Process lifecycle ---------------------------------------------------------------------------

        /// <summary>
        /// Download-if-needed then launch the local server. Returns false when the binary is unavailable
        /// (e.g. in CI, or no release published yet) or the server is already running/transitioning. The
        /// server is launched with <c>client-transport=streamableHttp</c> and the bearer token (when auth is
        /// required) — the token is passed ONLY in the process arguments and is NEVER logged.
        /// </summary>
        public async Task<bool> StartServerAsync(int port, int pluginTimeoutMs, bool authRequired, string? token)
        {
            if (!await DownloadServerBinaryIfNeeded())
            {
                _logWarning("[Godot-MCP] cannot start local server: binary unavailable (CI, or no release published yet).");
                return false;
            }

            return StartServer(port, pluginTimeoutMs, authRequired, token);
        }

        /// <summary>
        /// Launch the already-cached server binary as a child process and begin startup verification.
        /// Synchronous; assumes the binary exists (call <see cref="StartServerAsync"/> to download first).
        /// </summary>
        public bool StartServer(int port, int pluginTimeoutMs, bool authRequired, string? token)
        {
            lock (_processMutex)
            {
                if (_status is GodotMcpServerStatus.Running or GodotMcpServerStatus.Starting or GodotMcpServerStatus.Stopping)
                {
                    _logWarning($"[Godot-MCP] local server is already {_status}.");
                    return false;
                }

                var executablePath = ExecutableFullPath();
                if (!File.Exists(executablePath))
                {
                    _logError($"[Godot-MCP] server binary not found at: {executablePath}");
                    return false;
                }

                SetStatus(GodotMcpServerStatus.Starting, marshalled: false);

                // Free the port from any orphaned server WE previously left behind for THIS project
                // (scoped to this project's cache dir — never cross-kills another project's server).
                KillOrphanedServerProcesses();

                try
                {
                    // The args carry the secret token (when required) — build them via the pure-managed
                    // builder and NEVER log the resulting string.
                    var arguments = GodotMcpServerView.BuildLaunchArguments(port, pluginTimeoutMs, authRequired, token);

                    var startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WorkingDirectory = CachePlatformPath()
                    };

                    var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
                    process.Exited += OnProcessExited;

                    // DRAIN the redirected stdout/stderr so a verbose server cannot fill the ~4KB OS pipe
                    // buffer and BLOCK on a write nothing is reading. The handlers DISCARD every line —
                    // they MUST NOT log the content, because the server echoes its own launch arguments
                    // (including the secret token) to stdout. We keep redirect=true (so that token-bearing
                    // echo never reaches the editor console) and simply throw the drained lines away.
                    process.OutputDataReceived += DiscardServerOutput;
                    process.ErrorDataReceived += DiscardServerOutput;

                    if (!process.Start())
                    {
                        _logError("[Godot-MCP] failed to start local server process.");
                        process.OutputDataReceived -= DiscardServerOutput;
                        process.ErrorDataReceived -= DiscardServerOutput;
                        process.Dispose();
                        SetStatus(GodotMcpServerStatus.Stopped, marshalled: false);
                        return false;
                    }

                    // Begin async draining now that the process is running; pairs with the detach +
                    // CancelOutputRead/CancelErrorRead in CleanupProcess.
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    _serverProcess = process;
                    _log($"[Godot-MCP] local server process started (PID {process.Id}, port {port}); verifying…");

                    ScheduleStartupVerification(process);
                    return true;
                }
                catch (Exception ex)
                {
                    _logError($"[Godot-MCP] failed to start local server: {ex.Message}");
                    SetStatus(GodotMcpServerStatus.Stopped, marshalled: false);
                    return false;
                }
            }
        }

        /// <summary>
        /// Stop the local server: send a graceful terminate signal, then force-kill after a timeout if it
        /// has not exited. When <paramref name="force"/> is true (editor quit) this blocks until exit;
        /// otherwise the wait + force-kill safety net runs on a background task and cleanup happens on the
        /// editor main thread.
        /// </summary>
        public bool StopServer(bool force = false)
        {
            Process? process;
            lock (_processMutex)
            {
                if (_status is GodotMcpServerStatus.Stopped or GodotMcpServerStatus.Stopping)
                    return true;

                if (_serverProcess == null)
                {
                    SetStatus(GodotMcpServerStatus.Stopped, marshalled: false);
                    return true;
                }

                SetStatus(GodotMcpServerStatus.Stopping, marshalled: false);
                process = _serverProcess;
            }

            try
            {
                if (!process.HasExited)
                {
                    try { process.Kill(); } catch (Exception ex) { _logWarning($"[Godot-MCP] kill signal failed: {ex.Message}"); }
                }

                if (force)
                {
                    if (!process.HasExited)
                        process.WaitForExit(5000);
                    CleanupProcess();
                }
                else if (process.HasExited)
                {
                    CleanupProcess();
                }
                else
                {
                    ScheduleForceKill(process);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logError($"[Godot-MCP] error stopping local server: {ex.Message}");
                CleanupProcess();
                return false;
            }
        }

        /// <summary>
        /// Schedule a one-shot startup-verification check ~5s out on a background task: if the process has
        /// exited early (e.g. port already in use), report Stopped; otherwise promote Starting → Running.
        /// The status change is marshalled onto the editor main thread.
        /// </summary>
        void ScheduleStartupVerification(Process process)
        {
            Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(StartupVerificationSeconds));

                    bool exitedEarly;
                    try { exitedEarly = process.HasExited; }
                    catch { exitedEarly = true; }

                    lock (_processMutex)
                    {
                        if (_status != GodotMcpServerStatus.Starting || !ReferenceEquals(_serverProcess, process))
                            return; // superseded by a stop / restart
                    }

                    if (exitedEarly)
                    {
                        _logError("[Godot-MCP] local server exited early within the startup window (port in use?).");
                        CleanupProcess();
                    }
                    else
                    {
                        SetStatus(GodotMcpServerStatus.Running, marshalled: true);
                        _log("[Godot-MCP] local server verified and running.");
                    }
                }
                catch (Exception ex)
                {
                    _logWarning($"[Godot-MCP] startup verification error: {ex.Message}");
                }
            });
        }

        /// <summary>Background wait-then-force-kill safety net; cleans up on the editor main thread when done.</summary>
        void ScheduleForceKill(Process process)
        {
            Task.Run(() =>
            {
                try
                {
                    if (!process.HasExited && !process.WaitForExit(5000))
                    {
                        try { process.Kill(); process.WaitForExit(2000); }
                        catch (Exception ex) { _logWarning($"[Godot-MCP] force-kill failed: {ex.Message}"); }
                    }
                }
                catch (InvalidOperationException) { /* already exited/disposed */ }

                CleanupProcess();
            });
        }

        /// <summary>
        /// Kill an orphaned server process that THIS project previously launched — scoped so it can NEVER
        /// cross-kill another Godot project's hosted server. The scope test is purely path-based
        /// (<see cref="GodotMcpServerOwnership.IsOwnedByThisProject"/>): a candidate is killed only when its
        /// executable path resolves under THIS project's cache folder (<see cref="CachePlatformPath"/>) — i.e.
        /// it is OUR binary, left running by a prior editor session that did not stop cleanly. We also skip
        /// our own current process. Path comparison is a deterministic, cross-platform string test (no
        /// fragile <c>netstat</c>/<c>lsof</c> parsing), so it behaves identically on every OS. Failures are
        /// swallowed (fail-safe).
        /// </summary>
        void KillOrphanedServerProcesses()
        {
            var ownPath = ExecutableFullPath();

            try
            {
                var currentPid = -1;
                lock (_processMutex) { currentPid = _serverProcess?.Id ?? -1; }

                foreach (var proc in Process.GetProcesses())
                {
                    try
                    {
                        if (proc.Id == currentPid)
                            continue;

                        // Cheap name pre-filter before the (potentially throwing) MainModule access.
                        var name = proc.ProcessName.ToLowerInvariant();
                        if (!name.Contains(GodotMcpServerView.ExecutableName))
                            continue;

                        // The load-bearing scope gate: only kill if this process's executable is OUR cached
                        // binary (path under this project's cache folder). MainModule can throw (access denied
                        // / 32-vs-64-bit / exited) — on any failure we DO NOT kill (fail closed: never touch a
                        // process we cannot positively attribute to this project).
                        string? candidatePath;
                        try { candidatePath = proc.MainModule?.FileName; }
                        catch { continue; }

                        if (!GodotMcpServerOwnership.IsOwnedByThisProject(candidatePath, ownPath))
                            continue;

                        _logWarning($"[Godot-MCP] killing orphaned local server process (PID {proc.Id}) from this project.");
                        proc.Kill();
                        proc.WaitForExit(3000);
                    }
                    catch { /* per-process best effort */ }
                    finally { proc.Dispose(); }
                }
            }
            catch (Exception ex)
            {
                _log($"[Godot-MCP] orphaned-server cleanup skipped: {ex.Message}");
            }
        }

        void OnProcessExited(object? sender, EventArgs e)
        {
            _log("[Godot-MCP] local server process exited.");
            // Process.Exited fires on a thread-pool thread; marshal cleanup onto the editor main thread so
            // the status push (which the dock observes) is raised there.
            if (MainThreadDispatcher.Instance != null && !MainThreadDispatcher.IsMainThread)
                MainThreadDispatcher.Enqueue(CleanupProcess);
            else
                CleanupProcess();
        }

        void CleanupProcess()
        {
            Process? toDispose;
            lock (_processMutex)
            {
                toDispose = _serverProcess;
                _serverProcess = null;
                SetStatus(GodotMcpServerStatus.Stopped, marshalled: false);
            }

            if (toDispose != null)
            {
                Task.Run(() =>
                {
                    try
                    {
                        toDispose.Exited -= OnProcessExited;

                        // Stop draining + detach the discard handlers before dispose (mirrors the attach in
                        // StartServer); CancelOutputRead/CancelErrorRead are best-effort (no-op if reading
                        // never started or already stopped).
                        try { toDispose.CancelOutputRead(); } catch { }
                        try { toDispose.CancelErrorRead(); } catch { }
                        toDispose.OutputDataReceived -= DiscardServerOutput;
                        toDispose.ErrorDataReceived -= DiscardServerOutput;

                        toDispose.Dispose();
                    }
                    catch (Exception ex) { _log($"[Godot-MCP] error disposing server process: {ex.Message}"); }
                });
            }
        }

        /// <summary>
        /// Drain handler for the server's redirected stdout/stderr: DISCARDS every received line. This exists
        /// solely so the OS pipe buffer cannot fill and block the child process — it MUST NOT log the line
        /// content, because the server echoes its own launch arguments (including the secret token) to stdout.
        /// Throwing the lines away keeps the token off every log surface while still draining the pipe.
        /// </summary>
        static void DiscardServerOutput(object sender, DataReceivedEventArgs e)
        {
            // Intentionally empty: read-and-discard. Do NOT log e.Data — it may contain the token.
        }

        /// <summary>
        /// Set + raise the new status. Must be called holding <see cref="_processMutex"/> for the status
        /// field write (the public callers do). The <paramref name="marshalled"/> flag records whether the
        /// raise came from a thread-pool hop (true) so the manager hops to the main thread before raising;
        /// when false the caller is already on the main thread (a UI click / inline path).
        /// </summary>
        void SetStatus(GodotMcpServerStatus status, bool marshalled)
        {
            _status = status;

            void Raise() => StatusChanged?.Invoke(status);

            if (marshalled
                && MainThreadDispatcher.Instance != null
                && !MainThreadDispatcher.IsMainThread)
            {
                MainThreadDispatcher.Enqueue(Raise);
            }
            else
            {
                Raise();
            }
        }

        // --- Filesystem helpers --------------------------------------------------------------------------

        static void TryDeleteDirectory(string path)
        {
            try { Directory.Delete(path, recursive: true); } catch { /* best effort; the create+overwrite below recovers */ }
        }

        /// <summary>
        /// Mark a file executable (0755) on Unix via <c>chmod</c>. Best-effort; a failure is non-fatal (the
        /// launch attempt surfaces any real permission problem). On Windows this is never called.
        /// </summary>
        void TrySetExecutable(string filePath)
        {
            try
            {
                using var chmod = Process.Start(new ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"0755 \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                chmod?.WaitForExit(3000);
            }
            catch (Exception ex)
            {
                _logWarning($"[Godot-MCP] could not chmod server binary: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Stop the server synchronously so we never leave an orphaned process behind on plugin teardown.
            try { StopServer(force: true); } catch { /* teardown best effort */ }
        }
    }
}
#endif
