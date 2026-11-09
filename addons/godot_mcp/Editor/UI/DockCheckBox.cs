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
}
#endif
