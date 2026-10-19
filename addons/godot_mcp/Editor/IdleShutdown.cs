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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Quits an editor started by <c>godot-cli exec</c> / <c>open --headless</c> after
    /// <c>AIGD_SESSION_IDLE_SECONDS</c> without tool calls, so a warm headless editor does not live forever. Off unless
    /// the variable is set (see <see cref="IdleShutdownTimer.ReadSeconds"/>): a user's own editor is never closed.
    /// Ticked from <see cref="GodotMcpPlugin._Process"/>; <see cref="SaveAndQuit"/> is also the <c>session-close</c> path.
    /// </summary>
    sealed class IdleShutdown
    {
        /// <summary>Seconds between idle checks: well under the timer's 1 s stall threshold, far cheaper than every frame.</summary>
        const double CheckIntervalSeconds = 0.25;

        readonly IdleShutdownTimer _timer;
        double _sinceCheck;

        IdleShutdown(IdleShutdownTimer timer) { _timer = timer; }

        /// <summary>Null when the editor was not started with an idle timeout.</summary>
        public static IdleShutdown? ArmIfRequested()
        {
            var seconds = IdleShutdownTimer.ReadSeconds(name => OS.GetEnvironment(name));
            if (seconds == null)
                return null;
            if (DisplayServer.GetName() != "headless")
            {
                // A CLI session with a window (exec --window) is someone's working editor: never close it under them.
                GodotMcpLog.Info("[Godot-MCP] idle shutdown not armed: the editor has a window.");
                return null;
            }
            // A C# reload re-runs the plugin boot and starts the timer over, which is fine: a reload means activity.
            GodotMcpLog.Info($"[Godot-MCP] idle shutdown armed: the editor quits after {seconds.Value}s without tool calls.");
            return new IdleShutdown(new IdleShutdownTimer(TimeSpan.FromSeconds(seconds.Value), Now()));
        }

        /// <summary>True once the editor has been told to quit; the caller drops this instance then.</summary>
        public bool Tick(GodotMcpConnection? connection, double delta)
        {
            _sinceCheck += delta;
            if (_sinceCheck < CheckIntervalSeconds)
                return false;
            _sinceCheck = 0;
            var calls = connection?.Plugin?.ToolCallsCount ?? 0;
            var editor = EditorInterface.Singleton;
            var busy = Tools.ScriptExecutor.IsBusy || editor.IsPlayingScene() || editor.GetResourceFilesystem()?.IsScanning() == true;
            if (!_timer.ShouldQuit(Now(), calls, busy))
                return false;
            SaveAndQuit($"no tool calls for {_timer.Timeout.TotalSeconds:0}s");
            return true;
        }

        /// <summary>
        /// Main thread only. Saves every open scene that has a path (untitled ones have nowhere to go) and the external
        /// resources they edited, removes this editor's CLI lease and quits.
        /// </summary>
        public static void SaveAndQuit(string reason)
        {
            GodotMcpLog.Info($"[Godot-MCP] closing the CLI session ({reason}): saving and quitting.");
            try
            {
                EditorInterface.Singleton.SaveAllScenes();
            }
            catch (Exception e)
            {
                GodotMcpLog.Error($"[Godot-MCP] saving failed, quitting anyway: {e.Message}");
            }
            RemoveOwnLease();
            ((SceneTree)Engine.GetMainLoop()).Quit();
        }

        /// <summary>Remove <c>.ai-game-dev/session.json</c> when it is this editor's (same <c>AIGD_SESSION_NONCE</c>).</summary>
        public static void RemoveOwnLease()
        {
            if (SessionLease.DeleteIfOwned(ProjectSettings.GlobalizePath("res://"), OS.GetEnvironment(GodotMcpEnv.SessionNonce)))
                GodotMcpLog.Info("[Godot-MCP] removed the CLI session lease.");
        }

        /// <summary>Engine uptime as a timestamp: unlike the wall clock, a system clock change cannot fire or stall the timer.</summary>
        static DateTime Now() => DateTime.MinValue.AddMilliseconds(Time.GetTicksMsec());
    }
}
#endif
