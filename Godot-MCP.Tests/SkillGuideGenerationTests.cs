/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

using System;
using System.IO;
using System.Linq;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public class SkillGuideGenerationTests
    {
        [Fact]
        public void GlobalMetadataAndReferencesCoverToolsAndStandaloneGuides()
        {
            var root = Path.Combine(Path.GetTempPath(), "GodotSkillGuides_" + Guid.NewGuid().ToString("N"));
            try
            {
                var builder = new McpPluginBuilder(new com.IvanMurzak.McpPlugin.Common.Version());
                builder.WithConfig(config =>
                {
                    config.SkillsPath = root;
                    config.KeepConnected = false;
                    config.SkillMetadata["generated-by"] = "godot-mcp";
                    config.SkillMetadata["ai-game-dev"] = "exclude";
                });
                builder.WithSkillFileGenerator<GodotSkillFileGenerator>();
                builder.WithSkills(typeof(Skill_Cli), typeof(Skill_App));
                builder.WithTools(typeof(TestTools));
                using var plugin = (com.IvanMurzak.McpPlugin.McpPlugin)builder.Build(new Reflector());

                var files = Directory.GetFiles(root, "SKILL.md", SearchOption.AllDirectories);
                Assert.Equal(3, files.Length);
                foreach (var file in files)
                    Assert.Contains("metadata:\n  generated-by: godot-mcp\n  ai-game-dev: exclude\n",
                        File.ReadAllText(file).Replace("\r\n", "\n"));
                var tool = File.ReadAllText(Path.Combine(root, "guide-test", "SKILL.md"));
                Assert.Contains("godot-cli", tool);
                Assert.Contains("ai-game-dev-app", tool);
                Assert.DoesNotContain("npm install", tool);
                Assert.Contains("npm install -g godot-cli", File.ReadAllText(Path.Combine(root, "godot-cli", "SKILL.md")));
                Assert.Contains("Blender", File.ReadAllText(Path.Combine(root, "ai-game-dev-app", "SKILL.md")));
                Assert.Equal(new[] { "guide-test" }, plugin.McpManager.ToolManager!.GetAllTools().Select(t => t.Name));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [AiToolType]
        public class TestTools
        {
            [AiTool("guide-test")]
            public string Echo() => "ok";
        }
    }
}
