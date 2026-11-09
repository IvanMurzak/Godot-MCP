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
