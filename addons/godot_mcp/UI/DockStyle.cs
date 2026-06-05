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
