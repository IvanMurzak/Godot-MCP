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

        OptionButton? _agentSelector;
        VBoxContainer? _agentView;

        // Live state for the currently-shown configurator's view.
        GodotAgentConfigurator? _current;
        bool _revealToken;
        TextEdit? _snippetText;
        Button? _revealButton;
        Label? _statusLabel;

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

            AddChild(new HSeparator { Name = "AgentSeparator" });
            AddChild(new Label { Name = "AgentHeader", Text = "AI agent" });

            // Agent dropdown — populated from the registry, item id = registry index.
            var row = new HBoxContainer { Name = "AgentSelectorRow" };
            AddChild(row);
            row.AddChild(new Label { Name = "AgentSelectorLabel", Text = "Agent" });

            _agentSelector = new OptionButton { Name = "AgentSelector" };
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
            if (_connection.Config.SelectedAgentId != selected.AgentId)
            {
                _connection.Config.SelectedAgentId = selected.AgentId;
                _connection.Save();
            }

            ShowAgent(i);
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

            BuildAgentView(_current);
        }

        void BuildAgentView(GodotAgentConfigurator agent)
        {
            if (_agentView == null)
                return;

            _agentView.AddChild(new Label { Name = "AgentName", Text = agent.AgentName });

            // --- Links: Download (+ optional Tutorial). Open externally via OS.ShellOpen. ---
            var links = new HBoxContainer { Name = "Links" };
            _agentView.AddChild(links);
            links.AddChild(MakeLinkButton("Download", "Download", agent.DownloadUrl));
            if (!string.IsNullOrEmpty(agent.TutorialUrl))
                links.AddChild(MakeLinkButton("Tutorial", "Tutorial", agent.TutorialUrl!));

            // --- Generated HTTP-config snippet (read-only, token masked by default). ---
            _agentView.AddChild(new Label
            {
                Name = "SnippetLabel",
                Text = "Copy this into your MCP client config:"
            });

            _snippetText = new TextEdit
            {
                Name = "Snippet",
                Editable = false,
                CustomMinimumSize = new Vector2(0, 120),
                SizeFlagsHorizontal = SizeFlags.ExpandFill
            };
            _agentView.AddChild(_snippetText);

            // Snippet action buttons: Reveal token + Copy (copies the UNMASKED snippet).
            var snippetActions = new HBoxContainer { Name = "SnippetActions" };
            _agentView.AddChild(snippetActions);

            _revealButton = new Button { Name = "Reveal", Text = "Reveal token" };
            _revealButton.Pressed += OnRevealToggled;
            snippetActions.AddChild(_revealButton);

            var copyButton = new Button { Name = "Copy", Text = "Copy" };
            copyButton.Pressed += OnCopyPressed;
            snippetActions.AddChild(copyButton);

            // --- Config-file controls (only for agents that have a writable config). ---
            var configPath = ResolveConfigPath(agent);
            if (configPath != null)
            {
                _statusLabel = new Label { Name = "Status" };
                _agentView.AddChild(_statusLabel);

                _agentView.AddChild(new Label
                {
                    Name = "ConfigPath",
                    Text = configPath,
                    AutowrapMode = TextServer.AutowrapMode.WordSmart
                });

                var configActions = new HBoxContainer { Name = "ConfigActions" };
                _agentView.AddChild(configActions);

                var configureButton = new Button { Name = "Configure", Text = "Configure" };
                configureButton.Pressed += () => OnConfigurePressed(agent, configPath);
                configActions.AddChild(configureButton);

                var removeButton = new Button { Name = "Remove", Text = "Remove" };
                removeButton.Pressed += () => OnRemovePressed(agent, configPath);
                configActions.AddChild(removeButton);
            }
            else
            {
                _statusLabel = null;
            }

            RefreshSnippet();
            RefreshStatus();
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

        /// <summary>Re-render the "Configured"/"Not configured" status label for the current agent.</summary>
        void RefreshStatus()
        {
            if (_current == null || _statusLabel == null)
                return;

            var configPath = ResolveConfigPath(_current);
            if (configPath == null)
                return;

            var configured = _current.IsConfigured(configPath, McpUrl);
            _statusLabel.Text = configured ? "Configured" : "Not configured";
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

        static Button MakeLinkButton(string name, string text, string url)
        {
            var button = new Button { Name = name, Text = text, TooltipText = url };
            button.Pressed += () => OS.ShellOpen(url);
            return button;
        }
    }
}
#endif
