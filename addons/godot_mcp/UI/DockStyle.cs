/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.Collections.Generic;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Editor-only (<c>#if TOOLS</c>) styling helpers that translate the pure-managed <see cref="DockTheme"/> palette
    /// into real Godot <see cref="Color"/> / <see cref="StyleBoxFlat"/> / control resources, and apply them across
    /// the dock so it mimics Unity-MCP's MainWindow. This is the Godot analog of Unity-MCP's USS stylesheets: rather
    /// than a `.uss` cascade, Godot styling is done in code via <see cref="StyleBox"/>es pushed as theme overrides on
    /// individual controls (and reusable card / warning / foldout factory methods below).
    ///
    /// <para>
    /// All decision NUMBERS (colours, radii, sizes) live in <see cref="DockTheme"/> (pure-managed, CI-unit-tested);
    /// this class only constructs Godot resources from them, so it is verified via the headless Godot smoke
    /// (<c>test.md</c> Suite 3), not the plain-xUnit host.
    /// </para>
    /// </summary>
    internal static class DockStyle
    {
        // --- Color mapping (DockTheme tuple -> Godot Color) ---------------------------------------------------

        public static Color Rgb((float R, float G, float B) c) => new Color(c.R, c.G, c.B);
        public static Color Rgba((float R, float G, float B, float A) c) => new Color(c.R, c.G, c.B, c.A);

        // --- Card / frame-group --------------------------------------------------------------------------------

        /// <summary>
        /// Build the dark-blue rounded "card" <see cref="StyleBoxFlat"/> (Unity's <c>.frame-group</c>): tinted bg,
        /// 16px corner radius, 8px content padding on all sides.
        /// </summary>
        public static StyleBoxFlat CardStyleBox()
        {
            var box = new StyleBoxFlat
            {
                BgColor = Rgba(DockTheme.CardBackground)
            };
            box.SetCornerRadiusAll(DockTheme.CardCornerRadius);
            box.ContentMarginLeft = DockTheme.CardContentPadding;
            box.ContentMarginRight = DockTheme.CardContentPadding;
            box.ContentMarginTop = DockTheme.CardContentPadding;
            box.ContentMarginBottom = DockTheme.CardContentPadding;
            return box;
        }

        /// <summary>
        /// Wrap <paramref name="content"/> in a styled card: a <see cref="MarginContainer"/> (outer margin) holding a
        /// <see cref="PanelContainer"/> skinned with <see cref="CardStyleBox"/>. The caller adds the returned
        /// container to the dock body; the content is reparented INTO the card.
        /// </summary>
        public static MarginContainer Card(Control content, string name)
        {
            var margin = new MarginContainer { Name = name + "CardMargin", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            margin.AddThemeConstantOverride("margin_left", DockTheme.CardMargin);
            margin.AddThemeConstantOverride("margin_right", DockTheme.CardMargin);
            margin.AddThemeConstantOverride("margin_top", DockTheme.CardMargin);
            margin.AddThemeConstantOverride("margin_bottom", DockTheme.CardMargin);

            var panel = new PanelContainer { Name = name + "Card", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", CardStyleBox());
            margin.AddChild(panel);
            panel.AddChild(content);
            return margin;
        }

        // --- Typography ----------------------------------------------------------------------------------------

        /// <summary>Apply the 20px bold header look to a <see cref="Label"/>.</summary>
        public static void ApplyHeader(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeHeader);
        }

        /// <summary>Apply the 16px bold section-title look to a <see cref="Label"/>.</summary>
        public static void ApplySectionTitle(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSectionTitle);
        }

        /// <summary>Apply the muted-gray description look to a <see cref="Label"/>.</summary>
        public static void ApplyDescription(Label label)
        {
            label.AddThemeColorOverride("font_color", Rgb(DockTheme.ColorDescriptionMuted));
            label.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        }

        /// <summary>
        /// Apply the muted, SINGLE-LINE, ellipsis-truncated config-path look (Unity's <c>labelConfigPath</c>:
        /// <c>section-desc</c> + <c>text-overflow: ellipsis; white-space: nowrap</c>). Unlike
        /// <see cref="ApplyDescription"/> this never wraps — it clips with a trailing ellipsis so a long path hugs a
        /// single line. The caller sets <see cref="Control.SizeFlagsHorizontal"/> / <see cref="Label.HorizontalAlignment"/>
        /// for right-alignment within its row.
        /// </summary>
        public static void ApplyConfigPath(Label label)
        {
            label.AddThemeColorOverride("font_color", Rgb(DockTheme.ColorDescriptionMuted));
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.ClipText = true;
        }

        /// <summary>Apply the 13px bold sub/timeline label look to a <see cref="Label"/>.</summary>
        public static void ApplySubLabel(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSubLabel);
        }

        // --- Buttons -------------------------------------------------------------------------------------------

        /// <summary>Skin <paramref name="button"/> as the PRIMARY action (cyan bg, dark text) — e.g. Configure when not configured.</summary>
        public static void ApplyPrimaryButton(Button button)
        {
            var bg = Rgb(DockTheme.ButtonPrimary);
            ApplyButtonBackground(button, bg, bg.Lightened(0.1f), DockTheme.ButtonSecondaryCornerRadius);
            button.AddThemeColorOverride("font_color", Rgb(DockTheme.ButtonPrimaryText));
            button.AddThemeColorOverride("font_hover_color", Rgb(DockTheme.ButtonPrimaryText));
            button.AddThemeColorOverride("font_pressed_color", Rgb(DockTheme.ButtonPrimaryText));
        }

        /// <summary>Skin <paramref name="button"/> as a compact SECONDARY action (gray bg, 4px radius, ~20px tall).</summary>
        public static void ApplySecondaryButton(Button button)
        {
            var bg = Rgb(DockTheme.ButtonSecondary);
            ApplyButtonBackground(button, bg, bg.Lightened(0.1f), DockTheme.ButtonSecondaryCornerRadius);
            button.CustomMinimumSize = new Vector2(0, DockTheme.ButtonSecondaryHeight);
        }

        /// <summary>Skin <paramref name="button"/> as an ALERT / Remove action (dark-red bg, brighter red hover).</summary>
        public static void ApplyAlertButton(Button button)
        {
            ApplyButtonBackground(button, Rgb(DockTheme.ButtonAlert), Rgb(DockTheme.ButtonAlertHover), DockTheme.ButtonSecondaryCornerRadius);
            button.CustomMinimumSize = new Vector2(0, DockTheme.ButtonSecondaryHeight);
        }

        /// <summary>
        /// Skin <paramref name="button"/> as the MCP-features "Open" action (Unity's <c>.btn-secondary</c>): gray
        /// <see cref="DockTheme.ButtonSecondary"/> fill, a <see cref="DockTheme.ButtonOpenBorder"/> 1px border,
        /// <see cref="DockTheme.ButtonOpenCornerRadius"/> radius, and <see cref="DockTheme.ButtonOpenHeight"/> tall.
        /// Distinct from <see cref="ApplySecondaryButton"/> (the compact 20px/4px-radius/no-border variant used by
        /// the agent action row) so the dock's feature rows match the taller bordered Unity button exactly.
        /// </summary>
        public static void ApplyOpenButton(Button button)
        {
            var bg = Rgb(DockTheme.ButtonSecondary);
            var border = Rgb(DockTheme.ButtonOpenBorder);
            ApplyBorderedButtonBackground(button, bg, bg.Lightened(0.1f), border, DockTheme.ButtonOpenCornerRadius);
            button.CustomMinimumSize = new Vector2(0, DockTheme.ButtonOpenHeight);
        }

        static void ApplyBorderedButtonBackground(Button button, Color normal, Color hover, Color border, int cornerRadius)
        {
            StyleBoxFlat Make(Color bg)
            {
                var box = new StyleBoxFlat { BgColor = bg, BorderColor = border };
                box.SetCornerRadiusAll(cornerRadius);
                box.SetBorderWidthAll(1);
                box.ContentMarginLeft = 10;
                box.ContentMarginRight = 10;
                box.ContentMarginTop = 4;
                box.ContentMarginBottom = 4;
                return box;
            }

            button.AddThemeStyleboxOverride("normal", Make(normal));
            button.AddThemeStyleboxOverride("hover", Make(hover));
            button.AddThemeStyleboxOverride("pressed", Make(normal.Darkened(0.1f)));
        }

        static void ApplyButtonBackground(Button button, Color normal, Color hover, int cornerRadius)
        {
            var normalBox = new StyleBoxFlat { BgColor = normal };
            normalBox.SetCornerRadiusAll(cornerRadius);
            normalBox.ContentMarginLeft = 8;
            normalBox.ContentMarginRight = 8;

            var hoverBox = new StyleBoxFlat { BgColor = hover };
            hoverBox.SetCornerRadiusAll(cornerRadius);
            hoverBox.ContentMarginLeft = 8;
            hoverBox.ContentMarginRight = 8;

            var pressedBox = new StyleBoxFlat { BgColor = normal.Darkened(0.1f) };
            pressedBox.SetCornerRadiusAll(cornerRadius);
            pressedBox.ContentMarginLeft = 8;
            pressedBox.ContentMarginRight = 8;

            button.AddThemeStyleboxOverride("normal", normalBox);
            button.AddThemeStyleboxOverride("hover", hoverBox);
            button.AddThemeStyleboxOverride("pressed", pressedBox);
        }

        // --- Links ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Build a flat, link-coloured <see cref="Button"/> that opens <paramref name="url"/> via
        /// <see cref="OS.ShellOpen"/>. Flat (no button chrome) + light-blue text, mimicking an inline hyperlink.
        /// </summary>
        public static Button LinkButton(string name, string text, string url)
        {
            var button = new Button
            {
                Name = name,
                Text = text,
                TooltipText = url,
                Flat = true,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            var link = Rgb(DockTheme.Link);
            button.AddThemeColorOverride("font_color", link);
            button.AddThemeColorOverride("font_hover_color", link.Lightened(0.2f));
            button.AddThemeColorOverride("font_pressed_color", link);
            button.Pressed += () => OS.ShellOpen(url);
            return button;
        }

        /// <summary>
        /// Build a row of link buttons separated by the "•" glyph. <paramref name="links"/> is a list of
        /// (name, text, url); separators are inserted between them. Returns the row to add into a parent.
        /// </summary>
        public static HBoxContainer LinkRow(string name, IReadOnlyList<(string Name, string Text, string Url)> links)
        {
            var row = new HBoxContainer { Name = name };
            row.AddThemeConstantOverride("separation", 0);
            for (int i = 0; i < links.Count; i++)
            {
                if (i > 0)
                    row.AddChild(new Label { Text = DockTheme.LinkSeparator });
                row.AddChild(LinkButton(links[i].Name, links[i].Text, links[i].Url));
            }
            return row;
        }

        // --- Alert / warning frame -----------------------------------------------------------------------------

        /// <summary>
        /// Build a styled warning/alert card holding the <paramref name="message"/> (Unity's warning frame): tinted
        /// amber bg, amber border, 10px radius; the message text is the warm <see cref="DockTheme.WarningMessage"/>.
        /// </summary>
        public static PanelContainer WarningFrame(string message)
        {
            var box = new StyleBoxFlat
            {
                BgColor = Rgba(DockTheme.WarningBackground),
                BorderColor = Rgba(DockTheme.WarningBorder)
            };
            box.SetCornerRadiusAll(DockTheme.WarningCornerRadius);
            box.SetBorderWidthAll(1);
            box.ContentMarginLeft = 8;
            box.ContentMarginRight = 8;
            box.ContentMarginTop = 6;
            box.ContentMarginBottom = 6;

            var panel = new PanelContainer { Name = "WarningFrame", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", box);

            var label = new Label
            {
                Name = "WarningMessage",
                Text = message,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            label.AddThemeColorOverride("font_color", Rgb(DockTheme.WarningMessage));
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeWarningMessage);
            panel.AddChild(label);
            return panel;
        }

        /// <summary>
        /// Build a full amber ALERT panel (Unity's warning frame with an action): the same tinted amber bg /
        /// border / radius as <see cref="WarningFrame"/>, but with a bold amber <paramref name="title"/> over a
        /// warm <paramref name="message"/> and a primary (cyan) action button. Used for "Authorization Required"
        /// and "Connection Required". The returned panel is shown/hidden by the caller per the pure-managed
        /// <see cref="ConnectionPanelView.ShowAuthorizationRequired"/> / <see cref="ConnectionPanelView.ShowConnectionRequired"/>
        /// rules. <paramref name="onPressed"/> wires the button.
        /// </summary>
        public static PanelContainer AlertPanel(string name, string title, string message, string buttonText, System.Action onPressed)
        {
            var box = new StyleBoxFlat
            {
                BgColor = Rgba(DockTheme.WarningBackground),
                BorderColor = Rgba(DockTheme.WarningBorder)
            };
            box.SetCornerRadiusAll(DockTheme.WarningCornerRadius);
            box.SetBorderWidthAll(1);
            box.ContentMarginLeft = 8;
            box.ContentMarginRight = 8;
            box.ContentMarginTop = 6;
            box.ContentMarginBottom = 6;

            var panel = new PanelContainer { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", box);

            var col = new VBoxContainer { Name = "AlertContent" };
            col.AddThemeConstantOverride("separation", 4);
            panel.AddChild(col);

            var titleLabel = new Label { Name = "AlertTitle", Text = title };
            titleLabel.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSubLabel);
            titleLabel.AddThemeColorOverride("font_color", Rgb(DockTheme.WarningTitle));
            col.AddChild(titleLabel);

            var messageLabel = new Label
            {
                Name = "AlertMessage",
                Text = message,
                AutowrapMode = TextServer.AutowrapMode.WordSmart
            };
            messageLabel.AddThemeColorOverride("font_color", Rgb(DockTheme.WarningMessage));
            messageLabel.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeWarningMessage);
            col.AddChild(messageLabel);

            var button = new Button { Name = "AlertButton", Text = buttonText };
            ApplyPrimaryButton(button);
            button.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;
            button.Pressed += () => onPressed();
            col.AddChild(button);

            return panel;
        }

        // --- Inputs --------------------------------------------------------------------------------------------

        /// <summary>Build the input (LineEdit/OptionButton) normal <see cref="StyleBoxFlat"/>: translucent-black bg, 6px radius, subtle border.</summary>
        public static StyleBoxFlat InputStyleBox()
        {
            var box = new StyleBoxFlat
            {
                BgColor = Rgba(DockTheme.InputBackground),
                BorderColor = Rgba(DockTheme.InputBorder)
            };
            box.SetCornerRadiusAll(DockTheme.InputCornerRadius);
            box.SetBorderWidthAll(1);
            box.ContentMarginLeft = 6;
            box.ContentMarginRight = 6;
            box.ContentMarginTop = 4;
            box.ContentMarginBottom = 4;
            return box;
        }

        /// <summary>Skin a <see cref="LineEdit"/> with the input style.</summary>
        public static void ApplyInput(LineEdit field)
        {
            field.AddThemeStyleboxOverride("normal", InputStyleBox());
        }

        // --- Divider -------------------------------------------------------------------------------------------

        /// <summary>Build a 1px section divider <see cref="ColorRect"/> in the dark divider colour.</summary>
        public static ColorRect Divider(string name = "Divider")
        {
            return new ColorRect
            {
                Name = name,
                Color = Rgb(DockTheme.Divider),
                CustomMinimumSize = new Vector2(0, 1),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
        }

        // --- Feature list rows (Tools / Prompts / Resources windows) -------------------------------------------

        /// <summary>
        /// Build the per-row "card" <see cref="StyleBoxFlat"/> for a feature item: rounded
        /// (<see cref="DockTheme.RowCornerRadius"/>), padded (<see cref="DockTheme.RowContentPadding"/>), and tinted
        /// by enabled-state — soft green when <paramref name="enabled"/>, soft red otherwise
        /// (<see cref="DockTheme.RowTint"/>).
        /// </summary>
        public static StyleBoxFlat RowStyleBox(bool enabled)
        {
            var box = new StyleBoxFlat
            {
                BgColor = Rgba(DockTheme.RowTint(enabled))
            };
            box.SetCornerRadiusAll(DockTheme.RowCornerRadius);
            box.ContentMarginLeft = DockTheme.RowContentPadding;
            box.ContentMarginRight = DockTheme.RowContentPadding;
            box.ContentMarginTop = DockTheme.RowContentPadding;
            box.ContentMarginBottom = DockTheme.RowContentPadding;
            return box;
        }

        /// <summary>
        /// Wrap a feature row's <paramref name="content"/> in a tinted, rounded <see cref="PanelContainer"/> card
        /// (<see cref="RowStyleBox"/>) whose tint reflects <paramref name="enabled"/>. The caller adds the returned
        /// panel to the list; the content is reparented INTO the card.
        /// </summary>
        public static PanelContainer RowCard(Control content, string name, bool enabled)
        {
            var panel = new PanelContainer { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", RowStyleBox(enabled));
            panel.AddChild(content);
            return panel;
        }

        /// <summary>Apply the 16px bold row-title look + a coloured metadata-label colour to a <see cref="Label"/>.</summary>
        public static void ApplyRowTitle(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSectionTitle);
        }

        /// <summary>Apply the muted-gray row-id (sub-label) look to a <see cref="Label"/>.</summary>
        public static void ApplyRowId(Label label)
        {
            label.AddThemeColorOverride("font_color", Rgb(DockTheme.RowIdMuted));
        }

        /// <summary>Tint a metadata <see cref="Label"/> (role / uri / mimetype / token) with an arbitrary palette RGB.</summary>
        public static void ApplyMetadataColor(Label label, (float R, float G, float B) color)
        {
            label.AddThemeColorOverride("font_color", Rgb(color));
        }

        // --- Filter bar (search field + status dropdown + stats label) ----------------------------------------

        /// <summary>Skin an <see cref="OptionButton"/> (the status filter) with the input style.</summary>
        public static void ApplyOptionButton(OptionButton option)
        {
            option.AddThemeStyleboxOverride("normal", InputStyleBox());
        }

        // --- Segmented control (reusable Custom|Cloud / stdio|http / none|required toggle) ---------------------

        /// <summary>
        /// Build a horizontal SEGMENTED CONTROL: a track-skinned <see cref="HBoxContainer"/> holding one toggle
        /// <see cref="Button"/> per option, where exactly one segment is "selected" (dark highlight + cyan text)
        /// and the rest are muted. Mirrors Unity-MCP's segmented mode/transport/auth toggle. The numbers
        /// (track/selected colours, radii, per-segment width/font) all come from <see cref="DockTheme"/>;
        /// the index/selection rules come from the pure-managed (unit-tested) <see cref="SegmentedControlModel"/>.
        ///
        /// <para>
        /// <paramref name="onSelected"/> fires with the chosen option index when the user clicks a NOT-already-
        /// selected segment (clicking the active segment is a no-op). The caller owns the value→index mapping and
        /// re-renders selection via <see cref="SetSegmentedSelection"/> after persisting; this builder does NOT
        /// auto-toggle, so the visual selection never drifts from the backing config.
        /// </para>
        /// </summary>
        public static PanelContainer SegmentedControl(
            string name,
            System.Collections.Generic.IReadOnlyList<string> options,
            int selectedIndex,
            System.Action<int> onSelected)
        {
            var track = new HBoxContainer { Name = name };
            track.AddThemeConstantOverride("separation", 0);

            var trackBox = new StyleBoxFlat { BgColor = Rgba(DockTheme.SegmentTrackBackground) };
            trackBox.SetCornerRadiusAll(DockTheme.SegmentTrackCornerRadius);
            trackBox.ContentMarginLeft = DockTheme.SegmentTrackPadding;
            trackBox.ContentMarginRight = DockTheme.SegmentTrackPadding;
            trackBox.ContentMarginTop = DockTheme.SegmentTrackPadding;
            trackBox.ContentMarginBottom = DockTheme.SegmentTrackPadding;

            // The track skin is applied via a PanelContainer wrapper so the pill background frames the segments.
            var panel = new PanelContainer { Name = name + "Track" };
            panel.AddThemeStyleboxOverride("panel", trackBox);
            panel.AddChild(track);

            var clamped = SegmentedControlModel.ClampSelected(selectedIndex, options.Count);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i; // capture for the lambda
                var segment = new Button
                {
                    Name = "Segment" + i,
                    Text = options[i],
                    ToggleMode = true,
                    ButtonPressed = SegmentedControlModel.IsSelected(i, clamped),
                    Flat = true,
                    CustomMinimumSize = new Vector2(DockTheme.SegmentMinWidth, 0)
                };
                segment.AddThemeFontSizeOverride("font_size", DockTheme.SegmentFontSize);
                ApplySegmentStyle(segment, SegmentedControlModel.IsSelected(i, clamped));

                segment.Pressed += () =>
                {
                    // Clicking the already-selected segment is a no-op (and we keep it visually pressed).
                    if (SegmentedControlModel.IsSelected(index, GetSegmentedSelection(track)))
                    {
                        SetSegmentedSelection(track, GetSegmentedSelection(track));
                        return;
                    }
                    onSelected(index);
                };
                track.AddChild(segment);
            }

            return panel;
        }

        /// <summary>
        /// Re-render which segment of a <see cref="SegmentedControl"/> is selected — call after the backing
        /// value changes (e.g. after persisting a mode toggle). <paramref name="track"/> is the inner
        /// <see cref="HBoxContainer"/> returned indirectly by <see cref="SegmentedControl"/> (reach it via the
        /// panel's first child); pass the panel and this resolves it.
        /// </summary>
        public static void SetSegmentedSelection(Control trackOrPanel, int selectedIndex)
        {
            var track = ResolveSegmentTrack(trackOrPanel);
            if (track == null)
                return;

            var clamped = SegmentedControlModel.ClampSelected(selectedIndex, track.GetChildCount());
            for (int i = 0; i < track.GetChildCount(); i++)
            {
                if (track.GetChild(i) is Button segment)
                {
                    var isSel = SegmentedControlModel.IsSelected(i, clamped);
                    segment.ButtonPressed = isSel;
                    ApplySegmentStyle(segment, isSel);
                }
            }
        }

        static int GetSegmentedSelection(HBoxContainer track)
        {
            for (int i = 0; i < track.GetChildCount(); i++)
            {
                if (track.GetChild(i) is Button segment && segment.ButtonPressed)
                    return i;
            }
            return 0;
        }

        static HBoxContainer? ResolveSegmentTrack(Control trackOrPanel)
        {
            if (trackOrPanel is HBoxContainer hbox)
                return hbox;
            // SegmentedControl returns a PanelContainer wrapping the HBox track.
            if (trackOrPanel.GetChildCount() > 0 && trackOrPanel.GetChild(0) is HBoxContainer inner)
                return inner;
            return null;
        }

        static void ApplySegmentStyle(Button segment, bool selected)
        {
            if (selected)
            {
                var box = new StyleBoxFlat { BgColor = Rgba(DockTheme.SegmentSelectedBackground) };
                box.SetCornerRadiusAll(DockTheme.SegmentSelectedCornerRadius);
                box.ContentMarginLeft = 8;
                box.ContentMarginRight = 8;
                box.ContentMarginTop = 2;
                box.ContentMarginBottom = 2;
                segment.AddThemeStyleboxOverride("normal", box);
                segment.AddThemeStyleboxOverride("hover", box);
                segment.AddThemeStyleboxOverride("pressed", box);

                var text = Rgb(DockTheme.SegmentSelectedText);
                segment.AddThemeColorOverride("font_color", text);
                segment.AddThemeColorOverride("font_hover_color", text);
                segment.AddThemeColorOverride("font_pressed_color", text);
            }
            else
            {
                // Unselected: transparent (track shows through) + muted text.
                var empty = new StyleBoxEmpty();
                segment.AddThemeStyleboxOverride("normal", empty);
                segment.AddThemeStyleboxOverride("hover", empty);
                segment.AddThemeStyleboxOverride("pressed", empty);

                var muted = Rgb(DockTheme.SegmentUnselectedText);
                segment.AddThemeColorOverride("font_color", muted);
                segment.AddThemeColorOverride("font_hover_color", muted.Lightened(0.2f));
                segment.AddThemeColorOverride("font_pressed_color", muted);
            }
        }

        // --- Vertical timeline (Godot -> MCP server -> AI agent) ----------------------------------------------

        /// <summary>
        /// Build the status circle for a timeline point in a given <see cref="ConnectionPanelView.TimelinePointState"/>:
        /// <c>Online</c> = filled green disc, <c>Connecting</c> = green RING (transparent fill, 2px green border),
        /// <c>Disconnected</c> = filled orange disc. A <see cref="Panel"/> sized to <see cref="DockTheme.StatusDotSize"/>
        /// with a fully-rounded <see cref="StyleBoxFlat"/>. Use <see cref="ApplyTimelineCircle"/> to re-style an
        /// existing circle in place (the panel reuses one node across status changes).
        /// </summary>
        public static Panel TimelineCircle(string name, ConnectionPanelView.TimelinePointState state)
        {
            var circle = new Panel
            {
                Name = name,
                CustomMinimumSize = new Vector2(DockTheme.StatusDotSize, DockTheme.StatusDotSize),
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
            };
            ApplyTimelineCircle(circle, state);
            return circle;
        }

        /// <summary>
        /// Re-style an existing timeline circle <see cref="Panel"/> for the given
        /// <see cref="ConnectionPanelView.TimelinePointState"/> in place (filled disc vs green ring). Called on
        /// every status change so a single circle node tracks the live state.
        /// </summary>
        public static void ApplyTimelineCircle(Panel circle, ConnectionPanelView.TimelinePointState state)
        {
            var radius = DockTheme.StatusDotSize / 2;
            StyleBoxFlat box;
            switch (state)
            {
                case ConnectionPanelView.TimelinePointState.Online:
                    box = new StyleBoxFlat { BgColor = Rgb(DockTheme.StatusOnline) };
                    break;
                case ConnectionPanelView.TimelinePointState.Connecting:
                    // Green RING: transparent fill + 2px green border.
                    box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), BorderColor = Rgb(DockTheme.StatusOnline) };
                    box.SetBorderWidthAll(DockTheme.TimelineRingBorderWidth);
                    break;
                default:
                    box = new StyleBoxFlat { BgColor = Rgb(DockTheme.StatusDisconnected) };
                    break;
            }
            box.SetCornerRadiusAll(radius);
            circle.AddThemeStyleboxOverride("panel", box);
        }

        /// <summary>
        /// Build the 2px vertical connecting line drawn between consecutive timeline points
        /// (<see cref="DockTheme.TimelineLine"/>). A thin <see cref="ColorRect"/> that ExpandFills vertically so
        /// it spans the gap; the LAST point passes a hidden one (no line below the final point).
        /// </summary>
        public static ColorRect TimelineLine(string name = "TimelineLine")
        {
            return new ColorRect
            {
                Name = name,
                Color = Rgb(DockTheme.TimelineLine),
                CustomMinimumSize = new Vector2(DockTheme.TimelineLineWidth, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter
            };
        }

        /// <summary>
        /// Build a 13px timeline-point title (Unity's timeline label) WITH a thin underline: a
        /// <see cref="VBoxContainer"/> holding the <see cref="Label"/> over a 1px divider-coloured underline rule.
        /// Godot's plain <see cref="Label"/> has no font-underline override, so the underline is a real 1px
        /// <see cref="ColorRect"/> that hugs the label width — reliable across Godot versions.
        /// </summary>
        public static VBoxContainer TimelineLabel(string name, string text)
        {
            var box = new VBoxContainer { Name = name };
            box.AddThemeConstantOverride("separation", 1);
            box.SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin;

            var label = new Label { Name = "Text", Text = text };
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSubLabel);
            box.AddChild(label);

            var underline = new ColorRect
            {
                Name = "Underline",
                Color = Rgb(DockTheme.Divider).Lightened(0.4f),
                CustomMinimumSize = new Vector2(0, 1),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            box.AddChild(underline);
            return box;
        }

        // --- Foldout (collapsible section: a toggle Button + a child VBox shown/hidden) ------------------------

        /// <summary>
        /// Build a collapsible foldout: a toggle <see cref="Button"/> whose press shows/hides a returned content
        /// <see cref="VBoxContainer"/>. The Godot analog of Unity's <c>TemplateFoldout</c>. The caller adds
        /// <paramref name="container"/> (the OUTER VBox holding both the toggle and the content) to its parent, and
        /// fills <c>content</c> with the foldout's children.
        /// </summary>
        public static (VBoxContainer Container, VBoxContainer Content) Foldout(string title, bool startExpanded = false)
        {
            var container = new VBoxContainer { Name = title.Replace(" ", string.Empty) + "Foldout" };
            container.AddThemeConstantOverride("separation", 2);

            var toggle = new Button
            {
                Name = "Toggle",
                ToggleMode = true,
                ButtonPressed = startExpanded,
                Flat = true,
                Alignment = HorizontalAlignment.Left,
                Text = (startExpanded ? "▾ " : "▸ ") + title
            };
            container.AddChild(toggle);

            var content = new VBoxContainer { Name = "Content", Visible = startExpanded };
            content.AddThemeConstantOverride("separation", 2);
            container.AddChild(content);

            toggle.Toggled += pressed =>
            {
                content.Visible = pressed;
                toggle.Text = (pressed ? "▾ " : "▸ ") + title;
            };

            return (container, content);
        }
    }
}
#endif
