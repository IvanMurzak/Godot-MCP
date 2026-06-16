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

        // --- Signal connection (object+method form) -----------------------------------------------------------
        // Every Godot-signal connection in the dock is made via an OBJECT+METHOD Callable
        // (`new Callable(ownerGodotObject, "<MethodName>")`), NOT a C# delegate/lambda. Object+method Callables
        // are NOT ManagedCallables — they never enter the native `ManagedCallable::instances` registry that
        // `CSharpLanguage::reload_assemblies` iterates on a Build-Project hot-reload, so they CANNOT raise the
        //   ERROR: csharp_script.cpp - managed_callable->delegate_handle.value == nullptr
        // flood that delegate/lambda connections produced. The target Godot Object is kept alive by the scene
        // tree, so no managed GC-rooting (the old KeepAlive ConditionalWeakTable) is needed or used.
        //
        // The convenience helpers below own a tiny `[Tool] partial` GodotObject subclass (LinkButton,
        // IconButton, GoldenButton, AlertPanel, SegmentedControl, Foldout) so the click logic is an INSTANCE
        // METHOD on a real GodotObject, connectable via MethodName.

        /// <summary>
        /// Connect <paramref name="button"/>'s <c>Pressed</c> signal to the instance method named
        /// <paramref name="methodName"/> on <paramref name="owner"/> via an OBJECT+METHOD <see cref="Callable"/>.
        /// Object+method Callables are not ManagedCallables, so they never trigger the
        /// <c>delegate_handle.value == nullptr</c> hot-reload flood that <c>button.Pressed += handler</c> (a C#
        /// delegate) would. The handler MUST be an instance method on <paramref name="owner"/> visible to Godot.
        /// </summary>
        public static void ConnectPressed(BaseButton button, GodotObject owner, StringName methodName)
            => button.Connect(BaseButton.SignalName.Pressed, new Callable(owner, methodName));

        // --- Bold font (Unity's `-unity-font-style: bold` on headers / section-titles / timeline-labels) ----------
        // Godot Labels have no font-style flag; bold requires a bold Font resource. In the editor the bold face is
        // available from the editor theme ("bold" under "EditorFonts"). Resolved once and cached; degrades to no-op
        // (regular weight) if unavailable, so it never throws during dock construction.

        static Font? _editorBoldFont;
        static bool _editorBoldFontResolved;

        static Font? EditorBoldFont()
        {
            if (_editorBoldFontResolved)
                return _editorBoldFont;
            _editorBoldFontResolved = true;
            try
            {
                var theme = EditorInterface.Singleton?.GetEditorTheme();
                if (theme != null && theme.HasFont("bold", "EditorFonts"))
                    _editorBoldFont = theme.GetFont("bold", "EditorFonts");
            }
            catch { /* no editor theme (headless/test) — stay regular weight */ }
            return _editorBoldFont;
        }

        /// <summary>Apply the editor bold font face to a <see cref="Label"/> (Unity's bold headers). No-op if unavailable.</summary>
        public static void ApplyBold(Label label)
        {
            var f = EditorBoldFont();
            if (f != null)
                label.AddThemeFontOverride("font", f);
        }

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
            ApplyBold(label);
        }

        /// <summary>Apply the 16px bold section-title look to a <see cref="Label"/>.</summary>
        public static void ApplySectionTitle(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSectionTitle);
            ApplyBold(label);
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
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeConfigPath); // small — paths sit quietly beside the bigger header
            label.AutowrapMode = TextServer.AutowrapMode.Off;
            label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
            label.ClipText = true;
        }

        /// <summary>Apply the 13px bold sub/timeline label look to a <see cref="Label"/>.</summary>
        public static void ApplySubLabel(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeSubLabel);
        }

        /// <summary>
        /// Build a 13px sub-label with a thin underline drawn as a BOTTOM BORDER (Unity's <c>.timeline-label</c>
        /// <c>border-bottom</c>), hugging just the text width. Unlike <see cref="TimelineLabel"/> (a VBox with a
        /// separate underline rule below the text — which lifts the text up off the row baseline), the border-box
        /// keeps the label at its natural height so it stays vertically aligned with a sibling on the same row (e.g.
        /// the right-aligned config path). Returned as a <see cref="PanelContainer"/> that shrinks to the text and
        /// centers vertically in its row.
        /// </summary>
        public static PanelContainer UnderlinedSubLabel(string name, string text)
        {
            var box = new StyleBoxFlat
            {
                BgColor = new Color(0, 0, 0, 0),
                DrawCenter = false,
                BorderColor = Rgb(DockTheme.Divider).Lightened(0.4f)
            };
            box.BorderWidthBottom = 1;
            box.ContentMarginLeft = 0;
            box.ContentMarginRight = 0;
            box.ContentMarginTop = 0;
            box.ContentMarginBottom = 2; // small gap so the rule sits just under the text, not jammed against it

            var panel = new PanelContainer
            {
                Name = name,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
                SizeFlagsVertical = Control.SizeFlags.ShrinkCenter
            };
            panel.AddThemeStyleboxOverride("panel", box);

            var label = new Label { Name = "Text", Text = text };
            // Unity's `.timeline-label` is 13px BOLD; render it bold + clearly larger (Godot's base font renders
            // smaller, so the underlined section labels — Skills / MCP / the connection timeline points — looked tiny).
            label.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeUnderlinedLabel);
            ApplyBold(label);
            panel.AddChild(label);
            return panel;
        }

        /// <summary>
        /// Update the text of an <see cref="UnderlinedSubLabel"/> (its inner "Text" <see cref="Label"/>), so the
        /// bottom-border underline re-hugs the new text width. Used to make the "Godot" timeline label carry the
        /// live connection status ("Godot" / "Godot: connecting..." / "Godot: connected"). No-op if the expected
        /// inner label is absent.
        /// </summary>
        public static void SetUnderlinedSubLabelText(PanelContainer underlinedLabel, string text)
        {
            if (underlinedLabel.GetChildCount() > 0 && underlinedLabel.GetChild(0) is Label inner)
                inner.Text = text;
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

        /// <summary>
        /// Skin <paramref name="button"/> as a COMPACT PRIMARY action — same cyan fill + dark text as
        /// <see cref="ApplyPrimaryButton"/> but at the compact (~20px) height of <see cref="ApplySecondaryButton"/>,
        /// matching Unity's <c>.btn-compact.btn-primary</c>. Used for the MCP "Configure" button so it sits inline with
        /// the compact "Remove" button (not the big full-width primary used by the alert frame).
        /// </summary>
        public static void ApplyCompactPrimaryButton(Button button)
        {
            var bg = Rgb(DockTheme.ButtonPrimary);
            ApplyCompactButtonBase(button, bg, bg.Lightened(0.1f));
            button.AddThemeColorOverride("font_color", Rgb(DockTheme.ButtonPrimaryText));
            button.AddThemeColorOverride("font_hover_color", Rgb(DockTheme.ButtonPrimaryText));
            button.AddThemeColorOverride("font_pressed_color", Rgb(DockTheme.ButtonPrimaryText));
        }

        /// <summary>Skin <paramref name="button"/> as a compact SECONDARY action (gray bg, 4px radius, ~20px tall).</summary>
        public static void ApplySecondaryButton(Button button)
        {
            var bg = Rgb(DockTheme.ButtonSecondary);
            ApplyCompactButtonBase(button, bg, bg.Lightened(0.1f));
        }

        /// <summary>Skin <paramref name="button"/> as an ALERT / Remove action (dark-red bg, brighter red hover).</summary>
        public static void ApplyAlertButton(Button button)
        {
            ApplyCompactButtonBase(button, Rgb(DockTheme.ButtonAlert), Rgb(DockTheme.ButtonAlertHover));
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

        static void ApplyButtonBackground(Button button, Color normal, Color hover, int cornerRadius,
                                          int hMargin = 8, int vMargin = 4)
        {
            StyleBoxFlat Make(Color bg)
            {
                var box = new StyleBoxFlat { BgColor = bg };
                box.SetCornerRadiusAll(cornerRadius);
                box.ContentMarginLeft = hMargin;
                box.ContentMarginRight = hMargin;
                box.ContentMarginTop = vMargin;
                box.ContentMarginBottom = vMargin;
                return box;
            }

            button.AddThemeStyleboxOverride("normal", Make(normal));
            button.AddThemeStyleboxOverride("hover", Make(hover));
            button.AddThemeStyleboxOverride("pressed", Make(normal.Darkened(0.1f)));
        }

        /// <summary>
        /// Shared skin for the dock's COMPACT buttons (Unity's <c>.btn-compact</c>: ~20px tall, 4px radius, small
        /// font, tight vertical padding). One place so Configure / Reconfigure / Remove / Generate all match. The
        /// caller sets the text colours after (so a compact-primary can override to dark-on-cyan).
        /// </summary>
        static void ApplyCompactButtonBase(Button button, Color normal, Color hover)
        {
            ApplyButtonBackground(button, normal, hover, DockTheme.ButtonSecondaryCornerRadius, hMargin: 8, vMargin: 0);
            button.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeCompactButton);
            button.CustomMinimumSize = new Vector2(0, DockTheme.ButtonSecondaryHeight);
        }

        // --- Icons (header logo + footer icon buttons; mirrors the agent-icon load pattern) -------------------

        /// <summary>
        /// Defensively load a texture under <see cref="DockTheme.IconsDir"/> by <paramref name="fileName"/>.
        /// Returns <c>null</c> when the asset is missing OR has not been imported yet (Godot generates the
        /// <c>.import</c> sidecar on first editor open, and <c>.import</c> files are gitignored) OR is not a
        /// <see cref="Texture2D"/> — so a missing/un-imported icon degrades to no-icon, never crashing the dock.
        /// Mirrors <c>AgentConfiguratorsPanel.LoadAgentIcon</c>.
        /// </summary>
        public static Texture2D? LoadIcon(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return null;

            var path = DockTheme.IconsDir + fileName;
            if (!ResourceLoader.Exists(path))
                return null;

            return ResourceLoader.Load(path) as Texture2D;
        }

        /// <summary>
        /// Apply an optional leading <paramref name="iconFileName"/> texture (20px, under
        /// <see cref="DockTheme.IconsDir"/>) and the <paramref name="text"/> label to <paramref name="button"/>
        /// via Godot's native <see cref="Button.Icon"/> slot. When an icon is present the text gets a small
        /// leading space to mimic the Unity icon-then-label gap; when the asset is missing / un-imported the
        /// button degrades to text-only.
        /// </summary>
        private static void ApplyIconAndText(Button button, string text, string? iconFileName)
        {
            var texture = string.IsNullOrEmpty(iconFileName) ? null : LoadIcon(iconFileName!);
            if (texture != null)
            {
                button.Icon = texture;
                button.AddThemeConstantOverride("icon_max_width", DockTheme.ButtonIconSize);
                button.Text = " " + text; // small gap after the icon, matching the Unity icon-then-label spacing.
            }
            else
            {
                button.Text = text;
            }
        }

        /// <summary>
        /// Build the header AI-cube logo as a square <see cref="TextureRect"/> (Unity's <c>imgLogoPivot</c>,
        /// 60px, scaled to fit, aspect-preserved). Returns <c>null</c> when the logo asset is missing /
        /// un-imported so the header silently omits it (the config column then fills the width). Editor-only.
        /// </summary>
        public static TextureRect? HeaderLogo()
        {
            var texture = LoadIcon(DockTheme.LogoFileName);
            if (texture == null)
                return null;

            return new TextureRect
            {
                Name = "HeaderLogo",
                Texture = texture,
                CustomMinimumSize = new Vector2(DockTheme.HeaderLogoSize, DockTheme.HeaderLogoSize),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkEnd
            };
        }

        /// <summary>
        /// Build a secondary "button with icon" (Unity's <c>.btn-secondary.btn-with-icon</c>): a flat gray
        /// bordered button whose content is an optional leading <paramref name="iconFileName"/> texture
        /// (20px, under <see cref="DockTheme.IconsDir"/>) followed by the <paramref name="text"/> label. When
        /// the icon asset is missing / un-imported the button degrades to text-only. <paramref name="onPressed"/>
        /// wires the click. The icon+label are applied via Godot's native <see cref="Button.Icon"/> slot
        /// (icon_max_width + a leading space on the text), not a separate container child.
        /// </summary>
        public static Button IconButton(string name, string text, string? iconFileName, System.Action onPressed)
        {
            var button = new DockActionButton
            {
                Name = name,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };
            ApplyOpenButton(button);
            button.AddThemeColorOverride("font_color", Rgb(DockTheme.ButtonSecondaryText));

            ApplyIconAndText(button, text, iconFileName);

            button.BindPressed(onPressed);
            return button;
        }

        /// <summary>
        /// Build the gold "GitHub Star" button (Unity's <c>.btn-golden.btn-with-icon</c>): a dark
        /// <see cref="DockTheme.ButtonGoldenBackground"/> fill, a <see cref="DockTheme.ButtonGoldenBorder"/>
        /// border, gold <see cref="DockTheme.ButtonGoldenText"/> text, and the gold star
        /// (<see cref="DockTheme.StarIconFileName"/>) as a leading icon. Degrades to text-only when the star
        /// asset is missing / un-imported. <paramref name="onPressed"/> wires the click.
        /// </summary>
        public static Button GoldenButton(string name, string text, string? iconFileName, System.Action onPressed)
        {
            var button = new DockActionButton
            {
                Name = name,
                MouseDefaultCursorShape = Control.CursorShape.PointingHand
            };

            var normal = Rgb(DockTheme.ButtonGoldenBackground);
            var hover = Rgb(DockTheme.ButtonGoldenHover);
            var border = Rgb(DockTheme.ButtonGoldenBorder);
            ApplyBorderedButtonBackground(button, normal, hover, border, DockTheme.ButtonOpenCornerRadius);
            button.CustomMinimumSize = new Vector2(0, DockTheme.ButtonOpenHeight);

            var textColor = Rgb(DockTheme.ButtonGoldenText);
            button.AddThemeColorOverride("font_color", textColor);
            button.AddThemeColorOverride("font_hover_color", textColor.Lightened(0.1f));
            button.AddThemeColorOverride("font_pressed_color", textColor.Lightened(0.2f));

            ApplyIconAndText(button, text, iconFileName);

            button.BindPressed(onPressed);
            return button;
        }

        // --- Links ---------------------------------------------------------------------------------------------

        /// <summary>
        /// Build a flat, link-coloured <see cref="Button"/> that opens <paramref name="url"/> via
        /// <see cref="OS.ShellOpen"/>. Flat (no button chrome) + light-blue text, mimicking an inline hyperlink.
        /// </summary>
        public static Button LinkButton(string name, string text, string url)
        {
            var button = new DockLinkButton
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
            button.AddThemeFontSizeOverride("font_size", DockTheme.FontSizeLink); // smaller than the body (Download / Tutorial links)
            // Object+method Callable: the button opens its own stored URL via its OnPressed instance method.
            button.BindUrl(url);
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
            // Unity's .alert-frame padding: 10px (top/bottom) 12px (left/right).
            box.ContentMarginLeft = 12;
            box.ContentMarginRight = 12;
            box.ContentMarginTop = 10;
            box.ContentMarginBottom = 10;

            var panel = new PanelContainer { Name = name, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            panel.AddThemeStyleboxOverride("panel", box);

            var col = new VBoxContainer { Name = "AlertContent" };
            col.AddThemeConstantOverride("separation", 6);
            panel.AddChild(col);

            var titleLabel = new Label { Name = "AlertTitle", Text = title };
            titleLabel.AddThemeFontSizeOverride("font_size", 16); // bold amber title — enlarged for readability
            ApplyBold(titleLabel);
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

            // Full-width, centered cyan button — Unity's alert button is a `.btn-primary` that fills the frame
            // (`alertButton` spans the panel), not a left-hugging compact button.
            var button = new DockActionButton { Name = "AlertButton", Text = buttonText };
            ApplyPrimaryButton(button);
            button.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            button.BindPressed(onPressed);
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
        /// Metadata key used to stash the AUTHORITATIVE selected index on a segmented control's track node.
        /// The native <c>Button.ButtonPressed</c> flags are NOT a reliable source of truth here: the segments
        /// are independent toggle buttons with no shared <c>ButtonGroup</c>, so Godot can leave two segments
        /// pressed at once (it flips the clicked button's <c>ButtonPressed</c> BEFORE emitting <c>Pressed</c>
        /// and never un-presses the previous one). Tracking the selection on the track itself keeps the
        /// builder's per-segment closures and the static <see cref="SetSegmentedSelection"/> reading/writing
        /// the same value, so the "already-selected" no-op check never misfires (issue #107).
        /// </summary>
        const string SegmentedSelectionMetaKey = "godot_mcp_segmented_selected_index";

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
        /// re-renders selection via <see cref="SetSegmentedSelection"/> after persisting. On a real change the
        /// builder advances the authoritative track meta + visuals OPTIMISTICALLY before invoking
        /// <paramref name="onSelected"/>, so the selection cannot drift even if a caller skips the round-trip;
        /// a caller that DOES call <see cref="SetSegmentedSelection"/> simply re-asserts the same index.
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
            // Seed the authoritative selection on the track itself (see SegmentedSelectionMetaKey). The
            // native ButtonPressed flags are not trustworthy without a ButtonGroup, so we never probe them.
            track.SetMeta(SegmentedSelectionMetaKey, clamped);
            for (int i = 0; i < options.Count; i++)
            {
                var segment = new DockSegmentButton
                {
                    Name = "Segment" + i,
                    Text = options[i],
                    ToggleMode = true,
                    // Flat=false so the overridden "normal" stylebox actually DRAWS — a flat button suppresses its
                    // background, which is why the ACTIVE segment's highlight pill never showed. Unselected segments
                    // override to a transparent box, so only the selected one paints its pill.
                    Flat = false,
                    CustomMinimumSize = new Vector2(DockTheme.SegmentMinWidth, 0)
                };
                // Carry the per-segment click state on the segment instance (index + the track it reports into +
                // the caller's onSelected) so the click handler is a real INSTANCE METHOD connected via an
                // object+method Callable — no captured-lambda delegate enters the ManagedCallable registry.
                segment.Bind(i, track, onSelected);
                // Set the pressed state WITHOUT emitting Pressed/Toggled so initial build never re-enters
                // the click handler.
                segment.SetPressedNoSignal(SegmentedControlModel.IsSelected(i, clamped));
                segment.AddThemeFontSizeOverride("font_size", DockTheme.SegmentFontSize);
                ApplySegmentStyle(segment, SegmentedControlModel.IsSelected(i, clamped));

                segment.Connect(BaseButton.SignalName.Pressed, new Callable(segment, DockSegmentButton.MethodName.OnPressed));
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
            // Record the authoritative selection on the track so the click handler's no-op check stays correct.
            track.SetMeta(SegmentedSelectionMetaKey, clamped);
            for (int i = 0; i < track.GetChildCount(); i++)
            {
                if (track.GetChild(i) is Button segment)
                {
                    var isSel = SegmentedControlModel.IsSelected(i, clamped);
                    // SetPressedNoSignal so restyling never re-enters the Pressed handler (avoids feedback loops).
                    segment.SetPressedNoSignal(isSel);
                    ApplySegmentStyle(segment, isSel);
                }
            }
        }

        /// <summary>
        /// Read the AUTHORITATIVE selected index for a segmented control's track from the metadata stashed by
        /// <see cref="SegmentedControl"/> / <see cref="SetSegmentedSelection"/>. Does NOT probe the segments'
        /// native <c>ButtonPressed</c> flags — those are unreliable for a group of independent toggle buttons
        /// with no <c>ButtonGroup</c> (issue #107). Falls back to <c>0</c> when the meta is absent.
        /// </summary>
        internal static int GetSegmentedSelection(HBoxContainer track)
        {
            if (track.HasMeta(SegmentedSelectionMetaKey))
                return (int)track.GetMeta(SegmentedSelectionMetaKey);
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
                box.ContentMarginTop = 1;
                box.ContentMarginBottom = 1;
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
                // Unselected: TRANSPARENT fill (the track shows through, so only the selected segment gets the dark
                // highlight) but the SAME padding as the selected segment — Unity pads every segment equally
                // (`padding: 2px 8px`) and only the highlight background differs, so the segments don't jump size
                // when selection changes. (A StyleBoxEmpty here gave the unselected segment zero padding → uneven.)
                StyleBoxFlat Transparent()
                {
                    var box = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0), DrawCenter = false };
                    box.SetCornerRadiusAll(DockTheme.SegmentSelectedCornerRadius);
                    box.ContentMarginLeft = 8;
                    box.ContentMarginRight = 8;
                    box.ContentMarginTop = 1;
                    box.ContentMarginBottom = 1;
                    return box;
                }
                segment.AddThemeStyleboxOverride("normal", Transparent());
                segment.AddThemeStyleboxOverride("hover", Transparent());
                segment.AddThemeStyleboxOverride("pressed", Transparent());

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

            var toggle = new DockFoldoutToggle
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

            // Object+method Callable on the toggle instance: it carries the title + the content it reveals and
            // flips them in its own OnToggled instance method (no captured-lambda delegate connection).
            toggle.Bind(title, content);
            toggle.Connect(BaseButton.SignalName.Toggled, new Callable(toggle, DockFoldoutToggle.MethodName.OnToggled));

            return (container, content);
        }
    }
}
#endif
