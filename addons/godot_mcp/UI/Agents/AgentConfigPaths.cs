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
using System.IO;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// The operating-system families an AI-agent configurator resolves its config-file path against. Injected
    /// (rather than read from <c>Godot.OS</c>) so the per-OS path resolution is pure-managed and unit-testable in
    /// the plain-xUnit host. The editor wiring maps <c>Godot.OS.GetName()</c> onto this in
    /// <c>AgentConfiguratorsPanel.cs</c> (<c>#if TOOLS</c>).
    /// </summary>
    public enum AgentOs
    {
        Windows,
        MacOS,
        Linux
    }

    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) per-OS config-path helpers for the AI-agent
    /// configurators. All inputs (OS family, home dir, %APPDATA%, project root) are INJECTED so the resolution is
    /// deterministic and unit-testable; the editor passes the real values (<c>Godot.OS.GetName()</c>,
    /// <c>OS.GetEnvironment("USERPROFILE"/"HOME")</c>, <c>OS.GetEnvironment("APPDATA")</c>, and
    /// <c>ProjectSettings.GlobalizePath("res://")</c>).
    /// </summary>
    public static class AgentConfigPaths
    {
        /// <summary>
        /// Combine + normalize path segments to an absolute config path. Uses <see cref="Path.Combine(string[])"/>
        /// so the host OS's separator is applied, then collapses any mixed separators to the platform separator via
        /// <see cref="Path.GetFullPath(string)"/>-free normalization (we keep it simple: forward-slash inputs are
        /// fine on every platform for the consumers, which open the file with <c>System.IO</c>).
        /// </summary>
        static string Combine(params string[] parts) => Path.Combine(parts);

        /// <summary>
        /// Resolve the Claude Desktop config path for <paramref name="os"/>:
        /// <list type="bullet">
        ///   <item>Windows: <c>&lt;appData&gt;/Claude/claude_desktop_config.json</c></item>
        ///   <item>macOS: <c>&lt;home&gt;/Library/Application Support/Claude/claude_desktop_config.json</c></item>
        ///   <item>Linux: <c>&lt;home&gt;/.config/Claude/claude_desktop_config.json</c></item>
        /// </list>
        /// </summary>
        public static string ClaudeDesktop(AgentOs os, string home, string appData) => os switch
        {
            AgentOs.Windows => Combine(appData, "Claude", "claude_desktop_config.json"),
            AgentOs.MacOS => Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json"),
            _ => Combine(home, ".config", "Claude", "claude_desktop_config.json"),
        };

        /// <summary>Claude Code project-local config: <c>&lt;projectRoot&gt;/.mcp.json</c>.</summary>
        public static string ClaudeCode(string projectRoot) => Combine(projectRoot, ".mcp.json");

        /// <summary>Cursor project-local config: <c>&lt;projectRoot&gt;/.cursor/mcp.json</c>.</summary>
        public static string Cursor(string projectRoot) => Combine(projectRoot, ".cursor", "mcp.json");

        /// <summary>VS Code project-local config: <c>&lt;projectRoot&gt;/.vscode/mcp.json</c>.</summary>
        public static string VisualStudioCode(string projectRoot) => Combine(projectRoot, ".vscode", "mcp.json");

        /// <summary>Visual Studio (Copilot) project-local config: <c>&lt;projectRoot&gt;/.vs/mcp.json</c>.</summary>
        public static string VisualStudio(string projectRoot) => Combine(projectRoot, ".vs", "mcp.json");

        /// <summary>Rider (Junie) project-local config: <c>&lt;projectRoot&gt;/.junie/mcp/mcp.json</c>.</summary>
        public static string Rider(string projectRoot) => Combine(projectRoot, ".junie", "mcp", "mcp.json");

        /// <summary>
        /// GitHub Copilot CLI project-local config: <c>&lt;projectRoot&gt;/.mcp.json</c>. Copilot CLI v1.0.12+
        /// discovers workspace-local MCP configs from the working directory up to the git root, so the same
        /// <c>.mcp.json</c> is shared with Claude Code (mirrors Unity-MCP's GitHubCopilotCliConfigurator).
        /// </summary>
        public static string GitHubCopilotCli(string projectRoot) => Combine(projectRoot, ".mcp.json");

        /// <summary>Gemini project-local config: <c>&lt;projectRoot&gt;/.gemini/settings.json</c>.</summary>
        public static string Gemini(string projectRoot) => Combine(projectRoot, ".gemini", "settings.json");

        /// <summary>
        /// Cline global config inside VS Code's globalStorage for <paramref name="os"/>:
        /// <list type="bullet">
        ///   <item>Windows: <c>&lt;appData&gt;/Code/User/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json</c></item>
        ///   <item>macOS: <c>&lt;home&gt;/Library/Application Support/Code/User/globalStorage/.../cline_mcp_settings.json</c></item>
        ///   <item>Linux: <c>&lt;home&gt;/.config/Code/User/globalStorage/.../cline_mcp_settings.json</c></item>
        /// </list>
        /// Mirrors Unity-MCP's ClineConfigurator (global, shared across all projects).
        /// </summary>
        public static string Cline(AgentOs os, string home, string appData) => os switch
        {
            AgentOs.Windows => Combine(appData, "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            AgentOs.MacOS => Combine(home, "Library", "Application Support", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
            _ => Combine(home, ".config", "Code", "User", "globalStorage", "saoudrizwan.claude-dev", "settings", "cline_mcp_settings.json"),
        };

        /// <summary>Open Code project-local config: <c>&lt;projectRoot&gt;/opencode.json</c>.</summary>
        public static string OpenCode(string projectRoot) => Combine(projectRoot, "opencode.json");

        /// <summary>Kilo Code project-local config: <c>&lt;projectRoot&gt;/.kilocode/mcp.json</c>.</summary>
        public static string KiloCode(string projectRoot) => Combine(projectRoot, ".kilocode", "mcp.json");

        /// <summary>Zoo Code project-local config: <c>&lt;projectRoot&gt;/.roo/mcp.json</c>.</summary>
        public static string ZooCode(string projectRoot) => Combine(projectRoot, ".roo", "mcp.json");

        /// <summary>
        /// Claude Code project-local skills directory: <c>&lt;projectRoot&gt;/.claude/skills</c>. The destination the
        /// skill-generation engine (<c>IMcpPlugin.GenerateSkillFiles</c>) writes a <c>SKILL.md</c>-per-tool into. The
        /// Godot analog of Unity-MCP's <c>ClaudeCodeConfigurator.SkillsPath = ".claude/skills"</c> resolved against the
        /// project root. Pure-managed (the project root is injected) so it is unit-testable.
        /// </summary>
        public static string ClaudeCodeSkills(string projectRoot) => Combine(projectRoot, ".claude", "skills");

        /// <summary>
        /// Render <paramref name="absolutePath"/> for DISPLAY relative to <paramref name="projectRoot"/>: returns the
        /// project-relative form (e.g. <c>.claude/skills</c>) when the path is inside the project root, <c>"."</c> when
        /// it equals the project root, and the original absolute path unchanged when it is OUTSIDE the project (or on
        /// any error — safety fallback). Separators are normalized to <c>'/'</c> and trailing slashes trimmed before the
        /// ordinal containment check. The Godot analog of Unity-MCP's <c>ToDisplayPath</c>: the skills card shows the
        /// short relative path while keeping the full absolute path in the tooltip. Pure-managed (paths injected, no
        /// Godot native types, no IO) so it is unit-testable in the plain-xUnit host.
        /// </summary>
        public static string ToDisplayPath(string absolutePath, string projectRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(projectRoot))
                    return absolutePath;

                var normalizedPath = absolutePath.Replace('\\', '/').TrimEnd('/');
                var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');

                if (string.Equals(normalizedPath, normalizedRoot, System.StringComparison.Ordinal))
                    return ".";

                var prefix = normalizedRoot + "/";
                if (normalizedPath.StartsWith(prefix, System.StringComparison.Ordinal))
                    return normalizedPath.Substring(prefix.Length);

                // Outside the project root (or empty root) — return the original absolute path untouched.
                return absolutePath;
            }
            catch
            {
                return absolutePath;
            }
        }

        /// <summary>
        /// Validate a user-or-config-supplied skills path is a SAFE in-project relative path: rejects an absolute /
        /// rooted path and any <c>..</c> traversal segment (mirrors Unity-MCP's <c>Tool_Skills.GenerateAll</c> guard).
        /// Returns <c>true</c> when <paramref name="relativePath"/> is null/empty (the resolver falls back to the
        /// per-agent default) OR a clean relative path; <c>false</c> when it is rooted or escapes the project root.
        /// Pure-managed (no IO, no Godot types) so it is unit-testable; the editor calls this before resolving an
        /// override against the project root.
        /// </summary>
        public static bool IsSafeRelativeSkillsPath(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return true;

            // Reject absolute / rooted forms with a PURELY STRING-BASED test so the result is identical on every
            // host OS. System.IO.Path.IsPathRooted is platform-dependent — on Linux it returns false for a Windows
            // drive-letter path like "C:\Windows" (a drive letter is not a Linux root), which would let such a path
            // slip past the guard on a Linux CI runner while being rejected on Windows. Cover all absolute shapes:
            //   - POSIX absolute:        leading '/'
            //   - Windows UNC / rooted:  leading '\' (single or "\\server\share")
            //   - Windows drive-letter:  "<letter>:" prefix, with '\' OR '/' or nothing after (C:\, C:/, C:foo)
            var normalized = relativePath!.Replace('\\', '/');

            if (normalized.StartsWith("/"))
                return false;

            if (normalized.Length >= 2 &&
                normalized[1] == ':' &&
                ((normalized[0] >= 'A' && normalized[0] <= 'Z') || (normalized[0] >= 'a' && normalized[0] <= 'z')))
                return false;

            // Belt-and-suspenders: still honour the platform check too (catches any rooted form not covered above).
            if (Path.IsPathRooted(relativePath))
                return false;

            // Reject '..' traversal segments (after backslash normalization above).
            if (normalized == ".." || normalized.StartsWith("../") || normalized.Contains("/../") || normalized.EndsWith("/.."))
                return false;

            return true;
        }
    }
}
