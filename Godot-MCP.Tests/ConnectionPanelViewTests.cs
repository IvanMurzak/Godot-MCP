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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using Microsoft.AspNetCore.SignalR.Client;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed presentation logic of the connection panel (<see cref="ConnectionPanelView"/>):
    /// the <see cref="HubConnectionState"/> + <c>keepConnected</c> → <see cref="ConnectionStatus"/> reduction,
    /// the label/button-text/disabled/colour mappings, and the server-URL validation rule. The editor
    /// Control wiring (<c>ConnectionPanel.cs</c>, <c>#if TOOLS</c>) instantiates live Godot nodes and is
    /// verified via the headless Godot smoke (test.md Suite 3) — NOT here.
    ///
    /// <para>
    /// Also covers the mode-toggle persistence round-trip via <see cref="GodotMcpConfigStore"/>, which is
    /// exactly what the panel's mode selector triggers (write <c>ConnectionMode</c> → <c>Save()</c>).
    /// </para>
    /// </summary>
    public class ConnectionPanelViewTests
    {
        // --- Reduce: HubConnectionState + keepConnected -> ConnectionStatus ---

        [Fact]
        public void Reduce_Connected_AndKeepConnected_IsConnected()
        {
            Assert.Equal(ConnectionStatus.Connected,
                ConnectionPanelView.Reduce(HubConnectionState.Connected, keepConnected: true));
        }

        [Fact]
        public void Reduce_Connected_ButNotKeepConnected_IsDisconnected()
        {
            // KeepConnected off means the user asked to disconnect; even a momentarily-Connected hub
            // should read as Disconnected (matches the Unity reference's GetConnectionStatusClass).
            Assert.Equal(ConnectionStatus.Disconnected,
                ConnectionPanelView.Reduce(HubConnectionState.Connected, keepConnected: false));
        }

        [Theory]
        [InlineData(HubConnectionState.Connecting)]
        [InlineData(HubConnectionState.Reconnecting)]
        [InlineData(HubConnectionState.Disconnected)]
        public void Reduce_NotConnected_WhileKeepConnected_IsConnecting(HubConnectionState state)
        {
            // KeepConnected on + not yet Connected = the client is trying/retrying => Connecting.
            Assert.Equal(ConnectionStatus.Connecting,
                ConnectionPanelView.Reduce(state, keepConnected: true));
        }

        [Theory]
        [InlineData(HubConnectionState.Connecting)]
        [InlineData(HubConnectionState.Reconnecting)]
        [InlineData(HubConnectionState.Disconnected)]
        public void Reduce_NotConnected_NotKeepConnected_IsDisconnected(HubConnectionState state)
        {
            Assert.Equal(ConnectionStatus.Disconnected,
                ConnectionPanelView.Reduce(state, keepConnected: false));
        }

        // --- Reduce: exhaustive matrix (every HubConnectionState × keepConnected → expected status) ---

        [Theory]
        // keepConnected = true
        [InlineData(HubConnectionState.Connected, true, ConnectionStatus.Connected)]
        [InlineData(HubConnectionState.Connecting, true, ConnectionStatus.Connecting)]
        [InlineData(HubConnectionState.Reconnecting, true, ConnectionStatus.Connecting)]
        [InlineData(HubConnectionState.Disconnected, true, ConnectionStatus.Connecting)]
        // keepConnected = false (the client is not trying to be connected → always Disconnected)
        [InlineData(HubConnectionState.Connected, false, ConnectionStatus.Disconnected)]
        [InlineData(HubConnectionState.Connecting, false, ConnectionStatus.Disconnected)]
        [InlineData(HubConnectionState.Reconnecting, false, ConnectionStatus.Disconnected)]
        [InlineData(HubConnectionState.Disconnected, false, ConnectionStatus.Disconnected)]
        public void Reduce_FullMatrix(HubConnectionState state, bool keepConnected, ConnectionStatus expected)
        {
            Assert.Equal(expected, ConnectionPanelView.Reduce(state, keepConnected));
        }

        // --- StatusLabel ---

        [Theory]
        [InlineData(ConnectionStatus.Connected, "Connected")]
        [InlineData(ConnectionStatus.Connecting, "Connecting…")]
        [InlineData(ConnectionStatus.Disconnected, "Disconnected")]
        public void StatusLabel_MapsEachStatus(ConnectionStatus status, string expected)
        {
            Assert.Equal(expected, ConnectionPanelView.StatusLabel(status));
        }

        // --- ButtonText + ButtonDisabled ---

        [Theory]
        [InlineData(ConnectionStatus.Connected, ConnectionPanelView.ButtonTextDisconnect)]
        [InlineData(ConnectionStatus.Connecting, ConnectionPanelView.ButtonTextConnecting)]
        [InlineData(ConnectionStatus.Disconnected, ConnectionPanelView.ButtonTextConnect)]
        public void ButtonText_MapsEachStatus(ConnectionStatus status, string expected)
        {
            Assert.Equal(expected, ConnectionPanelView.ButtonText(status));
        }

        [Theory]
        [InlineData(ConnectionStatus.Connecting, true)]
        [InlineData(ConnectionStatus.Connected, false)]
        [InlineData(ConnectionStatus.Disconnected, false)]
        public void ButtonDisabled_OnlyWhileConnecting(ConnectionStatus status, bool expectedDisabled)
        {
            Assert.Equal(expectedDisabled, ConnectionPanelView.ButtonDisabled(status));
        }

        // --- StatusColor ---

        [Fact]
        public void StatusColor_DistinctPerStatus()
        {
            var connected = ConnectionPanelView.StatusColor(ConnectionStatus.Connected);
            var connecting = ConnectionPanelView.StatusColor(ConnectionStatus.Connecting);
            var disconnected = ConnectionPanelView.StatusColor(ConnectionStatus.Disconnected);

            Assert.Equal(ConnectionPanelView.ColorConnected, connected);
            Assert.Equal(ConnectionPanelView.ColorConnecting, connecting);
            Assert.Equal(ConnectionPanelView.ColorDisconnected, disconnected);

            // The three are visually distinct (green / amber / gray), not accidentally equal.
            Assert.NotEqual(connected, connecting);
            Assert.NotEqual(connecting, disconnected);
            Assert.NotEqual(connected, disconnected);
        }

        // --- IsValidServerUrl ---

        [Theory]
        [InlineData("http://localhost:8080")]
        [InlineData("https://ai-game.dev")]
        [InlineData("http://127.0.0.1:5300")]
        [InlineData("  http://localhost:9000  ")]   // surrounding whitespace trimmed
        [InlineData("\"https://example.com\"")]      // wrapping quotes trimmed
        [InlineData("https://example.com/")]          // trailing slash tolerated
        public void IsValidServerUrl_AcceptsAbsoluteHttpUrls(string url)
        {
            Assert.True(ConnectionPanelView.IsValidServerUrl(url));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("localhost:8080")]   // no scheme
        [InlineData("ftp://example.com")] // wrong scheme
        [InlineData("not a url")]
        [InlineData("/relative/path")]
        public void IsValidServerUrl_RejectsNonHttpOrRelative(string? url)
        {
            Assert.False(ConnectionPanelView.IsValidServerUrl(url));
        }

        // --- Mode-toggle persistence round-trip (what the panel's mode selector triggers) ---

        [Fact]
        public void ModeToggle_PersistsAndReloads()
        {
            var dir = Path.Combine(Path.GetTempPath(), "godot-mcp-panel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "godot-mcp-config.json");
            try
            {
                // Default config is Cloud; the panel flips it to Custom + saves.
                var config = new GodotMcpConfig { ConnectionMode = GodotMcpConnectionMode.Cloud };
                config.ConnectionMode = GodotMcpConnectionMode.Custom;
                GodotMcpConfigStore.Save(path, config);

                var reloaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(reloaded);
                Assert.Equal(GodotMcpConnectionMode.Custom, reloaded!.ConnectionMode);

                // And back to Cloud.
                reloaded.ConnectionMode = GodotMcpConnectionMode.Cloud;
                GodotMcpConfigStore.Save(path, reloaded);
                var reloaded2 = GodotMcpConfigStore.Load(path);
                Assert.NotNull(reloaded2);
                Assert.Equal(GodotMcpConnectionMode.Cloud, reloaded2!.ConnectionMode);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
            }
        }

        // --- Timeline circle state (ConnectionStatus -> TimelinePointState) ---

        [Theory]
        [InlineData(ConnectionStatus.Connected, ConnectionPanelView.TimelinePointState.Online)]
        [InlineData(ConnectionStatus.Connecting, ConnectionPanelView.TimelinePointState.Connecting)]
        [InlineData(ConnectionStatus.Disconnected, ConnectionPanelView.TimelinePointState.Disconnected)]
        public void PointState_MapsEachStatus(ConnectionStatus status, ConnectionPanelView.TimelinePointState expected)
        {
            Assert.Equal(expected, ConnectionPanelView.PointState(status));
        }

        // --- AgentLabel ---

        [Fact]
        public void AgentLabel_NoAgent_IsConnectsOnDemand()
        {
            Assert.Equal("AI agent (connects on demand)", ConnectionPanelView.AgentLabel(null, null));
            Assert.Equal("AI agent (connects on demand)", ConnectionPanelView.AgentLabel("", "1.0"));
            Assert.Equal("AI agent (connects on demand)", ConnectionPanelView.AgentLabel("   ", null));
        }

        [Fact]
        public void AgentLabel_NameOnly_OmitsVersionParens()
        {
            Assert.Equal("AI agent: Cursor", ConnectionPanelView.AgentLabel("Cursor", null));
            Assert.Equal("AI agent: Cursor", ConnectionPanelView.AgentLabel(" Cursor ", "  "));
        }

        [Fact]
        public void AgentLabel_NameAndVersion_IncludesParens()
        {
            Assert.Equal("AI agent: Claude (1.2.0)", ConnectionPanelView.AgentLabel("Claude", "1.2.0"));
            Assert.Equal("AI agent: Claude (1.2.0)", ConnectionPanelView.AgentLabel(" Claude ", " 1.2.0 "));
        }

        // --- Alert visibility: Authorization Required (Cloud, no token) ---

        [Theory]
        [InlineData(true, false, true)]    // cloud + no token -> show
        [InlineData(true, true, false)]    // cloud + token -> hide
        [InlineData(false, false, false)]  // custom -> hide regardless
        [InlineData(false, true, false)]
        public void ShowAuthorizationRequired_OnlyCloudWithoutToken(bool cloud, bool hasToken, bool expected)
        {
            Assert.Equal(expected, ConnectionPanelView.ShowAuthorizationRequired(cloud, hasToken));
        }

        // --- Alert visibility: Connection Required (ready but disconnected) ---

        [Theory]
        // Custom mode, disconnected -> show (Custom never needs a cloud token).
        [InlineData(false, false, ConnectionStatus.Disconnected, true)]
        // Custom mode, connecting / connected -> hide.
        [InlineData(false, false, ConnectionStatus.Connecting, false)]
        [InlineData(false, false, ConnectionStatus.Connected, false)]
        // Cloud + token, disconnected -> show.
        [InlineData(true, true, ConnectionStatus.Disconnected, true)]
        // Cloud + token, connected -> hide.
        [InlineData(true, true, ConnectionStatus.Connected, false)]
        // Cloud WITHOUT token -> the Authorization alert owns this; Connection alert stays hidden.
        [InlineData(true, false, ConnectionStatus.Disconnected, false)]
        [InlineData(true, false, ConnectionStatus.Connecting, false)]
        public void ShowConnectionRequired_RespectsModeTokenAndStatus(
            bool cloud, bool hasToken, ConnectionStatus status, bool expected)
        {
            Assert.Equal(expected, ConnectionPanelView.ShowConnectionRequired(cloud, hasToken, status));
        }

        [Fact]
        public void Alerts_AreMutuallyExclusive_InCloudWithoutToken()
        {
            // In Cloud-mode-without-token the Authorization alert shows and the Connection alert must NOT,
            // so the two never stack on the same condition.
            Assert.True(ConnectionPanelView.ShowAuthorizationRequired(true, false));
            Assert.False(ConnectionPanelView.ShowConnectionRequired(true, false, ConnectionStatus.Disconnected));
        }

        [Fact]
        public void CustomHost_PersistsAndReloads()
        {
            var dir = Path.Combine(Path.GetTempPath(), "godot-mcp-panel-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "godot-mcp-config.json");
            try
            {
                // The panel writes CustomHost on a valid URL submit, then Save()s.
                var config = new GodotMcpConfig { CustomHost = "http://localhost:5300" };
                GodotMcpConfigStore.Save(path, config);

                var reloaded = GodotMcpConfigStore.Load(path);
                Assert.NotNull(reloaded);
                Assert.Equal("http://localhost:5300", reloaded!.CustomHost);
            }
            finally
            {
                try { Directory.Delete(dir, recursive: true); } catch { /* best-effort */ }
            }
        }
    }
}
