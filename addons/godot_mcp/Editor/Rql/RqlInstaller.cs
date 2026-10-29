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
using System;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Wires RQL into the editor connection (the Godot counterpart of Unity-MCP's <c>ApplyToolFormat</c>). Installed by
    /// <c>GodotMcpPlugin</c> at boot, which hands <see cref="Configure"/> to the <see cref="GodotMcpConnection"/> it
    /// creates (its <c>configurePlugin</c> parameter), so the runtime-agnostic connection
    /// never references editor code. For every plugin the connection builds it:
    /// <list type="bullet">
    /// <item>gives the RQL <c>tools</c> source the tool manager. The game-only tool families RQL replaces in the editor
    /// (console-* → the <c>console</c> source, reflection-method-* → <c>call</c> / <c>CallMethod</c> / rql-schema) and
    /// runtime-errors-* are <c>ToolTarget.Runtime</c>: the editor's plugin (role Editor) never registers them, the in-game runtime
    /// (<c>GodotMcpRuntime</c>, role Runtime) does, and the server sends their calls to the game while the editor is connected;</item>
    /// <item>installs the tool data layer the config asks for (<see cref="GodotMcpConfig.ActiveToolFormat"/>): RQL
    /// (<see cref="RqlToolBinding"/>) or ReflectorNet JSON.</item>
    /// </list>
    /// <see cref="Uninstall"/> (plugin teardown) clears the hook and drops the engine and RQL's caches.
    /// </summary>
    internal static class RqlInstaller
    {
        /// <summary>Wire the engine and the binding to the editor; returns the plugin configuration for the connection.</summary>
        public static Action<IMcpPlugin, GodotMcpConfig> Install(Func<global::Godot.EditorUndoRedoManager?> undoRedo)
        {
            RqlGodot.UndoRedo = undoRedo;
            RqlToolBinding.EngineProvider = () => RqlGodot.Engine;
            RqlToolBinding.MainThreadRunner = f => MainThread.Instance.Run(f);
            return Configure;
        }

        public static void Uninstall()
        {
            RqlToolBinding.EngineProvider = null;
            RqlToolBinding.MainThreadRunner = null;
            RqlGodot.UndoRedo = null;
            RqlTools.Manager = null;
            var used = RqlGodot.Reset();
            GodotTypes.Reset();
            if (!used) return; // no engine was ever built: RQL's process-wide caches hold nothing of this assembly
            var cleared = RqlStaticCaches.Clear();
            GodotMcpLog.Info($"[Godot-MCP] RQL reset ({cleared} RQL caches cleared).");
        }

        /// <summary>The plugin configuration: editor tool surface, then the tool data layer.</summary>
        private static void Configure(IMcpPlugin plugin, GodotMcpConfig config)
        {
            var tools = plugin.McpManager?.ToolManager;
            if (tools == null) return;
            RqlTools.Manager = tools; // the tools source lists every tool, hidden ones included
            try
            {
                RqlToolBinding.Apply(tools, config.ActiveToolFormat, config.ToolTextEscaping);
            }
            catch (Exception ex)
            {
                // Without the binding every tool keeps ReflectorNet's JSON surface; the RQL tools still work.
                GodotMcpLog.Error($"[Godot-MCP] installing the RQL tool data layer failed ({ex.GetType().Name}: {ex.Message}); tools keep the JSON format.");
                tools.ToolBinding = null;
            }
        }
    }
}
#endif
