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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.Godot.MCP.UI;

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
        GodotMcpDock? _dock;

        public override void _EnterTree()
        {
            // Install the process-wide log collector first so every lifecycle line below (resolver
            // probes, plugin-loaded, connection state) is captured for the 'console-get-logs' tool.
            // Godot's C# API exposes no global managed log hook, so the collector is fed explicitly by
            // the plugin's own logging path (Log/LogWarning/LogError helpers) — see GodotLogCollector.
            GodotLogCollector.Current = new GodotLogCollector();

            // FIRST: teach the editor's default AssemblyLoadContext how to find the addon's transitive
            // NuGet dependency assemblies (ReflectorNet / McpPlugin / ...). Godot does not probe the
            // project's *.deps.json, so without this hook the first touch of a NuGet-dependency type
            // throws FileNotFoundException at runtime. The resolver references only BCL types, so
            // installing it here does NOT prematurely load the very assemblies it resolves — it must
            // run before any code path (BootMcp below) reaches a NuGet-dependency type. See
            // GodotMcpAssemblyResolver for the full rationale.
            GodotMcpAssemblyResolver.Log = msg => Log(msg);
            GodotMcpAssemblyResolver.Install();

            // Pump for off-thread → main-thread work. Added as a child of this EditorPlugin Node so it
            // lives in the editor SceneTree and gets _Process ticks for the lifetime of the plugin.
            _dispatcher = new MainThreadDispatcher { Name = DispatcherNodeName };
            AddChild(_dispatcher);

            // Route ReflectorNet's MainThread.Instance through the dispatcher.
            GodotMainThread.Install();

            Log("[Godot-MCP] plugin loaded");

            // Build the connection, register the dock (wired to it), and boot the MCP connection. All three
            // touch NuGet-dependency types (McpPlugin / ReflectorNet), so they live in a single non-inlined
            // method invoked AFTER the assembly resolver is installed above — see BootMcp's remarks.
            BootMcp();
        }

        /// <summary>
        /// Instantiate the <see cref="GodotMcpDock"/> (wired to <paramref name="connection"/>) and add it to
        /// an editor dock slot. Isolated and defensively wrapped so a UI failure cannot take down plugin
        /// load or the connection boot — the dock is additive scaffolding, not load-bearing for the MCP path.
        /// </summary>
        void RegisterDock(GodotMcpConnection? connection)
        {
            try
            {
                _dock = new GodotMcpDock(connection);
                AddControlToDock(DockSlot.RightUl, _dock);
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to register editor dock: {ex.Message}");
                _dock = null;
            }
        }

        /// <summary>
        /// Print an informational lifecycle line to the Godot output AND capture it into the
        /// <see cref="GodotLogCollector"/> so it is retrievable via the <c>console-get-logs</c> tool.
        /// </summary>
        static void Log(string message)
        {
            GD.Print(message);
            GodotLogCollector.Current?.Append(GodotLogType.Log, message);
        }

        /// <summary>Warning-level analog of <see cref="Log"/>.</summary>
        static void LogWarning(string message)
        {
            GD.PushWarning(message);
            GodotLogCollector.Current?.Append(GodotLogType.Warning, message);
        }

        /// <summary>Error-level analog of <see cref="Log"/>.</summary>
        static void LogError(string message)
        {
            GD.PushError(message);
            GodotLogCollector.Current?.Append(GodotLogType.Error, message);
        }

        /// <summary>
        /// Build the connection, register the dock wired to it, and boot the MCP connection. Isolated in
        /// its own non-inlined method so the JIT does not resolve the NuGet-dependency types it (and the
        /// dock's <c>ConnectionPanel</c>) reference until AFTER <see cref="GodotMcpAssemblyResolver"/> has
        /// been installed by <see cref="_EnterTree"/>. (Type references are resolved when a method is
        /// JIT-compiled; keeping this out of <c>_EnterTree</c> guarantees the resolver wins the race.)
        /// Dock registration runs even if the connection construction throws, so a UI failure and a
        /// connection failure are independent — each is caught and logged, keeping the editor usable.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        void BootMcp()
        {
            try
            {
                _connection = new GodotMcpConnection();
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to create MCP connection: {ex.Message}");
                _connection = null;
            }

            // Register the dock (wired to the connection if one was built; header-only otherwise).
            RegisterDock(_connection);

            if (_connection == null)
                return;

            try
            {
                _connection.Start();
            }
            catch (System.Exception ex)
            {
                LogError($"[Godot-MCP] failed to start MCP connection: {ex.Message}");
            }
        }

        public override void _ExitTree()
        {
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }

            if (_dock != null)
            {
                // Remove from the dock slot before freeing so Godot does not hold a dangling control ref.
                RemoveControlFromDocks(_dock);
                _dock.QueueFree();
                _dock = null;
            }

            if (_dispatcher != null)
            {
                _dispatcher.QueueFree();
                _dispatcher = null;
            }

            Log("[Godot-MCP] plugin unloaded");

            // Release the collector so a stale buffer does not outlive this plugin instance (a fresh one
            // is installed on the next _EnterTree).
            GodotLogCollector.Current = null;
        }
    }
}
#endif
