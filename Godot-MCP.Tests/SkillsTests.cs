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
using Xunit;
using AgentConfig = com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the Godot-side pure-managed Skills surface AFTER the #142 convergence onto the shared
    /// <c>com.IvanMurzak.McpPlugin.AgentConfig</c> module: the skills-path display + relative-path safety guard
    /// (<see cref="SkillsPathUtils"/>), the <see cref="SkillsPlan"/> resolution over the shared
    /// <see cref="AgentConfig.AiAgentConfigurator"/>, the Godot registry VIEW
    /// (<see cref="GodotAgentConfigurators"/> — Unity-only agent filtered out), and the auto-generate-ON config
    /// default. The shared configurator/JSON/path logic is unit-tested upstream in MCP-Plugin-dotnet; the dock UI
    /// (<c>SkillsPanel.cs</c>, <c>AgentConfiguratorsPanel.cs</c>, <c>#if TOOLS</c>) is verified via the headless
    /// Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class SkillsTests
    {
        const string ProjectRoot = "/home/dev/MyGodotProject";

        // --- SkillsPathUtils.IsSafeRelativeSkillsPath -------------------------------------------------------

        [Theory]
        [InlineData(null)]            // null → falls back to default (safe)
        [InlineData("")]             // empty → falls back to default (safe)
        [InlineData(".claude/skills")]
        [InlineData("skills")]
        [InlineData("a/b/c")]
        public void IsSafeRelativeSkillsPath_AllowsCleanRelativePaths(string? path)
        {
            Assert.True(SkillsPathUtils.IsSafeRelativeSkillsPath(path));
        }

        [Theory]
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("a/../../escape")]
        [InlineData("a/..")]
        [InlineData(@"..\escape")]   // backslash traversal normalized
        public void IsSafeRelativeSkillsPath_RejectsTraversal(string path)
        {
            Assert.False(SkillsPathUtils.IsSafeRelativeSkillsPath(path));
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
            Assert.False(SkillsPathUtils.IsSafeRelativeSkillsPath(path));
        }

        // --- SkillsPathUtils.ToDisplayPath -------------------------------------------------------------------

        [Fact]
        public void ToDisplayPath_InsideProject_ReturnsRelative()
        {
            var skillsDir = Path.Combine(ProjectRoot, ".claude", "skills"); // /home/dev/MyGodotProject/.claude/skills
            Assert.Equal(".claude/skills", SkillsPathUtils.ToDisplayPath(skillsDir, ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_EqualToProjectRoot_ReturnsDot()
        {
            Assert.Equal(".", SkillsPathUtils.ToDisplayPath(ProjectRoot, ProjectRoot));
            // Trailing-slash on the path must not defeat the equality check.
            Assert.Equal(".", SkillsPathUtils.ToDisplayPath(ProjectRoot + "/", ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_OutsideProject_ReturnsAbsoluteUnchanged()
        {
            const string outside = "/somewhere/else/.claude/skills";
            Assert.Equal(outside, SkillsPathUtils.ToDisplayPath(outside, ProjectRoot));

            // A sibling path that merely shares a prefix STRING but is not a child directory must NOT be treated as
            // inside (the trailing-slash boundary guards against `/home/dev/MyGodotProject-other`).
            const string sibling = "/home/dev/MyGodotProject-other/.claude/skills";
            Assert.Equal(sibling, SkillsPathUtils.ToDisplayPath(sibling, ProjectRoot));
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
            Assert.Equal(expected, SkillsPathUtils.ToDisplayPath(absolutePath, projectRoot));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public void ToDisplayPath_NullOrEmptyPath_ReturnsInputUnchanged(string? absolutePath)
        {
            Assert.Equal(absolutePath, SkillsPathUtils.ToDisplayPath(absolutePath!, ProjectRoot));
        }

        [Fact]
        public void ToDisplayPath_EmptyProjectRoot_ReturnsAbsoluteUnchanged()
        {
            const string abs = "/home/dev/proj/.claude/skills";
            Assert.Equal(abs, SkillsPathUtils.ToDisplayPath(abs, string.Empty));
        }

        // --- Shared registry VIEW (GodotAgentConfigurators) -------------------------------------------------

        [Fact]
        public void Registry_ExcludesUnityOnlyAgent_ButKeepsTheRest()
        {
            var ids = GodotAgentConfigurators.All.Select(a => a.AgentId).ToList();

            // The Unity-Editor-specific agent is filtered out for Godot.
            Assert.DoesNotContain(GodotAgentConfigurators.ExcludedAgentId, ids);
            Assert.Null(GodotAgentConfigurators.GetByAgentId(GodotAgentConfigurators.ExcludedAgentId));

            // The Godot-relevant agents survive, in the shared display order (Custom last).
            Assert.Contains("claude-code", ids);
            Assert.Contains("other-custom", ids);
            Assert.Equal("other-custom", ids.Last());

            // The filtered view = the shared registry minus exactly the excluded agent.
            Assert.Equal(AgentConfig.AiAgentConfiguratorRegistry.All.Count - 1, GodotAgentConfigurators.All.Count);
        }

        [Fact]
        public void Registry_GetIndexByAgentId_RoundTrips()
        {
            for (int i = 0; i < GodotAgentConfigurators.All.Count; i++)
            {
                var id = GodotAgentConfigurators.All[i].AgentId;
                Assert.Equal(i, GodotAgentConfigurators.GetIndexByAgentId(id));
            }
            Assert.Equal(-1, GodotAgentConfigurators.GetIndexByAgentId("does-not-exist"));
            Assert.Equal(-1, GodotAgentConfigurators.GetIndexByAgentId(null));
        }

        // --- SkillsPlan.Resolve over the shared configurator ------------------------------------------------

        [Fact]
        public void SkillsPlan_Resolve_SkillsCapableAgent_IsSupported_WithProjectRelativeDir()
        {
            var claude = GodotAgentConfigurators.GetByAgentId("claude-code");
            Assert.NotNull(claude);
            Assert.True(claude!.SupportsSkills);

            Assert.NotNull(claude.SkillsPath);

            var plan = SkillsPlan.Resolve(claude, ProjectRoot);
            Assert.True(plan.Supported);
            Assert.NotNull(plan.SkillsDir);
            // The shared SkillsPath is a project-relative path combined with the project root here.
            Assert.Equal(Path.Combine(ProjectRoot, claude.SkillsPath!), plan.SkillsDir);
        }

        [Fact]
        public void SkillsPlan_Resolve_NullAgent_IsUnsupported()
        {
            var plan = SkillsPlan.Resolve(null, ProjectRoot);
            Assert.False(plan.Supported);
            Assert.Null(plan.SkillsDir);
        }

        [Fact]
        public void SkillsPlan_Resolve_NonSkillsAgent_IsUnsupported()
        {
            // Claude Desktop is one of the shared agents that does NOT support skills.
            var noSkills = GodotAgentConfigurators.All.FirstOrDefault(a => !a.SupportsSkills);
            Assert.NotNull(noSkills);

            var plan = SkillsPlan.Resolve(noSkills, ProjectRoot);
            Assert.False(plan.Supported);
            Assert.Null(plan.SkillsDir);
        }

        [Fact]
        public void SkillsPlan_SupportedFlag_MatchesConfiguratorSupportsSkills_ForEveryAgent()
        {
            foreach (var agent in GodotAgentConfigurators.All)
            {
                var plan = SkillsPlan.Resolve(agent, ProjectRoot);
                // Supported iff the shared configurator advertises a safe, non-empty skills path.
                var expectSupported = agent.SupportsSkills
                    && !string.IsNullOrEmpty(agent.SkillsPath)
                    && SkillsPathUtils.IsSafeRelativeSkillsPath(agent.SkillsPath);
                Assert.Equal(expectSupported, plan.Supported);
                Assert.Equal(plan.Supported, plan.SkillsDir != null);
            }
        }

        // --- SkillsBootstrap.PrimeForCtorSkillGeneration (ctor skill-gen exception fix) ---------------------

        [Fact]
        public void PrimeForCtor_SkillsCapableAgent_SetsAbsoluteSkillsPathAndProjectRoot()
        {
            // Regression for the boot-time McpPlugin 6.10.0 ctor skill-gen exception: before Build(), a
            // skills-capable agent's config must carry an ABSOLUTE SkillsPath (so McpPlugin.ResolveSkillsPath
            // takes its rooted branch and never needs ProjectRootPath) plus the project root.
            var claude = GodotAgentConfigurators.GetByAgentId("claude-code");
            Assert.NotNull(claude);
            Assert.True(claude!.SupportsSkills);

            var config = new GodotMcpConfig(); // GenerateSkillFiles defaults ON, SkillsPath defaults relative "SKILLS"
            SkillsBootstrap.PrimeForCtorSkillGeneration(config, claude, ProjectRoot);

            // Still ON (supported agent → ctor generation should run, not be disabled).
            Assert.True(config.GenerateSkillFiles);

            // SkillsPath is now the ABSOLUTE resolved skills dir — the same destination SkillsPlan resolves.
            var expectedDir = SkillsPlan.Resolve(claude, ProjectRoot).SkillsDir;
            Assert.Equal(expectedDir, config.SkillsPath);

            // Crucially ROOTED: this is what makes McpPlugin's ResolveSkillsPath skip the throw branch entirely.
            Assert.True(Path.IsPathRooted(config.SkillsPath));

            // Project root is set too, for live-config consistency with the post-build swap-and-restore.
            Assert.Equal(ProjectRoot, config.ProjectRootPath);
        }

        [Fact]
        public void PrimeForCtor_Destination_MatchesPostBuildAutoGenerateDestination()
        {
            // The ctor-time destination MUST equal the post-build MaybeAutoGenerateSkills destination
            // (both go through SkillsPlan.Resolve), so skills can never land in two different places.
            var claude = GodotAgentConfigurators.GetByAgentId("claude-code");
            Assert.NotNull(claude);

            var config = new GodotMcpConfig();
            SkillsBootstrap.PrimeForCtorSkillGeneration(config, claude, ProjectRoot);

            var postBuildDir = SkillsPlan.Resolve(claude, ProjectRoot).SkillsDir; // what MaybeAutoGenerateSkills uses
            Assert.Equal(postBuildDir, config.SkillsPath);
        }

        [Fact]
        public void PrimeForCtor_NonSkillsAgent_DisablesCtorGenerationAndLeavesPathRelative()
        {
            // An agent without a skills dir → disable ctor-time generation so McpPlugin's ctor SKIPS it instead
            // of throwing on the relative default path or writing a stray relative SKILLS/ directory.
            var noSkills = GodotAgentConfigurators.All.FirstOrDefault(a => !a.SupportsSkills);
            Assert.NotNull(noSkills);

            var config = new GodotMcpConfig();
            var originalSkillsPath = config.SkillsPath; // the relative built-in default
            SkillsBootstrap.PrimeForCtorSkillGeneration(config, noSkills, ProjectRoot);

            Assert.False(config.GenerateSkillFiles);     // ctor generation suppressed → no throw
            Assert.Equal(originalSkillsPath, config.SkillsPath); // untouched (no spurious absolute path)
        }

        [Fact]
        public void PrimeForCtor_NullAgent_DisablesCtorGeneration()
        {
            var config = new GodotMcpConfig();
            SkillsBootstrap.PrimeForCtorSkillGeneration(config, null, ProjectRoot);
            Assert.False(config.GenerateSkillFiles);
        }

        [Fact]
        public void PrimeForCtor_GenerateSkillFilesAlreadyOff_IsNoOp()
        {
            // A persisted/user OFF toggle must be honoured: priming must not re-enable generation, and (since the
            // ctor will not generate) must not bother rewriting the path.
            var claude = GodotAgentConfigurators.GetByAgentId("claude-code");
            var config = new GodotMcpConfig { GenerateSkillFiles = false };
            var originalSkillsPath = config.SkillsPath;
            var originalProjectRoot = config.ProjectRootPath;

            SkillsBootstrap.PrimeForCtorSkillGeneration(config, claude, ProjectRoot);

            Assert.False(config.GenerateSkillFiles);            // stays OFF
            Assert.Equal(originalSkillsPath, config.SkillsPath); // untouched
            Assert.Equal(originalProjectRoot, config.ProjectRootPath); // untouched
        }

        [Fact]
        public void PrimeForCtor_NullConfig_DoesNotThrow()
        {
            // Defensive: a null config is a no-op, never an NRE on the boot path.
            var claude = GodotAgentConfigurators.GetByAgentId("claude-code");
            var ex = Record.Exception(() => SkillsBootstrap.PrimeForCtorSkillGeneration(null!, claude, ProjectRoot));
            Assert.Null(ex);
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
