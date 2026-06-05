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
using System.Reflection;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using Godot;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Owns the lifecycle of the reused <c>com.IvanMurzak.McpPlugin</c> SignalR client for the
    /// Godot editor plugin. The Godot analog of Unity-MCP's <c>UnityMcpPlugin.BuildMcpPlugin</c> +
    /// <c>Connect</c> path, condensed to the single-instance editor case.
    ///
    /// <para>
    /// Responsibilities:
    /// <list type="bullet">
    ///   <item>Build the <see cref="Reflector"/> with the Godot type converters
    ///   (<see cref="GodotReflectorFactory"/>).</item>
    ///   <item>Build an <see cref="IMcpPlugin"/> via <see cref="McpPluginBuilder"/>, scanning the
    ///   addon assembly for <c>[AiToolType]</c>/<c>[AiTool]</c> methods (the <c>ping</c> tool today).</item>
    ///   <item>Apply the resolved <see cref="GodotMcpConfig"/> (Cloud/Custom host + bearer token).</item>
    ///   <item>Connect. Auto-reconnect/backoff is handled inside the McpPlugin client when
    ///   <see cref="ConnectionConfig.KeepConnected"/> is true — NOT reimplemented here.</item>
    /// </list>
    /// </para>
    ///
    /// This type is editor-only (<c>#if TOOLS</c>): it instantiates the SignalR client and is driven
    /// by <see cref="GodotMcpPlugin"/>'s tree lifecycle.
    /// </summary>
    public sealed class GodotMcpConnection : IDisposable
    {
        /// <summary>Plugin version reported to the server in the MCP handshake.</summary>
        public const string PluginVersion = "0.1.0";

        readonly GodotMcpConfig _config;
        IMcpPlugin? _plugin;
        Reflector? _publishedReflector;

        /// <summary>The active config (resolved Host/Token/mode are read live off this).</summary>
        public GodotMcpConfig Config => _config;

        /// <summary>The built plugin instance, or null before <see cref="Start"/> / after <see cref="Dispose"/>.</summary>
        public IMcpPlugin? Plugin => _plugin;

        public GodotMcpConnection(GodotMcpConfig? config = null)
        {
            _config = config ?? new GodotMcpConfig();
        }

        /// <summary>
        /// Build the plugin (reflector + tool scan + config) and initiate the connection. Idempotent:
        /// a second call while a plugin already exists is a no-op. The connect itself is fire-and-forget
        /// from the editor's perspective — the McpPlugin client manages (re)connection in the background.
        /// </summary>
        public void Start()
        {
            if (_plugin != null)
            {
                GD.Print("[Godot-MCP] connection already started; ignoring duplicate Start().");
                return;
            }

            // Layer the project-root `.env` BENEATH the live process-env overrides the config already
            // applies in its getters. Godot is launched from the GUI (no inherited shell exports), so a
            // committed `res://.env` is how a project self-configures its MCP host/token. Process env
            // still wins because GodotMcpConfig reads it live on every Host/Token/ActiveMode access —
            // see GodotMcpEnvFile's precedence note.
            ApplyProjectEnvFile();

            Reflector reflector = GodotReflectorFactory.CreateDefaultReflector();

            // Publish the connection's reflector as the ambient one so tool handlers (e.g. node-modify)
            // share the exact converter set registered here instead of building their own.
            GodotMcpReflector.Current = reflector;
            _publishedReflector = reflector;

            var version = new McpVersion
            {
                Api = com.IvanMurzak.McpPlugin.Common.Consts.ApiVersion,
                Plugin = PluginVersion,
                Environment = $"Godot {Engine.GetVersionInfo()["string"]}"
            };

            // Scan THIS addon assembly for [AiToolType]/[AiTool] (the ping tool, and future families).
            Assembly addonAssembly = typeof(GodotMcpConnection).Assembly;

            var builder = new McpPluginBuilder(version)
                .SetConfig(_config)
                .WithToolsFromAssembly(addonAssembly)
                .WithPromptsFromAssembly(addonAssembly)
                .WithResourcesFromAssembly(addonAssembly);

            _plugin = builder.Build(reflector);

            var mode = _config.ActiveMode;
            var host = _config.Host;
            GD.Print($"[Godot-MCP] connecting (mode={mode}, host={host}) ...");

            // Fire-and-forget connect; KeepConnected drives reconnection in the client.
            _ = ConnectAsync();
        }

        /// <summary>
        /// Resolve the project-root <c>.env</c> (<c>res://.env</c> → absolute via
        /// <see cref="ProjectSettings.GlobalizePath(string)"/>) and apply its recognized
        /// <c>GODOT_MCP_*</c> values to <see cref="_config"/> beneath the process-env layer. The native
        /// <c>ProjectSettings</c> call is the only Godot dependency; the parse/apply core is pure-managed
        /// (<see cref="GodotMcpEnvFile"/>). A missing file is a silent no-op.
        /// </summary>
        void ApplyProjectEnvFile()
        {
            string envPath;
            try
            {
                envPath = ProjectSettings.GlobalizePath("res://.env");
            }
            catch (Exception ex)
            {
                GD.PushWarning($"[Godot-MCP] could not resolve res://.env path: {ex.Message}");
                return;
            }

            var values = GodotMcpEnvFile.LoadFile(envPath);
            if (values.Count == 0)
                return;

            GodotMcpEnvFile.Apply(_config, values);
            GD.Print($"[Godot-MCP] applied {values.Count} setting(s) from project .env ({envPath}).");
        }

        async Task ConnectAsync()
        {
            var plugin = _plugin;
            if (plugin == null)
                return;

            try
            {
                var ok = await plugin.Connect();
                if (ok)
                    GD.Print("[Godot-MCP] connected.");
                else
                    GD.PushWarning("[Godot-MCP] initial connect returned false; client will keep retrying if KeepConnected.");
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] connect failed: {ex.Message}");
            }
        }

        public void Dispose()
        {
            // Clear the ambient reflector if it is the one we published, so a stale instance does not
            // outlive the connection that owned it.
            if (ReferenceEquals(GodotMcpReflector.Current, _publishedReflector))
                GodotMcpReflector.Current = null;
            _publishedReflector = null;

            if (_plugin != null)
            {
                try
                {
                    _plugin.Dispose();
                }
                catch (Exception ex)
                {
                    GD.PushError($"[Godot-MCP] error disposing connection: {ex.Message}");
                }
                _plugin = null;
            }
        }
    }
}
#endif
