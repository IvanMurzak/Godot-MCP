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

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Editor entry point for the Godot-MCP addon. Referenced by
    /// <c>addons/godot_mcp/plugin.cfg</c> (the <c>script</c> field) and loaded by the
    /// Godot Editor when the plugin is enabled.
    ///
    /// This scaffold only boots and logs a startup line. The MCP connection to
    /// ai-game.dev (SignalR client via com.IvanMurzak.McpPlugin) is intentionally
    /// NOT wired up here — that is a separate downstream task.
    /// </summary>
    [Tool]
    public partial class GodotMcpPlugin : EditorPlugin
    {
        public override void _EnterTree()
        {
            GD.Print("[Godot-MCP] plugin loaded");
        }

        public override void _ExitTree()
        {
            GD.Print("[Godot-MCP] plugin unloaded");
        }
    }
}
#endif
