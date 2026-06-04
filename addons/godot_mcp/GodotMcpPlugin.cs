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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Editor entry point for the Godot-MCP addon. Referenced by
    /// <c>addons/godot_mcp/plugin.cfg</c> (the <c>script</c> field) and loaded by the
    /// Godot Editor when the plugin is enabled.
    ///
    /// On load it installs the editor main-thread dispatcher (the Godot analog of Unity's
    /// <c>MainThread.Instance.Run</c>) so downstream tool handlers can marshal Godot API calls
    /// onto the main thread, then boots the MCP connection (SignalR client via
    /// com.IvanMurzak.McpPlugin) to the configured server (cloud ai-game.dev by default, or a
    /// custom override) and registers the addon's MCP tools.
    /// </summary>
    [Tool]
    public partial class GodotMcpPlugin : EditorPlugin
    {
        const string DispatcherNodeName = "GodotMcpMainThreadDispatcher";

        MainThreadDispatcher? _dispatcher;
        GodotMcpConnection? _connection;

        public override void _EnterTree()
        {
            // Pump for off-thread → main-thread work. Added as a child of this EditorPlugin Node so it
            // lives in the editor SceneTree and gets _Process ticks for the lifetime of the plugin.
            _dispatcher = new MainThreadDispatcher { Name = DispatcherNodeName };
            AddChild(_dispatcher);

            // Route ReflectorNet's MainThread.Instance through the dispatcher.
            GodotMainThread.Install();

            GD.Print("[Godot-MCP] plugin loaded");

            // Boot the MCP connection (after the dispatcher is installed so tool handlers can marshal
            // onto the main thread). Reuses the McpPlugin SignalR client + bearer auth; mode/URL/token
            // come from GodotMcpConfig (env-overridable). Failures here must not break plugin load.
            try
            {
                _connection = new GodotMcpConnection();
                _connection.Start();
            }
            catch (System.Exception ex)
            {
                GD.PushError($"[Godot-MCP] failed to start MCP connection: {ex.Message}");
            }
        }

        public override void _ExitTree()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            if (_dispatcher != null)
            {
                _dispatcher.QueueFree();
                _dispatcher = null;
            }

            GD.Print("[Godot-MCP] plugin unloaded");
        }
    }
}
#endif
