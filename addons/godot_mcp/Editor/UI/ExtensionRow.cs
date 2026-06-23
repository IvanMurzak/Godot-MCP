/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Extensions;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// One row of the dock's <see cref="ExtensionsPanel"/> for a single <see cref="GodotExtensionDescriptor"/> —
    /// the Godot <see cref="Control"/> analog of Unity-MCP's <c>ExtensionPanel</c> row. Styled like
    /// <see cref="FeatureRow"/>'s row-top layout (flex-start, space-between): a LEFT column with the extension name
    /// as a 16px section title over a muted, wrapping description, and a RIGHT action button whose label + skin
    /// follow the install state — "Install" / "Update" (primary cyan), "Installed" (disabled), or "Checking…"
    /// (disabled, transient). Editor-only (<c>#if TOOLS</c>): verified via the headless Godot smoke (test.md
    /// Suite 3). Holds NO live connection-event subscriptions — its state is read synchronously by the panel.
    /// </summary>
    [Tool]
    public partial class ExtensionRow : HBoxContainer
    {
        /// <summary>The extension descriptor this row represents.</summary>
        public GodotExtensionDescriptor Descriptor { get; }

        readonly Action<GodotExtensionDescriptor> _onAction;

        Button _actionButton = null!;

        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public ExtensionRow() { }

        public ExtensionRow(GodotExtensionDescriptor descriptor, Action<GodotExtensionDescriptor> onAction)
        {
            Descriptor = descriptor;
            _onAction = onAction;
            Name = $"{descriptor.PackageId}Row";
            BuildUi();
        }

        void BuildUi()
        {
            AddThemeConstantOverride("separation", 8);
            Alignment = AlignmentMode.Begin;

            // --- LEFT: name (section title) over a muted, wrapping description (+ optional tool count). ---
            var leftColumn = new VBoxContainer
            {
                Name = "InfoColumn",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            leftColumn.AddThemeConstantOverride("separation", 2);
            AddChild(leftColumn);

            var nameLabel = new Label { Name = "NameLabel", Text = Descriptor.Name };
            DockStyle.ApplySectionTitle(nameLabel);
            leftColumn.AddChild(nameLabel);

            var descriptionLabel = new Label
            {
                Name = "DescriptionLabel",
                Text = Descriptor.Description,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            DockStyle.ApplyDescription(descriptionLabel);
            leftColumn.AddChild(descriptionLabel);

            // --- RIGHT: the install/update/installed action button (state set via ShowState). ---
            _actionButton = new Button
            {
                Name = "ActionButton",
                Text = "Checking…",
                Disabled = true,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
            DockStyle.ApplyPrimaryButton(_actionButton);
            // Object+method Callable to this row's own instance method (no captured-lambda delegate connection).
            DockStyle.ConnectPressed(_actionButton, this, MethodName.OnActionPressed);
            AddChild(_actionButton);
        }

        /// <summary>Action-button <c>pressed</c> handler (object+method Callable): raise the panel's install/update callback.</summary>
        public void OnActionPressed() => _onAction(Descriptor);

        /// <summary>
        /// Reflect <paramref name="state"/> in the action button: NotInstalled → enabled "Install",
        /// UpdateAvailable → enabled "Update", Installed → disabled "Installed". Primary (cyan) skin for the
        /// actionable states; the disabled "Installed" reads as inert.
        /// </summary>
        public void ShowState(ExtensionInstallState state)
        {
            switch (state)
            {
                case ExtensionInstallState.NotInstalled:
                    _actionButton.Text = "Install";
                    _actionButton.Disabled = false;
                    break;
                case ExtensionInstallState.UpdateAvailable:
                    _actionButton.Text = "Update";
                    _actionButton.Disabled = false;
                    break;
                default:
                    _actionButton.Text = "Installed";
                    _actionButton.Disabled = true;
                    break;
            }
        }

        /// <summary>Show the transient "Checking…" (disabled) state while the consumer <c>.csproj</c> is re-read.</summary>
        public void ShowChecking()
        {
            _actionButton.Text = "Checking…";
            _actionButton.Disabled = true;
        }
    }
}
#endif
