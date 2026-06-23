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
    /// (gated on the shared <c>AiAgentConfigurator.SupportsSkills</c>, exactly like Unity gates its skills UI).
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
    /// <see cref="SkillsPathUtils"/> (CI-unit-tested); this class is verified via the headless Godot smoke
    /// (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [Tool]
    public partial class SkillsPanel : VBoxContainer
    {
        readonly GodotMcpConnection _connection;

        VBoxContainer? _body;
        DockCheckBox? _autoGenerateToggle;
        Label? _pathLabel;
        Label? _statusLabel;

        /// <summary>
        /// Construct the section wired to the live <paramref name="connection"/> (it reads the selected agent + the
        /// auto-generate toggle off the connection's <see cref="GodotMcpConfig"/>, persists the toggle via the
        /// connection's <c>Save</c>, and drives the connection's live plugin to generate). Only built by the dock when
        /// a live connection exists (the AI-agent / features cards share the same connection-null guard).
        /// </summary>
        /// <summary>
        /// Parameterless ctor for Godot's C# hot-reload bridge (godotengine/godot#51626): a "Build Project"
        /// reload re-instantiates every live [Tool] script via its parameterless ctor, so a parameter-only
        /// class throws MissingMemberException ("does not define a parameterless constructor") and breaks the
        /// reload (it crashed the editor before this was added). The reloaded plugin re-adds a FRESH, wired
        /// dock (see GodotMcpPlugin's reload re-entry), so this re-instantiated shell is a discarded orphan —
        /// it only has to exist without faulting.
        /// </summary>
        public SkillsPanel() { }

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

            // "Skills" header with the underline (Unity's `labelSkillsHeader class="timeline-label"`), matching the
            // MCP header — a 13px label with a bottom-border underline hugging the text.
            headerRow.AddChild(DockStyle.UnderlinedSubLabel("SkillsHeader", "Skills"));

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

            // Show the resolved skills output path in the header project-root-relative (e.g. `.claude/skills`); keep the
            // full absolute path as the tooltip. The label still ellipsis-truncates if the relative form is long.
            if (_pathLabel != null)
            {
                _pathLabel.Text = SkillsPathUtils.ToDisplayPath(plan.SkillsDir!, ProjectRoot());
                _pathLabel.TooltipText = plan.SkillsDir!;
            }

            // --- Auto-generate toggle + Generate on ONE row (Unity's skills row, space-between): a left group of
            //     the "Auto-generate" label + checkbox, then the compact "Generate" button on the right. ---
            var controlRow = new HBoxContainer { Name = "SkillsControlRow", SizeFlagsHorizontal = SizeFlags.ExpandFill };
            controlRow.Alignment = BoxContainer.AlignmentMode.Center;
            _body.AddChild(controlRow);

            var toggleLabel = new Label { Name = "AutoGenerateLabel", Text = "Auto-generate" };
            DockStyle.ApplyDescription(toggleLabel);
            toggleLabel.AutowrapMode = TextServer.AutowrapMode.Off;
            controlRow.AddChild(toggleLabel);

            _autoGenerateToggle = new DockCheckBox
            {
                Name = "AutoGenerateToggle",
                ButtonPressed = _connection.Config.GenerateSkillFiles
            };
            // Object+method Callable on the checkbox instance (no delegate += into the ManagedCallable registry).
            _autoGenerateToggle.BindToggled(OnAutoGenerateToggled);
            _autoGenerateToggle.Connect(BaseButton.SignalName.Toggled, new Callable(_autoGenerateToggle, DockCheckBox.MethodName.OnToggled));
            controlRow.AddChild(_autoGenerateToggle);

            controlRow.AddChild(new Control { Name = "SkillsSpacer", SizeFlagsHorizontal = SizeFlags.ExpandFill });

            var generateButton = new Button { Name = "Generate", Text = "Generate" };
            DockStyle.ApplySecondaryButton(generateButton); // compact gray (Unity's .btn-compact), not the big primary
            DockStyle.ConnectPressed(generateButton, this, MethodName.OnGeneratePressed);
            controlRow.AddChild(generateButton);

            // --- Last-result status line (muted; turns amber on failure). HIDDEN until there's a result, so the
            //     empty line doesn't add a large gap between the Skills row and the MCP config line below. ---
            _statusLabel = new Label { Name = "SkillsStatus", SizeFlagsHorizontal = SizeFlags.ExpandFill, Visible = false };
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
        public void OnGeneratePressed()
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

            _statusLabel.Visible = true; // reveal the line now that there's a result to show
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

        /// <summary>Resolve the skills plan for the persisted-selected agent (shared registry) against the live project root.</summary>
        SkillsPlan ResolvePlan()
        {
            var agent = GodotAgentConfigurators.GetByAgentId(_connection.Config.SelectedAgentId);
            return SkillsPlan.Resolve(agent, ProjectRoot());
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

        // No KeepAlive teardown is needed: every signal here is an OBJECT+METHOD Callable (the auto-generate
        // checkbox connects to its own instance method; the Generate button connects to this panel's instance
        // method), which is not a ManagedCallable and never enters the native registry the hot-reload iterates.
        // The target controls are kept alive by the tree and freed with the panel.
    }
}
#endif
