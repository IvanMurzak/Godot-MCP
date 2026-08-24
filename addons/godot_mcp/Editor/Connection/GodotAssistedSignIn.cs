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

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The D4 assisted-sign-in ladder for the Godot dock (oauth-client-error-hygiene e2, 02 §C5): when the
    /// credential provider surfaces a terminal sign-in-required verdict, the panel — besides rendering the
    /// status — auto-starts the device-authorization flow ONCE per editor session, which opens the default
    /// browser at the verification URL and polls until the user approves. Both entries funnel into the SAME
    /// <c>startAuthorize</c> seam the panel wires to its Authorize-button flow:
    /// <list type="bullet">
    ///   <item><see cref="OnSignInRequiredVerdict"/> — the automatic entry. Gated: the FIRST verdict of the
    ///   editor session starts the flow; every recurrence (the carousel guard — a freshly-authorized family
    ///   dying again, or the device code expiring unattended) renders status only, never a second unattended
    ///   browser-open. The pinned McpPlugin 8.1.0 exposes no <c>SignInRequiredReason</c>, so the guard keys
    ///   on verdict RECURRENCE, not reason class.</item>
    ///   <item><see cref="OnManualAuthorize"/> — the user's own Authorize click. NEVER gated: manual
    ///   re-auth must keep working after the auto-open budget is spent.</item>
    /// </list>
    ///
    /// <para>
    /// <b>Session persistence:</b> the once-per-editor-session gate defaults to a PROCESS environment
    /// variable (<see cref="AutoOpenClaimedSessionKey"/>). Process env is process state, not assembly
    /// state — it survives the addon's collectible-ALC hot-reload (a "Build Project" re-instantiates every
    /// [Tool] script and would reset a static field) and dies with the editor process, so a fresh editor
    /// session re-arms the auto-open (deliberate — D4 wants the user involved until authorization
    /// succeeds). The Godot analog of Unity's <c>SessionState</c> once-gate (02 §C4).
    /// </para>
    ///
    /// <para>Pure-managed (no Godot native types, no <c>#if TOOLS</c>): the session store is an injectable
    /// seam, so the once-gate + carousel guard are unit-tested in the plain-xUnit host. The browser-open
    /// itself stays inside the panel's flow wiring (<c>OS.ShellOpen</c> on WaitingForUser).</para>
    /// </summary>
    public sealed class GodotAssistedSignIn
    {
        /// <summary>
        /// The session-store key claimed by the first auto-open of the editor session. A process
        /// environment variable by default (see the class docs for why: it must survive the collectible-ALC
        /// hot-reload and die with the editor process).
        /// </summary>
        public const string AutoOpenClaimedSessionKey = "GODOT_MCP_ASSISTED_SIGNIN_AUTO_OPENED";

        /// <summary>The value stored under <see cref="AutoOpenClaimedSessionKey"/> once claimed.</summary>
        public const string AutoOpenClaimedValue = "1";

        readonly Action _startAuthorize;
        readonly Func<string, string?> _getSessionValue;
        readonly Action<string, string> _setSessionValue;

        /// <summary>
        /// Construct the ladder. <paramref name="startAuthorize"/> is the ONE flow entry both the automatic
        /// verdict path and the manual Authorize path invoke (the panel wires it to its device-auth flow
        /// start, which opens the browser + polls). <paramref name="getSessionValue"/> /
        /// <paramref name="setSessionValue"/> are the session-store seam — process environment variables by
        /// default, injectable (a dictionary) for deterministic tests.
        /// </summary>
        public GodotAssistedSignIn(
            Action startAuthorize,
            Func<string, string?>? getSessionValue = null,
            Action<string, string>? setSessionValue = null)
        {
            _startAuthorize = startAuthorize ?? throw new ArgumentNullException(nameof(startAuthorize));
            _getSessionValue = getSessionValue ?? Environment.GetEnvironmentVariable;
            _setSessionValue = setSessionValue ?? Environment.SetEnvironmentVariable;
        }

        /// <summary>
        /// True while the auto-open budget for this editor session is unspent (no verdict has claimed it yet).
        /// </summary>
        public bool AutoOpenAvailable => _getSessionValue(AutoOpenClaimedSessionKey) != AutoOpenClaimedValue;

        /// <summary>
        /// The automatic entry — a terminal sign-in-required verdict from the credential provider. The
        /// first verdict of the editor session claims the once-gate and starts the authorize flow (returns
        /// <c>true</c>); every later verdict — the carousel guard — starts nothing and returns <c>false</c>
        /// (the caller renders the persistent status; the user's manual Authorize keeps working via
        /// <see cref="OnManualAuthorize"/>).
        /// </summary>
        public bool OnSignInRequiredVerdict()
        {
            if (!AutoOpenAvailable)
                return false;

            _setSessionValue(AutoOpenClaimedSessionKey, AutoOpenClaimedValue);
            _startAuthorize();
            return true;
        }

        /// <summary>
        /// The manual entry — the user pressed Authorize. Always starts the flow, regardless of the
        /// auto-open gate (spending the automatic budget must never lock the user out of re-auth).
        /// </summary>
        public void OnManualAuthorize() => _startAuthorize();
    }
}
