/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// Pure-managed result of the addon presence gate (CLASS-B). Every editor tool's FIRST action probes
    /// whether the Terrain3D GDExtension is installed (via <c>ClassDB</c>, see <c>AddonInterop</c>); when it
    /// is absent the tool returns a structured <c>Installed: false</c> payload carrying this shape instead of
    /// throwing a raw exception (a raw throw is an opaque HTTP-500 to the LLM; a structured result tells the
    /// model exactly what to install). Pure-managed (no Godot native types) so the shape + hint text are
    /// CI-unit-testable with no Godot binary.
    /// </summary>
    public sealed record AddonGateResult(bool Installed, string Addon, string? MissingClass, string Hint);

    /// <summary>
    /// Pure-managed factory + constants for the Terrain3D presence gate. The addon's class name is referenced
    /// here as a STRING only — the package never takes a compile-time dependency on the Terrain3D GDExtension.
    /// </summary>
    public static class AddonGate
    {
        /// <summary>Display name of the wrapped third-party addon (gate result + install hint).</summary>
        public const string AddonName = "Terrain3D";

        /// <summary>
        /// The <c>ClassDB</c> class the gate probes for — the real GDExtension node the tools instantiate.
        /// (GDExtension classes live in <c>ClassDB</c>, NOT in the GDScript global class list.)
        /// </summary>
        public const string PresenceClass = "Terrain3D";

        /// <summary>Actionable install hint surfaced to the LLM when the addon is absent.</summary>
        public const string InstallHint =
            "Install the 'Terrain3D' GDExtension addon (TokisanGames/Terrain3D, tested against v1.0.2-stable) " +
            "into addons/terrain_3d/, restart Godot, and enable it under Project Settings -> Plugins. Terrain3D " +
            "ships precompiled binaries, so no C++ build is needed. Requires Godot 4.4 or newer.";

        /// <summary>A gate failure for the default presence class.</summary>
        public static AddonGateResult NotInstalled() => new(false, AddonName, PresenceClass, InstallHint);

        /// <summary>A gate failure naming a specific missing class.</summary>
        public static AddonGateResult NotInstalled(string missingClass) =>
            new(false, AddonName, missingClass, InstallHint);

        /// <summary>A passing gate (addon present).</summary>
        public static AddonGateResult Ok() => new(true, AddonName, null, string.Empty);
    }
}
