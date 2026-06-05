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
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.Godot.MCP.UI.Agents.Impl;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed AI-agent configurator core: the snippet/entry shape (http type + url +
    /// headers-only-when-token + token masking), the READ-MERGE-WRITE file ops (Configure / IsConfigured /
    /// Remove round-trip on a temp file, robust to missing/empty/invalid files, never clobbering siblings), the
    /// per-OS path resolver, and the registry. The dock UI (<c>AgentConfiguratorsPanel.cs</c>, <c>#if TOOLS</c>)
    /// is verified via the headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class AgentConfiguratorTests
    {
        const string Url = "https://ai-game.dev/mcp";
        const string Token = "secret-bearer-xyz";

        static GodotAgentConfigurator ClaudeCode() => new ClaudeCodeConfigurator();

        // --- BuildServerEntry / snippet shape ---

        [Fact]
        public void ServerEntry_IsHttp_WithUrl_AndNoHeadersWhenNoToken()
        {
            var entry = AgentConfigJson.BuildServerEntry(Url, token: null);
            Assert.Equal("http", entry["type"]!.GetValue<string>());
            Assert.Equal(Url, entry["url"]!.GetValue<string>());
            Assert.False(entry.ContainsKey("headers"));
        }

        [Fact]
        public void ServerEntry_AddsAuthorizationHeader_WhenTokenPresent()
        {
            var entry = AgentConfigJson.BuildServerEntry(Url, Token);
            var auth = entry["headers"]!["Authorization"]!.GetValue<string>();
            Assert.Equal($"Bearer {Token}", auth);
        }

        [Fact]
        public void Snippet_MasksTheToken_WhenMaskRequested()
        {
            var snippet = ClaudeCode().BuildSnippet(Url, Token, maskToken: true);
            // The real token must NOT appear in the masked snippet.
            Assert.DoesNotContain(Token, snippet);
            Assert.Contains("Bearer ****", snippet);
        }

        [Fact]
        public void Snippet_ContainsRealToken_WhenNotMasked()
        {
            var snippet = ClaudeCode().BuildSnippet(Url, Token, maskToken: false);
            Assert.Contains($"Bearer {Token}", snippet);
        }

        [Fact]
        public void Snippet_NestsEntryUnderBodyPathAndServerKey()
        {
            var agent = ClaudeCode();
            var snippet = agent.BuildSnippet(Url, token: null, maskToken: false);
            var root = JsonNode.Parse(snippet)!.AsObject();
            var entry = root[agent.BodyPath]![agent.ServerKey]!.AsObject();
            Assert.Equal(Url, entry["url"]!.GetValue<string>());
        }

        [Fact]
        public void Snippet_VsCode_UsesServersBodyPath()
        {
            var vscode = new VisualStudioCodeConfigurator();
            Assert.Equal("servers", vscode.BodyPath);
            var root = JsonNode.Parse(vscode.BuildSnippet(Url, null, false))!.AsObject();
            Assert.NotNull(root["servers"]);
            Assert.Null(root["mcpServers"]);
        }

        // --- Configure / IsConfigured / Remove round-trip ---

        [Fact]
        public void Configure_CreatesEntry_AndIsConfigured_ThenRemove_RemovesIt()
        {
            var agent = ClaudeCode();
            var path = TempFile();
            try
            {
                Assert.False(agent.IsConfigured(path, Url)); // missing file

                agent.Configure(path, Url, Token);
                Assert.True(File.Exists(path));
                Assert.True(agent.IsConfigured(path, Url));

                // The real token landed in the written file.
                Assert.Contains($"Bearer {Token}", File.ReadAllText(path));

                Assert.True(agent.Remove(path));
                Assert.False(agent.IsConfigured(path, Url));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void Configure_DoesNotClobberPreexistingUnrelatedServer()
        {
            var agent = ClaudeCode();
            var path = TempFile();
            try
            {
                // Seed a config with an unrelated sibling server + an unrelated top-level key.
                File.WriteAllText(path,
                    "{ \"mcpServers\": { \"some-other-server\": { \"type\": \"http\", \"url\": \"http://other\" } }, \"unrelated\": 42 }");

                agent.Configure(path, Url, Token);

                var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
                var servers = root["mcpServers"]!.AsObject();

                // Both the sibling and the addon entry must be present; the unrelated key preserved.
                Assert.True(servers.ContainsKey("some-other-server"));
                Assert.True(servers.ContainsKey(agent.ServerKey));
                Assert.Equal("http://other", servers["some-other-server"]!["url"]!.GetValue<string>());
                Assert.Equal(42, root["unrelated"]!.GetValue<int>());

                // Remove drops only the addon entry, keeping the sibling.
                agent.Remove(path);
                servers = JsonNode.Parse(File.ReadAllText(path))!.AsObject()["mcpServers"]!.AsObject();
                Assert.True(servers.ContainsKey("some-other-server"));
                Assert.False(servers.ContainsKey(agent.ServerKey));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void IsConfigured_FalseWhenUrlDiffers()
        {
            var agent = ClaudeCode();
            var path = TempFile();
            try
            {
                agent.Configure(path, "https://ai-game.dev/mcp", Token);
                Assert.False(agent.IsConfigured(path, "http://localhost:8080/mcp"));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Theory]
        [InlineData("")]                       // empty file
        [InlineData("   ")]                     // whitespace
        [InlineData("{ not valid json ")]       // malformed
        [InlineData("[1,2,3]")]                 // valid json but not an object
        public void FileOps_AreRobustToMissingEmptyOrInvalidFile(string content)
        {
            var agent = ClaudeCode();
            var path = TempFile();
            try
            {
                File.WriteAllText(path, content);

                // IsConfigured on a bad file is false (no throw); Remove is a no-op (returns false).
                Assert.False(agent.IsConfigured(path, Url));
                Assert.False(agent.Remove(path));

                // Configure recovers by starting fresh — entry lands, file becomes valid.
                agent.Configure(path, Url, Token);
                Assert.True(agent.IsConfigured(path, Url));
            }
            finally
            {
                Cleanup(path);
            }
        }

        [Fact]
        public void Configure_CreatesMissingParentDirectories()
        {
            var agent = ClaudeCode();
            var dir = Path.Combine(Path.GetTempPath(), "godot-mcp-agent-" + Guid.NewGuid().ToString("N"), "nested");
            var path = Path.Combine(dir, ".mcp.json");
            try
            {
                Assert.False(Directory.Exists(dir));
                agent.Configure(path, Url, Token);
                Assert.True(File.Exists(path));
            }
            finally
            {
                if (Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(path))!))
                    Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(path))!, recursive: true);
            }
        }

        // --- Registry ---

        [Fact]
        public void Registry_CustomConfiguratorIsLast()
        {
            var all = GodotAgentConfiguratorRegistry.All;
            Assert.IsType<CustomConfigurator>(all[^1]);
        }

        [Fact]
        public void Registry_AgentIdsAreUnique()
        {
            var ids = GodotAgentConfiguratorRegistry.All.Select(c => c.AgentId).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }

        [Fact]
        public void Registry_AgentNamesAreUnique_AndMatchOrder()
        {
            var names = GodotAgentConfiguratorRegistry.AgentNames;
            Assert.Equal(names.Count, names.Distinct().Count());
            Assert.Equal(GodotAgentConfiguratorRegistry.All.Count, names.Count);
            Assert.Equal(GodotAgentConfiguratorRegistry.All.Select(c => c.AgentName), names);
        }

        [Fact]
        public void Registry_LookupsByIdAndIndexAgree()
        {
            var index = GodotAgentConfiguratorRegistry.GetIndexByAgentId("cursor");
            Assert.True(index >= 0);
            var byId = GodotAgentConfiguratorRegistry.GetByAgentId("cursor");
            Assert.NotNull(byId);
            Assert.Equal("cursor", byId!.AgentId);
            Assert.Same(byId, GodotAgentConfiguratorRegistry.All[index]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("does-not-exist")]
        public void Registry_UnknownId_ReturnsNullAndMinusOne(string? id)
        {
            Assert.Null(GodotAgentConfiguratorRegistry.GetByAgentId(id));
            Assert.Equal(-1, GodotAgentConfiguratorRegistry.GetIndexByAgentId(id));
        }

        [Fact]
        public void CustomConfigurator_HasNoConfigPath_SnippetOnly()
        {
            var custom = new CustomConfigurator();
            Assert.Null(custom.ConfigFilePath(AgentOs.Windows, @"C:\Users\u", @"C:\Users\u\AppData\Roaming", @"C:\proj"));
        }

        // --- Per-OS path resolution (injected OS / home / appData / projectRoot) ---

        [Fact]
        public void ClaudeCode_PathIsProjectLocalMcpJson()
        {
            var agent = new ClaudeCodeConfigurator();
            var path = agent.ConfigFilePath(AgentOs.Linux, "/home/u", "", "/proj")!;
            Assert.Equal(Path.Combine("/proj", ".mcp.json"), path);
        }

        [Fact]
        public void Cursor_PathIsProjectLocalCursorMcpJson()
        {
            var path = new CursorConfigurator().ConfigFilePath(AgentOs.Linux, "/home/u", "", "/proj")!;
            Assert.Equal(Path.Combine("/proj", ".cursor", "mcp.json"), path);
        }

        [Fact]
        public void VsCode_PathIsProjectLocalVscodeMcpJson()
        {
            var path = new VisualStudioCodeConfigurator().ConfigFilePath(AgentOs.Linux, "/home/u", "", "/proj")!;
            Assert.Equal(Path.Combine("/proj", ".vscode", "mcp.json"), path);
        }

        [Theory]
        [InlineData(AgentOs.Windows)]
        [InlineData(AgentOs.MacOS)]
        [InlineData(AgentOs.Linux)]
        public void ClaudeDesktop_PathIsPerOs(AgentOs os)
        {
            var agent = new ClaudeDesktopConfigurator();
            var home = os == AgentOs.Windows ? @"C:\Users\u" : "/home/u";
            var appData = @"C:\Users\u\AppData\Roaming";
            var path = agent.ConfigFilePath(os, home, appData, "/proj")!;

            Assert.EndsWith("claude_desktop_config.json", path);
            switch (os)
            {
                case AgentOs.Windows:
                    Assert.Equal(Path.Combine(appData, "Claude", "claude_desktop_config.json"), path);
                    break;
                case AgentOs.MacOS:
                    Assert.Equal(Path.Combine(home, "Library", "Application Support", "Claude", "claude_desktop_config.json"), path);
                    break;
                default:
                    Assert.Equal(Path.Combine(home, ".config", "Claude", "claude_desktop_config.json"), path);
                    break;
            }
        }

        // --- helpers ---

        static string TempFile() =>
            Path.Combine(Path.GetTempPath(), "godot-mcp-agent-" + Guid.NewGuid().ToString("N") + ".json");

        static void Cleanup(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* best-effort */ }
        }
    }
}
