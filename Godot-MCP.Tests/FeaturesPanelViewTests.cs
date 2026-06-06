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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed features-panel label formatting (<see cref="FeaturesPanelView"/>) — the
    /// "&lt;Title&gt;: X / Y" count row, the "~N tokens" sub-label, and the "—" placeholders. The editor Control
    /// wiring (FeaturesPanel.cs / FeatureRow.cs / FeatureListWindow.cs, all <c>#if TOOLS</c>) instantiates live
    /// Godot nodes and is verified via the headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class FeaturesPanelViewTests
    {
        [Theory]
        [InlineData(GodotMcpFeatureKind.Tools, "Tools")]
        [InlineData(GodotMcpFeatureKind.Prompts, "Prompts")]
        [InlineData(GodotMcpFeatureKind.Resources, "Resources")]
        public void Title_maps_each_kind(GodotMcpFeatureKind kind, string expected)
        {
            Assert.Equal(expected, FeaturesPanelView.Title(kind));
        }

        [Fact]
        public void CountLabel_formats_enabled_over_total_with_title()
        {
            Assert.Equal("Tools: 3 / 7", FeaturesPanelView.CountLabel(GodotMcpFeatureKind.Tools, 3, 7));
            Assert.Equal("Prompts: 0 / 0", FeaturesPanelView.CountLabel(GodotMcpFeatureKind.Prompts, 0, 0));
            Assert.Equal("Resources: 5 / 5", FeaturesPanelView.CountLabel(GodotMcpFeatureKind.Resources, 5, 5));
        }

        [Fact]
        public void UnavailableLabel_uses_the_dash_placeholder_for_both_counts()
        {
            Assert.Equal("Tools: — / —", FeaturesPanelView.UnavailableLabel(GodotMcpFeatureKind.Tools));
            Assert.Equal("—", FeaturesPanelView.Unavailable);
        }

        [Fact]
        public void TokenLabel_formats_with_tilde_and_suffix()
        {
            Assert.Equal("~999 tokens", FeaturesPanelView.TokenLabel(999));
            Assert.Equal("~0 tokens", FeaturesPanelView.TokenLabel(0));
            // 1000+ is abbreviated with a k suffix.
            Assert.Equal("~1.2k tokens", FeaturesPanelView.TokenLabel(1234));
        }

        [Theory]
        [InlineData(0, "0")]
        [InlineData(1, "1")]
        [InlineData(999, "999")]            // just under the threshold — rendered as-is
        [InlineData(1000, "1k")]            // exact thousand — no decimal
        [InlineData(1500, "1.5k")]          // half-thousand — one decimal
        [InlineData(1234, "1.2k")]          // rounded to one decimal
        [InlineData(2000, "2k")]            // trailing .0 trimmed
        [InlineData(12345, "12.3k")]
        [InlineData(1999, "2k")]            // rounds up at one decimal
        public void FormatTokenCount_abbreviates_thousands_with_k(int count, string expected)
        {
            Assert.Equal(expected, FeaturesPanelView.FormatTokenCount(count));
        }

        [Fact]
        public void UnavailableTokenLabel_uses_the_dash_placeholder()
        {
            Assert.Equal("~— tokens", FeaturesPanelView.UnavailableTokenLabel());
        }

        [Fact]
        public void TokenTotalLabel_formats_with_tilde_total_suffix_and_k_abbrev()
        {
            Assert.Equal("~999 tokens total", FeaturesPanelView.TokenTotalLabel(999));
            Assert.Equal("~0 tokens total", FeaturesPanelView.TokenTotalLabel(0));
            // Shares FormatTokenCount with TokenLabel, so the k-abbreviation is identical.
            Assert.Equal("~1.5k tokens total", FeaturesPanelView.TokenTotalLabel(1500));
            Assert.Equal("~12.3k tokens total", FeaturesPanelView.TokenTotalLabel(12345));
        }

        [Fact]
        public void UnavailableTokenTotalLabel_uses_the_dash_placeholder()
        {
            Assert.Equal("~— tokens total", FeaturesPanelView.UnavailableTokenTotalLabel());
        }
    }
}
