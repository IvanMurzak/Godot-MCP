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
            // Bumped above Unity's literal px values: Godot's editor renders a smaller glyph at the same px, so
            // the headers/section-titles looked undersized in the dock. See the DockTheme docs for each.
            Assert.Equal(24, DockTheme.FontSizeHeader);
            Assert.Equal(20, DockTheme.FontSizeSectionTitle);
            Assert.Equal(13, DockTheme.FontSizeSubLabel);
        }

        [Fact]
        public void LinkColor_Is4FC3F7()
        {
            AssertRgb8(DockTheme.Link, 79, 195, 247); // #4FC3F7
        }

        [Fact]
        public void OpenButton_Palette_MatchesBriefReferenceValues()
        {
            // btn-secondary: gray fill rgb(70,70,70), border rgb(100,100,100), 30px tall, 6px radius.
            AssertRgb8(DockTheme.ButtonSecondary, 70, 70, 70);
            AssertRgb8(DockTheme.ButtonOpenBorder, 100, 100, 100);
            Assert.Equal(30, DockTheme.ButtonOpenHeight);
            Assert.Equal(6, DockTheme.ButtonOpenCornerRadius);
        }

        [Fact]
        public void TokenSubLabel_Palette_MatchesBriefReferenceValues()
        {
            // "~N tokens total" sub-label: gray rgb(150,150,150). Font bumped 11→15 (Godot renders smaller; operator polish).
            AssertRgb8(DockTheme.ColorTokenSubLabel, 150, 150, 150);
            Assert.Equal(15, DockTheme.FontSizeTokenSubLabel);
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

        // --- Segmented control palette (Unity-MCP's segmented toggle) ---

        [Fact]
        public void SegmentTrack_MatchesBriefRgbaAndSizing()
        {
            // track bg rgba(255,255,255, 0.05)
            AssertRgba8(DockTheme.SegmentTrackBackground, 255, 255, 255, 0.05f);
            Assert.Equal(6, DockTheme.SegmentTrackCornerRadius);
            Assert.Equal(1, DockTheme.SegmentTrackPadding);
        }

        [Fact]
        public void SegmentSelected_MatchesBriefRgbaAndSizing()
        {
            // selected highlight: solid lighter-gray raised pill Color8(100,100,100) so the active segment is
            // clearly distinct from the muted track (was a barely-visible translucent darken).
            AssertRgba8(DockTheme.SegmentSelectedBackground, 100, 100, 100, 1.0f);
            Assert.Equal(4, DockTheme.SegmentSelectedCornerRadius);
        }

        [Fact]
        public void SegmentText_SelectedIsCyanPrimary_UnselectedIsMutedGray()
        {
            // selected text = primary cyan Color8(175,232,230)
            Assert.Equal(DockTheme.ButtonPrimary, DockTheme.SegmentSelectedText);
            AssertRgb8(DockTheme.SegmentSelectedText, 175, 232, 230);
            // unselected text = muted gray (reuses the description muted gray)
            Assert.Equal(DockTheme.ColorDescriptionMuted, DockTheme.SegmentUnselectedText);
        }

        [Fact]
        public void SegmentSizing_MatchesBrief()
        {
            Assert.Equal(40, DockTheme.SegmentMinWidth);
            Assert.Equal(12, DockTheme.SegmentFontSize);
        }

        // --- Vertical timeline ---

        [Fact]
        public void TimelineLine_MatchesBrief8BitAndWidth()
        {
            // connecting line rgb(80,80,80), 2px wide.
            AssertRgb8(DockTheme.TimelineLine, 80, 80, 80);
            Assert.Equal(2, DockTheme.TimelineLineWidth);
        }

        [Fact]
        public void TimelineIndicatorAndRing_MatchBrief()
        {
            Assert.Equal(20, DockTheme.TimelineIndicatorWidth);
            Assert.Equal(2, DockTheme.TimelineRingBorderWidth);
        }

        [Fact]
        public void WarningFrame_MatchesBriefRgbaAndColors()
        {
            // amber warning frame: bg rgba(180,120,40, 0.12), border rgba(220,160,60, 0.45),
            // title Color8(255,200,100), message Color8(210,180,130).
            AssertRgba8(DockTheme.WarningBackground, 180, 120, 40, 0.12f);
            AssertRgba8(DockTheme.WarningBorder, 220, 160, 60, 0.45f);
            AssertRgb8(DockTheme.WarningTitle, 255, 200, 100);
            AssertRgb8(DockTheme.WarningMessage, 210, 180, 130);
            Assert.Equal(10, DockTheme.WarningCornerRadius);
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
