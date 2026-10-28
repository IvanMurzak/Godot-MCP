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
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.McpPlugin.Common.Model;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// Runs <c>script-build</c>'s <c>dotnet build</c> detached and delivers its result through the request's id
    /// (<c>NotifyToolRequestCompleted</c>), usually from the NEXT load of this assembly: a successful build changes the
    /// DLL, Godot reloads it (behind the <see cref="AssemblyReloadGate"/>) and the reloaded plugin reports.
    /// <para>
    /// Nothing of this assembly runs while the build does: no output-reading threads or exit callbacks (they would pin
    /// the assembly when Godot reloads it mid-build). The build writes an MSBuild file log, the state is a
    /// <see cref="BuildRecord"/> file under <c>.godot/godot_mcp/</c>, and <see cref="Tick"/> polls both on the main
    /// thread; a later load finds the build by its process id.
    /// </para>
    /// </summary>
    sealed class ProjectBuildTracker
    {
        /// <summary>The tracker of the booted plugin (script-build starts builds through it); null when not booted.</summary>
        public static ProjectBuildTracker? Current { get; set; }

        /// <summary>Identifies this load of the assembly: a record written by another load means Godot reloaded it.</summary>
        static readonly string LoadId = Guid.NewGuid().ToString("N");

        const double PollSeconds = 0.25;
        static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(5);
        /// <summary>How long a build whose output Godot did not reload waits (from its end) before it is reported anyway.</summary>
        static readonly TimeSpan ReloadTimeout = TimeSpan.FromSeconds(30);
        /// <summary>A build process gone without its log summary is reported after this grace (the log is flushed last).</summary>
        static readonly TimeSpan LogGrace = TimeSpan.FromSeconds(2);

        readonly string? _dllPath;
        BuildRecord? _record;
        /// <summary>The build process of <see cref="_record"/> once found (null: not looked up yet, or gone).</summary>
        Process? _process;
        bool _processLookedUp;
        BuildOutcome? _outcome;
        ResponseCallTool? _result;
        Task? _sending;
        DateTime? _finishedAt;
        DateTime? _goneAt;
        double _sincePoll = PollSeconds;

        public ProjectBuildTracker(string? dllPath)
        {
            _dllPath = dllPath;
            // Only Start (this load) or a load before the last reload writes a record, so the file is read once here.
            _record = BuildRecord.Parse(ReadText(RecordPath));
        }

        static string StateDir => ProjectSettings.GlobalizePath("res://.godot/godot_mcp");
        static string RecordPath => Path.Combine(StateDir, "build.txt");

        /// <summary>Start a build for <paramref name="requestId"/> (main thread). Returns the "processing" message.</summary>
        public string Start(string requestId)
        {
            // One build at a time, and the previous one's result is handed to the server first (its request still waits for
            // it). Once that send has started, the agent may already have the result and ask again: let it.
            if (_record != null && _sending == null)
                throw new InvalidOperationException("The previous script-build has not delivered its result yet (its build is running, or " +
                                                    "Godot is reloading the C# assembly); retry once it has.");

            var csproj = new Extensions.ConsumerProjectFile().Path
                ?? throw new InvalidOperationException("This project has no C# project (.csproj) to build.");
            var projectDir = Path.GetDirectoryName(csproj)!;
            Directory.CreateDirectory(StateDir);
            var log = Path.Combine(StateDir, "build.log");
            File.Delete(log);

            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = projectDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            // The editor's Debug configuration, with no console output (it would go to the editor's stdout), a file log the
            // tracker reads (in English: its summary line is matched), and no build servers outliving the build. Godot's own
            // builds drop a Developer Command Prompt's Platform variable too: it would change the output folder.
            start.Environment["DOTNET_CLI_UI_LANGUAGE"] = "en";
            start.Environment.Remove("Platform");
            foreach (var arg in new[] { "build", csproj, "-c", "Debug", "-nologo", "-noConsoleLogger", "--disable-build-servers",
                         $"-flp:LogFile={log};Verbosity=minimal;Summary" })
                start.ArgumentList.Add(arg);
            var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start dotnet (is the .NET SDK installed and on PATH?).");

            var record = new BuildRecord
            {
                RequestId = requestId,
                ProcessId = process.Id,
                LogPath = log,
                ProjectDir = projectDir,
                DllWriteTicksAtStart = ReloadGateDriver.WriteTime(_dllPath)?.Ticks ?? 0,
                StartedUtcTicks = DateTime.UtcNow.Ticks,
                LoadId = LoadId,
            };
            try { File.WriteAllText(RecordPath, record.Serialize()); }
            catch (Exception)
            {
                // Without its record the result could not be delivered after the reload: do not build at all.
                try { process.Kill(entireProcessTree: true); } catch (Exception) { /* already gone */ }
                process.Dispose();
                throw;
            }
            ResetBuild();
            _record = record;
            _process = process;
            _processLookedUp = true;
            GodotMcpLog.Info($"[Godot-MCP] script-build: dotnet build started (pid {process.Id}).");
            return "Building the project's C# (dotnet build) in the background. When it succeeds Godot reloads the C# assembly and the " +
                   "plugin reconnects; the result (the compiler warnings, or the compiler errors) is delivered then. Tool calls made " +
                   "while the reload is pending are refused with a retryable error.";
        }

        /// <summary>Main-thread tick: settle the build once it ended and report it over a connected plugin.</summary>
        public void Tick(double delta, GodotMcpConnection? connection)
        {
            if (_record is not { } record)
                return;
            _sincePoll += delta;
            if (_sincePoll < PollSeconds)
                return;
            _sincePoll = 0;
            try
            {
                if (_sending != null)
                {
                    // Polled, not continued: no callback of this assembly runs on a thread-pool thread.
                    if (!_sending.IsCompleted)
                        return;
                    if (_sending.Status == TaskStatus.RanToCompletion)
                    {
                        GodotMcpLog.Info($"[Godot-MCP] script-build: result delivered ({_result?.Status}).");
                        Forget();
                        return;
                    }
                    _sending = null; // the send failed (connection dropped): send again once connected
                }

                try { _result ??= Evaluate(record); }
                catch (Exception ex) { _result = ResponseCallTool.Error($"Tracking the C# build failed: {ex.Message}", ResponseErrorKind.Internal); }
                if (_result == null || connection?.ConnectionStatus != ConnectionStatus.Connected || connection.Plugin?.McpManagerHub is not { } hub)
                    return;
                _sending = hub.NotifyToolRequestCompleted(new RequestToolCompletedData
                {
                    RequestId = record.RequestId,
                    Result = _result.SetRequestID(record.RequestId),
                });
            }
            catch (Exception ex)
            {
                GodotMcpLog.Error($"[Godot-MCP] script-build: delivering the result failed: {ex.Message}");
                _sending = null;
            }
        }

        /// <summary>The result of the build, or null while it (or Godot's reload of its output) is still to come.</summary>
        ResponseCallTool? Evaluate(BuildRecord record)
        {
            var now = DateTime.UtcNow;
            if (_outcome == null)
            {
                // The summary is the log's last line: read it only once the build process is gone.
                if (FindProcess(record) is { HasExited: false })
                {
                    if (now - new DateTime(record.StartedUtcTicks, DateTimeKind.Utc) < BuildTimeout)
                        return null;
                    try { _process?.Kill(entireProcessTree: true); } catch (Exception) { /* already gone */ }
                    return ResponseCallTool.Error($"The C# build did not finish in {BuildTimeout.TotalMinutes:0} minutes and was stopped.", ResponseErrorKind.Timeout);
                }
                _outcome = BuildOutcome.FromLog(ReadText(record.LogPath), record.ProjectDir);
                if (_outcome == null)
                {
                    _goneAt ??= now;
                    return now - _goneAt < LogGrace
                        ? null
                        : ResponseCallTool.Error($"The C# build ended without a result; see its log: {record.LogPath}", ResponseErrorKind.Internal);
                }
            }
            var outcome = _outcome;
            if (!outcome.Succeeded)
                return ResponseCallTool.Error(outcome.FailureText(), ResponseErrorKind.BadRequest);

            var dllNow = ReloadGateDriver.WriteTime(_dllPath);
            var dllAtStart = record.DllWriteTicksAtStart > 0 ? AssemblyReloadGate.ToSeconds(new DateTime(record.DllWriteTicksAtStart, DateTimeKind.Utc)) : 0;
            if (dllNow == null || AssemblyReloadGate.ToSeconds(dllNow.Value) <= dllAtStart)
                return ResponseCallTool.Success(outcome.SuccessText("; the assembly was already up to date, so there was nothing to reload."));
            if (record.LoadId != LoadId)
                return ResponseCallTool.Success(outcome.SuccessText(" and Godot reloaded the C# assembly: new and changed C# " +
                                                                        "scripts and [GlobalClass] types are usable now."));
            _finishedAt ??= now;
            return now - _finishedAt < ReloadTimeout
                ? null
                : ResponseCallTool.Success(outcome.SuccessText(", but Godot has not reloaded the C# assembly (C# hot " +
                                                                   "reload may be off): focus the editor window or restart the editor to load it."));
        }

        /// <summary>
        /// The build process of <paramref name="record"/> (looked up once per load: after a reload the process id comes from
        /// the record, checked against its start time so a later process with the same id is not taken for it), or null.
        /// </summary>
        Process? FindProcess(BuildRecord record)
        {
            if (_processLookedUp)
                return _process;
            _processLookedUp = true;
            try
            {
                var process = Process.GetProcessById(record.ProcessId);
                if (Math.Abs((process.StartTime.ToUniversalTime() - new DateTime(record.StartedUtcTicks, DateTimeKind.Utc)).TotalSeconds) < 10)
                    return _process = process;
                process.Dispose();
            }
            catch (Exception) { /* exited, or not ours to inspect */ }
            return null;
        }

        /// <summary>Plugin teardown: let go of the process handle; the build itself keeps running and the next load reports it.</summary>
        public void Release()
        {
            _process?.Dispose();
            _process = null;
            _processLookedUp = false;
        }

        void ResetBuild()
        {
            Release();
            _record = null;
            _outcome = null;
            _result = null;
            _sending = null;
            _finishedAt = null;
            _goneAt = null;
        }

        void Forget()
        {
            ResetBuild();
            try { File.Delete(RecordPath); } catch (Exception) { /* a stale record is re-evaluated and reported once more at worst */ }
        }

        static string? ReadText(string path)
        {
            try
            {
                if (!File.Exists(path))
                    return null;
                // The build may still hold its log open: open it shared.
                using var stream = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }
    }
}
#endif
