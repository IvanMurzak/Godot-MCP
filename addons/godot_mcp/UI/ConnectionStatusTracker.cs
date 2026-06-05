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

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) holder for the connection panel's current
    /// <see cref="ConnectionStatus"/> plus the de-duplication rule that decides whether a candidate status
    /// is a real transition worth pushing to the UI. Extracted out of the editor-only
    /// <c>GodotMcpConnection.PublishStatus</c> so the status-path behaviour — de-dup, late-subscriber
    /// convergence, and reconnect reset — is unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host
    /// without constructing a single Godot <see cref="Godot.Control"/> or a live SignalR client.
    ///
    /// <para>
    /// This was the missing piece behind issue #42 ("status stuck at Connecting…"): the connection
    /// MECHANISM was healthy (the transport connected and the handshake completed), but a terminal
    /// <c>Connected</c> render could be lost across the off-thread → main-thread marshalling / de-dup
    /// boundary, and nothing re-seeded the panel afterwards. The fix is two-fold: (1) a periodic re-sync in
    /// the panel that reads <see cref="Current"/> directly (bypassing the event), and (2) this explicit,
    /// tested tracker so the de-dup can never permanently hide a needed render — a re-sync that reads the
    /// live <see cref="Current"/> always renders the latest status regardless of event delivery.
    /// </para>
    /// </summary>
    public sealed class ConnectionStatusTracker
    {
        ConnectionStatus _current;

        /// <summary>
        /// Construct a tracker seeded at <paramref name="initial"/> (defaults to
        /// <see cref="ConnectionStatus.Disconnected"/>, matching a freshly-built connection before any
        /// reactive value has arrived).
        /// </summary>
        public ConnectionStatusTracker(ConnectionStatus initial = ConnectionStatus.Disconnected)
        {
            _current = initial;
        }

        /// <summary>
        /// The latest known status. A late subscriber / periodic re-sync reads this directly to converge on
        /// the current state even if it never saw the transition event that produced it.
        /// </summary>
        public ConnectionStatus Current => _current;

        /// <summary>
        /// Offer a candidate <paramref name="status"/>. When it differs from <see cref="Current"/> the
        /// tracker advances (updating <see cref="Current"/> BEFORE returning, so a getter read inside the
        /// caller's change-notification already reflects the new value) and returns <c>true</c>. When it is
        /// identical the tracker is unchanged and returns <c>false</c> — this is the de-dup that keeps
        /// identical consecutive states (e.g. the seed plus an immediate first push) from double-firing the
        /// UI. The de-dup is render-safe because the panel's periodic re-sync reads <see cref="Current"/>
        /// directly rather than depending on a transition having fired.
        /// </summary>
        public bool TryAdvance(ConnectionStatus status)
        {
            if (_current == status)
                return false;

            _current = status;
            return true;
        }

        /// <summary>
        /// Reset the tracker to <paramref name="status"/> (defaults to
        /// <see cref="ConnectionStatus.Disconnected"/>) unconditionally, returning whether the value
        /// changed. Used when a reconnect rebuilds the underlying plugin/subscription: the next status seed
        /// from the NEW connection should be free to advance from a clean baseline rather than being
        /// de-duped against a stale value carried over from the previous plugin instance.
        /// </summary>
        public bool Reset(ConnectionStatus status = ConnectionStatus.Disconnected)
        {
            if (_current == status)
                return false;

            _current = status;
            return true;
        }
    }
}
