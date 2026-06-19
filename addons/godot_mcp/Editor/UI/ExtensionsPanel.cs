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
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Extensions;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The dock's "Extensions" section — the Godot <see cref="Control"/> analog of Unity-MCP's
    /// <c>ExtensionPanel</c> / <c>MainWindowEditor.Extensions</c>. A <see cref="VBoxContainer"/> the
    /// <see cref="GodotMcpDock"/> inserts as a card BETWEEN the Skills card and the support footer. It lists every
    /// <see cref="GodotExtensionRegistry"/> entry as an <see cref="ExtensionRow"/> (name + description + an
    /// Install/Update/Installed button); when the registry is EMPTY (no extension package ships yet) it renders an
    /// honest "coming soon" placeholder + a docs link instead.
    ///
    /// <para>
    /// Install distribution is a NuGet <c>&lt;PackageReference&gt;</c> in the CONSUMER's game <c>.csproj</c>
    /// (Godot compiles every <c>.cs</c> under the project into one assembly): the button runs the pure-managed
    /// <see cref="ExtensionInstaller"/> (planner + the <see cref="IConsumerProjectFile"/> IO seam) and, since Godot
    /// has no programmatic restore, surfaces a "rebuild solutions" notice on a successful write. Install STATE is
    /// read SYNCHRONOUSLY from the consumer <c>.csproj</c> via the <see cref="InstalledStateDetector"/> — this panel
    /// holds NO live connection-event subscriptions, so it neither needs nor adds the <c>_EnterTree</c> re-subscribe
    /// dance the connection-bound panels use (it cannot regress the #56 FeaturesPanel re-subscription because it
    /// never subscribes to anything).
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI nodes. ALL the install/detect/plan LOGIC lives in
    /// the pure-managed <c>Extensions/</c> types (CI-unit-tested); this class is verified via the headless Godot
    /// smoke (test.md Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class ExtensionsPanel : VBoxContainer
    {
        readonly IConsumerProjectFile _projectFile;

        VBoxContainer? _body;
        Label? _statusLabel;
        readonly List<ExtensionRow> _rows = new();

        /// <summary>Construct with the default editor <see cref="ConsumerProjectFile"/> (globalizes <c>res://</c>, locates the game <c>.csproj</c>).</summary>
        public ExtensionsPanel() : this(new ConsumerProjectFile())
        {
        }

        /// <summary>
        /// Construct with an injected <paramref name="projectFile"/> — the production path uses the real
        /// <see cref="ConsumerProjectFile"/>; the seam exists so the install flow stays testable in the pure layer.
        /// </summary>
        public ExtensionsPanel(IConsumerProjectFile projectFile)
        {
            _projectFile = projectFile;
            Name = "ExtensionsPanel";
            BuildUi();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 6);

            var header = new Label { Name = "ExtensionsHeader", Text = ExtensionsPanelText.Header };
            DockStyle.ApplyHeader(header);
            AddChild(header);

            _body = new VBoxContainer { Name = "ExtensionsBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _body.AddThemeConstantOverride("separation", 6);
            AddChild(_body);

            // Status line (muted; turns amber on a failed install). Lives under the body so it is visible in both
            // the placeholder and the rows layouts.
            _statusLabel = new Label { Name = "ExtensionsStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
            DockStyle.ApplyDescription(_statusLabel);
            AddChild(_statusLabel);

            RebuildBody();
        }

        /// <summary>
        /// Rebuild the body for the current registry + consumer-project state. EMPTY registry → the "coming soon"
        /// placeholder + docs link. Non-empty → one <see cref="ExtensionRow"/> per descriptor, each seeded with its
        /// install state read synchronously from the consumer <c>.csproj</c>. Clears the previous controls first so a
        /// refresh starts empty.
        /// </summary>
        void RebuildBody()
        {
            if (_body == null)
                return;

            foreach (var child in _body.GetChildren())
            {
                _body.RemoveChild(child);
                child.QueueFree();
            }
            _rows.Clear();

            if (GodotExtensionRegistry.IsEmpty)
            {
                BuildPlaceholder(_body);
                return;
            }

            // Read the consumer .csproj ONCE and derive every row's state from the same parsed map (synchronous).
            var csproj = _projectFile.Exists ? _projectFile.Read() : null;
            var installed = InstalledStateDetector.ParsePackageReferences(csproj);

            foreach (var descriptor in GodotExtensionRegistry.All)
            {
                var row = new ExtensionRow(descriptor, OnRowAction);
                _body.AddChild(DockStyle.RowCard(row, $"{descriptor.PackageId}Card", enabled: true));
                row.ShowState(InstalledStateDetector.StateFor(descriptor, installed));
                _rows.Add(row);
            }

            // When there is no consumer .csproj to install into, disable the actions + explain why.
            if (!_projectFile.Exists)
            {
                foreach (var row in _rows)
                    row.ShowChecking();
                SetStatus(ExtensionsPanelText.NoProjectFileNotice, error: true);
            }
        }

        /// <summary>Render the honest "coming soon" placeholder + a docs / template-repo link (registry empty).</summary>
        void BuildPlaceholder(Container parent)
        {
            var comingSoon = new Label
            {
                Name = "ComingSoon",
                Text = ExtensionsPanelText.ComingSoonText,
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            DockStyle.ApplyDescription(comingSoon);
            parent.AddChild(comingSoon);

            parent.AddChild(DockStyle.LinkRow("ExtensionsDocsLinkRow", new[]
            {
                ("ExtensionsDocsLink", ExtensionsPanelText.DocsLinkText, ExtensionsPanelText.DocsUrl)
            }));
        }

        /// <summary>
        /// Install / update the row's extension: re-read the consumer <c>.csproj</c>, run the pure
        /// <see cref="ExtensionInstaller"/> (plan → write), surface the result message, and re-seed every row's state
        /// from the (now possibly changed) <c>.csproj</c>. A successful write tells the user to rebuild solutions
        /// (Godot has no programmatic restore). Failures never throw out of the handler — they show in the status.
        /// </summary>
        void OnRowAction(GodotExtensionDescriptor descriptor)
        {
            // Optimistic "Checking…" while we re-read + write.
            foreach (var row in _rows)
            {
                if (row.Descriptor.PackageId == descriptor.PackageId)
                    row.ShowChecking();
            }

            var result = ExtensionInstaller.Install(descriptor, _projectFile);
            var error = result.Outcome == ExtensionInstallOutcome.Failed
                || result.Outcome == ExtensionInstallOutcome.NoProjectFile;
            SetStatus(result.Message, error);

            // Re-seed every row from the current .csproj so the buttons reflect the new state.
            RefreshStates();
        }

        /// <summary>Re-read the consumer <c>.csproj</c> and re-apply each row's install state (no full rebuild — rows persist).</summary>
        void RefreshStates()
        {
            var csproj = _projectFile.Exists ? _projectFile.Read() : null;
            var installed = InstalledStateDetector.ParsePackageReferences(csproj);
            foreach (var row in _rows)
                row.ShowState(InstalledStateDetector.StateFor(row.Descriptor, installed));
        }

        void SetStatus(string text, bool error)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.Text = text;
            _statusLabel.Visible = !string.IsNullOrEmpty(text);
            _statusLabel.AddThemeColorOverride(
                "font_color",
                error ? DockStyle.Rgb(DockTheme.WarningText) : DockStyle.Rgb(DockTheme.ColorDescriptionMuted));
        }

        /// <summary>
        /// Re-render the section from current state — rebuilds the body so a registry change (a future extension being
        /// added) or a consumer-<c>.csproj</c> change outside the dock is reflected. Safe to call any number of times.
        /// Forwarded from <see cref="GodotMcpDock.Refresh"/>.
        /// </summary>
        public void Refresh() => RebuildBody();
    }
}
#endif
