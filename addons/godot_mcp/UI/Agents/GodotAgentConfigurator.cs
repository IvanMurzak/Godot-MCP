/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Abstract base for an AI-agent configurator — the Godot analog of Unity-MCP's
    /// <c>AiAgentConfigurator</c>, condensed to the HTTP-only client shape Godot-MCP needs. Godot ships NO server
    /// binary: the plugin is a CLIENT of the shared/cloud MCP server, so every configurator emits ONLY the HTTP
    /// form of an MCP-client entry (pointing the AI client at the SAME server the plugin uses, plus the bearer
    /// token). There is no stdio "launch the server" form here.
    ///
    /// <para>
    /// This base is PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>): it carries the agent's metadata
    /// (name / id / urls), the JSON-nav knobs (<see cref="BodyPath"/> / <see cref="ServerKey"/>), and delegates all
    /// snippet/file work to the pure-managed <see cref="AgentConfigJson"/> helper. The dock UI that renders a
    /// configurator (<c>AgentConfiguratorsPanel.cs</c>) is <c>#if TOOLS</c> and reads from these members; keeping
    /// the base outside the guard makes the registry + concrete configurators CI-unit-testable.
    /// </para>
    ///
    /// <para>
    /// <b>To add a new AI agent:</b> create a <see cref="GodotAgentConfigurator"/> subclass under <c>Impl/</c> and
    /// add one line to <see cref="GodotAgentConfiguratorRegistry"/>. That is the entire scaling contract.
    /// </para>
    /// </summary>
    public abstract class GodotAgentConfigurator
    {
        /// <summary>Display name of the AI agent (shown in the dock dropdown and the panel header).</summary>
        public abstract string AgentName { get; }

        /// <summary>Stable unique id (used for the persisted selected-agent value and registry lookups).</summary>
        public abstract string AgentId { get; }

        /// <summary>The agent's download / docs URL (opened by the panel's Download button).</summary>
        public abstract string DownloadUrl { get; }

        /// <summary>Optional tutorial URL (the panel hides its Tutorial button when null/empty).</summary>
        public virtual string? TutorialUrl => null;

        /// <summary>Optional icon file name for the panel (null = no icon). Display-only metadata.</summary>
        public virtual string? IconFileName => null;

        // --- Per-agent help metadata (parity with Unity-MCP's *Configurator.OnUICreated) -----------------------
        // Each member below carries the agent-specific guidance the dock's per-agent view renders: a short
        // description, an optional warning banner, and the collapsible "Manual Configuration Steps" /
        // "Troubleshooting" foldouts. PURE-MANAGED (plain strings/lists, no Godot native types, no #if TOOLS), so
        // the panel reads them and the registry can be CI-unit-tested for "ported agents carry non-empty help".
        // The strings are adapted from the corresponding Unity Impl/*Configurator.OnUICreated, dropping the
        // stdio-only specifics (Godot-MCP is HTTP-only) and reworded Unity -> Godot.

        /// <summary>
        /// A short one-or-two-line description shown under the agent name (the analog of Unity's
        /// <c>ContainerUnderHeader</c> description labels). Null = no description line.
        /// </summary>
        public virtual string? Description => null;

        /// <summary>
        /// An optional warning banner shown prominently under the agent name (the analog of Unity's
        /// <c>TemplateWarningLabel</c> / <c>TemplateAlertLabel</c>). Null = no warning. Use this for "IMPORTANT: …"
        /// guidance (e.g. Claude Desktop's "prefer Claude Code", VS Code's "start the server manually each time").
        /// </summary>
        public virtual string? WarningText => null;

        /// <summary>
        /// The ordered "Manual Configuration Steps" the panel renders inside a collapsible foldout. Empty = the
        /// foldout is omitted. Each string is one step; they describe how to apply the addon's HTTP entry to this
        /// agent's config file (the analog of Unity's <c>TemplateFoldout("Manual Configuration Steps")</c> body).
        /// </summary>
        public virtual IReadOnlyList<string> ManualSteps => System.Array.Empty<string>();

        /// <summary>
        /// The ordered "Troubleshooting" tips the panel renders inside a collapsible foldout. Empty = the foldout
        /// is omitted. Each string is one tip (the analog of Unity's <c>TemplateFoldout("Troubleshooting")</c> body).
        /// </summary>
        public virtual IReadOnlyList<string> Troubleshooting => System.Array.Empty<string>();

        /// <summary>
        /// The entry name the addon's server is written under inside the client config (the key of the
        /// <see cref="BodyPath"/> object). Defaults to <c>ai-game-developer</c>; mirrors the product name so a user
        /// scanning their config recognizes it. Subclasses rarely override this.
        /// </summary>
        public virtual string ServerKey => "ai-game-developer";

        /// <summary>
        /// The JSON top-level nav under which servers are listed in this agent's config format. Almost every MCP
        /// client uses <c>mcpServers</c>; VS Code uses <c>servers</c>. Subclasses override this when they differ.
        /// </summary>
        public virtual string BodyPath => "mcpServers";

        /// <summary>
        /// The per-OS absolute config-file path for this agent, or <c>null</c> when the agent has no on-disk config
        /// the addon can write (the Custom configurator) — a null path means the panel shows the copyable snippet
        /// only, with no Configure/Remove buttons. Implementations resolve the path from the injected OS family /
        /// home / appData / projectRoot via <see cref="AgentConfigPaths"/> so they stay pure-managed.
        /// </summary>
        /// <param name="os">The host OS family (injected; the editor maps <c>Godot.OS.GetName()</c> onto it).</param>
        /// <param name="home">The user home directory (USERPROFILE / HOME).</param>
        /// <param name="appData">The Windows %APPDATA% directory (ignored on macOS/Linux).</param>
        /// <param name="projectRoot">The absolute Godot project root (<c>ProjectSettings.GlobalizePath("res://")</c>).</param>
        public abstract string? ConfigFilePath(AgentOs os, string home, string appData, string projectRoot);

        // --- Skills capability (parity with Unity-MCP's AiAgentConfigurator.SkillsPath / SupportsSkills) ---------

        /// <summary>
        /// Whether this agent supports auto-generated MCP "skills" (a <c>SKILL.md</c>-per-tool directory the
        /// skill-generation engine writes). Defaults to <c>false</c> (most agents do not); the Claude Code
        /// configurator overrides it to <c>true</c>. Mirrors the <see cref="ShouldShowJson"/> / <see cref="ConfigFilePath"/>
        /// capability pattern: the dock gates the Skills section's visibility on this (showing a "not supported" line
        /// otherwise), exactly like Unity-MCP gates its skills UI to Claude. Pure-managed so the registry's
        /// skills-capability set is unit-testable.
        /// </summary>
        public virtual bool SupportsSkills => false;

        /// <summary>
        /// The per-OS absolute skills directory the addon's skill-generation engine writes into for this agent, or
        /// <c>null</c> when the agent does not support skills (the default — see <see cref="SupportsSkills"/>). The
        /// Godot analog of Unity-MCP's project-relative <c>SkillsPath = ".claude/skills"</c>, resolved here to an
        /// absolute path so the engine's swap-and-restore call receives a concrete destination. Implementations
        /// resolve from the injected project root via <see cref="AgentConfigPaths"/> so they stay pure-managed and
        /// unit-testable; the editor passes <c>ProjectSettings.GlobalizePath("res://")</c> as <paramref name="projectRoot"/>.
        /// The signature mirrors <see cref="ConfigFilePath"/> for a uniform editor call site, though only the
        /// project root is consulted today.
        /// </summary>
        /// <param name="os">The host OS family (injected; the editor maps <c>Godot.OS.GetName()</c> onto it).</param>
        /// <param name="home">The user home directory (USERPROFILE / HOME).</param>
        /// <param name="appData">The Windows %APPDATA% directory (ignored on macOS/Linux).</param>
        /// <param name="projectRoot">The absolute Godot project root (<c>ProjectSettings.GlobalizePath("res://")</c>).</param>
        public virtual string? SkillsDir(AgentOs os, string home, string appData, string projectRoot) => null;

        /// <summary>
        /// The dock's Config-vs-JSON decision predicate: TRUE when the panel should show the copyable JSON snippet
        /// for this agent (because it has NO writable config-file path the addon can drive), FALSE when the panel
        /// should instead show the Configure / Remove buttons + status (because a config-file path exists). This is
        /// the KEY behavior of the AI-agent section: agents with a known config file get a one-click Configure/Remove
        /// row; only Custom / manual agents fall back to the raw JSON snippet. Pure-managed (the path is injected) so
        /// it is unit-testable; the editor passes the per-OS resolved path from <see cref="ConfigFilePath"/>.
        /// </summary>
        public static bool ShouldShowJson(string? resolvedConfigPath) => string.IsNullOrEmpty(resolvedConfigPath);

        /// <summary>
        /// Build the JSON snippet a user copies into this agent's MCP client — the addon's HTTP entry under
        /// <see cref="BodyPath"/> → <see cref="ServerKey"/>. When <paramref name="maskToken"/> is true the bearer is
        /// masked (on-screen display); pass false to emit the real token (copy / configure path). Delegates to the
        /// pure-managed <see cref="AgentConfigJson.BuildSnippet"/>.
        /// </summary>
        public string BuildSnippet(string mcpUrl, string? token, bool maskToken) =>
            AgentConfigJson.BuildSnippet(BodyPath, ServerKey, mcpUrl, token, maskToken);

        /// <summary>True when this agent's config file already contains the addon's entry pointing at <paramref name="mcpUrl"/>.</summary>
        public bool IsConfigured(string configPath, string mcpUrl) =>
            AgentConfigJson.IsConfigured(configPath, BodyPath, ServerKey, mcpUrl);

        /// <summary>
        /// Classify this agent's on-disk config relative to <paramref name="mcpUrl"/>: <see cref="AgentConfigState.Missing"/>
        /// (no entry → "Setup Required" alert), <see cref="AgentConfigState.Stale"/> (entry points at a different url →
        /// "Reconfiguration Required" alert), or <see cref="AgentConfigState.UpToDate"/> (no alert). Delegates to the
        /// pure-managed <see cref="AgentConfigJson.ConfigState"/>.
        /// </summary>
        public AgentConfigState ConfigState(string configPath, string mcpUrl) =>
            AgentConfigJson.ConfigState(configPath, BodyPath, ServerKey, mcpUrl);

        /// <summary>READ-MERGE-WRITE the addon's HTTP entry into this agent's config file (real token), preserving siblings.</summary>
        public void Configure(string configPath, string mcpUrl, string? token) =>
            AgentConfigJson.Configure(configPath, BodyPath, ServerKey, mcpUrl, token);

        /// <summary>Remove the addon's entry from this agent's config file, preserving siblings. Returns true when one was removed.</summary>
        public bool Remove(string configPath) =>
            AgentConfigJson.Remove(configPath, BodyPath, ServerKey);
    }
}
