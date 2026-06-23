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
using com.IvanMurzak.Godot.MCP.UI.Agents;
using Godot;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The dock's "AI agent" section — a THIN Godot <see cref="Control"/> adapter over the shared
    /// engine-agnostic <c>com.IvanMurzak.McpPlugin.AgentConfig</c> module (the Godot analog of Unity-MCP's
    /// <c>AiAgentConfiguratorView</c>). Post-#142, Godot retired its local configurator copy
    /// (<c>GodotAgentConfigurator</c> + <c>Impl/*</c> + <c>AgentConfigJson</c> + <c>AgentConfigPaths</c> +
    /// <c>AgentAlertView</c>): the shared library now owns ALL configurator logic — config-file building,
    /// detection, the three-state <see cref="AgentConfig.ConfiguratorStatus"/>, and the per-agent UI content
    /// as an engine-agnostic <see cref="AgentConfig.AgentConfiguratorDescription"/> DTO. This panel only maps
    /// that DTO onto Godot Control widgets and wires Configure / Remove / Reconfigure back to the shared
    /// config's <c>Configure()</c> / <c>Unconfigure()</c>. No per-agent logic lives here — every agent renders
    /// through the same DTO walk.
    ///
    /// <para>
    /// Godot-MCP is an HTTP-only CLIENT of the shared/cloud server, so the panel always describes the
    /// <see cref="TransportMethod.streamableHttp"/> transport (no stdio container). Godot's connection state is
    /// bridged into the shared <see cref="AgentConfig.AgentConfiguratorSettings"/> via
    /// <see cref="AgentConfiguratorSettingsFactory.Create"/> (runtime OS detection for per-OS config paths).
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI nodes and reads the live
    /// <see cref="GodotMcpConfig"/> off the threaded-in connection. All snippet/file LOGIC lives in the shared
    /// module (CI-unit-tested upstream); this class is verified via the headless Godot smoke (<c>test.md</c>
    /// Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class AgentConfiguratorsPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        OptionButton? _agentSelector;
        VBoxContainer? _agentView;

        // The Skills section, rebuilt inside the agent body (ABOVE the MCP config sections) per Unity's
        // `containerSkills`. Owned by this panel (recreated on each agent switch), not a separate dock card.
        SkillsPanel? _skillsSection;

        // The body column (right of the optional 40px agent icon) that the per-agent sub-views are built into.
        VBoxContainer? _agentBody;

        /// <summary>
        /// The addon-relative directory holding the optional per-agent icon assets. A configurator's
        /// <see cref="AgentConfig.AiAgentConfigurator.IconName"/> is resolved against this; a missing file falls
        /// back to no-icon (never crashes — see <see cref="LoadAgentIcon"/>).
        /// </summary>
        const string IconsDir = "res://addons/godot_mcp/Icons/";

        // Live state for the currently-shown configurator.
        AgentConfig.AiAgentConfigurator? _current;

        // Configure/Remove status-row controls + the reconfigure alert host, rebuilt per agent switch.
        Label? _statusLabel;
        Button? _configureButton;
        Button? _removeButton;
        VBoxContainer? _alertHost;

        /// <summary>
        /// Construct the section wired to the live <paramref name="connection"/> (it reads the resolved MCP-client
        /// URL + token + mode off the connection's <see cref="GodotMcpConfig"/> and persists the selected agent via
        /// the connection's <c>Save</c>). Only built by the dock when a live connection exists.
        /// </summary>
        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public AgentConfiguratorsPanel() { }

        public AgentConfiguratorsPanel(GodotMcpConnection connection)
        {
            _connection = connection;
            Name = "AgentConfigurators";
            BuildUi();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 4);

            // Agent selector — a SINGLE row mirroring Unity's MainWindow "AI agent" row: the 20px-bold "AI agent"
            // header on the left, the agent OptionButton filling the remaining width on the right.
            var row = new HBoxContainer { Name = "AgentSelectorRow" };
            row.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(row);

            var headerLabel = new Label { Name = "AgentHeader", Text = "AI agent" };
            DockStyle.ApplyHeader(headerLabel);
            row.AddChild(headerLabel);

            // Agent dropdown — populated from the shared registry (Unity-AI filtered out), item id = registry index.
            _agentSelector = new OptionButton { Name = "AgentSelector", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var names = GodotAgentConfigurators.AgentNames;
            for (int i = 0; i < names.Count; i++)
                _agentSelector.AddItem(names[i], i);
            // Object+method Callable (not a delegate +=) so it never enters the ManagedCallable hot-reload registry.
            _agentSelector.Connect(OptionButton.SignalName.ItemSelected, new Callable(this, MethodName.OnAgentSelected));
            row.AddChild(_agentSelector);

            // Swappable per-agent view — wrapped in the ONE blue frame-group this section gets. The card persists
            // across agent switches; ShowAgent only clears the children of _agentView.
            _agentView = new VBoxContainer { Name = "AgentView", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _agentView.AddThemeConstantOverride("separation", 4);
            AddChild(DockStyle.Card(_agentView, "AiAgentBody"));

            // Restore the persisted selection (default claude-code), falling back to the first agent.
            var persistedId = _connection.Config.SelectedAgentId;
            var index = GodotAgentConfigurators.GetIndexByAgentId(persistedId);
            if (index < 0)
                index = 0;

            _agentSelector.Selected = _agentSelector.GetItemIndex(index);
            ShowAgent(index);
        }

        public void OnAgentSelected(long index)
        {
            var i = (int)index;
            var all = GodotAgentConfigurators.All;
            if (i < 0 || i >= all.Count)
                return;

            // Persist the selected agent id so the choice survives a restart.
            var selected = all[i];
            var changed = _connection.Config.SelectedAgentId != selected.AgentId;
            if (changed)
            {
                _connection.Config.SelectedAgentId = selected.AgentId;
                _connection.Save();
            }

            // ShowAgent rebuilds the whole agent view — including the Skills section — so a dependent re-render
            // needs no separate event; the Skills section now lives inside this view.
            ShowAgent(i);
        }

        /// <summary>Rebuild the per-agent view for the configurator at <see cref="GodotAgentConfigurators.All"/> index <paramref name="index"/>.</summary>
        void ShowAgent(int index)
        {
            if (_agentView == null)
                return;

            var all = GodotAgentConfigurators.All;
            if (index < 0 || index >= all.Count)
                return;

            _current = all[index];

            // Clear the previous agent's view synchronously: detach + free each child so the rebuild below starts
            // from an empty container. Reset the per-view node refs so a stale Refresh() cannot touch a freed node.
            foreach (var child in _agentView.GetChildren())
            {
                _agentView.RemoveChild(child);
                child.QueueFree();
            }
            _statusLabel = null;
            _configureButton = null;
            _removeButton = null;
            _alertHost = null;
            _agentBody = null;
            _skillsSection = null;

            BuildAgentView(_current);
        }

        void BuildAgentView(AgentConfig.AiAgentConfigurator agent)
        {
            if (_agentView == null)
                return;

            var settings = CurrentSettings();
            var description = agent.Describe(settings, TransportMethod.streamableHttp, Logger);

            // --- Agent header row: a 40px per-agent icon (LEFT) + a column holding the agent NAME (section-title)
            //     over the Download/Tutorial links (from the DTO's Links) — a faithful port of Unity-MCP's
            //     AiAgentTemplateConfig header. The icon is gracefully omitted when its asset is missing. ---
            var headerRow = new HBoxContainer { Name = "AgentHeaderRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerRow.AddThemeConstantOverride("separation", 8);
            headerRow.Alignment = BoxContainer.AlignmentMode.Begin;
            _agentView.AddChild(headerRow);

            var icon = LoadAgentIcon(description.IconName);
            if (icon != null)
                headerRow.AddChild(icon);

            var headerCol = new VBoxContainer { Name = "AgentHeaderCol", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerCol.AddThemeConstantOverride("separation", -3); // pull the links up snug under the agent name
            headerRow.AddChild(headerCol);

            var nameLabel = new Label { Name = "AgentName", Text = description.AgentName };
            DockStyle.ApplySectionTitle(nameLabel);
            headerCol.AddChild(nameLabel);

            // Links: the DTO's header links (Download + optional Tutorial), flat link buttons separated by "•".
            var linkDefs = new List<(string Name, string Text, string Url)>();
            for (int i = 0; i < description.Links.Count; i++)
            {
                var link = description.Links[i];
                if (!string.IsNullOrEmpty(link.Url))
                    linkDefs.Add(("Link" + i, link.Text, link.Url!));
            }
            if (linkDefs.Count > 0)
                headerCol.AddChild(DockStyle.LinkRow("Links", linkDefs));

            // --- Body (full width, below the header). ---
            _agentBody = new VBoxContainer { Name = "AgentBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _agentBody.AddThemeConstantOverride("separation", 4);
            _agentView.AddChild(_agentBody);

            // Order mirrors Unity's containerAlert → status row → containerSkills → containerHttp.
            // The reconfigure/setup alert + the Configure/Remove status row only exist for agents with a detectable
            // config (the Custom agent has no writable config file — snippet/Docker only).
            if (HasDetectableConfig(agent))
            {
                _alertHost = new VBoxContainer { Name = "AlertHost", SizeFlagsHorizontal = SizeFlags.ExpandFill };
                _agentBody.AddChild(_alertHost);
                BuildConfigureStatusRow(agent);
            }

            // Skills sit ABOVE the DTO sections (Unity's containerSkills order).
            BuildSkillsSection();

            // Walk the shared DTO's sections — each becomes a collapsible foldout of mapped item widgets.
            foreach (var section in description.Sections)
                _agentBody.AddChild(BuildSection(agent, section));

            RefreshStatus();
        }

        /// <summary>
        /// True when the configurator exposes a real, writable config file. The shared Custom configurator has no
        /// detectable config file (snippet/Docker only), so it is excluded from the Configure/Remove status row and
        /// the setup/reconfigure alert — mirroring Unity's <c>HasDetectableConfig</c>.
        /// </summary>
        static bool HasDetectableConfig(AgentConfig.AiAgentConfigurator agent)
            => agent is not AgentConfig.Impl.CustomConfigurator;

        /// <summary>
        /// Build one collapsible foldout from a shared <see cref="AgentConfig.ConfigurationSection"/> — the section's
        /// heading is the foldout title (expanded when <see cref="AgentConfig.ConfigurationSection.ExpandedFirst"/>),
        /// and each <see cref="AgentConfig.ConfigurationItem"/> is mapped onto a Godot widget by kind.
        /// </summary>
        Control BuildSection(AgentConfig.AiAgentConfigurator agent, AgentConfig.ConfigurationSection section)
        {
            var (container, content) = DockStyle.Foldout(section.Heading, startExpanded: section.ExpandedFirst);
            foreach (var item in section.Items)
            {
                // The Custom agent's editable skills path is owned by the dedicated Skills section (SkillsPanel adds
                // the auto-generate toggle + Generate button the DTO EditableField can't). Skip that pair here to
                // avoid rendering the field twice — matches Unity's AiAgentConfiguratorView.
                if (agent is AgentConfig.Impl.CustomConfigurator && IsCustomSkillsPathItem(item))
                    continue;

                var element = BuildItem(item);
                if (element != null)
                    content.AddChild(element);
            }
            return container;
        }

        /// <summary>
        /// True for the shared <see cref="AgentConfig.Impl.CustomConfigurator"/>'s editable skills-path items — the
        /// <see cref="AgentConfig.ConfigurationItemKind.EditableField"/> and its preceding
        /// "Skills output path (editable):" description. These are rendered by the dedicated Skills section instead,
        /// so the section walk skips them (mirrors Unity's <c>IsCustomSkillsPathItem</c>).
        /// </summary>
        static bool IsCustomSkillsPathItem(AgentConfig.ConfigurationItem item)
            => item.Kind == AgentConfig.ConfigurationItemKind.EditableField
                || (item.Kind == AgentConfig.ConfigurationItemKind.Description
                    && item.Text == "Skills output path (editable):");

        /// <summary>
        /// Map a single shared <see cref="AgentConfig.ConfigurationItem"/> onto a Godot Control. The kind vocabulary
        /// matches the shared DTO 1:1 — Description / Warning / Alert / ReadOnlyField / EditableField / Link.
        /// </summary>
        Control? BuildItem(AgentConfig.ConfigurationItem item)
        {
            switch (item.Kind)
            {
                case AgentConfig.ConfigurationItemKind.Description:
                    return DescriptionLabel(item.Text);
                case AgentConfig.ConfigurationItemKind.Warning:
                    return DockStyle.WarningFrame(item.Text);
                case AgentConfig.ConfigurationItemKind.Alert:
                    return AlertLabel(item.Text);
                case AgentConfig.ConfigurationItemKind.ReadOnlyField:
                    return ReadOnlyField(item.Text);
                case AgentConfig.ConfigurationItemKind.EditableField:
                    // Godot has no per-section editable field today (the only DTO EditableField is the Custom
                    // skills path, owned by the Skills section and skipped above). Render read-only as a safe
                    // fallback so an upstream addition never silently drops content.
                    return ReadOnlyField(item.Text);
                case AgentConfig.ConfigurationItemKind.Link:
                    return DockStyle.LinkButton("ItemLink", item.Text, item.Url ?? string.Empty);
                default:
                    return DescriptionLabel(item.Text);
            }
        }

        static Label DescriptionLabel(string text)
        {
            var label = new Label { Name = "Description", Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            DockStyle.ApplyDescription(label);
            return label;
        }

        static Label AlertLabel(string text)
        {
            var label = new Label { Name = "Alert", Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart };
            label.AddThemeColorOverride("font_color", DockStyle.Rgb(DockTheme.WarningText));
            return label;
        }

        /// <summary>
        /// A read-only, selectable, copyable multi-line field for a DTO command / JSON / TOML snippet (the shared
        /// configurators embed the REAL token directly — writing the user's own client config is the point, matching
        /// Unity's read-only fields). Auto-sizes to a few lines and grows for multi-line content.
        /// </summary>
        static TextEdit ReadOnlyField(string text)
        {
            var lines = text.Split('\n').Length;
            var height = Mathf.Clamp(lines, 1, 14) * 20 + 12;
            return new TextEdit
            {
                Name = "ReadOnlyField",
                Text = text,
                Editable = false,
                CustomMinimumSize = new Vector2(0, height),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ScrollFitContentHeight = true
            };
        }

        /// <summary>
        /// Load the optional 40px <see cref="TextureRect"/> icon named <paramref name="iconName"/> from
        /// <see cref="IconsDir"/>. Returns null (no icon, body fills full width) when the agent declares no icon OR the
        /// asset is missing / not a texture — this NEVER crashes the dock on a missing asset.
        /// </summary>
        TextureRect? LoadAgentIcon(string? iconName)
        {
            if (string.IsNullOrEmpty(iconName))
                return null;

            var path = IconsDir + iconName;
            if (!ResourceLoader.Exists(path))
                return null;

            if (ResourceLoader.Load(path) is not Texture2D texture)
                return null;

            return new TextureRect
            {
                Name = "AgentIcon",
                Texture = texture,
                CustomMinimumSize = new Vector2(40, 40),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ShrinkCenter
            };
        }

        /// <summary>
        /// Build the Unity-style Configure/Remove row for agents WITH a writable config file: an MCP header + the
        /// config path (right-aligned, ellipsis), then a status label ("Configured (http)"/"Not configured") + a
        /// Configure / Reconfigure primary button and a Remove alert button (visible only when an entry exists). The
        /// real token flows into the written file via the shared config's <c>Configure()</c>; it is never logged.
        /// </summary>
        void BuildConfigureStatusRow(AgentConfig.AiAgentConfigurator agent)
        {
            if (_agentBody == null)
                return;

            var settings = CurrentSettings();
            var config = agent.GetHttpConfig(settings, Logger);

            // --- Row 1: "Model Context Protocol (MCP)" header + right-aligned ellipsis config path. ---
            var headerRow = new HBoxContainer { Name = "McpHeaderRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerRow.Alignment = BoxContainer.AlignmentMode.Center;
            _agentBody.AddChild(headerRow);

            headerRow.AddChild(DockStyle.UnderlinedSubLabel("McpHeader", "Model Context Protocol (MCP)"));

            var configPathLabel = new Label
            {
                Name = "ConfigPath",
                Text = SkillsPathUtils.ToDisplayPath(config.ConfigPath, ProjectRoot),
                TooltipText = config.ConfigPath,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockStyle.ApplyConfigPath(configPathLabel);
            headerRow.AddChild(configPathLabel);

            // --- Row 2: status text + right-aligned Remove/Configure buttons. ---
            var statusRow = new HBoxContainer { Name = "ConfigStatusRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            statusRow.Alignment = BoxContainer.AlignmentMode.Center;
            _agentBody.AddChild(statusRow);

            _statusLabel = new Label { Name = "Status", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            DockStyle.ApplyDescription(_statusLabel);
            _statusLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            statusRow.AddChild(_statusLabel);

            var configActions = new HBoxContainer { Name = "ConfigActions" };
            statusRow.AddChild(configActions);

            // Button order mirrors Unity: Remove first (left), Configure second (right). Connected via object+method
            // Callables to parameterless instance handlers that re-resolve the agent + a fresh settings snapshot.
            _removeButton = new Button { Name = "Remove", Text = "Remove" };
            DockStyle.ApplyAlertButton(_removeButton);
            DockStyle.ConnectPressed(_removeButton, this, MethodName.OnRemoveButtonPressed);
            configActions.AddChild(_removeButton);

            _configureButton = new Button { Name = "Configure", Text = "Configure" };
            DockStyle.ConnectPressed(_configureButton, this, MethodName.OnConfigureButtonPressed);
            configActions.AddChild(_configureButton);
            // Text / styling / Remove visibility are driven by RefreshStatus().
        }

        /// <summary>Build the Skills section INSIDE the agent body (Unity's containerSkills). A fresh panel per agent switch.</summary>
        void BuildSkillsSection()
        {
            if (_agentBody == null)
                return;

            _skillsSection = new SkillsPanel(_connection);
            _agentBody.AddChild(_skillsSection);
        }

        /// <summary>
        /// Configure-button <c>pressed</c> handler (object+method Callable). Writes the addon's HTTP entry into the
        /// current agent's config file via the shared config's <c>Configure()</c> (REAL token; never logged), then
        /// re-evaluates the status + alert.
        /// </summary>
        public void OnConfigureButtonPressed()
        {
            if (_current == null)
                return;
            var config = _current.GetHttpConfig(CurrentSettings(), Logger);
            config.Configure();
            RefreshStatus();
        }

        /// <summary>Remove-button <c>pressed</c> handler (object+method Callable). Removes the addon's entry via <c>Unconfigure()</c>.</summary>
        public void OnRemoveButtonPressed()
        {
            if (_current == null)
                return;
            var config = _current.GetHttpConfig(CurrentSettings(), Logger);
            config.Unconfigure();
            RefreshStatus();
        }

        /// <summary>
        /// Re-render the Configure/Remove status AND the Setup/Reconfiguration alert for the current agent (only
        /// agents WITH a config-file path have these). Drives the "Configured (http)"/"Not configured" label, flips
        /// the Configure button to "Reconfigure" when configured, shows Remove only when an entry exists, and
        /// (re)builds the amber alert per the shared three-state <see cref="AgentConfig.ConfiguratorStatus"/>.
        /// </summary>
        void RefreshStatus()
        {
            if (_current == null || !HasDetectableConfig(_current) || _statusLabel == null)
                return;

            var settings = CurrentSettings();
            var status = _current.GetStatus(settings, TransportMethod.streamableHttp, Logger);
            var isConfigured = _current.IsConfigured(settings, TransportMethod.streamableHttp, Logger);
            var anyDetected = _current.IsDetected(settings, Logger);

            _statusLabel.Text = isConfigured ? "Configured (http)" : "Not configured";
            _statusLabel.AddThemeColorOverride(
                "font_color",
                isConfigured ? DockStyle.Rgb(DockTheme.StatusOnline) : DockStyle.Rgb(DockTheme.WarningText));

            if (_configureButton != null)
            {
                _configureButton.Text = isConfigured ? "Reconfigure" : "Configure";
                if (isConfigured)
                    DockStyle.ApplySecondaryButton(_configureButton);
                else
                    DockStyle.ApplyCompactPrimaryButton(_configureButton);
            }
            if (_removeButton != null)
                _removeButton.Visible = anyDetected;

            RefreshAlert(status);
        }

        /// <summary>
        /// (Re)build the AI-agent "Setup Required" / "Reconfiguration Required" amber alert into the reserved
        /// <c>AlertHost</c> slot, driven by the shared three-state <paramref name="status"/>. Cleared (no alert) when
        /// the status is <see cref="AgentConfig.ConfiguratorStatus.Configured"/>. The action button reuses the same
        /// Configure path (writes the entry, then RefreshStatus re-evaluates + clears the alert). Reuses
        /// <see cref="DockStyle.AlertPanel"/> so the amber chrome is not duplicated.
        /// </summary>
        void RefreshAlert(AgentConfig.ConfiguratorStatus status)
        {
            if (_alertHost == null)
                return;

            foreach (var child in _alertHost.GetChildren())
            {
                _alertHost.RemoveChild(child);
                child.QueueFree();
            }

            if (status == AgentConfig.ConfiguratorStatus.Configured)
                return;

            var (title, message, button) = status == AgentConfig.ConfiguratorStatus.ReconfigureNeeded
                ? ("Reconfiguration Required",
                   "Connection settings have changed. The existing MCP configuration is outdated and needs to be updated.",
                   "Reconfigure")
                : ("Setup Required",
                   "At least one of the following must be configured:\n• MCP Configuration",
                   "Configure");

            var panel = DockStyle.AlertPanel("AgentAlert", title, message, button, OnConfigureButtonPressed);
            _alertHost.AddChild(panel);
        }

        /// <summary>
        /// Re-sync the section when the connection URL/token/mode changes (forwarded from
        /// <see cref="GodotMcpDock.Refresh"/>). The DTO sections embed the live URL/token snapshot, so a full agent
        /// rebuild is the simplest correct refresh; the Skills section re-evaluates inside it.
        /// </summary>
        public void Refresh()
        {
            var index = _agentSelector?.GetSelectedId() ?? -1;
            if (index < 0)
            {
                RefreshStatus();
                _skillsSection?.Refresh();
                return;
            }
            ShowAgent(index);
        }

        // --- live resolution off the connection config -------------------------------------------------------

        /// <summary>A fresh shared-settings snapshot bridging the live Godot connection state (URL/token/mode/auth).</summary>
        AgentConfig.AgentConfiguratorSettings CurrentSettings() => AgentConfiguratorSettingsFactory.Create(_connection.Config);

        /// <summary>The absolute project root (globalized <c>res://</c>, no trailing slash) — used to render config paths project-relative.</summary>
        string ProjectRoot => ProjectSettings.GlobalizePath("res://").TrimEnd('/');

        /// <summary>The logger passed to the shared configurator calls — UI rendering does not surface config-write logs.</summary>
        static ILogger Logger => NullLogger.Instance;

        // No KeepAlive teardown is needed: every signal here is an OBJECT+METHOD Callable (the agent selector + the
        // Configure/Remove buttons connect to this panel's instance methods), which is not a ManagedCallable and never
        // enters the native registry the Build-Project hot-reload iterates. The alert button uses an Action that
        // targets this instance's method; the target controls are kept alive by the tree and freed with the panel.
    }
}
#endif
