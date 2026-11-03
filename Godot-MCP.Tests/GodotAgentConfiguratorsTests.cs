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
using System.Linq;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.McpPlugin.AgentConfig;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins which agents the dock may Configure over Godot's only transport (HTTP). Claude Desktop starts only stdio
    /// servers from claude_desktop_config.json and skips an HTTP entry as invalid, so Godot must never write one.
    /// </summary>
    public class GodotAgentConfiguratorsTests
    {
        const string ClaudeDesktopId = "claude-desktop";

        static AgentConfiguratorSettings Settings(ConnectionMode mode) =>
            AgentConfiguratorSettings.CreateForHost(
                projectRootPath: "/home/dev/projects/MyGame",
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: mode == ConnectionMode.Cloud ? "https://ai-game.dev/mcp" : "http://localhost:26610",
                connectionMode: mode);

        [Fact]
        public void Transport_IsStreamableHttp()
            => Assert.Equal(TransportMethod.streamableHttp, GodotAgentConfigurators.Transport);

        [Fact]
        public void ClaudeDesktop_CannotBeConfigured_EveryOtherAgentCan()
        {
            var blocked = GodotAgentConfigurators.All
                .Where(a => !GodotAgentConfigurators.CanConfigure(a))
                .Select(a => a.AgentId)
                .ToList();

            Assert.Equal(new[] { ClaudeDesktopId }, blocked);
        }

        [Fact]
        public void ClaudeDesktop_WithACustomServer_IsUnsupported()
        {
            var agent = GodotAgentConfigurators.GetByAgentId(ClaudeDesktopId)!;
            var settings = Settings(ConnectionMode.Local);

            var description = agent.Describe(settings, GodotAgentConfigurators.Transport, NullLogger.Instance);

            Assert.Equal(AgentSetupKind.Unsupported, description.SetupKind);
            Assert.False(description.IsConfigured);
            Assert.Equal(ConfiguratorStatus.NotConfigured, agent.GetStatus(settings, GodotAgentConfigurators.Transport, NullLogger.Instance));
        }

        [Fact]
        public void ClaudeDesktop_InCloudMode_IsAManualClaudeConnector_WithTheProjectPinnedUrl()
        {
            // The panel renders these sections as they are: the link must be clickable and the URL copyable.
            var agent = GodotAgentConfigurators.GetByAgentId(ClaudeDesktopId)!;
            var settings = Settings(ConnectionMode.Cloud);

            var description = agent.Describe(settings.ForDisplay(), GodotAgentConfigurators.Transport, NullLogger.Instance);
            var items = description.Sections.SelectMany(s => s.Items).ToList();

            Assert.Equal(AgentSetupKind.Manual, description.SetupKind);
            Assert.False(description.TransportSupported); // so no Godot path writes claude_desktop_config.json
            Assert.False(GodotAgentConfigurators.CanConfigure(agent));
            Assert.Contains(items, i => i.Kind == ConfigurationItemKind.Link && i.Url == "https://claude.ai/customize/connectors");
            Assert.Contains(items, i => i.Kind == ConfigurationItemKind.ReadOnlyField && i.Text == settings.PinnedHttpUrl);
            Assert.Contains("/mcp/p/", settings.PinnedHttpUrl);
            Assert.DoesNotContain(items, i => i.Kind == ConfigurationItemKind.Alert);
        }

        [Theory]
        [InlineData(ConnectionMode.Local)]
        [InlineData(ConnectionMode.Cloud)]
        public void EveryConfigurableAgent_DescribesTheHttpTransportAsSupported(ConnectionMode mode)
        {
            var settings = Settings(mode);
            foreach (var agent in GodotAgentConfigurators.All.Where(GodotAgentConfigurators.CanConfigure))
                Assert.Equal(AgentSetupKind.ConfigFile, agent.Describe(settings, GodotAgentConfigurators.Transport, NullLogger.Instance).SetupKind);
        }

        [Fact]
        public void NoAgent_HasARestartNotice_ForTheHttpTransport()
        {
            // HTTP agents connect to the running server, so nothing asks for a restart after Configure / Remove.
            foreach (var agent in GodotAgentConfigurators.All)
                Assert.Null(agent.GetRestartNotice(GodotAgentConfigurators.Transport));
        }

        [Fact]
        public void UnsupportedTransportMessage_PointsAtTheCloudConnector_AndOffersNoStdioSetup()
        {
            var message = GodotAgentConfigurators.UnsupportedTransportMessage("Claude Desktop");

            Assert.Contains("Switch the connection to Cloud to add Claude Desktop as a Claude connector", message);
            Assert.DoesNotContain("stdio", message);
            // The shared alert's "select the stdio transport" step does not exist in the Godot dock.
            Assert.DoesNotContain("Select the Custom connection", message);
        }
    }
}
