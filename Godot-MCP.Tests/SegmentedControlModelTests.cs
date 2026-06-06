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
    /// Pins the pure-managed state logic of the dock's reusable segmented control
    /// (<see cref="SegmentedControlModel"/>): value→index resolution, the selected predicate, and the
    /// out-of-range clamp. The editor builder (<c>DockStyle.SegmentedControl</c>, <c>#if TOOLS</c>) consumes
    /// these and is verified via the headless Godot smoke (test.md Suite 3) — NOT here.
    /// </summary>
    public class SegmentedControlModelTests
    {
        static readonly string[] Modes = { "Custom", "Cloud" };

        [Theory]
        [InlineData("Custom", 0)]
        [InlineData("Cloud", 1)]
        public void IndexOf_ReturnsMatchingIndex(string value, int expected)
        {
            Assert.Equal(expected, SegmentedControlModel.IndexOf(Modes, value));
        }

        [Fact]
        public void IndexOf_UnknownValue_FallsBackToFirstSegment()
        {
            // Unknown value must still yield a stable in-range selection so the control never renders empty.
            Assert.Equal(0, SegmentedControlModel.IndexOf(Modes, "Nope"));
        }

        [Fact]
        public void IndexOf_EmptyOptions_ReturnsMinusOne()
        {
            Assert.Equal(-1, SegmentedControlModel.IndexOf(System.Array.Empty<string>(), "x"));
        }

        [Theory]
        [InlineData(0, 0, true)]
        [InlineData(1, 0, false)]
        [InlineData(1, 1, true)]
        public void IsSelected_MatchesIndex(int index, int selectedIndex, bool expected)
        {
            Assert.Equal(expected, SegmentedControlModel.IsSelected(index, selectedIndex));
        }

        [Theory]
        [InlineData(0, 2, 0)]
        [InlineData(1, 2, 1)]
        [InlineData(5, 2, 0)]    // out of range -> first
        [InlineData(-1, 2, 0)]   // "no value" -> first
        public void ClampSelected_KeepsSelectionInRange(int selected, int count, int expected)
        {
            Assert.Equal(expected, SegmentedControlModel.ClampSelected(selected, count));
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(3, 0)]
        public void ClampSelected_EmptySet_ReturnsMinusOne(int selected, int _)
        {
            Assert.Equal(-1, SegmentedControlModel.ClampSelected(selected, 0));
        }
    }
}
