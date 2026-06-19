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
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>) state logic for the dock's reusable
    /// horizontal SEGMENTED CONTROL — the Godot analog of Unity-MCP's segmented mode/transport/auth
    /// toggle. A segmented control is a small set of mutually-exclusive options where exactly one is
    /// selected; the selected segment is painted with the highlight + cyan text, the rest muted.
    ///
    /// <para>
    /// Keeping the index/selection rules here (rather than inline in the <c>#if TOOLS</c>
    /// <see cref="DockStyle"/> builder) makes them unit-testable in the plain-xUnit
    /// <c>Godot-MCP.Tests</c> host without constructing a single Godot <c>Control</c>: which segment is
    /// selected for a given value, what label each segment shows, and the
    /// <see cref="IsSelected"/> styling predicate the editor uses to decide selected-vs-muted paint.
    /// </para>
    /// </summary>
    public static class SegmentedControlModel
    {
        /// <summary>
        /// Resolve the selected segment index for a <paramref name="value"/> within
        /// <paramref name="options"/>. Returns the first matching index, or <c>0</c> when the value is not
        /// present (a stable, in-range fallback so the control never renders with no selection). An empty
        /// option set yields <c>-1</c> (nothing to select).
        /// </summary>
        public static int IndexOf(IReadOnlyList<string> options, string value)
        {
            if (options.Count == 0)
                return -1;

            for (int i = 0; i < options.Count; i++)
            {
                if (options[i] == value)
                    return i;
            }

            // Unknown value: fall back to the first segment so the control always has exactly one selection.
            return 0;
        }

        /// <summary>
        /// The styling predicate the editor builder calls per segment: <c>true</c> when segment
        /// <paramref name="index"/> is the <paramref name="selectedIndex"/> (paint it with the dark
        /// highlight + cyan text), <c>false</c> for the muted, unselected look.
        /// </summary>
        public static bool IsSelected(int index, int selectedIndex) => index == selectedIndex;

        /// <summary>
        /// Clamp an arbitrary <paramref name="selectedIndex"/> into the valid range for
        /// <paramref name="optionCount"/> segments. Out-of-range (including a <c>-1</c> "no value") collapses
        /// to <c>0</c> when there is at least one segment, or stays <c>-1</c> for an empty set. Used to keep a
        /// freshly-(re)built control from rendering a selection past its segment count.
        /// </summary>
        public static int ClampSelected(int selectedIndex, int optionCount)
        {
            if (optionCount <= 0)
                return -1;
            if (selectedIndex < 0 || selectedIndex >= optionCount)
                return 0;
            return selectedIndex;
        }
    }
}
