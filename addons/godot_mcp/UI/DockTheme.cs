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

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The Godot-MCP dock's PURE-MANAGED design palette — the single source of truth for the colours, corner radii,
    /// padding, margins, and font sizes the dock's styled "card" sections use. The Godot analog of Unity-MCP's USS
    /// variables (<c>MainWindow.uss</c> + <c>common/_base.uss</c> / <c>_buttons.uss</c> / <c>_status-indicators.uss</c>
    /// / <c>_typography.uss</c> / <c>_forms.uss</c> / <c>_foldout.uss</c>), translated into plain RGBA tuples + ints.
    ///
    /// <para>
    /// This file carries NO Godot native types (no <c>Color</c> / <c>StyleBox</c> / <c>Theme</c>) and is OUTSIDE
    /// <c>#if TOOLS</c>, so the palette numbers are unit-testable in the plain-xUnit host. The editor-only
    /// <c>DockStyle</c> (<c>#if TOOLS</c>) maps these tuples onto real Godot <c>Color</c> / <c>StyleBoxFlat</c> /
    /// <c>Theme</c> resources. RGBA components are 0..1 floats (Godot's <c>Color</c> convention); the 8-bit source
    /// values from the brief / Unity USS are pre-divided here so the editor wiring stays a 1:1 mapping.
    /// </para>
    /// </summary>
    public static class DockTheme
    {
        // --- Card / frame-group (Unity .frame-group: dark-blue tint, rounded, padded) -------------------------

        /// <summary>Card background — dark-blue tint <c>rgba(20,40,69, 0.2)</c> (Unity's <c>.frame-group</c> bg).</summary>
        public static readonly (float R, float G, float B, float A) CardBackground = (20f / 255f, 40f / 255f, 69f / 255f, 0.2f);

        /// <summary>Card corner radius (px).</summary>
        public const int CardCornerRadius = 16;

        /// <summary>Card inner content padding (px), applied on all four sides.</summary>
        public const int CardContentPadding = 8;

        /// <summary>Card outer margin (px), applied on all four sides between stacked cards.</summary>
        public const int CardMargin = 8;

        // --- Typography (Unity _typography.uss) ----------------------------------------------------------------

        /// <summary>Header / window-title font size (px) — bold.</summary>
        public const int FontSizeHeader = 20;

        /// <summary>Section-title font size (px) — bold.</summary>
        public const int FontSizeSectionTitle = 16;

        /// <summary>Timeline / sub-label font size (px) — bold.</summary>
        public const int FontSizeSubLabel = 13;

        /// <summary>Muted gray for section descriptions (Unity's muted description text).</summary>
        public static readonly (float R, float G, float B) ColorDescriptionMuted = (0.6f, 0.6f, 0.6f);

        /// <summary>
        /// The MCP-features token sub-label colour — gray <c>Color8(150,150,150)</c> (Unity's "~N tokens total"
        /// 11px sub-label). Distinct from <see cref="ColorDescriptionMuted"/> to match the Unity reference value.
        /// </summary>
        public static readonly (float R, float G, float B) ColorTokenSubLabel = (150f / 255f, 150f / 255f, 150f / 255f);

        /// <summary>The MCP-features token sub-label font size (px) — Unity's 11px "~N tokens total".</summary>
        public const int FontSizeTokenSubLabel = 11;

        // --- Status dot (Unity _status-indicators.uss) ---------------------------------------------------------

        /// <summary>Status-dot diameter (px).</summary>
        public const int StatusDotSize = 14;

        /// <summary>Online / connected green — <c>Color8(111,226,101)</c>.</summary>
        public static readonly (float R, float G, float B) StatusOnline = (111f / 255f, 226f / 255f, 101f / 255f);

        /// <summary>Disconnected orange — <c>Color8(220,76,9)</c>.</summary>
        public static readonly (float R, float G, float B) StatusDisconnected = (220f / 255f, 76f / 255f, 9f / 255f);

        /// <summary>Connecting amber/yellow — used for the mid-handshake state (Unity's "connecting" indicator).</summary>
        public static readonly (float R, float G, float B) StatusConnecting = (0.92f, 0.74f, 0.20f);

        /// <summary>
        /// Map the dock's <see cref="ConnectionStatus"/> to a status-dot RGB colour, per the brief's palette:
        /// online → green, connecting → amber, disconnected → orange. Pure-managed (no Godot types) so the dock's
        /// status-dot colour rule is unit-testable; the editor maps the returned tuple onto a Godot <c>Color</c>.
        /// </summary>
        public static (float R, float G, float B) StatusDotColor(ConnectionStatus status) => status switch
        {
            ConnectionStatus.Connected => StatusOnline,
            ConnectionStatus.Connecting => StatusConnecting,
            _ => StatusDisconnected
        };

        // --- Buttons (Unity _buttons.uss) ----------------------------------------------------------------------

        /// <summary>Primary button background — cyan <c>Color8(175,232,230)</c> (dark text on top).</summary>
        public static readonly (float R, float G, float B) ButtonPrimary = (175f / 255f, 232f / 255f, 230f / 255f);

        /// <summary>Dark text shown on a primary (cyan) button.</summary>
        public static readonly (float R, float G, float B) ButtonPrimaryText = (0.08f, 0.08f, 0.08f);

        /// <summary>Compact / secondary button background — gray <c>Color8(70,70,70)</c>.</summary>
        public static readonly (float R, float G, float B) ButtonSecondary = (70f / 255f, 70f / 255f, 70f / 255f);

        /// <summary>Compact / secondary button corner radius (px).</summary>
        public const int ButtonSecondaryCornerRadius = 4;

        /// <summary>Compact / secondary button height (px).</summary>
        public const int ButtonSecondaryHeight = 20;

        /// <summary>Alert / Remove button background — dark-red <c>Color8(88,44,44)</c> (hover brightens to red).</summary>
        public static readonly (float R, float G, float B) ButtonAlert = (88f / 255f, 44f / 255f, 44f / 255f);

        /// <summary>Alert / Remove button hover background — brighter red.</summary>
        public static readonly (float R, float G, float B) ButtonAlertHover = (140f / 255f, 50f / 255f, 50f / 255f);

        /// <summary>
        /// The MCP-features "Open" button border colour — <c>Color8(100,100,100)</c> (Unity's <c>.btn-secondary</c>
        /// border). The fill reuses <see cref="ButtonSecondary"/> gray.
        /// </summary>
        public static readonly (float R, float G, float B) ButtonOpenBorder = (100f / 255f, 100f / 255f, 100f / 255f);

        /// <summary>The MCP-features "Open" button corner radius (px) — Unity's <c>.btn-secondary</c> 6px radius.</summary>
        public const int ButtonOpenCornerRadius = 6;

        /// <summary>The MCP-features "Open" button height (px) — Unity's <c>.btn-secondary</c> 30px.</summary>
        public const int ButtonOpenHeight = 30;

        // --- Links (Unity _typography.uss / link style) --------------------------------------------------------

        /// <summary>Link colour — light-blue <c>#4FC3F7</c> / <c>Color8(79,195,247)</c>, shown as flat buttons.</summary>
        public static readonly (float R, float G, float B) Link = (79f / 255f, 195f / 255f, 247f / 255f);

        /// <summary>The separator glyph placed between multiple links.</summary>
        public const string LinkSeparator = " • ";

        // --- Alert / warning frame (Unity warning/alert) -------------------------------------------------------

        /// <summary>Warning frame background — <c>rgba(180,120,40, 0.12)</c>.</summary>
        public static readonly (float R, float G, float B, float A) WarningBackground = (180f / 255f, 120f / 255f, 40f / 255f, 0.12f);

        /// <summary>Warning frame border — <c>rgba(220,160,60, 0.45)</c>.</summary>
        public static readonly (float R, float G, float B, float A) WarningBorder = (220f / 255f, 160f / 255f, 60f / 255f, 0.45f);

        /// <summary>Warning frame corner radius (px).</summary>
        public const int WarningCornerRadius = 10;

        /// <summary>Warning title colour — <c>Color8(255,200,100)</c>, 13px bold.</summary>
        public static readonly (float R, float G, float B) WarningTitle = (255f / 255f, 200f / 255f, 100f / 255f);

        /// <summary>Warning message colour — <c>Color8(210,180,130)</c>, 12px.</summary>
        public static readonly (float R, float G, float B) WarningMessage = (210f / 255f, 180f / 255f, 130f / 255f);

        /// <summary>Warning message font size (px).</summary>
        public const int FontSizeWarningMessage = 12;

        /// <summary>Inline warning text colour — <c>#ffbf6b</c>.</summary>
        public static readonly (float R, float G, float B) WarningText = (0xff / 255f, 0xbf / 255f, 0x6b / 255f);

        /// <summary>Inline alert / error text colour — <c>#FF6B6B</c>.</summary>
        public static readonly (float R, float G, float B) AlertText = (0xff / 255f, 0x6b / 255f, 0x6b / 255f);

        // --- Inputs (Unity _forms.uss) -------------------------------------------------------------------------

        /// <summary>Input (LineEdit / OptionButton) background — <c>rgba(0,0,0, 0.25)</c>.</summary>
        public static readonly (float R, float G, float B, float A) InputBackground = (0f, 0f, 0f, 0.25f);

        /// <summary>Input corner radius (px).</summary>
        public const int InputCornerRadius = 6;

        /// <summary>Input subtle border — <c>rgba(255,255,255, 0.08)</c>.</summary>
        public static readonly (float R, float G, float B, float A) InputBorder = (1f, 1f, 1f, 0.08f);

        // --- Divider (Unity section separator) -----------------------------------------------------------------

        /// <summary>Divider colour — 1px <c>Color8(26,26,26)</c> separator between sections.</summary>
        public static readonly (float R, float G, float B) Divider = (26f / 255f, 26f / 255f, 26f / 255f);

        // --- Segmented control (Unity-MCP's mode / transport / auth segmented toggle) ---------------------------

        /// <summary>Segmented-control TRACK background — <c>rgba(255,255,255, 0.05)</c> (the pill the segments sit in).</summary>
        public static readonly (float R, float G, float B, float A) SegmentTrackBackground = (1f, 1f, 1f, 0.05f);

        /// <summary>Segmented-control track corner radius (px).</summary>
        public const int SegmentTrackCornerRadius = 6;

        /// <summary>Segmented-control track inner padding (px) around the segments.</summary>
        public const int SegmentTrackPadding = 2;

        /// <summary>SELECTED-segment highlight background — <c>rgba(0,0,0, 0.4)</c> (the dark pill under the active segment).</summary>
        public static readonly (float R, float G, float B, float A) SegmentSelectedBackground = (0f, 0f, 0f, 0.4f);

        /// <summary>Selected-segment highlight corner radius (px).</summary>
        public const int SegmentSelectedCornerRadius = 4;

        /// <summary>Selected-segment text colour — cyan <c>Color8(175,232,230)</c> (reuses the primary cyan).</summary>
        public static readonly (float R, float G, float B) SegmentSelectedText = ButtonPrimary;

        /// <summary>Unselected-segment text colour — muted gray (reuses the description muted gray).</summary>
        public static readonly (float R, float G, float B) SegmentUnselectedText = ColorDescriptionMuted;

        /// <summary>Per-segment minimum width (px).</summary>
        public const int SegmentMinWidth = 40;

        /// <summary>Per-segment font size (px).</summary>
        public const int SegmentFontSize = 12;

        // --- Vertical timeline (Godot -> MCP server -> AI agent) -----------------------------------------------

        /// <summary>Width of the timeline indicator column holding the status circle + connecting line (px).</summary>
        public const int TimelineIndicatorWidth = 20;

        /// <summary>The connecting line drawn between consecutive timeline points — <c>Color8(80,80,80)</c>, 2px wide.</summary>
        public static readonly (float R, float G, float B) TimelineLine = (80f / 255f, 80f / 255f, 80f / 255f);

        /// <summary>Timeline connecting-line thickness (px).</summary>
        public const int TimelineLineWidth = 2;

        /// <summary>Status-circle border width for the "connecting" RING state (px).</summary>
        public const int TimelineRingBorderWidth = 2;

        // --- Feature list rows (Tools / Prompts / Resources windows) -------------------------------------------

        /// <summary>
        /// Per-row card tint for an ENABLED feature item — soft translucent green <c>rgba(80,160,80, 0.18)</c>
        /// (Unity's list item ".checked" tint). The editor maps this onto the row's <c>StyleBoxFlat</c> bg.
        /// </summary>
        public static readonly (float R, float G, float B, float A) RowEnabledTint = (80f / 255f, 160f / 255f, 80f / 255f, 0.18f);

        /// <summary>
        /// Per-row card tint for a DISABLED feature item — soft translucent red <c>rgba(160,80,80, 0.18)</c>
        /// (Unity's un-checked / disabled list item tint). The editor maps this onto the row's <c>StyleBoxFlat</c> bg.
        /// </summary>
        public static readonly (float R, float G, float B, float A) RowDisabledTint = (160f / 255f, 80f / 255f, 80f / 255f, 0.18f);

        /// <summary>Feature-row card corner radius (px).</summary>
        public const int RowCornerRadius = 8;

        /// <summary>Feature-row card inner content padding (px), applied on all four sides.</summary>
        public const int RowContentPadding = 8;

        /// <summary>Muted gray for a feature row's id sub-label (under the title). Reuses the description muted gray.</summary>
        public static readonly (float R, float G, float B) RowIdMuted = ColorDescriptionMuted;

        /// <summary>Prompt-row "Role: X" label colour — soft blue <c>Color8(143,170,220)</c>.</summary>
        public static readonly (float R, float G, float B) RoleLabel = (143f / 255f, 170f / 255f, 220f / 255f);

        /// <summary>Resource-row URI label colour — yellow-green <c>Color8(154,205,50)</c>.</summary>
        public static readonly (float R, float G, float B) ResourceUri = (154f / 255f, 205f / 255f, 50f / 255f);

        /// <summary>Resource-row "MimeType: X" label colour — plum <c>Color8(221,160,221)</c>.</summary>
        public static readonly (float R, float G, float B) ResourceMimeType = (221f / 255f, 160f / 255f, 221f / 255f);

        /// <summary>
        /// Map a feature row's enabled-state to its card tint: enabled → soft green, disabled → soft red. Pure-managed
        /// (no Godot types) so the row-tint rule is unit-testable; the editor maps the returned tuple onto a
        /// Godot <c>Color</c> / <c>StyleBoxFlat</c>.
        /// </summary>
        public static (float R, float G, float B, float A) RowTint(bool enabled) =>
            enabled ? RowEnabledTint : RowDisabledTint;
    }
}
