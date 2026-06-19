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
    /// A <see cref="Button"/> whose <c>pressed</c> signal is connected via an OBJECT+METHOD <see cref="Callable"/>
    /// (<c>new Callable(this, MethodName.OnPressed)</c>) to its own instance method, which invokes a stored
    /// <see cref="System.Action"/>. The action is plain managed state on this GodotObject — it is NOT a signal
    /// connection, so (unlike a <c>button.Pressed += handler</c> delegate connection) it never enters the native
    /// <c>ManagedCallable::instances</c> registry that a Build-Project hot-reload iterates, and therefore cannot
    /// raise the <c>delegate_handle.value == nullptr</c> flood. Used by <see cref="DockStyle.IconButton"/> /
    /// <see cref="DockStyle.GoldenButton"/> / <see cref="DockStyle.AlertPanel"/> so the reusable factories keep
    /// their <c>System.Action</c> API while connecting object+method.
    /// </summary>
    [Tool]
    public partial class DockActionButton : Button
    {
        System.Action? _onPressed;

        /// <summary>Connect <c>pressed</c> object+method (NOT a delegate) once the button enters the tree.</summary>
        public override void _Ready()
            => Connect(BaseButton.SignalName.Pressed, new Callable(this, MethodName.OnPressed));

        /// <summary>Store the click action invoked by <see cref="OnPressed"/>; call once after construction.</summary>
        public void BindPressed(System.Action onPressed) => _onPressed = onPressed;

        /// <summary>Instance handler connected to <c>pressed</c> via an object+method <see cref="Callable"/>.</summary>
        public void OnPressed() => _onPressed?.Invoke();
    }

    /// <summary>
    /// A flat link-style <see cref="Button"/> that opens a stored URL via <see cref="OS.ShellOpen"/> from its own
    /// instance method, connected object+method (see <see cref="DockActionButton"/> for why this avoids the
    /// hot-reload <c>delegate_handle</c> flood). Backs <see cref="DockStyle.LinkButton"/>.
    /// </summary>
    [Tool]
    public partial class DockLinkButton : Button
    {
        string _url = string.Empty;

        /// <summary>Connect <c>pressed</c> object+method (NOT a delegate) once the button enters the tree.</summary>
        public override void _Ready()
            => Connect(BaseButton.SignalName.Pressed, new Callable(this, MethodName.OnPressed));

        /// <summary>Store the URL opened by <see cref="OnPressed"/>; call once after construction.</summary>
        public void BindUrl(string url) => _url = url ?? string.Empty;

        /// <summary>Instance handler connected to <c>pressed</c>: opens the stored URL in the OS browser.</summary>
        public void OnPressed()
        {
            if (!string.IsNullOrEmpty(_url))
                OS.ShellOpen(_url);
        }
    }

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

    /// <summary>
    /// A <see cref="CheckButton"/> whose <c>toggled</c> signal is connected via an object+method
    /// <see cref="Callable"/> to its own instance method, which invokes a stored <c>Action&lt;bool&gt;</c>. The
    /// action is plain managed state (not a signal connection), so it never enters the ManagedCallable hot-reload
    /// registry. Used for per-row enable/disable toggles whose handler closes over row-specific state (e.g. the
    /// feature item name) without a delegate Callable.
    /// </summary>
    [Tool]
    public partial class DockCheckToggle : CheckButton
    {
        System.Action<bool>? _onToggled;

        /// <summary>Store the toggle action invoked by <see cref="OnToggled"/>; call once after construction.</summary>
        public void BindToggled(System.Action<bool> onToggled) => _onToggled = onToggled;

        /// <summary>Instance handler connected to <c>toggled</c> via an object+method <see cref="Callable"/>.</summary>
        public void OnToggled(bool pressed) => _onToggled?.Invoke(pressed);
    }

    /// <summary>
    /// A <see cref="CheckBox"/> variant of <see cref="DockCheckToggle"/> (object+method <c>toggled</c> connection
    /// driving a stored <c>Action&lt;bool&gt;</c>). Used by the Skills auto-generate checkbox.
    /// </summary>
    [Tool]
    public partial class DockCheckBox : CheckBox
    {
        System.Action<bool>? _onToggled;

        /// <summary>Store the toggle action invoked by <see cref="OnToggled"/>; call once after construction.</summary>
        public void BindToggled(System.Action<bool> onToggled) => _onToggled = onToggled;

        /// <summary>Instance handler connected to <c>toggled</c> via an object+method <see cref="Callable"/>.</summary>
        public void OnToggled(bool pressed) => _onToggled?.Invoke(pressed);
    }

    /// <summary>
    /// The toggle button of a <see cref="DockStyle.Foldout"/>. Carries its title text + the content
    /// <see cref="VBoxContainer"/> it shows/hides, and flips them in its own <see cref="OnToggled"/> instance
    /// method (connected via an object+method <see cref="Callable"/> to the <c>toggled</c> signal) — replacing the
    /// former capturing-lambda delegate connection.
    /// </summary>
    [Tool]
    public partial class DockFoldoutToggle : Button
    {
        string _title = string.Empty;
        VBoxContainer _content = null!;

        /// <summary>Bind the foldout title + the content it reveals.</summary>
        public void Bind(string title, VBoxContainer content)
        {
            _title = title;
            _content = content;
        }

        /// <summary>Instance handler connected to <c>toggled</c>: show/hide the content and flip the arrow glyph.</summary>
        public void OnToggled(bool pressed)
        {
            if (_content != null)
                _content.Visible = pressed;
            Text = (pressed ? "▾ " : "▸ ") + _title;
        }
    }
}
#endif
