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
}
#endif
