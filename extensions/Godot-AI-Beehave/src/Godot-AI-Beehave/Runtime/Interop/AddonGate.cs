/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// Pure-managed (CI-unit-testable) presence-gate shape for the third-party Beehave addon. A <b>Class-B</b>
    /// extension must NOT depend on the addon, so every editor tool's FIRST action detects whether the addon is
    /// installed and — when it is not — returns a structured <c>Installed = false</c> result (NEVER a raw throw,
    /// which would be an opaque HTTP-500 to the LLM). This record is the canonical gate payload; the per-tool
    /// result records embed these same fields so the live e2e driver can assert <c>"Installed":true</c>.
    /// </summary>
    public sealed record AddonGateResult(bool Installed, string Addon, string? MissingClass, string Hint);

    /// <summary>
    /// Factory + canonical copy for the Beehave presence gate (addon display name + install hint text). Kept
    /// pure-managed so the hint shipped to the LLM is pinned by <c>AddonGateTests</c> and reused by every tool.
    /// </summary>
    public static class AddonGate
    {
        /// <summary>Display name of the wrapped addon (gate hint + catalog text).</summary>
        public const string AddonName = "Beehave";

        /// <summary>The actionable hint returned to the LLM when the addon is absent.</summary>
        public const string InstallHint =
            "Install 'Beehave' from the Godot Asset Library (or download bitbrain/beehave v2.9.2 into " +
            "addons/beehave/) and enable it under Project Settings -> Plugins, then rebuild.";

        /// <summary>The structured "addon not installed" gate result for a given missing class.</summary>
        public static AddonGateResult NotInstalled(string missingClass) =>
            new(false, AddonName, missingClass, InstallHint);

        /// <summary>The structured "addon present" gate result.</summary>
        public static AddonGateResult Ok() => new(true, AddonName, null, string.Empty);
    }
}
