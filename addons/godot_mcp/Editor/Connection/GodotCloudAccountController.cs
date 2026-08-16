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
using System.Threading;
using System.Threading.Tasks;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The pure-managed orchestration behind the dock's Cloud "Authorize" button (unified-machine-auth
    /// task f1). The <c>#if TOOLS</c> <c>ConnectionPanel</c> is a thin adapter over this: it builds the
    /// flow (browser-open, status UI) and calls <see cref="SignInAsync"/> for EVERYTHING that persists.
    ///
    /// <para>
    /// <b>The load-bearing invariant (O8, pinned by test):</b> a successful authorize persists the
    /// credential ONLY into the machine store (via <see cref="GodotAccountAuth.SignInAsync"/>'s guarded
    /// two-lock-hold commit). <paramref name="config"/> — the layer serialized to the legacy
    /// <c>user://godot-mcp-config.json</c> sink — is deliberately taken as a parameter and deliberately
    /// NOT written: <c>config.CloudToken</c> was exactly where the pre-f1 Authorize wrote the token in
    /// plaintext, and this seam existing is what lets a unit test assert that the sink gains no new
    /// cloudToken on the authorize path (G-SEC-1 plant 1). Do not "tidy" the parameter away — removing
    /// it removes the pin.
    /// </para>
    ///
    /// <para>Pure-managed (no Godot native types, no <c>#if TOOLS</c>); never logs or returns token
    /// material.</para>
    /// </summary>
    public static class GodotCloudAccountController
    {
        /// <summary>
        /// Run the F1 sign-in via <paramref name="account"/> against <paramref name="asBaseUrl"/>.
        /// <paramref name="config"/> is the persisted-config layer the LEGACY flow used to write the token
        /// into — it is intentionally left untouched (see the class docs). Returns the non-secret outcome
        /// for the caller's UI to render.
        /// </summary>
        public static async Task<GodotAccountSignInResult> SignInAsync(
            GodotAccountAuth account,
            GodotDeviceAuthFlow flow,
            string asBaseUrl,
            GodotMcpConfig config,
            CancellationToken cancellationToken = default)
        {
            if (account == null) throw new ArgumentNullException(nameof(account));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (config == null) throw new ArgumentNullException(nameof(config));

            var outcome = await account.SignInAsync(flow, asBaseUrl, cancellationToken).ConfigureAwait(false);

            // O8: the machine store is the ONLY credential sink on this path. config.CloudToken (the
            // user:// plaintext sink) gains no new value — read-fallback of a PRE-EXISTING value stays,
            // its write-path removal is the f4 follow-up.
            return outcome;
        }
    }
}
