/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.McpPlugin;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) decision for how the live
    /// <see cref="ConnectionConfig"/> must be primed for the reused <c>McpPlugin</c> CONSTRUCTOR's
    /// auto-skill-generation, BEFORE <c>McpPluginBuilder.Build()</c> runs.
    ///
    /// <para>
    /// Why this exists (issue: ctor skill-gen exception on editor boot). McpPlugin <b>6.10.0</b> removed the
    /// silent <c>Environment.CurrentDirectory</c> fallback (upstream issue #107): its ctor now calls
    /// <c>GenerateSkillFilesIfNeeded()</c> at construction time and, when <c>GenerateSkillFiles == true</c> with a
    /// RELATIVE <c>SkillsPath</c> (the built-in default <c>"SKILLS"</c>) and an unset
    /// <see cref="ConnectionConfig.ProjectRootPath"/>, throws
    /// <c>InvalidOperationException: "Cannot resolve relative SkillsPath 'SKILLS' …"</c>. The ctor catches+logs it,
    /// so it is non-fatal — but it spams an ERROR + stack on every boot and the ctor-time generation never runs.
    /// </para>
    ///
    /// <para>
    /// The addon already generates skills correctly AFTER <c>Build()</c> via
    /// <c>GodotMcpConnection.MaybeAutoGenerateSkills</c> (a swap-and-restore that targets the resolved
    /// per-agent <c>&lt;project&gt;/.claude/skills</c> dir). The fix is to make the ctor-time generation land at the
    /// SAME destination instead of throwing — by setting an ABSOLUTE <c>SkillsPath</c> (so
    /// <c>ResolveSkillsPath</c> takes the rooted branch and never needs <c>ProjectRootPath</c>) plus
    /// <see cref="ConnectionConfig.ProjectRootPath"/> on the live config before <c>Build()</c>. When the selected
    /// agent does NOT support skills we instead disable ctor-time generation, mirroring
    /// <c>MaybeAutoGenerateSkills</c>'s early-return — so no stray <c>SKILLS/</c> directory is ever written.
    /// </para>
    ///
    /// <para>
    /// This decision reuses <see cref="SkillsPlan.Resolve"/>, so the ctor-time destination can never diverge from
    /// the post-build auto-generate destination. The Godot dependency (resolving <c>res://</c> to the absolute
    /// project root) stays in the editor-coupled <c>GodotMcpConnection.Start</c> call site; the supported/path
    /// decision is pure-managed here and unit-tested in the plain-xUnit host.
    /// </para>
    /// </summary>
    public static class SkillsBootstrap
    {
        /// <summary>
        /// Prime <paramref name="config"/> for the McpPlugin ctor's auto-skill-generation so it resolves to the
        /// selected agent's <c>&lt;project&gt;/.claude/skills</c> destination instead of throwing on the relative
        /// default <c>SkillsPath</c>. Idempotent and side-effect-free beyond the two/one property writes; never
        /// touches disk (the actual generation is McpPlugin's, post-Build).
        ///
        /// <list type="bullet">
        ///   <item>When <paramref name="config"/>'s <see cref="ConnectionConfig.GenerateSkillFiles"/> is already OFF,
        ///   this is a no-op — the ctor will not generate, so there is nothing to prime.</item>
        ///   <item>When the selected agent SUPPORTS skills, sets <see cref="ConnectionConfig.SkillsPath"/> to the
        ///   ABSOLUTE resolved skills dir and <see cref="ConnectionConfig.ProjectRootPath"/> to
        ///   <paramref name="projectRoot"/>. The absolute <c>SkillsPath</c> alone removes the throw; the project
        ///   root is set too so the live config is internally consistent for the post-build swap-and-restore.</item>
        ///   <item>When the selected agent does NOT support skills (or none is selected), turns
        ///   <see cref="ConnectionConfig.GenerateSkillFiles"/> OFF so the ctor's generation is skipped — matching
        ///   <c>MaybeAutoGenerateSkills</c>'s "unsupported → return" behaviour, and guaranteeing no stray
        ///   relative-path directory is written.</item>
        /// </list>
        /// </summary>
        /// <param name="config">The live connection config that will be handed to <c>McpPluginBuilder.SetConfig</c>.</param>
        /// <param name="agent">The selected AI-agent configurator (may be null when none is selected).</param>
        /// <param name="projectRoot">The absolute project root (e.g. <c>ProjectSettings.GlobalizePath("res://").TrimEnd('/')</c>).</param>
        public static void PrimeForCtorSkillGeneration(ConnectionConfig config, AgentConfig.AiAgentConfigurator? agent, string projectRoot)
        {
            if (config == null)
                return;

            // Respect a user/persisted OFF toggle: if ctor-time generation is already disabled there is nothing to
            // prime (and we must not silently re-enable it).
            if (!config.GenerateSkillFiles)
                return;

            var plan = SkillsPlan.Resolve(agent, projectRoot);
            if (!plan.Supported || string.IsNullOrEmpty(plan.SkillsDir))
            {
                // No skills destination for the selected agent — disable ctor-time generation so McpPlugin's ctor
                // skips it (instead of throwing on the relative default path or writing a stray SKILLS/ dir). The
                // addon's post-build MaybeAutoGenerateSkills also returns early for an unsupported agent, so this
                // keeps the two paths consistent.
                config.GenerateSkillFiles = false;
                return;
            }

            // Point the ctor-time generation at the SAME absolute destination the post-build auto-generate uses.
            // An absolute SkillsPath makes McpPlugin.ResolveSkillsPath take its rooted branch (Path.GetFullPath),
            // so it no longer requires ProjectRootPath — but we set ProjectRootPath too for config consistency.
            config.SkillsPath = plan.SkillsDir!;
            config.ProjectRootPath = projectRoot;
        }
    }
}
