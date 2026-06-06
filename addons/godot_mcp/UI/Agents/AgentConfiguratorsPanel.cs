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
using System;
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The dock's "AI agent" section — the Godot <see cref="Control"/> analog of Unity-MCP's
    /// <c>MainWindowEditor.AiAgents</c>, condensed to the HTTP-only client shape. A <see cref="VBoxContainer"/> the
    /// <see cref="GodotMcpDock"/> inserts into its Body between the features section and the support footer. It
    /// renders a header + an agent <see cref="OptionButton"/> (populated from
    /// <see cref="GodotAgentConfiguratorRegistry.AgentNames"/>), and below it a swappable panel for the selected
    /// configurator: the agent's links, the generated HTTP-config snippet (token masked by default, with a Reveal
    /// toggle + Copy), and — for configurators that have a config-file path — Configure / Remove buttons + a status
    /// line + the config path.
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI nodes and reads the live
    /// <see cref="GodotMcpConfig"/> off the threaded-in connection for the URL/token/mode. All the snippet/file
    /// LOGIC lives in the pure-managed <see cref="GodotAgentConfigurator"/> + <see cref="AgentConfigJson"/> +
    /// <see cref="AgentConfigPaths"/> (CI-unit-tested); this class is verified via the headless Godot smoke
    /// (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class AgentConfiguratorsPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        /// <summary>
        /// Raised after the user picks a DIFFERENT AI agent in the dropdown (the persisted
        /// <see cref="GodotMcpConfig.SelectedAgentId"/> changed + was Saved). The dock subscribes so dependent
        /// sections — notably the Skills card, whose supported-state/path follow the selected agent — re-render. Fired
        /// on the editor main thread (the selection callback runs there), carries no payload (subscribers re-read the
        /// persisted selection). Not fired when the user re-picks the already-selected agent.
        /// </summary>
        public event Action? AgentSelectionChanged;

        OptionButton? _agentSelector;
        VBoxContainer? _agentView;

        // The body column (right of the optional 32px agent icon) that the per-agent sub-views are built into.
        // Distinct from _agentView (the outer swappable container) so the icon sits to the LEFT of the body.
        VBoxContainer? _agentBody;

        /// <summary>
        /// The addon-relative directory holding the optional per-agent icon assets. A configurator's
        /// <see cref="GodotAgentConfigurator.IconFileName"/> is resolved against this; a missing file falls back to
        /// no-icon (never crashes — see <see cref="LoadAgentIcon"/>).
        /// </summary>
        const string IconsDir = "res://addons/godot_mcp/Icons/";

        // Live state for the currently-shown configurator's view.
        GodotAgentConfigurator? _current;
        bool _revealToken;
        TextEdit? _snippetText;
        Button? _revealButton;
        Label? _statusLabel;
        Label? _configPathLabel;
        Button? _configureButton;
        Button? _removeButton;

        // The AI-agent "Setup Required" / "Reconfiguration Required" alert panel (reused DockStyle.AlertPanel),
        // rebuilt by RefreshStatus off the live AgentConfigState. Null for the Custom snippet agent (no config path).
        Control? _alertPanel;

        /// <summary>
        /// Construct the section wired to the live <paramref name="connection"/> (it reads the resolved MCP-client
        /// URL + token off the connection's <see cref="GodotMcpConfig"/> and persists the selected agent via the
        /// connection's <c>Save</c>). Only built by the dock when a live connection exists.
        /// </summary>
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

            // Agent selector — a SINGLE row mirroring Unity's MainWindow.uxml "AI agent" row
            // (<Label class="header"/> + <DropdownField flex-grow:1/>): the 20px-bold "AI agent" header on the
            // left, the agent OptionButton filling the remaining width on the right. No redundant second label.
            var row = new HBoxContainer { Name = "AgentSelectorRow" };
            row.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(row);

            var headerLabel = new Label { Name = "AgentHeader", Text = "AI agent" };
            DockStyle.ApplyHeader(headerLabel);
            row.AddChild(headerLabel);

            // Agent dropdown — populated from the registry, item id = registry index. Fills remaining width.
            _agentSelector = new OptionButton { Name = "AgentSelector", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var names = GodotAgentConfiguratorRegistry.AgentNames;
            for (int i = 0; i < names.Count; i++)
                _agentSelector.AddItem(names[i], i);
            _agentSelector.ItemSelected += OnAgentSelected;
            row.AddChild(_agentSelector);

            // Swappable per-agent view.
            _agentView = new VBoxContainer { Name = "AgentView", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _agentView.AddThemeConstantOverride("separation", 4);
            AddChild(_agentView);

            // Restore the persisted selection (default claude-code), falling back to the first agent.
            var persistedId = _connection.Config.SelectedAgentId;
            var index = GodotAgentConfiguratorRegistry.GetIndexByAgentId(persistedId);
            if (index < 0)
                index = 0;

            _agentSelector.Selected = _agentSelector.GetItemIndex(index);
            ShowAgent(index);
        }

        void OnAgentSelected(long index)
        {
            var i = (int)index;
            var all = GodotAgentConfiguratorRegistry.All;
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

            ShowAgent(i);

            // Notify dependent sections (the Skills card follows the selected agent) AFTER the persisted selection +
            // this panel's own view are updated, so a subscriber re-reading SelectedAgentId sees the new value.
            if (changed)
                AgentSelectionChanged?.Invoke();
        }

        /// <summary>Rebuild the per-agent view for the configurator at registry index <paramref name="index"/>.</summary>
        void ShowAgent(int index)
        {
            if (_agentView == null)
                return;

            var all = GodotAgentConfiguratorRegistry.All;
            if (index < 0 || index >= all.Count)
                return;

            _current = all[index];
            _revealToken = false; // reset masking when switching agents (never leak a revealed token across agents)

            // Clear the previous agent's view synchronously: detach + free each child so the rebuild below starts
            // from an empty container (QueueFree alone defers to the next idle frame, which would briefly double the
            // controls). Reset the per-view node refs so a stale Refresh() cannot touch a freed node.
            foreach (var child in _agentView.GetChildren())
            {
                _agentView.RemoveChild(child);
                child.QueueFree();
            }
            _snippetText = null;
            _revealButton = null;
            _statusLabel = null;
            _configPathLabel = null;
            _configureButton = null;
            _removeButton = null;
            _alertPanel = null;
            _agentBody = null;

            BuildAgentView(_current);
        }

        void BuildAgentView(GodotAgentConfigurator agent)
        {
            if (_agentView == null)
                return;

            // NOTE: the selected agent's name is intentionally NOT repeated here as a separate label (it is shown in
            // the agent OptionButton above — mirrors Unity, and the standalone name label was removed in #58).

            // --- Icon (left) + body (right) layout: a 32px per-agent icon, gracefully omitted when missing. ---
            var iconRow = new HBoxContainer { Name = "AgentIconRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            iconRow.AddThemeConstantOverride("separation", 8);
            iconRow.Alignment = BoxContainer.AlignmentMode.Begin;
            _agentView.AddChild(iconRow);

            var icon = LoadAgentIcon(agent);
            if (icon != null)
                iconRow.AddChild(icon);

            _agentBody = new VBoxContainer { Name = "AgentBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _agentBody.AddThemeConstantOverride("separation", 4);
            iconRow.AddChild(_agentBody);

            // --- Per-agent description (muted). ---
            if (!string.IsNullOrEmpty(agent.Description))
            {
                var desc = new Label { Name = "Description", Text = agent.Description! };
                DockStyle.ApplyDescription(desc);
                _agentBody.AddChild(desc);
            }

            // --- Per-agent warning banner (styled amber frame). ---
            if (!string.IsNullOrEmpty(agent.WarningText))
                _agentBody.AddChild(DockStyle.WarningFrame(agent.WarningText!));

            // --- Links: Download (+ optional Tutorial), as flat link buttons separated by "•". ---
            var linkDefs = new List<(string Name, string Text, string Url)>
            {
                ("Download", "Download", agent.DownloadUrl)
            };
            if (!string.IsNullOrEmpty(agent.TutorialUrl))
                linkDefs.Add(("Tutorial", "Tutorial", agent.TutorialUrl!));
            _agentBody.AddChild(DockStyle.LinkRow("Links", linkDefs));

            // --- The KEY decision: Configure/Remove for agents with a config-file path; copyable JSON otherwise. ---
            var configPath = ResolveConfigPath(agent);
            if (GodotAgentConfigurator.ShouldShowJson(configPath))
            {
                BuildSnippetView(agent);
            }
            else
            {
                // Only config-path agents (not the Custom snippet agent) get the Setup/Reconfiguration alert. The
                // alert frame is (re)built per state by RefreshStatus; an empty host slot reserves its position
                // ABOVE the configure/remove row, matching the Unity reference order.
                _agentBody.AddChild(new VBoxContainer { Name = "AlertHost", SizeFlagsHorizontal = SizeFlags.ExpandFill });
                BuildConfigureRemoveView(agent, configPath!);
            }

            // --- Per-agent help foldouts (Manual Configuration Steps / Troubleshooting). ---
            BuildHelpFoldouts(agent);

            RefreshSnippet();
            RefreshStatus();
        }

        /// <summary>
        /// Load the optional 32px <see cref="TextureRect"/> icon for <paramref name="agent"/> from
        /// <see cref="IconsDir"/> + the agent's <see cref="GodotAgentConfigurator.IconFileName"/>. Returns null
        /// (no icon, the body fills the full width) when the agent declares no icon file OR the asset is missing /
        /// not a texture — this NEVER crashes the dock on a missing asset.
        /// </summary>
        TextureRect? LoadAgentIcon(GodotAgentConfigurator agent)
        {
            var fileName = agent.IconFileName;
            if (string.IsNullOrEmpty(fileName))
                return null;

            var path = IconsDir + fileName;
            if (!ResourceLoader.Exists(path))
                return null;

            // Load defensively: a corrupt / non-texture asset must not crash the dock.
            if (ResourceLoader.Load(path) is not Texture2D texture)
                return null;

            return new TextureRect
            {
                Name = "AgentIcon",
                Texture = texture,
                CustomMinimumSize = new Vector2(32, 32),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                SizeFlagsVertical = SizeFlags.ShrinkBegin
            };
        }

        /// <summary>
        /// For an agent WITHOUT a writable config-file path (Custom): show the copyable HTTP snippet (read-only,
        /// token MASKED by default) + Reveal/Copy. No Configure/Remove buttons.
        /// </summary>
        void BuildSnippetView(GodotAgentConfigurator agent)
        {
            if (_agentBody == null)
                return;

            var snippetLabel = new Label
            {
                Name = "SnippetLabel",
                Text = "Copy this into your MCP client config:"
            };
            DockStyle.ApplyDescription(snippetLabel);
            _agentBody.AddChild(snippetLabel);

            _snippetText = new TextEdit
            {
                Name = "Snippet",
                Editable = false,
                CustomMinimumSize = new Vector2(0, 120),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _agentBody.AddChild(_snippetText);

            // Snippet action buttons: Reveal token + Copy (copies the UNMASKED snippet).
            var snippetActions = new HBoxContainer { Name = "SnippetActions" };
            _agentBody.AddChild(snippetActions);

            _revealButton = new Button { Name = "Reveal", Text = "Reveal token" };
            DockStyle.ApplySecondaryButton(_revealButton);
            _revealButton.Pressed += OnRevealToggled;
            snippetActions.AddChild(_revealButton);

            var copyButton = new Button { Name = "Copy", Text = "Copy" };
            DockStyle.ApplySecondaryButton(copyButton);
            copyButton.Pressed += OnCopyPressed;
            snippetActions.AddChild(copyButton);
        }

        /// <summary>
        /// For an agent WITH a writable config-file path (Claude Code/Desktop, Cursor, VS Code): show the
        /// Unity-style Configure/Remove row — a status label ("Configured"/"Not configured"), a Configure /
        /// Reconfigure primary button (writes the entry), a Remove alert button (visible only when configured),
        /// and the config-file path. NO raw JSON snippet for these agents. The real token still flows into the
        /// written file via Configure — it is never shown on-screen or logged.
        /// </summary>
        void BuildConfigureRemoveView(GodotAgentConfigurator agent, string configPath)
        {
            if (_agentBody == null)
                return;

            // Mirror Unity's TemplateConfigureStatus.uxml: a two-row block.
            //   Row 1 (space-between): "Model Context Protocol (MCP)" header on the LEFT + the config path on the
            //           RIGHT (right-aligned, single line, ellipsis-truncated, muted description style).
            //   Row 2 (space-between): the status text on the LEFT + the Remove/Configure buttons on the RIGHT.

            // --- Row 1: MCP header + right-aligned ellipsis config path. ---
            var headerRow = new HBoxContainer { Name = "McpHeaderRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerRow.Alignment = BoxContainer.AlignmentMode.Center;
            _agentBody.AddChild(headerRow);

            var mcpHeader = new Label { Name = "McpHeader", Text = "Model Context Protocol (MCP)" };
            DockStyle.ApplySubLabel(mcpHeader);
            headerRow.AddChild(mcpHeader);

            _configPathLabel = new Label
            {
                Name = "ConfigPath",
                Text = configPath,
                TooltipText = configPath,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockStyle.ApplyConfigPath(_configPathLabel);
            headerRow.AddChild(_configPathLabel);

            // --- Row 2: status text + right-aligned Remove/Configure buttons. ---
            var statusRow = new HBoxContainer { Name = "ConfigStatusRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            statusRow.Alignment = BoxContainer.AlignmentMode.Center;
            _agentBody.AddChild(statusRow);

            _statusLabel = new Label { Name = "Status", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            DockStyle.ApplyDescription(_statusLabel);
            _statusLabel.AutowrapMode = TextServer.AutowrapMode.Off; // single-line status, keep it on Row 2's left
            statusRow.AddChild(_statusLabel);

            var configActions = new HBoxContainer { Name = "ConfigActions" };
            statusRow.AddChild(configActions);

            // Button order mirrors Unity: Remove first (left), Configure second (right).
            _removeButton = new Button { Name = "Remove", Text = "Remove" };
            DockStyle.ApplyAlertButton(_removeButton);
            _removeButton.Pressed += () => OnRemovePressed(agent, configPath);
            configActions.AddChild(_removeButton);

            _configureButton = new Button { Name = "Configure", Text = "Configure" };
            _configureButton.Pressed += () => OnConfigurePressed(agent, configPath);
            configActions.AddChild(_configureButton);
            // Configure/Reconfigure text, primary-vs-secondary styling, and Remove visibility are all driven by
            // RefreshStatus() (called at the end of BuildAgentView) off the live IsConfigured() state.
        }

        /// <summary>Append the agent's "Manual Configuration Steps" / "Troubleshooting" collapsible foldouts when non-empty.</summary>
        void BuildHelpFoldouts(GodotAgentConfigurator agent)
        {
            if (_agentBody == null)
                return;

            if (agent.ManualSteps.Count > 0)
            {
                var (container, content) = DockStyle.Foldout("Manual Configuration Steps");
                foreach (var step in agent.ManualSteps)
                {
                    var label = new Label { Text = step };
                    DockStyle.ApplyDescription(label);
                    content.AddChild(label);
                }
                _agentBody.AddChild(container);
            }

            if (agent.Troubleshooting.Count > 0)
            {
                var (container, content) = DockStyle.Foldout("Troubleshooting");
                foreach (var tip in agent.Troubleshooting)
                {
                    var label = new Label { Text = tip };
                    DockStyle.ApplyDescription(label);
                    content.AddChild(label);
                }
                _agentBody.AddChild(container);
            }
        }

        void OnRevealToggled()
        {
            _revealToken = !_revealToken;
            if (_revealButton != null)
                _revealButton.Text = _revealToken ? "Hide token" : "Reveal token";
            RefreshSnippet();
        }

        void OnCopyPressed()
        {
            if (_current == null)
                return;

            // Copy ALWAYS uses the real token — writing the user's own client config is the point. Never logged.
            var snippet = _current.BuildSnippet(McpUrl, Token, maskToken: false);
            DisplayServer.ClipboardSet(snippet);
        }

        void OnConfigurePressed(GodotAgentConfigurator agent, string configPath)
        {
            // Configure writes the REAL token into the user's own config file. Never logged.
            agent.Configure(configPath, McpUrl, Token);
            RefreshStatus();
        }

        void OnRemovePressed(GodotAgentConfigurator agent, string configPath)
        {
            agent.Remove(configPath);
            RefreshStatus();
        }

        /// <summary>Re-render the snippet TextEdit honoring the current mask/reveal state. Never logs the token.</summary>
        void RefreshSnippet()
        {
            if (_current == null || _snippetText == null)
                return;

            _snippetText.Text = _current.BuildSnippet(McpUrl, Token, maskToken: !_revealToken);
        }

        /// <summary>
        /// Re-render the Configure/Remove status AND the Setup/Reconfiguration alert for the current agent (only
        /// agents WITH a config-file path have these). Drives the "Configured"/"Not configured" label, flips the
        /// Configure button to "Reconfigure" when already configured, shows the Remove button only when an entry
        /// exists, and (re)builds the amber alert panel per the live <see cref="AgentConfigState"/>. Reads the
        /// pure-managed <see cref="GodotAgentConfigurator.ConfigState"/> against the resolved config path.
        /// </summary>
        void RefreshStatus()
        {
            if (_current == null || _statusLabel == null)
                return;

            var configPath = ResolveConfigPath(_current);
            if (configPath == null)
                return;

            var state = _current.ConfigState(configPath, McpUrl);
            var configured = state == AgentConfigState.UpToDate;

            _statusLabel.Text = configured ? "Configured" : "Not configured";
            _statusLabel.AddThemeColorOverride(
                "font_color",
                configured ? DockStyle.Rgb(DockTheme.StatusOnline) : DockStyle.Rgb(DockTheme.WarningText));

            if (_configureButton != null)
            {
                // Primary (cyan) ONLY when an entry still needs writing; once configured it becomes a plain
                // "Reconfigure" secondary button (mirrors the brief — the call-to-action is the un-configured state).
                _configureButton.Text = configured ? "Reconfigure" : "Configure";
                if (configured)
                    DockStyle.ApplySecondaryButton(_configureButton);
                else
                    DockStyle.ApplyPrimaryButton(_configureButton);
            }
            if (_removeButton != null)
                _removeButton.Visible = configured;

            RefreshAlert(_current, configPath, state);
        }

        /// <summary>
        /// (Re)build the AI-agent "Setup Required" / "Reconfiguration Required" amber alert into the reserved
        /// <c>AlertHost</c> slot, driven by the pure-managed <see cref="AgentAlertView"/> off <paramref name="state"/>.
        /// Cleared (no alert) when the config is <see cref="AgentConfigState.UpToDate"/>. The action button reuses
        /// the same <see cref="OnConfigurePressed"/> path as the Configure button (writes the addon's entry, then
        /// RefreshStatus re-evaluates and clears the alert). Reuses <see cref="DockStyle.AlertPanel"/> (PR #61's
        /// amber frame) so the chrome is not duplicated.
        /// </summary>
        void RefreshAlert(GodotAgentConfigurator agent, string configPath, AgentConfigState state)
        {
            if (_agentBody == null)
                return;

            // The reserved host VBox sits ABOVE the configure/remove row (added in BuildAgentView for config-path
            // agents). The Custom snippet agent has no host, so there is nothing to render the alert into.
            var host = _agentBody.GetNodeOrNull<VBoxContainer>("AlertHost");
            if (host == null)
                return;

            // Clear any prior alert (state may have just flipped, e.g. after a Configure write).
            foreach (var child in host.GetChildren())
            {
                host.RemoveChild(child);
                child.QueueFree();
            }
            _alertPanel = null;

            if (!AgentAlertView.ShowAlert(state))
                return;

            _alertPanel = DockStyle.AlertPanel(
                "AgentAlert",
                AgentAlertView.Title(state),
                AgentAlertView.Message(state),
                AgentAlertView.ButtonText,
                () => OnConfigurePressed(agent, configPath));
            host.AddChild(_alertPanel);
        }

        /// <summary>
        /// Re-sync the section when the connection URL/token/mode changes (forwarded from
        /// <see cref="GodotMcpDock.Refresh"/>). Re-renders the snippet (URL/token may have changed) and the status.
        /// </summary>
        public void Refresh()
        {
            RefreshSnippet();
            RefreshStatus();
        }

        // --- live resolution off the connection config -------------------------------------------------------

        /// <summary>The MCP-client endpoint URL (resolved off the live config — Cloud <c>/mcp</c> or Custom <c>&lt;host&gt;/mcp</c>).</summary>
        string McpUrl => GodotMcpConfig.ResolveMcpClientUrl(_connection.Config);

        /// <summary>The active bearer token (routed by mode), or null/empty when none — never logged.</summary>
        string? Token => _connection.Config.Token;

        /// <summary>Resolve the agent's per-OS absolute config path from live editor values, or null (snippet-only).</summary>
        string? ResolveConfigPath(GodotAgentConfigurator agent)
        {
            var os = MapOs(OS.GetName());
            var home = OS.GetEnvironment("USERPROFILE");
            if (string.IsNullOrEmpty(home))
                home = OS.GetEnvironment("HOME");
            var appData = OS.GetEnvironment("APPDATA");
            var projectRoot = ProjectSettings.GlobalizePath("res://").TrimEnd('/');

            return agent.ConfigFilePath(os, home, appData, projectRoot);
        }

        /// <summary>Map Godot's <c>OS.GetName()</c> ("Windows"/"macOS"/"Linux"/…) onto the injectable <see cref="AgentOs"/>.</summary>
        static AgentOs MapOs(string godotOsName) => godotOsName switch
        {
            "Windows" => AgentOs.Windows,
            "macOS" => AgentOs.MacOS,
            _ => AgentOs.Linux,
        };
    }
}
#endif
