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
}
#endif
