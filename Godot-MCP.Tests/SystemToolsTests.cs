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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the addon's SYSTEM-tool surface (owner ruling 2026-07-25): <c>ping</c>,
    /// <c>godot-skill-create</c>, and <c>godot-skill-generate</c> must ALL be registered as
    /// <see cref="McpToolType.System"/>, engine-prefixed to match Unity's <c>unity-skill-*</c> pair.
    ///
    /// <para>
    /// WHY THIS MATTERS: <c>McpPluginBuilder</c> partitions tools by <c>ToolType</c> into two DISJOINT
    /// registries — Standard tools reach <c>McpToolManager</c> (and the MCP <c>tools/list</c> AI agents see),
    /// System tools reach <c>McpSystemToolManager</c> (the HTTP <c>/api/system-tools/</c> surface). The
    /// attribute is therefore not decoration: dropping it silently moves a tool to the other REST surface, and
    /// a client probing the system route gets "tool not found" (the exact production symptom that motivated
    /// this task). These asserts read the REAL <c>[AiTool]</c> attributes by reflection, so removing or
    /// flipping a <c>ToolType</c> fails CI.
    /// </para>
    /// </summary>
    public class SystemToolsTests
    {
        /// <summary>Read the <c>[AiTool]</c> attribute off a tool method, failing with a clear message if absent.</summary>
        static AiToolAttribute AiToolOf(Type toolType, string methodName)
        {
            var method = toolType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.True(method != null, $"{toolType.Name}.{methodName} not found — did the tool method get renamed?");

            var attribute = method!.GetCustomAttribute<AiToolAttribute>();
            Assert.True(attribute != null, $"{toolType.Name}.{methodName} is missing its [AiTool] attribute.");
            return attribute!;
        }

        public static IEnumerable<object[]> SystemToolCases() => new[]
        {
            new object[] { typeof(Tool_Ping), nameof(Tool_Ping.Ping), Tool_Ping.PingToolId, "ping" },
            new object[] { typeof(Tool_Skills), nameof(Tool_Skills.Create), Tool_Skills.SkillsCreateToolId, "godot-skill-create" },
            new object[] { typeof(Tool_Skills), nameof(Tool_Skills.GenerateAll), Tool_Skills.SkillsGenerateToolId, "godot-skill-generate" },
        };

        [Theory]
        [MemberData(nameof(SystemToolCases))]
        public void SystemTool_IsRegisteredAsSystem_WithTheEngineNamedToolId(
            Type toolType, string methodName, string toolIdConstant, string expectedToolId)
        {
            // The tool id is engine-prefixed (except the cross-engine `ping`) per the owner ruling.
            Assert.Equal(expectedToolId, toolIdConstant);

            var attribute = AiToolOf(toolType, methodName);

            Assert.Equal(expectedToolId, attribute.Name);
            Assert.Equal(McpToolType.System, attribute.ToolType);
        }

        [Fact]
        public void SystemTools_AreTheOnlyToolsOnTheSystemSurface_AndNoStandardToolLeaksOntoIt()
        {
            // Every OTHER [AiTool] on the two families must stay Standard — a guard against a future
            // copy-paste that sprays ToolType.System across an editor-driving tool by accident.
            var systemMethodNames = new[] { nameof(Tool_Ping.Ping), nameof(Tool_Skills.Create), nameof(Tool_Skills.GenerateAll) };

            foreach (var type in new[] { typeof(Tool_Ping), typeof(Tool_Skills) })
            {
                foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attribute = method.GetCustomAttribute<AiToolAttribute>();
                    if (attribute == null)
                        continue;

                    var expected = systemMethodNames.Contains(method.Name)
                        ? McpToolType.System
                        : McpToolType.Standard;

                    Assert.Equal(expected, attribute.ToolType);
                }
            }
        }

        [Fact]
        public void SkillTools_AreDisabledByDefault_MatchingTheUnityReference()
        {
            // Unity's unity-skill-create / unity-skill-generate ship `Enabled = false` (they are editor
            // infrastructure, not everyday agent tools). `Enabled` only affects the LISTING metadata —
            // McpSystemToolManager.RunSystemTool dispatches regardless — so the HTTP surface still works.
            Assert.False(AiToolOf(typeof(Tool_Skills), nameof(Tool_Skills.Create)).Enabled);
            Assert.False(AiToolOf(typeof(Tool_Skills), nameof(Tool_Skills.GenerateAll)).Enabled);

            // ping stays enabled — it is the liveness probe every client calls.
            Assert.True(AiToolOf(typeof(Tool_Ping), nameof(Tool_Ping.Ping)).Enabled);
        }

        [Fact]
        public void SkillTools_CarrySkillDescriptionAndBody_SoGeneratedSkillMdIsUseful()
        {
            // godot-skill-generate writes the YAML `description:` from [AiSkillDescription] and the markdown
            // body from [AiSkillBody]; a skill tool without them generates an empty-ish SKILL.md.
            foreach (var methodName in new[] { nameof(Tool_Skills.Create), nameof(Tool_Skills.GenerateAll) })
            {
                var method = typeof(Tool_Skills).GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance)!;

                var description = method.GetCustomAttribute<AiSkillDescriptionAttribute>();
                Assert.True(description != null, $"Tool_Skills.{methodName} is missing [AiSkillDescription].");
                Assert.False(string.IsNullOrWhiteSpace(description!.Description));

                var body = method.GetCustomAttribute<AiSkillBodyAttribute>();
                Assert.True(body != null, $"Tool_Skills.{methodName} is missing [AiSkillBody].");
                Assert.False(string.IsNullOrWhiteSpace(body!.Body));
            }
        }

        [Fact]
        public void ScriptCreateToolIdRef_MatchesTheScriptFamilysToolId()
        {
            // SkillsToolPaths quotes `script-create` in its ".gd is not a skill" rejection message, but the
            // Tool_Script family is editor-only (#if TOOLS) so it cannot reference the real constant. Pin the
            // literal so a rename of `script-create` fails here instead of shipping a dead pointer.
            Assert.Equal("script-create", SkillsToolPaths.ScriptCreateToolIdRef);
        }
    }

    /// <summary>
    /// Argument-guard coverage for the two <c>godot-skill-*</c> system tools. These run BEFORE the editor host
    /// is consulted, so a hostile path is refused identically with or without a live editor — which is exactly
    /// what makes them unit-testable in this plain-xUnit host.
    /// </summary>
    public class SkillsToolPathsTests
    {
        // --- godot-skill-create target path ------------------------------------------------------------

        [Theory]
        [InlineData("res://Skills/Tool_Sample.cs")]
        [InlineData("res://Tool_Sample.cs")]
        [InlineData("res://a/b/c/Deep.CS")]   // extension check is case-insensitive
        public void RequireSkillFileResPath_AcceptsResCSharpFilePaths(string path)
        {
            Assert.Equal(path, SkillsToolPaths.RequireSkillFileResPath(path, "path"));
        }

        [Fact]
        public void RequireSkillFileResPath_TrimsSurroundingWhitespace()
        {
            Assert.Equal("res://Skills/Tool_Sample.cs",
                SkillsToolPaths.RequireSkillFileResPath("  res://Skills/Tool_Sample.cs  ", "path"));
        }

        [Theory]
        [InlineData("res://Skills/player.gd")]   // GDScript cannot declare MCP tools
        [InlineData("res://Skills/notes.md")]
        [InlineData("res://Skills/Tool_Sample")] // no extension
        public void RequireSkillFileResPath_RejectsNonCSharpFiles(string path)
        {
            var ex = Assert.Throws<ArgumentException>(() => SkillsToolPaths.RequireSkillFileResPath(path, "path"));
            Assert.Contains(".cs", ex.Message);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("Skills/Tool_Sample.cs")]      // not res://-rooted
        [InlineData("/abs/Tool_Sample.cs")]
        [InlineData("res://")]                     // the bare project root is not a file
        [InlineData("res://Skills/")]              // a directory is not a file
        [InlineData("res://../outside/Tool_X.cs")] // parent traversal
        [InlineData("res://a/../../Tool_X.cs")]
        public void RequireSkillFileResPath_RejectsMalformedOrEscapingPaths(string? path)
        {
            Assert.Throws<ArgumentException>(() => SkillsToolPaths.RequireSkillFileResPath(path, "path"));
        }

        // --- godot-skill-generate output folder --------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void RequireRelativeSkillsFolder_NullOrBlank_MeansUseTheConfiguredFolder(string? path)
        {
            Assert.Null(SkillsToolPaths.RequireRelativeSkillsFolder(path, "path"));
        }

        [Theory]
        [InlineData(".claude/skills", ".claude/skills")]
        [InlineData("skills", "skills")]
        [InlineData(".claude/skills/", ".claude/skills")]   // trailing slash trimmed
        [InlineData(@".claude\skills", ".claude/skills")]   // backslashes normalized
        public void RequireRelativeSkillsFolder_NormalizesACleanRelativeFolder(string path, string expected)
        {
            Assert.Equal(expected, SkillsToolPaths.RequireRelativeSkillsFolder(path, "path"));
        }

        [Theory]
        [InlineData("/etc/skills")]              // POSIX absolute
        [InlineData(@"C:\Windows\skills")]       // Windows drive-letter — must reject on Linux CI too
        [InlineData("C:/Windows")]
        [InlineData(@"\\server\share")]          // UNC
        [InlineData("..")]
        [InlineData("../escape")]
        [InlineData("a/../../escape")]
        [InlineData("res://skills")]             // res:// is the WRONG root for this argument
        public void RequireRelativeSkillsFolder_RejectsAbsoluteResAndTraversalPaths(string path)
        {
            Assert.Throws<ArgumentException>(() => SkillsToolPaths.RequireRelativeSkillsFolder(path, "path"));
        }
    }

    /// <summary>
    /// Behaviour coverage for the <c>godot-skill-*</c> tool bodies via a fake <see cref="ISkillsToolHost"/>:
    /// the guard-then-delegate contract, the exact values forwarded to the editor half, and the actionable
    /// error raised when no editor host is registered (an exported game build, or a call that beat the boot).
    /// The live editor half (<c>GodotSkillsToolHost</c>, <c>#if TOOLS</c>) is verified by the headless Godot
    /// smoke — see <c>test.md</c> Suite 3.
    /// </summary>
    [Collection(SkillsToolHostCurrentCollection.Name)]
    public class ToolSkillsDispatchTests : IDisposable
    {
        readonly ISkillsToolHost? _originalHost;

        public ToolSkillsDispatchTests() => _originalHost = SkillsToolHost.Current;

        public void Dispose() => SkillsToolHost.Current = _originalHost;

        sealed class FakeSkillsToolHost : ISkillsToolHost
        {
            public string? CreatedPath { get; private set; }
            public string? CreatedCode { get; private set; }
            public int CreateCalls { get; private set; }

            public string? GeneratedFolder { get; private set; }
            public int GenerateCalls { get; private set; }

            public ScriptInfo CreateSkillFile(string resPath, string code)
            {
                CreateCalls++;
                CreatedPath = resPath;
                CreatedCode = code;
                return new ScriptInfo { ResourcePath = resPath, Language = "CSharp", Status = "Skill created." };
            }

            public SkillsGenerateInfo GenerateSkills(string? relativeFolder)
            {
                GenerateCalls++;
                GeneratedFolder = relativeFolder;
                return new SkillsGenerateInfo { SkillsFolder = relativeFolder ?? "<configured>", SkillCount = 7 };
            }
        }

        [Fact]
        public void Create_ForwardsTheNormalizedPathAndCodeToTheEditorHost()
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            var result = new Tool_Skills().Create("  res://Skills/Tool_Sample.cs  ", "// code");

            Assert.Equal(1, host.CreateCalls);
            Assert.Equal("res://Skills/Tool_Sample.cs", host.CreatedPath); // trimmed by the guard
            Assert.Equal("// code", host.CreatedCode);
            Assert.Equal("res://Skills/Tool_Sample.cs", result.ResourcePath);
        }

        [Fact]
        public void Create_RejectsABadPathBeforeTouchingTheEditorHost()
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            Assert.Throws<ArgumentException>(() => new Tool_Skills().Create("res://Skills/player.gd", "// code"));
            Assert.Equal(0, host.CreateCalls); // nothing was written
        }

        [Fact]
        public void Create_RejectsNullCode()
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            Assert.Throws<ArgumentNullException>(() => new Tool_Skills().Create("res://Skills/Tool_Sample.cs", null!));
            Assert.Equal(0, host.CreateCalls);
        }

        [Fact]
        public void Create_WithoutAnEditorHost_ThrowsAnActionableError()
        {
            SkillsToolHost.Current = null;

            var ex = Assert.Throws<InvalidOperationException>(
                () => new Tool_Skills().Create("res://Skills/Tool_Sample.cs", "// code"));
            Assert.Contains("EDITOR", ex.Message);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("", null)]
        [InlineData(".claude/skills", ".claude/skills")]
        [InlineData(@".claude\skills\", ".claude/skills")]
        public void GenerateAll_ForwardsTheNormalizedFolderOverride(string? input, string? expected)
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            var result = new Tool_Skills().GenerateAll(input);

            Assert.Equal(1, host.GenerateCalls);
            Assert.Equal(expected, host.GeneratedFolder);
            Assert.Equal(7, result.SkillCount);
        }

        [Fact]
        public void GenerateAll_DefaultsToTheConfiguredFolderWhenCalledWithNoArguments()
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            new Tool_Skills().GenerateAll();

            Assert.Equal(1, host.GenerateCalls);
            Assert.Null(host.GeneratedFolder); // null = "use the selected agent's configured folder"
        }

        [Fact]
        public void GenerateAll_RejectsAnEscapingOverrideBeforeTouchingTheEditorHost()
        {
            var host = new FakeSkillsToolHost();
            SkillsToolHost.Current = host;

            Assert.Throws<ArgumentException>(() => new Tool_Skills().GenerateAll("../outside"));
            Assert.Equal(0, host.GenerateCalls);
        }

        [Fact]
        public void GenerateAll_WithoutAnEditorHost_ThrowsAnActionableError()
        {
            SkillsToolHost.Current = null;

            var ex = Assert.Throws<InvalidOperationException>(() => new Tool_Skills().GenerateAll());
            Assert.Contains("EDITOR", ex.Message);
        }
    }

    /// <summary>
    /// Dedicated xUnit collection so <see cref="ToolSkillsDispatchTests"/> runs in isolation: it mutates the
    /// process-wide <see cref="SkillsToolHost.Current"/> static, which would race any other class that reads
    /// or writes the same static if it ran in the default parallel pool (issue #195's flaky-test shape).
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class SkillsToolHostCurrentCollection
    {
        public const string Name = "SkillsToolHost.Current (serial)";
    }
}
