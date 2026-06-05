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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.UI;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed connection status path behind issue #42 ("status stuck at Connecting…"):
    /// the <see cref="ConnectionStatusTracker"/> de-dup rule, the late-subscriber / re-sync convergence
    /// (a subscriber that attaches AFTER the status already advanced still converges by reading
    /// <see cref="ConnectionStatusTracker.Current"/> directly), and the Reconnect reset. The editor
    /// <c>ConnectionPanel</c> Timer wiring that drives the periodic re-sync is <c>#if TOOLS</c> and verified
    /// via the headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class ConnectionStatusTrackerTests
    {
        // --- De-dup: TryAdvance only reports a change on an actual transition ---

        [Fact]
        public void TryAdvance_FromSeedToNewStatus_AdvancesAndReportsChange()
        {
            var tracker = new ConnectionStatusTracker(); // seeds Disconnected

            Assert.True(tracker.TryAdvance(ConnectionStatus.Connecting));
            Assert.Equal(ConnectionStatus.Connecting, tracker.Current);
        }

        [Fact]
        public void TryAdvance_SameStatusTwice_SecondIsDeDuped()
        {
            var tracker = new ConnectionStatusTracker();

            Assert.True(tracker.TryAdvance(ConnectionStatus.Connected));
            // Identical consecutive state (e.g. the seed plus an immediate first push) must not re-fire.
            Assert.False(tracker.TryAdvance(ConnectionStatus.Connected));
            Assert.Equal(ConnectionStatus.Connected, tracker.Current);
        }

        [Fact]
        public void TryAdvance_SeededAtSameValue_FirstOfferIsDeDuped()
        {
            // A tracker seeded Disconnected, then offered Disconnected again, is a no-op (matches the
            // ConnectionPanel ctor seed followed by an immediate seed-from-current push of the same value).
            var tracker = new ConnectionStatusTracker(ConnectionStatus.Disconnected);

            Assert.False(tracker.TryAdvance(ConnectionStatus.Disconnected));
            Assert.Equal(ConnectionStatus.Disconnected, tracker.Current);
        }

        [Fact]
        public void TryAdvance_UpdatesCurrentBeforeReturning()
        {
            // The de-dup mutates Current BEFORE returning, so a getter read inside a change-notification
            // already reflects the new value (the connection raises ConnectionStatusChanged right after this).
            var tracker = new ConnectionStatusTracker();
            tracker.TryAdvance(ConnectionStatus.Connecting);
            Assert.Equal(ConnectionStatus.Connecting, tracker.Current);
        }

        // --- Late-subscriber convergence: the #42 fix in microcosm ---

        [Fact]
        public void LateSubscriber_ReadsLatestStatus_EvenIfItMissedTheTransitionEvent()
        {
            // Model the race: the status advances Disconnected -> Connecting -> Connected via the event path
            // BEFORE a subscriber (the dock panel) attaches. Events fired before attach are lost.
            var tracker = new ConnectionStatusTracker();
            var observedEvents = new List<ConnectionStatus>();

            // Simulate the connection's PublishStatus loop firing to NO subscriber yet.
            AdvanceAndMaybeRaise(tracker, ConnectionStatus.Connecting, subscriber: null, observedEvents);
            AdvanceAndMaybeRaise(tracker, ConnectionStatus.Connected, subscriber: null, observedEvents);

            // Subscriber attaches now — it missed every event.
            Assert.Empty(observedEvents);

            // The periodic re-sync reads Current DIRECTLY (bypassing the event) and converges on Connected.
            var rendered = tracker.Current;
            Assert.Equal(ConnectionStatus.Connected, rendered);
        }

        [Fact]
        public void Resync_AfterMissedPush_ConvergesOnLiveStatus()
        {
            // The panel rendered Connecting (its last seen event). A subsequent Connected push was "lost"
            // (never delivered to the panel), but the tracker advanced. The re-sync reads Current and the
            // panel re-applies the drift.
            var tracker = new ConnectionStatusTracker();
            tracker.TryAdvance(ConnectionStatus.Connecting);
            ConnectionStatus rendered = tracker.Current; // panel rendered Connecting

            // Lost push: tracker advances to Connected but the panel never saw the event.
            tracker.TryAdvance(ConnectionStatus.Connected);

            // Re-sync detects drift (rendered != live) and re-applies.
            if (rendered != tracker.Current)
                rendered = tracker.Current;

            Assert.Equal(ConnectionStatus.Connected, rendered);
        }

        // --- Reconnect reset: a rebuilt plugin's status seeds from a clean baseline ---

        [Fact]
        public void Reset_FromConnected_ReturnsToDisconnected_AndReportsChange()
        {
            var tracker = new ConnectionStatusTracker();
            tracker.TryAdvance(ConnectionStatus.Connected);

            Assert.True(tracker.Reset());
            Assert.Equal(ConnectionStatus.Disconnected, tracker.Current);
        }

        [Fact]
        public void Reset_WhenAlreadyAtTarget_IsNoOp()
        {
            var tracker = new ConnectionStatusTracker(); // already Disconnected
            Assert.False(tracker.Reset());
            Assert.Equal(ConnectionStatus.Disconnected, tracker.Current);
        }

        [Fact]
        public void Reconnect_ResetThenReseed_RendersNewConnectionsStatus()
        {
            // Old connection was Connected; a Reconnect resets to Disconnected, then the NEW plugin's seed
            // (Reduce over the fresh client) advances to Connecting and eventually Connected — none of which
            // is de-duped against the stale Connected from the old plugin because of the reset.
            var tracker = new ConnectionStatusTracker();
            tracker.TryAdvance(ConnectionStatus.Connected);

            // Reconnect resets.
            Assert.True(tracker.Reset());

            // New plugin seeds Connecting, then reaches Connected — both are real transitions now.
            Assert.True(tracker.TryAdvance(ConnectionStatus.Connecting));
            Assert.True(tracker.TryAdvance(ConnectionStatus.Connected));
            Assert.Equal(ConnectionStatus.Connected, tracker.Current);
        }

        // --- The reduction that feeds the tracker reaches Connected on a connected hub (end-to-end of #42) ---

        [Fact]
        public void HealthyHandshake_ReducesToConnected_AndTrackerAdvances()
        {
            // The diagnosis: the hub DID reach Connected (handshake completed) — so the reduced status the
            // tracker is offered IS Connected. Prove the path the panel depends on end-to-end.
            var tracker = new ConnectionStatusTracker();

            // boot seed: client starts Disconnected, KeepConnected on -> Connecting.
            tracker.TryAdvance(ConnectionPanelView.Reduce(HubConnectionState.Disconnected, keepConnected: true));
            Assert.Equal(ConnectionStatus.Connecting, tracker.Current);

            // hub connects + handshake completes -> Connected.
            Assert.True(tracker.TryAdvance(ConnectionPanelView.Reduce(HubConnectionState.Connected, keepConnected: true)));
            Assert.Equal(ConnectionStatus.Connected, tracker.Current);
        }

        // --- Helper modelling the connection's PublishStatus(event) path against an optional subscriber ---

        static void AdvanceAndMaybeRaise(
            ConnectionStatusTracker tracker,
            ConnectionStatus status,
            List<ConnectionStatus>? subscriber,
            List<ConnectionStatus> sink)
        {
            if (tracker.TryAdvance(status))
                subscriber?.Add(status); // only delivered if a subscriber is attached at fire time
            _ = sink;
        }
    }
}
