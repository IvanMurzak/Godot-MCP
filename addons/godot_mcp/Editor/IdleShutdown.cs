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
    /// Quits an editor started by <c>godot-cli exec</c> / <c>open --idle-timeout</c> after
    /// <c>AIGD_SESSION_IDLE_SECONDS</c> without tool calls, so a warm headless editor does not live forever. Off unless
    /// the variable is set (see <see cref="IdleShutdownTimer.ReadSeconds"/>): a user's own editor is never closed.
    /// Before quitting it saves the open scenes that have a file (an untitled scene has nowhere to go) and the
    /// resources they changed. Ticked from <see cref="GodotMcpPlugin._Process"/>.
    /// </summary>
    sealed class IdleShutdown
    {
        readonly IdleShutdownTimer _timer;

        IdleShutdown(IdleShutdownTimer timer) { _timer = timer; }

        /// <summary>Null when the editor was not started with an idle timeout.</summary>
        public static IdleShutdown? ArmIfRequested()
        {
            var seconds = IdleShutdownTimer.ReadSeconds(name => OS.GetEnvironment(name));
            if (seconds == null)
                return null;
            // A C# reload re-runs the plugin boot and starts the timer over, which is fine: a reload means activity.
            GodotMcpLog.Info($"[Godot-MCP] idle shutdown armed: the editor quits after {seconds.Value}s without tool calls.");
            return new IdleShutdown(new IdleShutdownTimer(TimeSpan.FromSeconds(seconds.Value), Now()));
        }

        /// <summary>True once the editor has been told to quit; the caller drops this instance then.</summary>
        public bool Tick(GodotMcpConnection? connection, SceneTree tree)
        {
            var calls = connection?.Plugin?.ToolCallsCount ?? 0;
            var editor = EditorInterface.Singleton;
            var busy = editor.IsPlayingScene() || editor.GetResourceFilesystem()?.IsScanning() == true;
            if (!_timer.ShouldQuit(Now(), calls, busy))
                return false;

            GodotMcpLog.Info($"[Godot-MCP] idle shutdown: no tool calls for {_timer.Timeout.TotalSeconds:0}s, saving and quitting.");
            try
            {
                // Saves every open scene that has a path (untitled ones are skipped) and the external resources they edited.
                editor.SaveAllScenes();
            }
            catch (Exception e)
            {
                GodotMcpLog.Error($"[Godot-MCP] idle shutdown: saving failed, quitting anyway: {e.Message}");
            }
            tree.Quit();
            return true;
        }

        /// <summary>Engine uptime as a timestamp: unlike the wall clock, a system clock change cannot fire or stall the timer.</summary>
        static DateTime Now() => DateTime.MinValue.AddMilliseconds(Time.GetTicksMsec());
    }
}
#endif
