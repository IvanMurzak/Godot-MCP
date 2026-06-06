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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// The dock's "Skills" section — the Godot <see cref="Control"/> analog of Unity-MCP's <c>TemplateSkillsSection</c>
    /// / <c>SetupSkillsUI</c>. A <see cref="VBoxContainer"/> the <see cref="GodotMcpDock"/> inserts into its Body
    /// BETWEEN the AI-agent card and the support footer. It reflects the SELECTED AI agent (read from the persisted
    /// <see cref="GodotMcpConfig.SelectedAgentId"/>): when that agent supports skills it renders a "Skills" header +
    /// the resolved skills output path (right-aligned, ellipsis, muted), an "Auto-generate" toggle bound to
    /// <see cref="GodotMcpConfig.GenerateSkillFiles"/> (persist + Save), a "Generate" button, and a last-result status
    /// line; when the agent does NOT support skills it shows a single muted "Skills not supported by this agent" line
    /// (gated on <see cref="GodotAgentConfigurator.SupportsSkills"/>, exactly like Unity gates its skills UI to Claude).
    ///
    /// <para>
    /// The generation ENGINE ships in the reused <c>com.IvanMurzak.McpPlugin</c> pin
    /// (<see cref="com.IvanMurzak.McpPlugin.IMcpPlugin.GenerateSkillFiles"/>); this panel only drives it. Manual
    /// Generate uses the swap-and-restore pattern (temporarily set the live config's <c>SkillsPath</c> +
    /// <c>ProjectRootPath</c> to the resolved destination, call the engine, restore) — mirroring Unity's
    /// <c>Tool_Skills.GenerateAll</c>. Auto-generate on addon load is driven separately from
    /// <see cref="GodotMcpConnection.Start"/> via <c>GenerateSkillFilesIfNeeded</c>.
    /// </para>
    ///
    /// <para>
    /// Editor-only (<c>#if TOOLS</c>): it constructs live Godot UI nodes and reads the live config off the threaded-in
    /// connection. The supported/path DECISION lives in the pure-managed <see cref="SkillsPlan"/> +
    /// <see cref="AgentConfigPaths"/> (CI-unit-tested); this class is verified via the headless Godot smoke
    /// (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class SkillsPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        VBoxContainer? _body;
        CheckBox? _autoGenerateToggle;
        Label? _pathLabel;
        Label? _statusLabel;

        /// <summary>
        /// Construct the section wired to the live <paramref name="connection"/> (it reads the selected agent + the
        /// auto-generate toggle off the connection's <see cref="GodotMcpConfig"/>, persists the toggle via the
        /// connection's <c>Save</c>, and drives the connection's live plugin to generate). Only built by the dock when
        /// a live connection exists (the AI-agent / features cards share the same connection-null guard).
        /// </summary>
        public SkillsPanel(GodotMcpConnection connection)
        {
            _connection = connection;
            Name = "Skills";
            BuildUi();
        }

        void BuildUi()
        {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            AddThemeConstantOverride("separation", 4);

            // --- Header row: "Skills" sub-label on the LEFT + the resolved path on the RIGHT (right-aligned,
            //     ellipsis, muted) — mirrors the AI-agent card's MCP-header row layout. ---
            var headerRow = new HBoxContainer { Name = "SkillsHeaderRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            headerRow.Alignment = BoxContainer.AlignmentMode.Center;
            AddChild(headerRow);

            var header = new Label { Name = "SkillsHeader", Text = "Skills" };
            DockStyle.ApplySubLabel(header);
            headerRow.AddChild(header);

            _pathLabel = new Label
            {
                Name = "SkillsPath",
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            DockStyle.ApplyConfigPath(_pathLabel);
            headerRow.AddChild(_pathLabel);

            // --- Swappable body: either the supported controls (toggle + Generate + status) or the unsupported line.
            _body = new VBoxContainer { Name = "SkillsBody", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            _body.AddThemeConstantOverride("separation", 4);
            AddChild(_body);

            RebuildBody();
        }

        /// <summary>
        /// Rebuild the body for the currently-selected agent's skills plan. Clears the previous controls synchronously
        /// (detach + free so a rebuild starts empty) and either renders the supported controls or the "not supported"
        /// muted line. Reads the selected agent off the persisted <see cref="GodotMcpConfig.SelectedAgentId"/> so the
        /// card always reflects the AI-agent card's dropdown selection (the dock's <see cref="GodotMcpDock.Refresh"/>
        /// re-runs this after a selection change persists).
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
            _autoGenerateToggle = null;
            _statusLabel = null;

            var plan = ResolvePlan();

            if (!plan.Supported)
            {
                if (_pathLabel != null)
                {
                    _pathLabel.Text = string.Empty;
                    _pathLabel.TooltipText = string.Empty;
                }

                var unsupported = new Label
                {
                    Name = "SkillsUnsupported",
                    Text = "Skills not supported by this agent."
                };
                DockStyle.ApplyDescription(unsupported);
                _body.AddChild(unsupported);
                return;
            }

            // Show the resolved skills output path in the header (full path as tooltip; the label ellipsis-truncates).
            if (_pathLabel != null)
            {
                _pathLabel.Text = plan.SkillsDir!;
                _pathLabel.TooltipText = plan.SkillsDir!;
            }

            // --- Auto-generate row: label on the LEFT + a CheckBox on the RIGHT bound to GenerateSkillFiles. ---
            var toggleRow = new HBoxContainer { Name = "AutoGenerateRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            toggleRow.Alignment = BoxContainer.AlignmentMode.Center;
            _body.AddChild(toggleRow);

            var toggleLabel = new Label { Name = "AutoGenerateLabel", Text = "Auto-generate", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            DockStyle.ApplyDescription(toggleLabel);
            toggleLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            toggleRow.AddChild(toggleLabel);

            _autoGenerateToggle = new CheckBox
            {
                Name = "AutoGenerateToggle",
                ButtonPressed = _connection.Config.GenerateSkillFiles
            };
            _autoGenerateToggle.Toggled += OnAutoGenerateToggled;
            toggleRow.AddChild(_autoGenerateToggle);

            // --- Generate button row (right-aligned primary action). ---
            var actionRow = new HBoxContainer { Name = "SkillsActionRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            actionRow.Alignment = BoxContainer.AlignmentMode.End;
            _body.AddChild(actionRow);

            var generateButton = new Button { Name = "Generate", Text = "Generate" };
            DockStyle.ApplyPrimaryButton(generateButton);
            generateButton.Pressed += OnGeneratePressed;
            actionRow.AddChild(generateButton);

            // --- Last-result status line (muted; turns amber on failure). ---
            _statusLabel = new Label { Name = "SkillsStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            DockStyle.ApplyDescription(_statusLabel);
            _body.AddChild(_statusLabel);
        }

        /// <summary>
        /// Persist the auto-generate toggle (the serialized <see cref="GodotMcpConfig.GenerateSkillFiles"/>) + Save.
        /// No generation is triggered here — the toggle only governs the BOOT-time
        /// <c>GenerateSkillFilesIfNeeded</c> path (in <see cref="GodotMcpConnection.Start"/>); the on-demand Generate
        /// button is the explicit action. Mirrors the dock's other persisted toggles (e.g. the Log Level dropdown).
        /// </summary>
        void OnAutoGenerateToggled(bool pressed)
        {
            if (_connection.Config.GenerateSkillFiles == pressed)
                return;

            _connection.Config.GenerateSkillFiles = pressed;
            _connection.Save();
        }

        /// <summary>
        /// On-demand "Generate": resolve the selected agent's skills directory, validate it, then call the live
        /// <see cref="com.IvanMurzak.McpPlugin.IMcpPlugin.GenerateSkillFiles"/> via the swap-and-restore pattern
        /// (temporarily point the live config's <c>SkillsPath</c> + <c>ProjectRootPath</c> at the resolved
        /// destination, generate, restore in <c>finally</c>) — mirroring Unity's <c>Tool_Skills.GenerateAll</c>. The
        /// destination directory is created if missing. The result is surfaced in the status line; failures never
        /// throw out of the UI handler.
        /// </summary>
        void OnGeneratePressed()
        {
            var plan = ResolvePlan();
            if (!plan.Supported || string.IsNullOrEmpty(plan.SkillsDir))
            {
                SetStatus("Skills are not supported by the selected agent.", error: true);
                return;
            }

            var skillsDir = plan.SkillsDir!;
            var projectRoot = ProjectRoot();

            // Defense-in-depth: the resolved dir is a fixed in-project relative path (`.claude/skills`), but validate
            // it stays inside the project root before writing (rejects an absolute-escape / `..` traversal).
            if (!IsSkillsDirInsideProject(skillsDir, projectRoot))
            {
                SetStatus("Refusing to generate: skills path escapes the project root.", error: true);
                return;
            }

            var plugin = _connection.Plugin;
            if (plugin == null)
            {
                SetStatus("Cannot generate: the MCP plugin is not initialized yet.", error: true);
                return;
            }

            try
            {
                if (!DirAccess.DirExistsAbsolute(skillsDir))
                {
                    var mkdir = DirAccess.MakeDirRecursiveAbsolute(skillsDir);
                    if (mkdir != Error.Ok)
                    {
                        SetStatus($"Could not create skills folder ({mkdir}).", error: true);
                        return;
                    }
                }

                var config = _connection.Config;
                var originalSkillsPath = config.SkillsPath;
                var originalProjectRoot = config.ProjectRootPath;
                bool ok;
                try
                {
                    config.SkillsPath = skillsDir;
                    config.ProjectRootPath = projectRoot;
                    ok = plugin.GenerateSkillFiles(skillsDir);
                }
                finally
                {
                    config.SkillsPath = originalSkillsPath;
                    config.ProjectRootPath = originalProjectRoot;
                }

                if (ok)
                    SetStatus($"Generated skills in {skillsDir}.", error: false);
                else
                    SetStatus("Skill generation reported no changes (or failed) — check the Output.", error: true);
            }
            catch (Exception ex)
            {
                // Never let a generation failure escape the editor UI handler; surface it in the status + Output.
                GD.PushError($"[Godot-MCP] skill generation failed: {ex.Message}");
                SetStatus($"Skill generation failed: {ex.Message}", error: true);
            }
        }

        /// <summary>Set the last-result status line text + colour (muted on success, amber on failure).</summary>
        void SetStatus(string text, bool error)
        {
            if (_statusLabel == null)
                return;

            _statusLabel.Text = text;
            _statusLabel.AddThemeColorOverride(
                "font_color",
                error ? DockStyle.Rgb(DockTheme.WarningText) : DockStyle.Rgb(DockTheme.ColorDescriptionMuted));
        }

        /// <summary>
        /// Re-sync the card when the selected agent (or anything it depends on) changes — forwarded from
        /// <see cref="GodotMcpDock.Refresh"/>, which the AI-agent card's selection-change path triggers. Rebuilds the
        /// body so a switch to/from a skills-capable agent flips the supported-state, path, and controls. The
        /// last-result status is intentionally reset by the rebuild (a stale "generated" line would be misleading
        /// after switching agents).
        /// </summary>
        public void Refresh() => RebuildBody();

        // --- live resolution off the connection config / editor ----------------------------------------------

        /// <summary>Resolve the skills plan for the persisted-selected agent against the live editor path values.</summary>
        SkillsPlan ResolvePlan()
        {
            var agent = GodotAgentConfiguratorRegistry.GetByAgentId(_connection.Config.SelectedAgentId);
            var os = MapOs(OS.GetName());
            var home = OS.GetEnvironment("USERPROFILE");
            if (string.IsNullOrEmpty(home))
                home = OS.GetEnvironment("HOME");
            var appData = OS.GetEnvironment("APPDATA");
            var projectRoot = ProjectRoot();

            return SkillsPlan.Resolve(agent, os, home, appData, projectRoot);
        }

        /// <summary>The absolute Godot project root (<c>res://</c> globalized, trailing slash stripped).</summary>
        static string ProjectRoot() => ProjectSettings.GlobalizePath("res://").TrimEnd('/');

        /// <summary>
        /// True when <paramref name="skillsDir"/> resolves to a location INSIDE <paramref name="projectRoot"/>. A
        /// last-line guard against an absolute-escape / <c>..</c> traversal before writing (the resolved dir is the
        /// fixed `.claude/skills` today, so this is belt-and-suspenders). Compares normalized full paths.
        /// </summary>
        static bool IsSkillsDirInsideProject(string skillsDir, string projectRoot)
        {
            try
            {
                var rootFull = System.IO.Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
                var dirFull = System.IO.Path.GetFullPath(skillsDir).Replace('\\', '/').TrimEnd('/');
                return dirFull == rootFull || dirFull.StartsWith(rootFull + "/", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
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
