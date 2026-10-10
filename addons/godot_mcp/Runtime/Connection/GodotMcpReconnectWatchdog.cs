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
using Microsoft.AspNetCore.SignalR.Client;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Restarts exhausted bounded reconnect batches from editor ticks. Owns no timers, tasks or
    /// transport, so an idle connection cannot keep the collectible editor assembly alive.
    /// </summary>
    internal sealed class GodotMcpReconnectWatchdog
    {
        internal const double RetryDelaySeconds = 30;
        double _idleSeconds;

        public bool Tick(double delta, bool enabled, HubConnectionState state, bool keepConnected,
            bool authorizationRejected = false, bool canRefresh = false)
        {
            if (!enabled || (authorizationRejected && !canRefresh) || state != HubConnectionState.Disconnected || keepConnected)
            {
                _idleSeconds = 0;
                return false;
            }

            // Ignore invalid deltas rather than poisoning the countdown indefinitely.
            if (double.IsNaN(delta) || double.IsInfinity(delta) || delta <= 0)
                return false;
            _idleSeconds += delta;
            if (_idleSeconds < RetryDelaySeconds)
                return false;
            _idleSeconds = 0;
            return true;
        }
    }
}
