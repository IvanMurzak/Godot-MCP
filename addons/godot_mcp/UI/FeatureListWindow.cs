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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// A nested editor <see cref="Window"/> listing every item of one <see cref="GodotMcpFeatureKind"/>, with a
    /// live TEXT filter + an All/Enabled/Disabled STATUS filter + a "Filtered: X, Total: Y" stat + an empty-state
    /// + per-row "card" styling — the Godot analog of Unity-MCP's <c>McpListWindowBase</c> (and its
    /// tools/prompts/resources subclasses), collapsed to ONE reusable class parameterized by kind. Each row is a
    /// tinted rounded card (soft green when enabled, soft red when disabled) showing the title + id + the
    /// kind-specific metadata (Tools: "~N tokens" + an "Input arguments (N)" foldout; Prompts: "Role: X" + an
    /// "Arguments (N)" foldout; Resources: the URI + "MimeType: …") + a Description foldout + a per-row
    /// enable/disable <see cref="CheckButton"/>. Toggling pushes the change to the live manager AND persists it via
    /// <see cref="GodotMcpConnection.SetFeatureEnabled"/> (the enable-map in <see cref="GodotMcpConfig"/>), then
    /// re-filters so a toggle under a non-All status hides/shows the row. A "Close" button (and the window-manager
    /// close request) frees the window — no leaks.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it builds live Godot UI <see cref="Node"/>s, so it is verified via the
    /// headless Godot smoke (test.md Suite 3), not the plain-xUnit host. The filter chain + stat formatting it
    /// drives is pure-managed (<see cref="FeatureFilter"/>) and unit-tested separately.
    /// </para>
    /// </summary>
    [Tool]
    public partial class FeatureListWindow : Window
    {
        readonly GodotMcpConnection _connection;
        readonly GodotMcpFeatureKind _kind;

        LineEdit _searchField = null!;
        OptionButton _statusOption = null!;
        Label _statsLabel = null!;
        VBoxContainer _list = null!;
        Label _emptyLabel = null!;

        /// <summary>The full, unfiltered item set for this kind, captured on (re)populate; the filter runs over it.</summary>
        IReadOnlyList<FeatureRowItem> _allItems = System.Array.Empty<FeatureRowItem>();

        public FeatureListWindow(GodotMcpConnection connection, GodotMcpFeatureKind kind)
        {
            _connection = connection;
            _kind = kind;
            Name = $"{FeaturesPanelView.Title(kind)}Window";
            Title = $"MCP {FeaturesPanelView.Title(kind)}";
            Size = new Vector2I(480, 560);
            Unresizable = false;

            // Free the window when the OS/editor close button (the X) is pressed.
            CloseRequested += OnClosePressed;

            BuildUi();
            ReloadItems();
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

            var header = new Label { Name = "Header", Text = $"MCP {FeaturesPanelView.Title(_kind)}" };
            DockStyle.ApplyHeader(header);
            root.AddChild(header);

            root.AddChild(BuildFilterBar());

            var scroll = new ScrollContainer
            {
                Name = "Scroll",
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
            };
            root.AddChild(scroll);

            _list = new VBoxContainer
            {
                Name = "List",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            _list.AddThemeConstantOverride("separation", 6);
            scroll.AddChild(_list);

            _emptyLabel = new Label
            {
                Name = "EmptyLabel",
                Text = $"No {FeaturesPanelView.Title(_kind).ToLowerInvariant()} by current filter.",
                Visible = false,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            DockStyle.ApplyDescription(_emptyLabel);
            root.AddChild(_emptyLabel);

            var closeButton = new Button { Name = "CloseButton", Text = "Close" };
            closeButton.Pressed += OnClosePressed;
            root.AddChild(closeButton);
        }

        /// <summary>Build the search field + status dropdown + right-aligned "Filtered: X, Total: Y" stat row.</summary>
        Control BuildFilterBar()
        {
            var bar = new HBoxContainer { Name = "FilterBar", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            bar.AddThemeConstantOverride("separation", 6);

            _searchField = new LineEdit
            {
                Name = "Search",
                PlaceholderText = "Filter…",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            DockStyle.ApplyInput(_searchField);
            // Live (no debounce): every keystroke re-filters.
            _searchField.TextChanged += _ => RebuildRows();
            bar.AddChild(_searchField);

            _statusOption = new OptionButton { Name = "Status" };
            // Order matches the FeatureStatusFilter enum so the selected index IS the enum value.
            _statusOption.AddItem("All", (int)FeatureStatusFilter.All);
            _statusOption.AddItem("Enabled", (int)FeatureStatusFilter.Enabled);
            _statusOption.AddItem("Disabled", (int)FeatureStatusFilter.Disabled);
            _statusOption.Select((int)FeatureStatusFilter.All);
            DockStyle.ApplyOptionButton(_statusOption);
            _statusOption.ItemSelected += _ => RebuildRows();
            bar.AddChild(_statusOption);

            _statsLabel = new Label
            {
                Name = "Stats",
                Text = FeatureFilter.FormatStats(0, 0),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockStyle.ApplyRowId(_statsLabel);
            bar.AddChild(_statsLabel);

            return bar;
        }

        /// <summary>Re-read the live item set off the connection, then rebuild the filtered rows.</summary>
        void ReloadItems()
        {
            _allItems = _connection.GetFeatureItems(_kind);
            RebuildRows();
        }

        /// <summary>The current status filter selected in the dropdown.</summary>
        FeatureStatusFilter CurrentStatus()
        {
            int id = _statusOption.GetSelectedId();
            return id >= 0 ? (FeatureStatusFilter)id : FeatureStatusFilter.All;
        }

        /// <summary>
        /// Run the pure-managed <see cref="FeatureFilter.Apply"/> over the cached item set with the current text +
        /// status, then rebuild the list rows. Updates the "Filtered: X, Total: Y" stat and toggles the
        /// empty-state when nothing matches.
        /// </summary>
        void RebuildRows()
        {
            var filtered = FeatureFilter.Apply(_allItems, CurrentStatus(), _searchField.Text, _kind);

            foreach (var child in _list.GetChildren())
                ((Node)child).QueueFree();

            foreach (var item in filtered)
                _list.AddChild(BuildItemRow(item));

            _statsLabel.Text = FeatureFilter.FormatStats(filtered.Count, _allItems.Count);

            bool empty = filtered.Count == 0;
            _emptyLabel.Visible = empty;
            _list.Visible = !empty;
        }

        /// <summary>Build one styled card row for a single feature item.</summary>
        Control BuildItemRow(FeatureRowItem item)
        {
            var content = new VBoxContainer { Name = "Content", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            content.AddThemeConstantOverride("separation", 2);

            // Header line: title (16px bold) on the left, enable/disable toggle on the right.
            var headerLine = new HBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            headerLine.AddThemeConstantOverride("separation", 8);

            var titleLabel = new Label
            {
                Name = "Title",
                Text = item.DisplayTitle,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
            };
            DockStyle.ApplyRowTitle(titleLabel);
            headerLine.AddChild(titleLabel);

            var toggle = new CheckButton { Name = "Toggle", ButtonPressed = item.Enabled };
            string itemName = item.Name;
            toggle.Toggled += pressed => OnItemToggled(itemName, pressed);
            headerLine.AddChild(toggle);
            content.AddChild(headerLine);

            // Id (muted), shown under the title when it differs from the title.
            if (item.DisplayTitle != item.Name)
            {
                var idLabel = new Label { Name = "Id", Text = item.Name };
                DockStyle.ApplyRowId(idLabel);
                content.AddChild(idLabel);
            }

            AddKindMetadata(content, item);

            // Description foldout (only when a description exists).
            if (!string.IsNullOrEmpty(item.Description))
            {
                var (foldout, body) = DockStyle.Foldout("Description");
                var descLabel = new Label
                {
                    Name = "DescriptionText",
                    Text = item.Description,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                };
                DockStyle.ApplyDescription(descLabel);
                body.AddChild(descLabel);
                content.AddChild(foldout);
            }

            return DockStyle.RowCard(content, $"Item_{item.Name}", item.Enabled);
        }

        /// <summary>Append the kind-specific metadata labels / argument foldouts to a row's content box.</summary>
        void AddKindMetadata(VBoxContainer content, FeatureRowItem item)
        {
            switch (_kind)
            {
                case GodotMcpFeatureKind.Tools:
                    var tokenLabel = new Label
                    {
                        Name = "Tokens",
                        Text = FeaturesPanelView.TokenLabel(item.TokenCount)
                    };
                    DockStyle.ApplyMetadataColor(tokenLabel, DockTheme.RowIdMuted);
                    content.AddChild(tokenLabel);
                    AddArgumentFoldout(content, "Input arguments", item.Inputs);
                    break;

                case GodotMcpFeatureKind.Prompts:
                    if (!string.IsNullOrEmpty(item.Role))
                    {
                        var roleLabel = new Label { Name = "Role", Text = $"Role: {item.Role}" };
                        DockStyle.ApplyMetadataColor(roleLabel, DockTheme.RoleLabel);
                        content.AddChild(roleLabel);
                    }
                    AddArgumentFoldout(content, "Arguments", item.Arguments);
                    break;

                default: // Resources
                    if (!string.IsNullOrEmpty(item.Uri))
                    {
                        var uriLabel = new Label
                        {
                            Name = "Uri",
                            Text = item.Uri,
                            AutowrapMode = TextServer.AutowrapMode.WordSmart
                        };
                        DockStyle.ApplyMetadataColor(uriLabel, DockTheme.ResourceUri);
                        content.AddChild(uriLabel);
                    }
                    if (!string.IsNullOrEmpty(item.MimeType))
                    {
                        var mimeLabel = new Label { Name = "MimeType", Text = $"MimeType: {item.MimeType}" };
                        DockStyle.ApplyMetadataColor(mimeLabel, DockTheme.ResourceMimeType);
                        content.AddChild(mimeLabel);
                    }
                    break;
            }
        }

        /// <summary>Add a "&lt;Title&gt; (N)" foldout listing each argument's name + optional description. No-op when empty.</summary>
        void AddArgumentFoldout(VBoxContainer content, string title, IReadOnlyList<FeatureArgument> arguments)
        {
            if (arguments.Count == 0)
                return;

            var (foldout, body) = DockStyle.Foldout($"{title} ({arguments.Count})");
            foreach (var arg in arguments)
            {
                var nameLabel = new Label { Text = arg.Name };
                DockStyle.ApplySubLabel(nameLabel);
                body.AddChild(nameLabel);

                if (!string.IsNullOrEmpty(arg.Description))
                {
                    var descLabel = new Label
                    {
                        Text = arg.Description,
                        AutowrapMode = TextServer.AutowrapMode.WordSmart
                    };
                    DockStyle.ApplyDescription(descLabel);
                    body.AddChild(descLabel);
                }
            }
            content.AddChild(foldout);
        }

        void OnItemToggled(string name, bool enabled)
        {
            // Push to the live manager AND persist into the enable-map (survives restart).
            _connection.SetFeatureEnabled(_kind, name, enabled);

            // Reflect the new enabled-state in the cached item set so a re-filter sees it, then re-filter — under a
            // non-All status this hides/shows the just-toggled row; the row tint also updates on rebuild.
            for (int i = 0; i < _allItems.Count; i++)
            {
                if (_allItems[i].Name == name)
                {
                    var updated = new List<FeatureRowItem>(_allItems);
                    updated[i] = updated[i] with { Enabled = enabled };
                    _allItems = updated;
                    break;
                }
            }
            RebuildRows();
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
