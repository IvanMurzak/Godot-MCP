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
using com.IvanMurzak.Godot.MCP.UI.Agents;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed AI-agent alert presentation (<see cref="AgentAlertView"/>) that drives the dock's
    /// "Setup Required" / "Reconfiguration Required" amber panel: the show/hide rule and the title/message copy per
    /// <see cref="AgentConfigState"/>. The editor wiring (<c>AgentConfiguratorsPanel.RefreshAlert</c>, <c>#if TOOLS</c>)
    /// builds the live <c>DockStyle.AlertPanel</c> Control and is verified via the headless Godot smoke
    /// (test.md Suite 3) — NOT here.
    /// </summary>
    public class AgentAlertViewTests
    {
        [Theory]
        [InlineData(AgentConfigState.Missing, true)]
        [InlineData(AgentConfigState.Stale, true)]
        [InlineData(AgentConfigState.UpToDate, false)]
        public void ShowAlert_OnlyWhenNotUpToDate(AgentConfigState state, bool expected)
        {
            Assert.Equal(expected, AgentAlertView.ShowAlert(state));
        }

        [Fact]
        public void Title_Missing_IsSetupRequired()
        {
            Assert.Equal("Setup Required", AgentAlertView.Title(AgentConfigState.Missing));
            Assert.Equal(AgentAlertView.SetupRequiredTitle, AgentAlertView.Title(AgentConfigState.Missing));
        }

        [Fact]
        public void Title_Stale_IsReconfigurationRequired()
        {
            Assert.Equal("Reconfiguration Required", AgentAlertView.Title(AgentConfigState.Stale));
            Assert.Equal(AgentAlertView.ReconfigurationRequiredTitle, AgentAlertView.Title(AgentConfigState.Stale));
        }

        [Fact]
        public void Title_UpToDate_IsEmpty()
        {
            // The alert is hidden when up-to-date; the title is empty (callers check ShowAlert first).
            Assert.Equal(string.Empty, AgentAlertView.Title(AgentConfigState.UpToDate));
        }

        [Fact]
        public void Message_Missing_IsSetupRequiredCopy()
        {
            Assert.Equal(AgentAlertView.SetupRequiredMessage, AgentAlertView.Message(AgentConfigState.Missing));
            Assert.StartsWith("At least one of the following must be configured:", AgentAlertView.Message(AgentConfigState.Missing));
            Assert.Contains("• MCP Configuration", AgentAlertView.Message(AgentConfigState.Missing));
        }

        [Fact]
        public void Message_Stale_IsReconfigurationCopy()
        {
            Assert.Equal(AgentAlertView.ReconfigurationRequiredMessage, AgentAlertView.Message(AgentConfigState.Stale));
            Assert.Contains("outdated", AgentAlertView.Message(AgentConfigState.Stale));
        }

        [Fact]
        public void Message_UpToDate_IsEmpty()
        {
            Assert.Equal(string.Empty, AgentAlertView.Message(AgentConfigState.UpToDate));
        }

        [Theory]
        [InlineData(AgentConfigState.Missing, "Configure")]
        [InlineData(AgentConfigState.Stale, "Reconfigure")]
        [InlineData(AgentConfigState.UpToDate, "")]
        public void ButtonText_PerState(AgentConfigState state, string expected)
        {
            Assert.Equal(expected, AgentAlertView.ButtonText(state));
        }
    }
}
