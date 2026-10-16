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
using System.Threading.Tasks;
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
    /// <b>Cloud project key</b> (project-keys contract §7, owner rulings 2026-09-23): in Cloud mode Configure
    /// writes this project's non-expiring, pin-bound key (<c>Authorization: Bearer agd_pk_…</c>) for EVERY agent.
    /// The key is obtained with <see cref="ProjectKeyProvider.GetOrMintAsync"/> OFF the main thread (it does
    /// network I/O and can wait up to ~75 s on the cross-process credential lock) and the result is marshalled
    /// back with <c>CallDeferred</c> before any config is written. "Regenerate key" mints a fresh key (revoking the
    /// old one) and rewrites every configured agent. No machine login, or a failed mint (e.g. the server's 404
    /// while the feature is off), degrades to the URL-only OAuth config — never an error. The key itself is never
    /// rendered: the manual-configuration snippets use <see cref="AgentConfig.AgentConfiguratorSettings.ForDisplay"/>.
    /// </para>
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
        // = null! suppresses CS8618 for the Godot-required parameterless ctor (below): Godot's hot-reload
        // re-instantiates [Tool] scripts via new() and cannot set this readonly field; the parameterized
        // ctor assigns the real value for the live-wired instance.
        readonly GodotMcpConnection _connection = null!;

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

        // Cloud project-key state (panel-wide, survives agent switches). _projectKey is the key Configure writes
        // (from the local cache or the last get-or-mint / regenerate) — never logged or rendered.
        string? _projectKey;
        bool _projectKeyBusy;
        bool _projectKeyUnavailable;
        bool _projectKeyRegenerateFailed;
        // The agent a Configure press is waiting to write once the off-thread get-or-mint returns.
        AgentConfig.AiAgentConfigurator? _pendingConfigure;
        Label? _projectKeyLabel;
        Button? _regenerateKeyButton;
        // The v2 routing pin — derived from the project root only, so constant for the editor session.
        string? _projectPin;

        /// <summary>Which off-thread project-key operation a <see cref="OnProjectKeyResolved"/> result belongs to.</summary>
        enum ProjectKeyOperation
        {
            Configure = 0,
            Regenerate = 1,
        }

        /// <summary>Engine id sent with a project-key mint (contract §2).</summary>
        const string ProjectKeyEngine = "godot";

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
            LoadCachedProjectKey();
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
            _projectKeyLabel = null;
            _regenerateKeyButton = null;

            BuildAgentView(_current);
        }

        void BuildAgentView(AgentConfig.AiAgentConfigurator agent)
        {
            if (_agentView == null)
                return;

            // ForDisplay: the manual-configuration snippets show the SAME shape Configure writes (the Authorization
            // header is present when a project key is in use) with the key itself redacted.
            var settings = CurrentSettings().ForDisplay();
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

            // Cloud project key: status line + "Regenerate key" (rewrites every configured agent).
            BuildProjectKeyRow();

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
            var config = agent.GetHttpConfig(settings, Logger, CurrentCredentialMode());

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
            _removeButton = new Button
            {
                Name = "Remove",
                Text = "Remove",
                TooltipText = AgentConfiguratorCredentialPolicy.RemoveTooltip(agent.AgentName)
            };
            DockStyle.ApplyAlertButton(_removeButton);
            DockStyle.ConnectPressed(_removeButton, this, MethodName.OnRemoveButtonPressed);
            configActions.AddChild(_removeButton);

            _configureButton = new Button
            {
                Name = "Configure",
                Text = "Configure",
                TooltipText = AgentConfiguratorCredentialPolicy.ConfigureTooltip(agent.AgentName)
            };
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
        /// The effective <see cref="AgentConfig.HttpCredentialMode"/> for the current agent, per the pure-managed
        /// <see cref="AgentConfiguratorCredentialPolicy"/> — always the same answer as the shared snapshot's own
        /// <c>ResolveHttpCredentialMode()</c>, so the written file, the status check and the manual steps (which
        /// McpPlugin 8.5 renders from <c>WritesHttpBearer</c>) agree. (The former "Advanced: use access token"
        /// opt-in was removed: it wrote a Bearer header the manual steps no longer showed, and in Cloud the
        /// project key replaces it.) Drives both
        /// the settings snapshot (<see cref="CurrentSettings"/>) and the explicit mode passed to
        /// <c>GetHttpConfig</c> on Configure / Remove.
        ///
        /// <para>
        /// mcp-authorize g5: when the LOCAL server is running Custom-mode <c>token</c> auth it is Bearer-gated,
        /// so the written AI-client config MUST carry <c>Authorization: Bearer &lt;local-secret&gt;</c> to reach
        /// it — force <see cref="AgentConfig.HttpCredentialMode.AccessToken"/> regardless of the (Cloud-oriented)
        /// OAuth advanced toggle, keeping the config-writer credential mode in agreement with the launch-side
        /// auth mode. The <c>none</c> (anonymous) and <c>oauth</c> (native MCP OAuth) modes stay URL-only via the
        /// default policy.
        /// </para>
        /// </summary>
        AgentConfig.HttpCredentialMode CurrentCredentialMode() => CredentialModeFor(_current);

        /// <summary>The effective credential mode for <paramref name="agent"/> (see <see cref="CurrentCredentialMode"/>).</summary>
        AgentConfig.HttpCredentialMode CredentialModeFor(AgentConfig.AiAgentConfigurator? agent) =>
            AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                _connection.Config.ActiveMode,
                _connection.Config.ActiveAuthOption,
                agent?.SupportsOAuth ?? true,
                useAccessToken: false,
                HasProjectKey);

        /// <summary>
        /// Configure-button <c>pressed</c> handler (object+method Callable). Writes the addon's HTTP entry into the
        /// current agent's config file via the shared config's <c>Configure()</c> (REAL credential; never logged),
        /// then re-evaluates the status + alert. In Cloud mode with a machine login it first gets (or mints) the
        /// project key OFF the main thread and writes once the result is marshalled back.
        /// </summary>
        public void OnConfigureButtonPressed()
        {
            if (_current == null || _projectKeyBusy)
                return;

            if (IsCloudMode)
            {
                if (_connection.Account.IsSignedIn)
                {
                    StartProjectKeyOperation(ProjectKeyOperation.Configure, _current);
                    return;
                }
                _projectKey = null; // no machine login ⇒ the URL-only OAuth config (contract §6)
            }

            WriteConfig(_current);
            RefreshStatus();
        }

        /// <summary>Remove-button <c>pressed</c> handler (object+method Callable). Removes the addon's entry via <c>Unconfigure()</c>.</summary>
        public void OnRemoveButtonPressed()
        {
            if (_current == null || _projectKeyBusy)
                return;
            var config = _current.GetHttpConfig(CurrentSettings(), Logger, CurrentCredentialMode());
            config.Unconfigure();
            RefreshStatus();
        }

        /// <summary>
        /// "Regenerate key" <c>pressed</c> handler (object+method Callable): mint a fresh project key off the main
        /// thread (the provider overwrites the cache entry and revokes the key it replaced), then rewrite every
        /// agent that already has a config entry.
        /// </summary>
        public void OnRegenerateKeyButtonPressed()
        {
            if (_projectKeyBusy || !IsCloudMode || !_connection.Account.IsSignedIn)
                return;
            StartProjectKeyOperation(ProjectKeyOperation.Regenerate, null);
        }

        /// <summary>Write <paramref name="agent"/>'s HTTP config for the current connection + project-key state.</summary>
        void WriteConfig(AgentConfig.AiAgentConfigurator agent)
            => agent.GetHttpConfig(SettingsFor(agent), Logger, CredentialModeFor(agent)).Configure();

        /// <summary>
        /// Run a project-key get-or-mint / regenerate OFF the editor main thread (it does network I/O and may wait
        /// up to ~75 s on the cross-process credential lock) and marshal the result back with <c>CallDeferred</c>
        /// to <see cref="OnProjectKeyResolved"/>. Everything touching Godot APIs (project root, pin) is resolved
        /// here, on the main thread, before the hop.
        /// </summary>
        void StartProjectKeyOperation(ProjectKeyOperation operation, AgentConfig.AiAgentConfigurator? pendingAgent)
        {
            var provider = _connection.Account.GetProjectKeyProvider(_connection.CloudBaseUrl);
            var pin = ProjectPin;
            var label = ProjectRoot;
            var machineName = System.Environment.MachineName;

            _projectKeyBusy = true;
            _pendingConfigure = pendingAgent;
            RefreshProjectKeyRow();

            _ = Task.Run(async () =>
            {
                string? key = null;
                var failure = string.Empty;
                try
                {
                    key = operation == ProjectKeyOperation.Regenerate
                        ? await provider.RegenerateAsync(pin, ProjectKeyEngine, machineName, label).ConfigureAwait(false)
                        : await provider.GetOrMintAsync(pin, ProjectKeyEngine, machineName, label).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // Any failure degrades to the URL-only config (contract §6); logged on the main thread.
                    failure = $"{ex.GetType().Name}: {ex.Message}";
                }

                try
                {
                    if (IsInstanceValid(this))
                        CallDeferred(MethodName.OnProjectKeyResolved, key ?? string.Empty, (int)operation, failure);
                }
                catch (ObjectDisposedException)
                {
                    // The panel was freed (dock rebuilt / hot reload) while the key was in flight — nothing to update.
                }
            });
        }

        /// <summary>
        /// Main-thread continuation of <see cref="StartProjectKeyOperation"/> (reached via <c>CallDeferred</c>):
        /// adopt the key (empty ⇒ none), then write the pending agent's config (Configure) or rewrite every agent
        /// that already has an entry (Regenerate), and refresh the status. A failed Regenerate leaves the previous
        /// key and every config untouched.
        /// </summary>
        public void OnProjectKeyResolved(string key, int operation, string failure)
        {
            if (!string.IsNullOrEmpty(failure))
                GodotMcpLog.Warning($"[Godot-MCP] project key {(ProjectKeyOperation)operation} failed: {failure}");
            _projectKeyBusy = false;
            var pending = _pendingConfigure;
            _pendingConfigure = null;
            var resolved = string.IsNullOrEmpty(key) ? null : key;

            if ((ProjectKeyOperation)operation == ProjectKeyOperation.Regenerate)
            {
                _projectKeyRegenerateFailed = resolved == null;
                if (resolved == null)
                {
                    RefreshProjectKeyRow(); // nothing was written
                    return;
                }
                _projectKey = resolved;
                _projectKeyUnavailable = false;
                RewriteConfiguredAgents();
            }
            else
            {
                _projectKey = resolved;
                _projectKeyUnavailable = resolved == null;
                _projectKeyRegenerateFailed = false;
                if (pending != null)
                    WriteConfig(pending);
            }

            // Rebuild the agent view: the manual-configuration sections render the credential shape, which the
            // key change (header present / absent) just altered.
            RebuildCurrentAgentView();
        }

        /// <summary>Rebuild the currently selected agent's view (falls back to a status refresh when none is selected).</summary>
        void RebuildCurrentAgentView()
        {
            var index = _agentSelector?.GetSelectedId() ?? -1;
            if (index >= 0)
                ShowAgent(index);
            else
                RefreshStatus();
        }

        /// <summary>
        /// Rewrite every agent that already has a config entry (the ones a regenerated key must reach), so no
        /// config keeps the revoked key. Agents without an entry are left alone — Regenerate never configures a
        /// new agent. One agent's failure does not stop the others.
        /// </summary>
        void RewriteConfiguredAgents()
        {
            // With a Cloud key every agent resolves the same credential mode, so one snapshot serves them all.
            var settings = CurrentSettings();
            var mode = CurrentCredentialMode();
            foreach (var agent in GodotAgentConfigurators.All)
            {
                if (!HasDetectableConfig(agent))
                    continue;
                try
                {
                    if (agent.IsDetected(settings, Logger))
                        agent.GetHttpConfig(settings, Logger, mode).Configure();
                }
                catch (Exception ex)
                {
                    GodotMcpLog.Warning($"[Godot-MCP] could not rewrite the {agent.AgentName} config with the new project key: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Adopt the project key the local cache (<c>~/.ai-game-dev/project-keys.json</c>) already holds for this
        /// project and signed-in account, so the status check compares against the key Configure would write —
        /// WITHOUT network or minting (a lock-free read). Anything else (not Cloud, signed out, no entry, another
        /// account's entry, unreadable cache) leaves no key.
        /// </summary>
        void LoadCachedProjectKey()
        {
            if (_projectKeyBusy)
                return;
            _projectKey = null;
            if (!IsCloudMode || !_connection.Account.IsSignedIn)
                return;
            try
            {
                var provider = _connection.Account.GetProjectKeyProvider(_connection.CloudBaseUrl);
                var entry = provider.Store.Get(provider.Issuer, ProjectPin);
                // Another account's entry is never adopted. When the credential records no subject the entry is
                // still adopted for the status display — Configure always re-validates via GetOrMintAsync.
                var subject = _connection.Account.Subject;
                if (entry != null && (subject == null || entry.Sub == subject))
                    _projectKey = string.IsNullOrEmpty(entry.Key) ? null : entry.Key;
            }
            catch (Exception ex)
            {
                GodotMcpLog.Warning($"[Godot-MCP] could not read the project-key cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Build the Cloud project-key row: a status line (whether a key is in use) and the "Regenerate key"
        /// button. Cloud mode only — local-server configs never carry a project key.
        /// </summary>
        void BuildProjectKeyRow()
        {
            if (_agentBody == null || !IsCloudMode)
                return;

            var row = new HBoxContainer { Name = "ProjectKeyRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.Alignment = BoxContainer.AlignmentMode.Center;
            _agentBody.AddChild(row);

            _projectKeyLabel = new Label { Name = "ProjectKeyStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            DockStyle.ApplyDescription(_projectKeyLabel);
            _projectKeyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            row.AddChild(_projectKeyLabel);

            _regenerateKeyButton = new Button
            {
                Name = "RegenerateKey",
                Text = AgentConfiguratorCredentialPolicy.RegenerateKeyLabel,
                TooltipText = AgentConfiguratorCredentialPolicy.RegenerateKeyTooltip
            };
            DockStyle.ApplySecondaryButton(_regenerateKeyButton);
            DockStyle.ConnectPressed(_regenerateKeyButton, this, MethodName.OnRegenerateKeyButtonPressed);
            row.AddChild(_regenerateKeyButton);
            // Text + enabled state are driven by RefreshProjectKeyRow().
        }

        /// <summary>Re-render the project-key status line + "Regenerate key" enabled state from the current state.</summary>
        void RefreshProjectKeyRow()
        {
            var status = AgentConfiguratorCredentialPolicy.ResolveProjectKeyStatus(
                _connection.Config.ActiveMode,
                _connection.Account.IsSignedIn,
                _projectKeyBusy,
                HasProjectKey,
                _projectKeyUnavailable,
                _projectKeyRegenerateFailed);

            if (_projectKeyLabel != null)
                _projectKeyLabel.Text = AgentConfiguratorCredentialPolicy.DescribeProjectKeyStatus(status) ?? string.Empty;
            if (_regenerateKeyButton != null)
                _regenerateKeyButton.Disabled = !AgentConfiguratorCredentialPolicy.CanRegenerateKey(status);
            if (_configureButton != null)
                _configureButton.Disabled = _projectKeyBusy;
            if (_removeButton != null)
                _removeButton.Disabled = _projectKeyBusy;
            // The Setup/Reconfigure alert's button runs Configure too — hide it while a key operation is in flight
            // (the status line says "working…").
            if (_alertHost != null)
                _alertHost.Visible = !_projectKeyBusy;
        }

        /// <summary>True in Cloud mode (the only mode a project key applies to).</summary>
        bool IsCloudMode => _connection.Config.ActiveMode == GodotMcpConnectionMode.Cloud;

        /// <summary>True when a Cloud project key is in use for the configs this panel writes.</summary>
        bool HasProjectKey => IsCloudMode && !string.IsNullOrEmpty(_projectKey);

        /// <summary>
        /// Re-render the Configure/Remove status AND the Setup/Reconfiguration alert for the current agent (only
        /// agents WITH a config-file path have these). Drives the "Configured (http)"/"Not configured" label, flips
        /// the Configure button to "Reconfigure" when configured, shows Remove only when an entry exists, and
        /// (re)builds the amber alert per the shared three-state <see cref="AgentConfig.ConfiguratorStatus"/>.
        /// </summary>
        void RefreshStatus()
        {
            RefreshProjectKeyRow();
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

            var panel = DockStyle.AlertPanel(
                "AgentAlert", title, message, button, OnConfigureButtonPressed,
                _current != null ? AgentConfiguratorCredentialPolicy.ConfigureTooltip(_current.AgentName) : null);
            _alertHost.AddChild(panel);
        }

        /// <summary>
        /// Re-sync the section when the connection URL/token/mode changes (forwarded from
        /// <see cref="GodotMcpDock.Refresh"/>). The DTO sections embed the live URL/token snapshot, so a full agent
        /// rebuild is the simplest correct refresh; the Skills section re-evaluates inside it.
        /// </summary>
        public void Refresh()
        {
            // Mode / sign-in may have changed: re-read the cached key (no network) before re-rendering.
            LoadCachedProjectKey();
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

        /// <summary>
        /// A fresh shared-settings snapshot bridging the live Godot connection state (URL/token/mode/auth) for the
        /// effective <see cref="CurrentCredentialMode"/> — URL-only on the default OAuth path, token-bearing when
        /// the "Advanced: use access token" opt-in (or a non-OAuth configurator) is in force.
        /// </summary>
        AgentConfig.AgentConfiguratorSettings CurrentSettings() => SettingsFor(_current);

        /// <summary>The settings snapshot for <paramref name="agent"/>, carrying the Cloud project key when one is in use.</summary>
        AgentConfig.AgentConfiguratorSettings SettingsFor(AgentConfig.AiAgentConfigurator? agent) =>
            AgentConfiguratorSettingsFactory.Create(_connection.Config, CredentialModeFor(agent), _projectKey);

        /// <summary>This project's v2 routing pin (computed once — it depends only on the project root).</summary>
        string ProjectPin => _projectPin ??= CurrentSettings().ProjectPin;

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
