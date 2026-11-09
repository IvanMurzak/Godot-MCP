/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Godot;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// One segment of a <see cref="DockStyle.SegmentedControl"/>. Carries its own option index, the track
    /// <see cref="HBoxContainer"/> it reports its selection into (the authoritative selection meta lives there),
    /// and the caller's <c>onSelected</c> callback. The click handler (<see cref="OnPressed"/>) is an instance
    /// method connected via an object+method <see cref="Callable"/>, replicating the former capturing-lambda body
    /// exactly — so the segment's selection logic no longer rides a delegate Callable.
    /// </summary>
    [Tool]
    public partial class DockSegmentButton : Button
    {
        int _index;
        HBoxContainer _track = null!;
        System.Action<int>? _onSelected;

        /// <summary>Bind this segment's option index, the track it reports into, and the selection callback.</summary>
        public void Bind(int index, HBoxContainer track, System.Action<int> onSelected)
        {
            _index = index;
            _track = track;
            _onSelected = onSelected;
        }

        /// <summary>
        /// Instance handler connected to <c>pressed</c> via an object+method <see cref="Callable"/>. Identical
        /// behaviour to the previous capturing lambda: compare against the authoritative track index, no-op on a
        /// re-click of the active segment (re-assert visuals), otherwise advance the selection optimistically and
        /// notify the caller.
        /// </summary>
        public void OnPressed()
        {
            // Compare against the AUTHORITATIVE index stashed on the track, not against any probe of native
            // ButtonPressed flags (which can report two-pressed/zero-pressed on mono/Linux).
            var current = DockStyle.GetSegmentedSelection(_track);
            if (SegmentedControlModel.IsSelected(_index, current))
            {
                // Re-clicking the active segment is a true no-op: re-assert visuals (Godot just toggled this
                // button OFF on the second click) and leave the authoritative index alone.
                DockStyle.SetSegmentedSelection(_track, current);
                return;
            }
            // Advance the authoritative selection OPTIMISTICALLY before notifying the caller, so the track meta and
            // the visual pressed-state cannot diverge if a consumer of onSelected persists the new mode but does
            // NOT round-trip through SetSegmentedSelection (e.g. an early-return on a no-op/persist-failure path).
            DockStyle.SetSegmentedSelection(_track, _index);
            _onSelected?.Invoke(_index);
        }
    }
}
#endif
