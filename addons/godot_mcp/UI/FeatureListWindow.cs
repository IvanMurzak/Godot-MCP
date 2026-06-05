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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// A nested editor <see cref="Window"/> listing every item of one <see cref="GodotMcpFeatureKind"/> with a
    /// per-item enable/disable <see cref="CheckBox"/> — the Godot analog of Unity-MCP's <c>McpToolsWindow</c>
    /// (and its prompts/resources siblings), collapsed to ONE reusable class parameterized by kind. Each row
    /// shows the item name + description and a checkbox reflecting the live enabled-state; toggling pushes the
    /// change to the live manager AND persists it via <see cref="GodotMcpConnection.SetFeatureEnabled"/> (the
    /// enable-map in <see cref="GodotMcpConfig"/>). A "Close" button (and the window-manager close request)
    /// frees the window — no leaks.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s, so it is verified via the
    /// headless Godot smoke (test.md Suite 3), not the plain-xUnit host. The enable-map merge/persist logic it
    /// drives is pure-managed (<see cref="GodotMcpFeatureStateMerge"/>) and unit-tested separately.
    /// </para>
    /// </summary>
    [Tool]
    public partial class FeatureListWindow : Window
    {
        readonly GodotMcpConnection _connection;
        readonly GodotMcpFeatureKind _kind;

        VBoxContainer _list = null!;
        Label _emptyLabel = null!;

        public FeatureListWindow(GodotMcpConnection connection, GodotMcpFeatureKind kind)
        {
            _connection = connection;
            _kind = kind;
            Name = $"{FeaturesPanelView.Title(kind)}Window";
            Title = $"MCP {FeaturesPanelView.Title(kind)}";
            Size = new Vector2I(420, 480);
            Unresizable = false;

            // Free the window when the OS/editor close button (the X) is pressed.
            CloseRequested += OnClosePressed;

            BuildUi();
            PopulateList();
        }

        void BuildUi()
        {
            var margin = new MarginContainer { Name = "Margin" };
            margin.AddThemeConstantOverride("margin_left", 8);
            margin.AddThemeConstantOverride("margin_right", 8);
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);
            margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            AddChild(margin);

            var root = new VBoxContainer { Name = "Root" };
            root.AddThemeConstantOverride("separation", 6);
            margin.AddChild(root);

            var scroll = new ScrollContainer
            {
                Name = "Scroll",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            root.AddChild(scroll);

            _list = new VBoxContainer
            {
                Name = "List",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _list.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(_list);

            _emptyLabel = new Label
            {
                Name = "EmptyLabel",
                Text = "No items (connect to populate the registry).",
                Visible = false
            };
            _list.AddChild(_emptyLabel);

            var closeButton = new Button { Name = "CloseButton", Text = "Close" };
            closeButton.Pressed += OnClosePressed;
            root.AddChild(closeButton);
        }

        /// <summary>
        /// Build one toggle row per live item of this kind. Reads items off the connection (name + description
        /// + current enabled). When the registry is empty (no connection yet), shows the empty placeholder.
        /// </summary>
        void PopulateList()
        {
            var items = _connection.GetFeatureItems(_kind);
            _emptyLabel.Visible = items.Count == 0;

            foreach (var item in items)
                _list.AddChild(BuildItemRow(item.Name, item.Description, item.Enabled));
        }

        Control BuildItemRow(string name, string? description, bool enabled)
        {
            var row = new VBoxContainer { Name = $"Item_{name}" };
            row.AddThemeConstantOverride("separation", 1);

            var checkBox = new CheckBox
            {
                Name = "Toggle",
                Text = name,
                ButtonPressed = enabled
            };
            // Capture the item name for the toggle callback; persist + push-live through the connection.
            string itemName = name;
            checkBox.Toggled += pressed => OnItemToggled(itemName, pressed);
            row.AddChild(checkBox);

            if (!string.IsNullOrEmpty(description))
            {
                var descLabel = new Label
                {
                    Name = "Description",
                    Text = description,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                descLabel.AddThemeColorOverride("font_color", new Color(0.65f, 0.65f, 0.65f));
                row.AddChild(descLabel);
            }

            return row;
        }

        void OnItemToggled(string name, bool enabled)
        {
            // Push to the live manager AND persist into the enable-map (survives restart).
            _connection.SetFeatureEnabled(_kind, name, enabled);
        }

        /// <summary>Pop the window up centred and visible. Called by the panel after parenting it into the tree.</summary>
        public void PopupCenteredAndShow()
        {
            PopupCentered(Size);
        }

        void OnClosePressed()
        {
            // QueueFree frees the window and all rows next idle frame — no leaks.
            QueueFree();
        }
    }
}
#endif
