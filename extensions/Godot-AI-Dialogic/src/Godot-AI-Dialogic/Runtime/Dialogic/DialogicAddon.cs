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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the <b>Dialogic addon's
    /// class / member / metadata name constants</b>. A Class-B extension references the third-party addon ONLY
    /// by string name (it never takes a compile-time dependency on Dialogic), so there are no compile-time
    /// enum/class types to lean on — <b>these constants ARE the contract</b>, and they are pinned by the unit
    /// tests so an upstream rename can't drift silently.
    ///
    /// <para>
    /// Verified against <c>dialogic-godot/dialogic @ 2.0-alpha-19</c>:
    /// <list type="bullet">
    ///   <item><c>DialogicTimeline</c> (Resources/timeline.gd) — <c>class_name</c>, extension <c>.dtl</c>,
    ///   text format via <c>from_text(String)</c> / <c>as_text()</c>; saved by the auto-registered
    ///   <c>DialogicTimelineFormatSaver</c> only when the <c>timeline_not_saved</c> meta is set.</item>
    ///   <item><c>DialogicCharacter</c> (Resources/character.gd) — <c>class_name</c>, extension <c>.dch</c>,
    ///   exported members <c>display_name</c>, <c>color</c>, <c>description</c>.</item>
    /// </list>
    /// The presence probe targets <see cref="PresenceClass"/> (a real <c>class_name</c> in the global script
    /// class list), NOT the <c>Dialogic</c> AUTOLOAD — the autoload is registered by the editor plugin and is
    /// not a global class, so a class-list probe for "Dialogic" would always be false.
    /// </para>
    /// </summary>
    public static class DialogicAddon
    {
        /// <summary>Display name of the wrapped addon (gate hint + catalog).</summary>
        public const string AddonName = "Dialogic";

        /// <summary>
        /// The <c>class_name</c> the presence gate probes in the global script class list. A reliable signal:
        /// the timeline resource type the tools instantiate (the <c>Dialogic</c> autoload is NOT a global class).
        /// </summary>
        public const string PresenceClass = "DialogicTimeline";

        /// <summary>The timeline resource <c>class_name</c> (extends <c>Resource</c>).</summary>
        public const string TimelineClass = "DialogicTimeline";

        /// <summary>The character resource <c>class_name</c> (extends <c>Resource</c>).</summary>
        public const string CharacterClass = "DialogicCharacter";

        /// <summary>Timeline resource file extension (no dot).</summary>
        public const string TimelineExtension = "dtl";

        /// <summary>Character resource file extension (no dot).</summary>
        public const string CharacterExtension = "dch";

        /// <summary>Character member: the display name (GDScript snake_case).</summary>
        public const string DisplayNameMember = "display_name";

        /// <summary>Character member: the color (GDScript snake_case).</summary>
        public const string ColorMember = "color";

        /// <summary>Character member: the description (GDScript snake_case).</summary>
        public const string DescriptionMember = "description";

        /// <summary>Timeline method: replace the events from a plain-text timeline (GDScript snake_case).</summary>
        public const string FromTextMethod = "from_text";

        /// <summary>Timeline method: serialize the events back to the plain-text timeline format.</summary>
        public const string AsTextMethod = "as_text";

        /// <summary>
        /// Meta key the timeline's <c>ResourceFormatSaver</c> checks before writing — it ONLY writes the
        /// <c>.dtl</c> text when this meta is <c>true</c>. The editor tools set it before <c>ResourceSaver.Save</c>.
        /// </summary>
        public const string TimelineNotSavedMeta = "timeline_not_saved";

        /// <summary>Human-facing install hint returned by the presence gate when the addon is absent.</summary>
        public const string InstallHint =
            "Install the 'Dialogic' addon (Godot Asset Library, or https://github.com/dialogic-godot/dialogic) " +
            "into res://addons/dialogic and enable it in Project Settings -> Plugins, then rebuild.";
    }
}
