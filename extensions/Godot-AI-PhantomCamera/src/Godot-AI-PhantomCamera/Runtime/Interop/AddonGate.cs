/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// The shared, pure-managed presence-gate result shape for this Class-B extension. When the Phantom Camera
    /// addon is NOT installed, an editor tool returns a structured <c>installed:false</c> payload (Option A
    /// from the Class-B guide) instead of throwing — a raw throw is an opaque HTTP-500 to the LLM, whereas
    /// this tells the model exactly what to install. The fields mirror those embedded on
    /// <see cref="PhantomCameraInfo"/> so the e2e driver can assert the graceful path.
    /// </summary>
    public sealed record AddonGateResult(bool Installed, string Addon, string? MissingClass, string Hint);

    /// <summary>
    /// Pure-managed constants + factories for the Phantom Camera presence gate. CI-unit-testable
    /// (<c>AddonGateTests</c>) — no Godot binary — so the addon name, the probe class, and the install hint
    /// are pinned in ONE place and can never drift between the tools.
    /// </summary>
    public static class AddonGate
    {
        /// <summary>Display name of the wrapped addon (catalog entry + gate hint text).</summary>
        public const string PhantomCameraAddon = "Phantom Camera";

        /// <summary>
        /// The GDScript <c>class_name</c> the gate probes for via the global script-class list. Its presence
        /// means the addon is installed (and its <c>class_name</c> types registered).
        /// </summary>
        public const string PresenceClass = PhantomCameraNames.PhantomCamera3D;

        /// <summary>The actionable hint surfaced when the addon is absent.</summary>
        public const string InstallHint =
            "Install 'Phantom Camera' from the Godot Asset Library (or " +
            "https://github.com/ramokz/phantom-camera, tested against v0.11.0.2), then enable it under " +
            "Project Settings -> Plugins so its PhantomCameraManager autoload is registered.";

        /// <summary>Build the structured "addon not installed" gate result.</summary>
        public static AddonGateResult NotInstalled(string? missingClass = null) =>
            new(false, PhantomCameraAddon, missingClass ?? PresenceClass, InstallHint);

        /// <summary>Build the "addon present" gate result.</summary>
        public static AddonGateResult Ok() => new(true, PhantomCameraAddon, null, string.Empty);
    }
}
