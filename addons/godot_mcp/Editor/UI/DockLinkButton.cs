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
}
#endif
