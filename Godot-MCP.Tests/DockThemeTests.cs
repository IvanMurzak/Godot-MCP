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
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed dock design palette (<see cref="DockTheme"/>): the brief's status-dot colours, the
    /// <see cref="ConnectionStatus"/> → dot-colour mapping, and a couple of the load-bearing 8-bit→float
    /// translations. The editor StyleBox/Theme wiring (<c>DockStyle.cs</c>, <c>#if TOOLS</c>) is verified via the
    /// headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class DockThemeTests
    {
        [Fact]
        public void StatusDotColor_MapsStatesToBriefPalette()
        {
            Assert.Equal(DockTheme.StatusOnline, DockTheme.StatusDotColor(ConnectionStatus.Connected));
            Assert.Equal(DockTheme.StatusConnecting, DockTheme.StatusDotColor(ConnectionStatus.Connecting));
            Assert.Equal(DockTheme.StatusDisconnected, DockTheme.StatusDotColor(ConnectionStatus.Disconnected));
        }

        [Fact]
        public void StatusColors_Match8BitSourceValues()
        {
            // online green Color8(111,226,101)
            AssertRgb8(DockTheme.StatusOnline, 111, 226, 101);
            // disconnected orange Color8(220,76,9)
            AssertRgb8(DockTheme.StatusDisconnected, 220, 76, 9);
        }

        [Fact]
        public void CardBackground_IsDarkBlueTint_WithExpectedAlpha()
        {
            // Color(20/255, 40/255, 69/255, 0.2)
            Assert.Equal(20f / 255f, DockTheme.CardBackground.R, 5);
            Assert.Equal(40f / 255f, DockTheme.CardBackground.G, 5);
            Assert.Equal(69f / 255f, DockTheme.CardBackground.B, 5);
            Assert.Equal(0.2f, DockTheme.CardBackground.A, 5);
        }

        [Fact]
        public void CardSizing_MatchesBrief()
        {
            Assert.Equal(16, DockTheme.CardCornerRadius);
            Assert.Equal(8, DockTheme.CardContentPadding);
            Assert.Equal(8, DockTheme.CardMargin);
            Assert.Equal(14, DockTheme.StatusDotSize);
        }

        [Fact]
        public void Typography_FontSizes_MatchBrief()
        {
            Assert.Equal(20, DockTheme.FontSizeHeader);
            Assert.Equal(16, DockTheme.FontSizeSectionTitle);
            Assert.Equal(13, DockTheme.FontSizeSubLabel);
        }

        [Fact]
        public void LinkColor_Is4FC3F7()
        {
            AssertRgb8(DockTheme.Link, 79, 195, 247); // #4FC3F7
        }

        [Fact]
        public void RowTint_MapsEnabledToGreenDisabledToRed()
        {
            Assert.Equal(DockTheme.RowEnabledTint, DockTheme.RowTint(enabled: true));
            Assert.Equal(DockTheme.RowDisabledTint, DockTheme.RowTint(enabled: false));
        }

        [Fact]
        public void RowTints_MatchBriefRgbaSourceValues()
        {
            // enabled soft green Color(80/255,160/255,80/255, 0.18)
            AssertRgba8(DockTheme.RowEnabledTint, 80, 160, 80, 0.18f);
            // disabled soft red Color(160/255,80/255,80/255, 0.18)
            AssertRgba8(DockTheme.RowDisabledTint, 160, 80, 80, 0.18f);
        }

        [Fact]
        public void RowSizing_MatchesBrief()
        {
            Assert.Equal(8, DockTheme.RowCornerRadius);
            Assert.Equal(8, DockTheme.RowContentPadding);
        }

        [Fact]
        public void MetadataColors_MatchBrief8BitSourceValues()
        {
            // prompt Role: Color8(143,170,220)
            AssertRgb8(DockTheme.RoleLabel, 143, 170, 220);
            // resource URI: Color8(154,205,50)
            AssertRgb8(DockTheme.ResourceUri, 154, 205, 50);
            // resource MimeType: Color8(221,160,221)
            AssertRgb8(DockTheme.ResourceMimeType, 221, 160, 221);
        }

        [Fact]
        public void RowIdMuted_ReusesDescriptionMutedGray()
        {
            Assert.Equal(DockTheme.ColorDescriptionMuted, DockTheme.RowIdMuted);
        }

        static void AssertRgb8((float R, float G, float B) c, int r8, int g8, int b8)
        {
            Assert.Equal(r8 / 255f, c.R, 5);
            Assert.Equal(g8 / 255f, c.G, 5);
            Assert.Equal(b8 / 255f, c.B, 5);
        }

        static void AssertRgba8((float R, float G, float B, float A) c, int r8, int g8, int b8, float a)
        {
            Assert.Equal(r8 / 255f, c.R, 5);
            Assert.Equal(g8 / 255f, c.G, 5);
            Assert.Equal(b8 / 255f, c.B, 5);
            Assert.Equal(a, c.A, 5);
        }
    }
}
