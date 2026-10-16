/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// Pure-managed (CI-unit-testable) result of the <b>mandatory Class-B presence gate</b>: whether the
    /// wrapped addon is installed, and (when not) which class was missing plus a human install hint. Every
    /// editor tool's FIRST action probes the addon and, when absent, returns a structured
    /// <c>Installed: false</c> payload built from <see cref="NotInstalled"/> — NEVER a raw throw (a throw is an
    /// opaque HTTP-500 to the LLM; a structured result tells the model exactly what to install and lets the
    /// E2E driver assert the graceful path).
    /// </summary>
    public sealed record AddonGateResult(bool Installed, string Addon, string? MissingClass, string Hint)
    {
        /// <summary>The addon is missing — carry the missing class name and the install hint.</summary>
        public static AddonGateResult NotInstalled(string addon, string missingClass, string hint) =>
            new(false, addon, missingClass, hint);

        /// <summary>The addon is present.</summary>
        public static AddonGateResult Ok(string addon) =>
            new(true, addon, null, string.Empty);
    }
}
