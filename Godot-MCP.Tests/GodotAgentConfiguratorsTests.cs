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

        [Theory]
        [InlineData(ConnectionMode.Local)]
        [InlineData(ConnectionMode.Cloud)]
        public void ClaudeDesktop_DescribesTheHttpTransportAsUnsupported_InBothModes(ConnectionMode mode)
        {
            var agent = GodotAgentConfigurators.GetByAgentId(ClaudeDesktopId)!;
            var settings = Settings(mode);

            var description = agent.Describe(settings, GodotAgentConfigurators.Transport, NullLogger.Instance);

            Assert.False(description.TransportSupported);
            Assert.False(description.IsConfigured);
            Assert.Equal(ConfiguratorStatus.NotConfigured, agent.GetStatus(settings, GodotAgentConfigurators.Transport, NullLogger.Instance));
        }

        [Theory]
        [InlineData(ConnectionMode.Local)]
        [InlineData(ConnectionMode.Cloud)]
        public void EveryConfigurableAgent_DescribesTheHttpTransportAsSupported(ConnectionMode mode)
        {
            var settings = Settings(mode);
            foreach (var agent in GodotAgentConfigurators.All.Where(GodotAgentConfigurators.CanConfigure))
                Assert.True(agent.Describe(settings, GodotAgentConfigurators.Transport, NullLogger.Instance).TransportSupported, agent.AgentId);
        }

        [Fact]
        public void NoAgent_HasARestartNotice_ForTheHttpTransport()
        {
            // HTTP agents connect to the running server, so nothing asks for a restart after Configure / Remove.
            foreach (var agent in GodotAgentConfigurators.All)
                Assert.Null(agent.GetRestartNotice(GodotAgentConfigurators.Transport));
        }

        [Fact]
        public void UnsupportedTransportMessage_NamesTheAgent_AndOffersNoStdioSetup()
        {
            var message = GodotAgentConfigurators.UnsupportedTransportMessage("Claude Desktop");

            Assert.Contains("Claude Desktop supports only the stdio transport", message);
            Assert.Contains("HTTP only", message);
            // The shared alert's "select the stdio transport" step does not exist in the Godot dock.
            Assert.DoesNotContain("Select the Custom connection", message);
        }
    }
}
