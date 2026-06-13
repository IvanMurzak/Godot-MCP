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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.Godot.MCP.UI.Agents.Impl;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed Skills-generation surface (issue #64): the per-agent skills-dir resolver
    /// (<see cref="AgentConfigPaths.ClaudeCodeSkills"/>), the relative-path safety guard
    /// (<see cref="AgentConfigPaths.IsSafeRelativeSkillsPath"/>), the <see cref="GodotAgentConfigurator.SupportsSkills"/>
    /// / <see cref="GodotAgentConfigurator.SkillsDir"/> capability (overridden only for Claude Code), the registry's
    /// skills-capability set, the <see cref="SkillsPlan"/> resolution, and the auto-generate-ON config default. The
    /// dock UI (<c>SkillsPanel.cs</c>, <c>#if TOOLS</c>) is verified via the headless Godot smoke (test.md Suite 3) —
    /// NOT here.
    /// </summary>
    public class SkillsTests
    {
        const string ProjectRoot = "/home/dev/MyGodotProject";
        const string Home = "/home/dev";
        const string AppData = @"C:\Users\dev\AppData\Roaming";

        // --- AgentConfigPaths.ClaudeCodeSkills ---------------------------------------------------------------

        [Fact]
        public void ClaudeCodeSkills_ResolvesProjectLocalDotClaudeSkills()
        {
            var dir = AgentConfigPaths.ClaudeCodeSkills(ProjectRoot);
            var expected = Path.Combine(ProjectRoot, ".claude", "skills");
            Assert.Equal(expected, dir);
        }

        // --- AgentConfigPaths.IsSafeRelativeSkillsPath -------------------------------------------------------

        [Theory]
        [InlineData(null)]            // null → falls back to default (safe)
        [InlineData("")]             // empty → falls back to default (safe)
        [InlineData(".claude/skills")]
        [InlineData("skills")]
        [InlineData("a/b/c")]
        public void IsSafeRelativeSkillsPath_AllowsCleanRelativePaths(string? path)
        {
            Assert.True(AgentConfigPaths.IsSafeRelativeSkillsPath(path));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("a/../../escape")]
        [InlineData("a/..")]
        [InlineData(@"..\escape")]   // backslash traversal normalized
        public void IsSafeRelativeSkillsPath_RejectsTraversal(string path)
        {
            Assert.False(AgentConfigPaths.IsSafeRelativeSkillsPath(path));
        }

        [Theory]
        [InlineData("/etc/passwd")]            // POSIX absolute
        [InlineData(@"C:\Windows\System32")]   // Windows drive-letter (backslash) — must reject on Linux too
        [InlineData("C:/Windows")]             // Windows drive-letter (forward slash)
        [InlineData("c:/windows")]             // lowercase drive letter
        [InlineData("C:relative")]             // drive-letter with no separator (still drive-rooted)
        [InlineData(@"\\server\share")]        // UNC path
        [InlineData(@"\rooted")]               // single leading backslash (Windows root-of-current-drive)
        public void IsSafeRelativeSkillsPath_RejectsAbsolutePaths(string path)
        {
            // The rejection MUST be OS-independent — these are all absolute/rooted forms regardless of host OS.
            // (Regression guard for the CI failure where Path.IsPathRooted("C:\\Windows") was false on Linux.)
            Assert.False(AgentConfigPaths.IsSafeRelativeSkillsPath(path));
        }

        // --- AgentConfigPaths.ToDisplayPath (issue #105) ----------------------------------------------------

        [Fact]
        public void ToDisplayPath_InsideProject_ReturnsRelative()
        {
            var skillsDir = Path.Combine(ProjectRoot, ".claude", "skills"); // /home/dev/MyGodotProject/.claude/skills
            Assert.Equal(".claude/skills", AgentConfigPaths.ToDisplayPath(skillsDir, ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_EqualToProjectRoot_ReturnsDot()
        {
            Assert.Equal(".", AgentConfigPaths.ToDisplayPath(ProjectRoot, ProjectRoot));
            // Trailing-slash on the path must not defeat the equality check.
            Assert.Equal(".", AgentConfigPaths.ToDisplayPath(ProjectRoot + "/", ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_OutsideProject_ReturnsAbsoluteUnchanged()
        {
            const string outside = "/somewhere/else/.claude/skills";
            Assert.Equal(outside, AgentConfigPaths.ToDisplayPath(outside, ProjectRoot));

            // A sibling path that merely shares a prefix STRING but is not a child directory must NOT be treated as
            // inside (the trailing-slash boundary guards against `/home/dev/MyGodotProject-other`).
            const string sibling = "/home/dev/MyGodotProject-other/.claude/skills";
            Assert.Equal(sibling, AgentConfigPaths.ToDisplayPath(sibling, ProjectRoot));
        }

        [Theory]
        // Backslash separators in the absolute path normalize to '/', and the relative result uses '/'.
        [InlineData(@"C:\proj\.claude\skills", @"C:\proj", ".claude/skills")]
        // A trailing slash on the input path is trimmed before the containment check.
        [InlineData("/home/dev/proj/.claude/skills/", "/home/dev/proj", ".claude/skills")]
        // Mixed separators in the project root normalize too.
        [InlineData("/home/dev/proj/.claude/skills", @"\home\dev\proj", ".claude/skills")]
        public void ToDisplayPath_NormalizesSeparatorsAndTrailingSlashes(string absolutePath, string projectRoot, string expected)
        {
            Assert.Equal(expected, AgentConfigPaths.ToDisplayPath(absolutePath, projectRoot));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ToDisplayPath_NullOrEmptyPath_ReturnsInputUnchanged(string? absolutePath)
        {
            Assert.Equal(absolutePath, AgentConfigPaths.ToDisplayPath(absolutePath!, ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_EmptyProjectRoot_ReturnsAbsoluteUnchanged()
        {
            const string abs = "/home/dev/proj/.claude/skills";
            Assert.Equal(abs, AgentConfigPaths.ToDisplayPath(abs, string.Empty));
        }

        // --- Per-agent capability (SupportsSkills / SkillsDir) ----------------------------------------------

        [Fact]
        public void ClaudeCode_SupportsSkills_AndResolvesSkillsDir()
        {
            var agent = new ClaudeCodeConfigurator();
            Assert.True(agent.SupportsSkills);

            var dir = agent.SkillsDir(AgentOs.Linux, Home, AppData, ProjectRoot);
            Assert.Equal(Path.Combine(ProjectRoot, ".claude", "skills"), dir);
        }

        [Fact]
        public void NonClaudeCodeAgents_DoNotSupportSkills_AndReturnNullSkillsDir()
        {
            foreach (var agent in GodotAgentConfiguratorRegistry.All.Where(a => a.AgentId != "claude-code"))
            {
                Assert.False(agent.SupportsSkills);
                Assert.Null(agent.SkillsDir(AgentOs.Linux, Home, AppData, ProjectRoot));
            }
        }

        /// <summary>
        /// The registry's skills-capable set is EXACTLY {claude-code} for v1 (owner-approved Claude-Code-only). A
        /// non-null SkillsDir lines up with SupportsSkills for every agent (no half-implemented capability).
        /// </summary>
        [Fact]
        public void Registry_ExactlyClaudeCodeSupportsSkills_WithNonNullDir()
        {
            var skillsCapable = GodotAgentConfiguratorRegistry.All
                .Where(a => a.SupportsSkills)
                .Select(a => a.AgentId)
                .ToList();

            Assert.Equal(new[] { "claude-code" }, skillsCapable);

            foreach (var agent in GodotAgentConfiguratorRegistry.All)
            {
                var dir = agent.SkillsDir(AgentOs.Linux, Home, AppData, ProjectRoot);
                // SkillsDir is non-null EXACTLY when the agent supports skills.
                Assert.Equal(agent.SupportsSkills, dir != null);
            }
        }

        // --- SkillsPlan.Resolve ------------------------------------------------------------------------------

        [Fact]
        public void SkillsPlan_Resolve_ClaudeCode_IsSupported_WithDir()
        {
            var plan = SkillsPlan.Resolve(new ClaudeCodeConfigurator(), AgentOs.Linux, Home, AppData, ProjectRoot);
            Assert.True(plan.Supported);
            Assert.Equal(Path.Combine(ProjectRoot, ".claude", "skills"), plan.SkillsDir);
        }

        [Fact]
        public void SkillsPlan_Resolve_NullAgent_IsUnsupported()
        {
            var plan = SkillsPlan.Resolve(null, AgentOs.Linux, Home, AppData, ProjectRoot);
            Assert.False(plan.Supported);
            Assert.Null(plan.SkillsDir);
        }

        [Fact]
        public void SkillsPlan_Resolve_UnsupportedAgent_IsUnsupported()
        {
            var cursor = GodotAgentConfiguratorRegistry.GetByAgentId("cursor");
            Assert.NotNull(cursor);

            var plan = SkillsPlan.Resolve(cursor, AgentOs.Linux, Home, AppData, ProjectRoot);
            Assert.False(plan.Supported);
            Assert.Null(plan.SkillsDir);
        }

        // --- Config default (auto-generate ON) ---------------------------------------------------------------

        [Fact]
        public void GodotMcpConfig_DefaultsAutoGenerateSkillsOn()
        {
            var config = new GodotMcpConfig();
            Assert.True(config.GenerateSkillFiles);
        }

        /// <summary>
        /// A persisted OFF override survives a round-trip through the config store's serialized layer (without this,
        /// the constructor's ON default would silently re-win on every boot). Verifies ApplyPersisted copies the flag.
        /// </summary>
        [Fact]
        public void GodotMcpConfig_PersistedAutoGenerateOff_IsHonoured()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "godot-mcp-skills-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            var path = Path.Combine(tempDir, "config.json");
            try
            {
                var saved = new GodotMcpConfig { GenerateSkillFiles = false };
                GodotMcpConfigStore.Save(path, saved);

                var loaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(loaded);

                var target = new GodotMcpConfig(); // constructor seeds ON
                GodotMcpConfigStore.ApplyPersisted(target, loaded);
                Assert.False(target.GenerateSkillFiles); // persisted OFF wins
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* best-effort cleanup */ }
            }
        }
    }
}
