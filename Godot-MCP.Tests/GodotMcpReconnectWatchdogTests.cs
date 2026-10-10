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
using com.IvanMurzak.Godot.MCP.Connection;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public class GodotMcpReconnectWatchdogTests
    {
        [Fact]
        public void ExhaustedTransport_RestartsRepeatedBatchesAfterIdleCooldown()
        {
            var watchdog = new GodotMcpReconnectWatchdog();
            Assert.False(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
            Assert.True(watchdog.Tick(1, true, HubConnectionState.Disconnected, false));
            Assert.False(watchdog.Tick(1, true, HubConnectionState.Disconnected, false));
            Assert.True(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
        }

        [Theory]
        [InlineData(HubConnectionState.Connected, false)]
        [InlineData(HubConnectionState.Connecting, false)]
        [InlineData(HubConnectionState.Reconnecting, false)]
        [InlineData(HubConnectionState.Disconnected, true)]
        public void ActiveTransport_DoesNotStartConcurrentBatchAndResetsCooldown(HubConnectionState state, bool keepConnected)
        {
            var watchdog = new GodotMcpReconnectWatchdog();
            Assert.False(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
            Assert.False(watchdog.Tick(120, true, state, keepConnected));
            Assert.False(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
            Assert.True(watchdog.Tick(1, true, HubConnectionState.Disconnected, false));
        }

        [Fact]
        public void DisabledIntentOrRejectedCredential_StopsRecoveryAndClearsCountdown()
        {
            var watchdog = new GodotMcpReconnectWatchdog();
            Assert.False(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
            Assert.False(watchdog.Tick(120, false, HubConnectionState.Disconnected, false));
            Assert.False(watchdog.Tick(29, true, HubConnectionState.Disconnected, false));
            Assert.True(watchdog.Tick(1, true, HubConnectionState.Disconnected, false));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void RejectedAuthorization_RetriesOnlyWhileAccountRemainsSignedIn(bool canRefresh)
        {
            var watchdog = new GodotMcpReconnectWatchdog();
            Assert.Equal(canRefresh, watchdog.Tick(30, true, HubConnectionState.Disconnected, false,
                authorizationRejected: true, canRefresh: canRefresh));
            Assert.Equal(canRefresh, watchdog.Tick(30, true, HubConnectionState.Disconnected, false,
                authorizationRejected: true, canRefresh: canRefresh));
        }

        [Theory]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(-1)]
        public void InvalidDelta_DoesNotPoisonRecovery(double delta)
        {
            var watchdog = new GodotMcpReconnectWatchdog();
            Assert.False(watchdog.Tick(delta, true, HubConnectionState.Disconnected, false));
            Assert.True(watchdog.Tick(30, true, HubConnectionState.Disconnected, false));
        }
    }
}
