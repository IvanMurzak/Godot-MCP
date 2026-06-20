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

namespace com.IvanMurzak.Godot.MCP.Connection.DevControl
{
    /// <summary>
    /// PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>) env-gate for the DEV-ONLY inject/control
    /// HTTP bridge (<see cref="DevControlServer"/>). The bridge is <b>unauthenticated</b> — its ONLY security
    /// boundary is (a) the <c>127.0.0.1</c> loopback bind, (b) the <c>#if TOOLS</c> editor-only compilation,
    /// and (c) this env gate: it is constructed/started ONLY when the <c>GODOT_MCP_DEV_CONTROL</c> variable is
    /// truthy. If that gate ever silently failed open, a shipped/editor session would expose an
    /// unauthenticated control surface that can drive the live dock. The gate is therefore <b>load-bearing</b>
    /// and is pinned by a unit test (<c>DevControlGateTests</c>) so a regression that wires DevControl on
    /// without the env var fails fast — at construction (<see cref="AssertEnabledOrThrow"/>) and in CI.
    ///
    /// <para>
    /// The boot site (<c>GodotMcpPlugin.StartDevControlIfEnabled</c>) resolves the variable with the standard
    /// precedence (process env &gt; project-root <c>.env</c> &gt; default) and passes the resolved raw string
    /// here; this class makes no I/O and reads no env itself, so it is CI-unit-testable in the plain-xUnit host
    /// (mirrors how <see cref="DevControlRouter"/> holds the bridge's pure routing logic).
    /// </para>
    /// </summary>
    public static class DevControlGate
    {
        /// <summary>The canonical truthy value of <c>GODOT_MCP_DEV_CONTROL</c> that enables the bridge.</summary>
        public const string EnabledValue = "1";

        /// <summary>
        /// The single source of truth for "is the dev-control bridge enabled?". Returns <c>true</c> ONLY when
        /// <paramref name="devControlValue"/> (the resolved <c>GODOT_MCP_DEV_CONTROL</c> value) is exactly
        /// <c>"1"</c> after trimming surrounding whitespace — every other value (null, empty, <c>"0"</c>,
        /// <c>"true"</c>, <c>"yes"</c>, anything else) is OFF. Deliberately strict: "exactly 1" is the documented
        /// contract (see <see cref="DevControlServer"/> / README) and a strict predicate cannot be tricked into
        /// failing open by a stray/garbage value. Pure — no env read, no I/O, no Godot types.
        /// </summary>
        public static bool IsEnabled(string? devControlValue)
            => string.Equals(devControlValue?.Trim(), EnabledValue, StringComparison.Ordinal);

        /// <summary>
        /// Boot-time guard for the <see cref="DevControlServer"/> construction path. Throws
        /// <see cref="InvalidOperationException"/> when <see cref="IsEnabled"/> is <c>false</c> for
        /// <paramref name="devControlValue"/>. The boot site already early-returns when the gate is off, so in
        /// normal operation this never throws; its job is to make the env gate <b>provably load-bearing</b> — a
        /// future refactor that reorders or drops the early-return and reaches the construction site with the
        /// gate off trips this assertion immediately (fail-fast) instead of silently opening an unauthenticated
        /// loopback control surface. Pure — no env read, no I/O, no Godot types.
        /// </summary>
        public static void AssertEnabledOrThrow(string? devControlValue)
        {
            if (!IsEnabled(devControlValue))
                throw new InvalidOperationException(
                    "DevControlServer must never be constructed unless GODOT_MCP_DEV_CONTROL is truthy " +
                    "(exactly \"1\"). The dev-control bridge is unauthenticated; this env gate is its security " +
                    "boundary and must stay load-bearing. Reaching this path with the gate off is a regression.");
        }
    }
}
