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
    /// <c>GodotMcpPlugin</c> at boot through <see cref="GodotMcpConnection.PluginBuilt"/>, so the runtime-agnostic connection
    /// never references editor code. For every plugin the connection builds it:
    /// <list type="bullet">
    /// <item>shapes the EDITOR tool surface: removes the runtime-safe data tools RQL replaces in the editor
    /// (<see cref="EditorReplacedTools"/>: console-* → the <c>console</c> source, reflection-method-* → <c>call</c> /
    /// <c>CallMethod</c> / rql-schema). The in-game runtime (<c>GodotMcpRuntime</c>) builds its own plugin and keeps them;</item>
    /// <item>installs the tool data layer the config asks for (<see cref="GodotMcpConfig.ActiveToolFormat"/>): RQL
    /// (<see cref="RqlToolBinding"/>) or ReflectorNet JSON.</item>
    /// </list>
    /// <see cref="Uninstall"/> (plugin teardown) clears the hook and drops the engine and RQL's caches.
    /// </summary>
    internal static class RqlInstaller
    {
        /// <summary>Runtime tools that RQL replaces in the editor (they stay registered in a game build's runtime).</summary>
        internal static readonly string[] EditorReplacedTools =
        {
            Tool_Console.ConsoleGetLogsToolId,
            Tool_Console.ConsoleClearLogsToolId,
            Tool_Reflection.ReflectionMethodFindToolId,
            Tool_Reflection.ReflectionMethodCallToolId,
        };

        public static void Install(Func<global::Godot.EditorUndoRedoManager?> undoRedo)
        {
            RqlGodot.UndoRedo = undoRedo;
            RqlToolBinding.EngineProvider = () => RqlGodot.Engine;
            RqlToolBinding.MainThreadRunner = f => MainThread.Instance.Run(f);
            GodotMcpConnection.PluginBuilt = Configure;
        }

        public static void Uninstall()
        {
            GodotMcpConnection.PluginBuilt = null;
            RqlToolBinding.EngineProvider = null;
            RqlToolBinding.MainThreadRunner = null;
            RqlGodot.UndoRedo = null;
            RqlGodot.Reset();
            GodotTypes.Reset();
            var cleared = RqlStaticCaches.Clear();
            GodotMcpLog.Info($"[Godot-MCP] RQL reset ({cleared} RQL caches cleared).");
        }

        private static void Configure(IMcpPlugin plugin, GodotMcpConfig config)
        {
            var tools = plugin.McpManager?.ToolManager;
            if (tools == null) return;
            foreach (var id in EditorReplacedTools)
                if (tools.HasTool(id)) tools.RemoveTool(id);
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
