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
    /// Pure-managed, serializable structured result every <c>dialogic-*</c> tool returns. Holds ONLY
    /// primitives + strings (no Godot native types), so it is safe to build inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate and return across the tool boundary, it serializes cleanly
    /// through ReflectorNet (PascalCase property names — the E2E fixture asserts <c>"Installed":true</c> etc.),
    /// and the pure-managed defaults helper can produce one with no Godot binary (CI-unit-testable).
    ///
    /// <para>
    /// This is the Class-B <b>presence-gate union</b> (Option A): the gate fields (<see cref="Installed"/>,
    /// <see cref="Addon"/>, <see cref="MissingClass"/>, <see cref="Hint"/>) carry the "addon not installed"
    /// state; the resource fields carry the real result when the addon IS installed.
    /// </para>
    /// </summary>
    public sealed class DialogicResourceInfo
    {
        /// <summary>Whether the wrapped Dialogic addon was installed when the tool ran.</summary>
        public bool Installed { get; set; } = true;

        /// <summary>The wrapped addon's display name (always <c>Dialogic</c>).</summary>
        public string Addon { get; set; } = DialogicAddon.AddonName;

        /// <summary>When <see cref="Installed"/> is false, the class the gate probed for and did not find.</summary>
        public string? MissingClass { get; set; }

        /// <summary>When <see cref="Installed"/> is false, the human-facing install hint; otherwise null.</summary>
        public string? Hint { get; set; }

        /// <summary>Kind of resource this result describes: <c>timeline</c>, <c>character</c>, or <c>defaults</c>.</summary>
        public string ResourceKind { get; set; } = string.Empty;

        /// <summary>The <c>res://</c> path of the authored/read resource (empty for a defaults snapshot).</summary>
        public string ResourcePath { get; set; } = string.Empty;

        /// <summary>Character display name (empty when not applicable).</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>Character color as <c>#rrggbb</c> (empty when not applicable).</summary>
        public string Color { get; set; } = string.Empty;

        /// <summary>Character description (empty when not applicable).</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>Number of (non-empty) text-event lines in a timeline (0 when not applicable).</summary>
        public int EventCount { get; set; }

        /// <summary>The timeline's plain-text content (empty when not applicable).</summary>
        public string TimelineText { get; set; } = string.Empty;

        /// <summary>Map a failed presence gate onto an <c>Installed: false</c> result.</summary>
        public static DialogicResourceInfo NotInstalled(AddonGateResult gate) => new()
        {
            Installed = false,
            Addon = gate.Addon,
            MissingClass = gate.MissingClass,
            Hint = gate.Hint,
        };
    }
}
