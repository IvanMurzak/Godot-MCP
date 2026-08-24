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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the D4 assisted-sign-in ladder (<see cref="GodotAssistedSignIn"/> — oauth-client-error-hygiene
    /// e2, 02 §C5): the auto-open on the FIRST sign-in-required verdict of an editor session, the carousel
    /// guard (a recurring verdict never re-opens unattended), the manual Authorize entry that is NEVER
    /// gated, and the session store surviving the collectible-ALC hot-reload (a fresh instance over the
    /// same session state stays claimed) while a fresh editor session re-arms. The flow start is the
    /// injected <c>startAuthorize</c> seam — the browser-open itself (<c>OS.ShellOpen</c> on
    /// WaitingForUser) lives in the <c>#if TOOLS</c> panel and is a Suite-3 smoke concern.
    /// </summary>
    public class GodotAssistedSignInTests
    {
        /// <summary>An in-memory session store + counting flow seam — one simulated editor session.</summary>
        sealed class Session
        {
            public readonly Dictionary<string, string> Store = new();
            public int FlowStarts;

            public GodotAssistedSignIn NewLadder() => new(
                startAuthorize: () => FlowStarts++,
                getSessionValue: key => Store.TryGetValue(key, out var value) ? value : null,
                setSessionValue: (key, value) => Store[key] = value);
        }

        [Fact]
        public void FirstVerdict_ClaimsTheGate_AndStartsTheAuthorizeFlow()
        {
            var session = new Session();
            var ladder = session.NewLadder();
            Assert.True(ladder.AutoOpenAvailable);

            var opened = ladder.OnSignInRequiredVerdict();

            Assert.True(opened);
            Assert.Equal(1, session.FlowStarts);
            Assert.False(ladder.AutoOpenAvailable);
            Assert.Equal(GodotAssistedSignIn.AutoOpenClaimedValue,
                session.Store[GodotAssistedSignIn.AutoOpenClaimedSessionKey]);
        }

        /// <summary>
        /// The carousel-guard fixture (task e2 DoD): the SECOND verdict of the session — a freshly-
        /// authorized family dying again, or the device code expiring unattended — starts nothing, and the
        /// user's manual Authorize still starts the flow (the same-fixture positive control: the spent gate
        /// gates only the AUTO entry, never the flow itself).
        /// </summary>
        [Fact]
        public void SecondVerdict_InSession_DoesNotReOpen_AndManualAuthorizeStillWorks()
        {
            var session = new Session();
            var ladder = session.NewLadder();

            Assert.True(ladder.OnSignInRequiredVerdict());
            Assert.Equal(1, session.FlowStarts);

            // The recurrence: no second unattended open.
            Assert.False(ladder.OnSignInRequiredVerdict());
            Assert.Equal(1, session.FlowStarts);

            // Same-fixture positive control: manual Authorize still starts the flow.
            ladder.OnManualAuthorize();
            Assert.Equal(2, session.FlowStarts);
        }

        /// <summary>
        /// The once-gate must survive the addon's collectible-ALC hot-reload ("Build Project"
        /// re-instantiates every [Tool] script, so the panel — and this ladder — are rebuilt): a FRESH
        /// ladder over the SAME session store stays claimed. Manual authorize keeps working there too.
        /// </summary>
        [Fact]
        public void HotReload_FreshLadderOverTheSameSession_StaysClaimed()
        {
            var session = new Session();
            Assert.True(session.NewLadder().OnSignInRequiredVerdict());
            Assert.Equal(1, session.FlowStarts);

            var reloaded = session.NewLadder(); // the post-reload instance, same process state
            Assert.False(reloaded.AutoOpenAvailable);
            Assert.False(reloaded.OnSignInRequiredVerdict());
            Assert.Equal(1, session.FlowStarts);

            reloaded.OnManualAuthorize();
            Assert.Equal(2, session.FlowStarts);
        }

        /// <summary>A fresh editor session (empty session store) re-arms the auto-open (deliberate — D4
        /// wants the user involved until authorization succeeds).</summary>
        [Fact]
        public void FreshEditorSession_ReArmsTheAutoOpen()
        {
            var first = new Session();
            Assert.True(first.NewLadder().OnSignInRequiredVerdict());

            var second = new Session(); // a new editor process: nothing carried over
            Assert.True(second.NewLadder().OnSignInRequiredVerdict());
            Assert.Equal(1, second.FlowStarts);
        }

        /// <summary>
        /// The manual entry never spends the AUTO budget: a user-initiated Authorize before any verdict
        /// leaves the session's one unattended auto-open available for a later verdict.
        /// </summary>
        [Fact]
        public void ManualAuthorize_DoesNotClaimTheAutoOpenGate()
        {
            var session = new Session();
            var ladder = session.NewLadder();

            ladder.OnManualAuthorize();
            Assert.Equal(1, session.FlowStarts);
            Assert.True(ladder.AutoOpenAvailable);

            Assert.True(ladder.OnSignInRequiredVerdict());
            Assert.Equal(2, session.FlowStarts);
        }

        /// <summary>
        /// The DEFAULT session store is the process environment (the seam that makes the gate survive the
        /// collectible-ALC hot-reload and die with the editor process): claiming through one
        /// default-constructed ladder is visible to another. Cleans the process env var up in all paths —
        /// no other test touches this key.
        /// </summary>
        [Fact]
        public void DefaultSessionStore_IsTheProcessEnvironment()
        {
            Environment.SetEnvironmentVariable(GodotAssistedSignIn.AutoOpenClaimedSessionKey, null);
            try
            {
                var starts = 0;
                var ladder = new GodotAssistedSignIn(() => starts++);
                Assert.True(ladder.AutoOpenAvailable);

                Assert.True(ladder.OnSignInRequiredVerdict());
                Assert.Equal(1, starts);
                Assert.Equal(GodotAssistedSignIn.AutoOpenClaimedValue,
                    Environment.GetEnvironmentVariable(GodotAssistedSignIn.AutoOpenClaimedSessionKey));

                // A second default-seam ladder (the hot-reloaded panel) sees the claim.
                var reloaded = new GodotAssistedSignIn(() => starts++);
                Assert.False(reloaded.AutoOpenAvailable);
                Assert.False(reloaded.OnSignInRequiredVerdict());
                Assert.Equal(1, starts);
            }
            finally
            {
                Environment.SetEnvironmentVariable(GodotAssistedSignIn.AutoOpenClaimedSessionKey, null);
            }
        }
    }
}
