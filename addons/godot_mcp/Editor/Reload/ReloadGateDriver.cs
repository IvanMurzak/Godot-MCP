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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Reload
{
    /// <summary>
    /// Runs the <see cref="AssemblyReloadGate"/> in the editor (one per plugin boot, main thread):
    /// <list type="bullet">
    /// <item>watches the built project DLL (<c>.godot/mono/temp/bin/Debug/&lt;assembly&gt;.dll</c>, the file Godot's
    /// <c>HotReloadAssemblyWatcher</c> watches) and turns the gate pending once it is newer than the loaded one;</item>
    /// <item>while calls admitted before that are still running, pauses the watcher's 0.5 s timer so Godot reloads
    /// after they finish (bounded by <see cref="AssemblyReloadGate.MaxHoldMs"/>). A reload Godot starts some other way
    /// (editor window focus) is not deferred; <see cref="DrainForUnload"/> covers it;</item>
    /// <item>wraps the plugin's tools in <see cref="GatedRunTool"/>;</item>
    /// <item>at teardown (<see cref="DrainForUnload"/>) refuses everything, fails the queued main-thread bodies of calls
    /// in flight without running them and waits for those calls to return, so no thread is left inside this assembly
    /// and no cache is refilled after teardown clears it.</item>
    /// </list>
    /// </summary>
    sealed class ReloadGateDriver
    {
        const double PollSeconds = 0.1;

        /// <summary>The main thread waits at most this long at teardown for the calls in flight to return.</summary>
        static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(3);

        const string WatcherNodeName = "HotReloadAssemblyWatcher";

        Timer? _watcherTimer;
        bool _holding;
        double _sincePoll = PollSeconds;

        ReloadGateDriver(string? dllPath)
        {
            DllPath = dllPath;
            Gate.SetBaseline(WriteTime(dllPath));
        }

        public AssemblyReloadGate Gate { get; } = new AssemblyReloadGate();

        /// <summary>The DLL Godot reloads this assembly from (null when there is none).</summary>
        public string? DllPath { get; }

        /// <summary>Main thread, at plugin boot. Also clears a refusal a previous teardown in this assembly left behind.</summary>
        public static ReloadGateDriver Install()
        {
            MainThreadDispatcher.RefusalReason = null;
            var driver = new ReloadGateDriver(BuiltAssemblyPath());
            driver._watcherTimer = FindWatcherTimer();
            // A previous instance may have died holding the timer (never leave Godot's reload switched off).
            if (driver._watcherTimer != null && GodotObject.IsInstanceValid(driver._watcherTimer))
                driver._watcherTimer.Paused = false;
            if (driver.DllPath == null)
                GodotMcpLog.Warning("[Godot-MCP] reload gate: the built project assembly was not found; C# reloads are not anticipated.");
            return driver;
        }

        /// <summary>The project assembly as Godot's editor builds it (the Debug configuration, which the helper prefers).</summary>
        static string? BuiltAssemblyPath() => Tools.ScriptExecuteSource.BuiltAssemblyPath(
            ProjectSettings.GlobalizePath("res://.godot/mono/temp/bin"), typeof(ReloadGateDriver).Assembly.GetName().Name);

        /// <summary>The write time of <paramref name="path"/>, or null when it is missing (one file-system call: polled 10 times a second).</summary>
        internal static DateTime? WriteTime(string? path)
        {
            if (path == null)
                return null;
            try
            {
                var time = File.GetLastWriteTimeUtc(path);
                return time.Year <= 1601 ? null : time; // a missing file reads as 1601-01-01
            }
            catch (Exception) { return null; }
        }

        static Timer? FindWatcherTimer()
        {
            try
            {
                var root = (Engine.GetMainLoop() as SceneTree)?.Root;
                var watcher = root?.FindChild(WatcherNodeName, recursive: true, owned: false);
                return watcher?.GetChildren().OfType<Timer>().FirstOrDefault();
            }
            catch (Exception ex)
            {
                GodotMcpLog.Warning($"[Godot-MCP] reload gate: Godot's {WatcherNodeName} not found ({ex.Message}); reloads are not deferred.");
                return null;
            }
        }

        /// <summary>Wrap the plugin's tools in the gate (the editor's plugin configuration).</summary>
        public void Configure(IMcpPlugin plugin) => GatedRunTool.WrapAll(plugin.McpManager?.ToolManager, Gate);

        /// <summary>Main-thread tick (<c>GodotMcpPlugin._Process</c>).</summary>
        public void Tick(double delta)
        {
            _sincePoll += delta;
            if (_sincePoll < PollSeconds)
                return;
            _sincePoll = 0;
            var now = (long)Time.GetTicksMsec();
            switch (Gate.Observe(WriteTime(DllPath), now))
            {
                case AssemblyReloadGate.Observation.BecamePending:
                    GodotMcpLog.Info($"[Godot-MCP] the project's C# assembly was rebuilt; new tool calls wait for Godot's reload ({Gate.InFlight} running).");
                    break;
                case AssemblyReloadGate.Observation.PendingExpired:
                    GodotMcpLog.Warning("[Godot-MCP] the rebuilt C# assembly was not reloaded by Godot; tool calls run again on the loaded one.");
                    break;
            }
            SetHold(Gate.HoldReload(now));
        }

        void SetHold(bool hold)
        {
            if (hold == _holding)
                return;
            _holding = hold;
            try
            {
                if (_watcherTimer == null || !GodotObject.IsInstanceValid(_watcherTimer))
                    return;
                _watcherTimer.Paused = hold;
                GodotMcpLog.Info(hold
                    ? $"[Godot-MCP] deferring Godot's C# reload until {Gate.InFlight} running tool call(s) finish."
                    : "[Godot-MCP] Godot's C# reload may start.");
            }
            catch (Exception ex)
            {
                GodotMcpLog.Warning($"[Godot-MCP] reload gate: could not {(hold ? "defer" : "release")} Godot's reload ({ex.Message}).");
            }
        }

        /// <summary>
        /// Teardown, main thread, before the connection goes away: refuse every new call and wait (bounded) until the calls
        /// in flight have returned their answers. On a <paramref name="reload"/> the queued main-thread bodies are refused
        /// too: Godot has already detached the dispatcher's script, so nothing else would ever settle them. (On a plain
        /// shutdown they still run, and the dispatcher's own <c>_ExitTree</c> drains the rest as before.) A call whose body
        /// is running below this teardown on the main thread (Godot started the reload from inside it) cannot return
        /// before it; it is not waited for, and <see cref="AssemblyReloadGate.Drained"/> cleans up after it instead.
        /// </summary>
        public void DrainForUnload(Func<int> inFlight, bool reload)
        {
            var reason = reload ? AssemblyReloadGate.UnloadingMessage : AssemblyReloadGate.ShutdownMessage;
            Gate.BeginUnload(reason);
            if (reload)
                MainThreadDispatcher.RefusalReason = reason;
            SetHold(false);

            var nested = MainThreadDispatcher.IsRunningAction ? 1 : 0;
            // McpPlugin's count also covers system tools, prompts and resources, which are not gated.
            int Running() => Math.Max(inFlight(), Gate.InFlight) - nested;
            var initial = Running();
            var clock = Stopwatch.StartNew();
            var running = initial;
            while (running > 0 && clock.Elapsed < DrainTimeout)
            {
                MainThreadDispatcher.FlushQueued();
                running = Running();
                if (running > 0)
                    System.Threading.Thread.Sleep(10);
            }
            MainThreadDispatcher.FlushQueued();
            if (nested > 0)
                GodotMcpLog.Warning("[Godot-MCP] the C# assembly started unloading inside a running tool call; Godot's first unload attempt may fail and is retried once it returns.");
            if (running > 0)
                GodotMcpLog.Warning($"[Godot-MCP] {running} tool call(s) still running after {DrainTimeout.TotalSeconds:0} s; unloading anyway.");
            else if (initial > 0)
                GodotMcpLog.Info($"[Godot-MCP] {initial} running tool call(s) drained for the unload in {clock.ElapsedMilliseconds} ms.");
        }
    }
}
#endif
