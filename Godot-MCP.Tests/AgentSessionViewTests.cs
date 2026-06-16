/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;
using McpClientData = com.IvanMurzak.McpPlugin.Common.Model.McpClientData;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public class AgentSessionViewTests
    {
        [Theory]
        [InlineData(0, ConnectionPanelView.TimelinePointState.Disconnected)]
        [InlineData(-1, ConnectionPanelView.TimelinePointState.Disconnected)]
        [InlineData(1, ConnectionPanelView.TimelinePointState.Online)]
        [InlineData(5, ConnectionPanelView.TimelinePointState.Online)]
        public void DotState_GreenOnlyWhenAgentsConnected(int count, ConnectionPanelView.TimelinePointState expected)
        {
            Assert.Equal(expected, AgentSessionView.DotState(count));
        }

        [Theory]
        [InlineData(0, "(connects on demand)")]
        [InlineData(-3, "(connects on demand)")]
        [InlineData(1, "(1 connected)")]
        [InlineData(2, "(2 connected)")]
        [InlineData(7, "(7 connected)")]
        public void Summary_MatchesCount(int count, string expected)
        {
            Assert.Equal(expected, AgentSessionView.Summary(count));
        }

        [Fact]
        public void DisplayName_PrefersTitleThenNameThenSessionThenFallback()
        {
            Assert.Equal("GitHub Copilot",
                AgentSessionView.DisplayName(new McpClientData { ClientTitle = "GitHub Copilot", ClientName = "copilot" }));
            Assert.Equal("copilot",
                AgentSessionView.DisplayName(new McpClientData { ClientTitle = "  ", ClientName = "copilot" }));
            Assert.Equal("session 1234abcd",
                AgentSessionView.DisplayName(new McpClientData { SessionId = "1234abcd-and-more" }));
            Assert.Equal("AI agent", AgentSessionView.DisplayName(new McpClientData()));
            Assert.Equal("AI agent", AgentSessionView.DisplayName(null));
        }

        [Fact]
        public void RowLabel_AppendsVersionWhenPresent()
        {
            Assert.Equal("GitHub Copilot (1.2.0)",
                AgentSessionView.RowLabel(new McpClientData { ClientTitle = "GitHub Copilot", ClientVersion = "1.2.0" }));
            Assert.Equal("Claude",
                AgentSessionView.RowLabel(new McpClientData { ClientName = "Claude", ClientVersion = "  " }));
            Assert.Equal("AI agent", AgentSessionView.RowLabel(null));
        }
    }
}
