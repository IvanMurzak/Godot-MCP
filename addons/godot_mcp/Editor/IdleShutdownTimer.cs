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
using System.Globalization;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Idle bookkeeping without Godot APIs (mirrors Unity-MCP's): any change of the tool-call counter (a new call, or a
    /// reset after a reconnect or reload) counts as activity, and so does a busy editor (playing, scanning) and a stalled
    /// frame loop (the main thread was blocked, typically by a long tool call that only just returned).
    /// </summary>
    public sealed class IdleShutdownTimer
    {
        /// <summary>A gap between two checks longer than this means the main thread was busy.</summary>
        public static readonly TimeSpan StallThreshold = TimeSpan.FromSeconds(1);

        readonly TimeSpan _timeout;
        DateTime _lastActivity;
        DateTime _lastCheck;
        ulong _lastCalls;

        public IdleShutdownTimer(TimeSpan timeout, DateTime now)
        {
            _timeout = timeout;
            _lastActivity = now;
            _lastCheck = now;
        }

        public TimeSpan Timeout => _timeout;

        public bool ShouldQuit(DateTime now, ulong toolCalls, bool busy)
        {
            var stalled = now - _lastCheck > StallThreshold;
            _lastCheck = now;
            if (busy || stalled || toolCalls != _lastCalls)
            {
                _lastCalls = toolCalls;
                _lastActivity = now;
                return false;
            }
            return now - _lastActivity >= _timeout;
        }

        /// <summary>
        /// The idle timeout an editor was started with (<c>AIGD_SESSION_IDLE_SECONDS</c>, a positive whole number), or
        /// null to never quit: unset (a user's own editor, or a CLI session started without one) or invalid.
        /// </summary>
        public static int? ReadSeconds(Func<string, string?> env)
        {
            var raw = env(GodotMcpEnv.SessionIdleSeconds)?.Trim();
            return !string.IsNullOrEmpty(raw) && int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds > 0
                ? seconds
                : (int?)null;
        }
    }
}
