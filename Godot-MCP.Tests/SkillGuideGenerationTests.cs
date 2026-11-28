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
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
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
        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GuideContainsOnlyTheTargetShellAndExecutableExamples(bool windows)
        {
            var guide = Skill_Cli.BuildMarkdown(windows);
            Assert.Contains(windows ? "```powershell" : "```bash", guide);
            Assert.DoesNotContain(windows ? "```bash" : "```powershell", guide);
            Assert.Contains(windows ? "npm.cmd install -g godot-cli" : "npm install -g godot-cli", guide);
            Assert.Contains("setup-skills claude-code", guide);
            Assert.DoesNotContain("rql-schema", guide);
            Assert.DoesNotContain("<tool-name>", guide);
            Assert.DoesNotMatch("@[A-Z_]+@", guide);
            Assert.DoesNotContain("<<'EOF'", guide);
        }

        [Fact]
        public async Task GeneratedCommandRoundTripsQuotesUnicodeAndNewlinesThroughTheHostShell()
        {
            var windows = Skill_Cli.IsWindows;
            const string json = "{\"text\":\"Bob's hero \\\"quoted\\\" C:\\\\Games\\\\Hero $HOME `tick`\\n\\u041f\\u0440\\u0438\\u0432\\u0435\\u0442\"}";
            var block = Skill_Cli.ToolCommand("run-tool", "example-tool", json, windows);
            var command = block.Substring(block.IndexOf('\n') + 1);
            command = command.Substring(0, command.LastIndexOf("```", StringComparison.Ordinal));
            var script = windows
                ? "$env:PATH = (Get-Location).Path + ';' + $env:PATH\n" + command
                : Skill_Cli.JsonFile(json, false) + "\ngodot-cli() { printf '%s' \"$4\"; }\n" + command;
            var root = Path.Combine(Path.GetTempPath(), "Skill shell path " + Guid.NewGuid().ToString("N"));
            var dir = Path.Combine(root, "project with spaces");
            Directory.CreateDirectory(dir);
            if (windows)
                script = "$ErrorActionPreference='Stop'\n$ProgressPreference='SilentlyContinue'\nSet-Location -LiteralPath '" +
                    dir.Replace("'", "''") + "'\n" + script + "\nif ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }\n";
            try
            {
                if (windows)
                    File.WriteAllText(Path.Combine(dir, "godot-cli.cmd"),
                        "@echo off\r\nif not \"%~1\"==\"run-tool\" exit /b 2\r\nif not \"%~3\"==\"--input-file\" exit /b 3\r\ntype \"%~4\"\r\n");
                var start = new ProcessStartInfo(windows ? "powershell.exe" : "/bin/bash")
                {
                    WorkingDirectory = windows ? root : dir, UseShellExecute = false,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                };
                if (windows)
                {
                    start.ArgumentList.Add("-NoProfile");
                    start.ArgumentList.Add("-NonInteractive");
                    start.ArgumentList.Add("-EncodedCommand");
                    start.ArgumentList.Add(Convert.ToBase64String(Encoding.Unicode.GetBytes(script)));
                }
                else { start.ArgumentList.Add("-c"); start.ArgumentList.Add(script); }
                using var process = Process.Start(start)!;
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(20000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Skill shell command hung"); }
                var diagnostics = await error;
                Assert.True(process.ExitCode == 0, $"Shell exited {process.ExitCode}: {diagnostics}");
                Assert.Equal(json, (await output).Trim());
                var bytes = File.ReadAllBytes(Path.Combine(dir, "args.json"));
                Assert.False(bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf);
                Assert.Equal(json, File.ReadAllText(Path.Combine(dir, "args.json")).TrimEnd('\n', '\r'));

            }
            finally { Directory.Delete(root, recursive: true); }
        }

        [Fact]
        public void ToolSkillsAreExcludedAndStandaloneGuidesIncludedForTheApp()
        {
            var root = Path.Combine(Path.GetTempPath(), "GodotSkillGuides_" + Guid.NewGuid().ToString("N"));
            try
            {
                var builder = new McpPluginBuilder(new com.IvanMurzak.McpPlugin.Common.Version());
                builder.WithConfig(config =>
                {
                    config.SkillsPath = root;
                    config.KeepConnected = false;
                    GodotSkillFileGenerator.ApplySkillMetadata(config);
                });
                builder.WithSkillFileGenerator<GodotSkillFileGenerator>();
                builder.WithSkills(typeof(Skill_Cli), typeof(Skill_App));
                builder.WithTools(typeof(TestTools));
                using var plugin = (com.IvanMurzak.McpPlugin.McpPlugin)builder.Build(new Reflector());

                Assert.Equal(3, Directory.GetFiles(root, "SKILL.md", SearchOption.AllDirectories).Length);
                string Read(string id) => File.ReadAllText(Path.Combine(root, id, "SKILL.md")).Replace("\r\n", "\n");
                Assert.Contains("metadata:\n  generated-by: godot-mcp\n  ai-game-dev: exclude\n", Read("guide-test"));
                foreach (var id in new[] { Skill_Cli.SkillId, Skill_App.SkillId })
                {
                    Assert.Contains("metadata:\n  generated-by: godot-mcp\n  ai-game-dev: include\n", Read(id));
                    Assert.DoesNotContain("ai-game-dev: exclude", Read(id));
                }
                var tool = File.ReadAllText(Path.Combine(root, "guide-test", "SKILL.md"));
                Assert.Contains("the `godot-cli` skill", tool);
                Assert.Contains("installation: `godot-cli-install`", tool);
                Assert.Contains("ai-game-dev-app", tool);
                Assert.DoesNotContain("npm install", tool);
                Assert.Contains(Skill_Cli.IsWindows ? "npm.cmd install -g godot-cli" : "npm install -g godot-cli", File.ReadAllText(Path.Combine(root, "godot-cli-install", "SKILL.md")));
                Assert.False(Directory.Exists(Path.Combine(root, "godot-cli")), "godot-cli belongs to the CLI's setup-skills");
                Assert.Contains("Blender", File.ReadAllText(Path.Combine(root, "ai-game-dev-app", "SKILL.md")));
                Assert.Equal(new[] { "guide-test" }, plugin.McpManager.ToolManager!.GetAllTools().Select(t => t.Name));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        [Fact]
        public void A_hidden_deprecated_alias_gets_no_skill_and_its_old_skill_is_removed()
        {
            var root = Path.Combine(Path.GetTempPath(), "GodotSkillAliases_" + Guid.NewGuid().ToString("N"));
            try
            {
                // The folder an addon older than the rename (DEV-314) generated for the old id.
                var stale = Path.Combine(root, DeprecatedToolAliases.ScriptExecute);
                Directory.CreateDirectory(stale);
                File.WriteAllText(Path.Combine(stale, "SKILL.md"), "old");

                var builder = new McpPluginBuilder(new com.IvanMurzak.McpPlugin.Common.Version());
                builder.WithConfig(config =>
                {
                    config.SkillsPath = root;
                    config.KeepConnected = false;
                });
                builder.WithSkillFileGenerator<GodotSkillFileGenerator>();
                builder.WithTools(typeof(AliasTools));
                using var plugin = (com.IvanMurzak.McpPlugin.McpPlugin)builder.Build(new Reflector());
                Assert.True(plugin.GenerateSkillFiles(root));

                Assert.True(File.Exists(Path.Combine(root, DeprecatedToolAliases.CsharpExec, "SKILL.md")));
                Assert.False(Directory.Exists(stale));
            }
            finally
            {
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
        }

        // No [AiToolType]: an assembly scan in another test must not pick these names up.
        public class AliasTools
        {
            [AiTool(DeprecatedToolAliases.CsharpExec)]
            public string Exec(string csharpCode) => csharpCode;

            [AiTool(DeprecatedToolAliases.ScriptExecute, Visible = false)]
            public string Alias(string csharpCode) => csharpCode;
        }

        [AiToolType]
        public class TestTools
        {
            [AiTool("guide-test")]
            public string Echo() => "ok";
        }
    }
}
