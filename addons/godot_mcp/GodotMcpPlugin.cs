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
using System.Runtime.CompilerServices;
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
            // FIRST: teach the editor's default AssemblyLoadContext how to find the addon's transitive
            // NuGet dependency assemblies (ReflectorNet / McpPlugin / ...). Godot does not probe the
            // project's *.deps.json, so without this hook the first touch of a NuGet-dependency type
            // throws FileNotFoundException at runtime. The resolver references only BCL types, so
            // installing it here does NOT prematurely load the very assemblies it resolves — it must
            // run before any code path (BootMcp below) reaches a NuGet-dependency type. See
            // GodotMcpAssemblyResolver for the full rationale.
            GodotMcpAssemblyResolver.Log = msg => GD.Print(msg);
            GodotMcpAssemblyResolver.Install();

            // Pump for off-thread → main-thread work. Added as a child of this EditorPlugin Node so it
            // lives in the editor SceneTree and gets _Process ticks for the lifetime of the plugin.
            _dispatcher = new MainThreadDispatcher { Name = DispatcherNodeName };
            AddChild(_dispatcher);

            // Route ReflectorNet's MainThread.Instance through the dispatcher.
            GodotMainThread.Install();

            GD.Print("[Godot-MCP] plugin loaded");

            BootMcp();
        }

        /// <summary>
        /// Boot the MCP connection. Isolated in its own non-inlined method so the JIT does not resolve
        /// the NuGet-dependency types it references until AFTER <see cref="GodotMcpAssemblyResolver"/>
        /// has been installed by <see cref="_EnterTree"/>. (Type references are resolved when a method
        /// is JIT-compiled; keeping this out of <c>_EnterTree</c> guarantees the resolver wins the race.)
        /// Failures here must not break plugin load — the catch keeps the editor usable.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        void BootMcp()
        {
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
