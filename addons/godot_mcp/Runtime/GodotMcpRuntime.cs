/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.MainThreadDispatch;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using Godot;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;

namespace com.IvanMurzak.Godot.MCP.Runtime
{
    /// <summary>
    /// Opt-in entry point for using Godot-MCP <b>inside a running / exported game</b> (debug or release),
    /// outside the editor. The Godot analog of Unity-MCP's <c>UnityMcpPluginRuntime.Initialize()</c>.
    ///
    /// <para>
    /// This type lives OUTSIDE <c>#if TOOLS</c>, so it compiles into an exported game build (where
    /// <c>TOOLS</c> is undefined and the editor <c>EditorPlugin</c>/dock/editor tool families are dropped).
    /// It reuses the existing runtime-agnostic stack — <see cref="GodotMcpConfig"/>,
    /// <see cref="GodotReflectorFactory"/>, <see cref="McpPluginBuilder"/>, <see cref="GodotMcpReflector"/>,
    /// and the <see cref="MainThreadDispatcher"/>/<see cref="GodotMainThread"/> pair — rather than inventing
    /// a parallel transport.
    /// </para>
    ///
    /// <para>
    /// Usage (the game developer writes this, e.g. from an autoload <c>_Ready</c>):
    /// <code>
    /// var mcp = GodotMcpRuntime.Initialize(builder =>
    /// {
    ///     builder.WithConfig(c =>
    ///     {
    ///         c.ConnectionMode = GodotMcpConnectionMode.Custom;
    ///         c.Host = "http://localhost:8080";
    ///     });
    ///     builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly()); // opt in YOUR [AiToolType] tools
    /// }).Build();
    ///
    /// await mcp.Connect();   // explicit — nothing connects until you ask
    /// // ... later ...
    /// await mcp.Disconnect();
    /// </code>
    /// </para>
    ///
    /// <para>
    /// <b>Security:</b> default OFF (no auto-connect), no persisted-config auto-load in a build (the
    /// developer supplies host/token in code or via <c>GODOT_MCP_*</c> env / project <c>.env</c>), and
    /// <b>zero tools by default</b> — only what the developer opts in is registered. Prefer a loopback host
    /// and a required token when exposing tools.
    /// </para>
    /// </summary>
    public static class GodotMcpRuntime
    {
        const string DispatcherNodeName = "GodotMcpRuntimeMainThreadDispatcher";

        /// <summary>
        /// Begin configuring a runtime MCP connection. Invoke the <paramref name="configure"/> callback to
        /// register tools and set host/token, then call <see cref="GodotMcpRuntimeBuilder.Build"/> on the
        /// returned builder to finalize and obtain the (default-OFF) connection handle.
        /// </summary>
        /// <param name="configure">
        /// Configures the connection — <see cref="GodotMcpRuntimeBuilder.WithConfig"/> for host/token/mode,
        /// <see cref="GodotMcpRuntimeBuilder.WithToolsFromAssembly"/> / <see cref="GodotMcpRuntimeBuilder.WithTools"/>
        /// to opt tools in. May be <c>null</c> for the zero-tool, env/.env-configured default.
        /// </param>
        public static GodotMcpRuntimeBuilder Initialize(Action<GodotMcpRuntimeBuilder>? configure = null)
        {
            var builder = new GodotMcpRuntimeBuilder();
            configure?.Invoke(builder);
            return builder;
        }

        /// <summary>
        /// Finalize a <see cref="GodotMcpRuntimeBuilder"/>: build the reused MCP plugin (reflector +
        /// opted-in tools + resolved config), bootstrap the main-thread dispatcher, and return the handle.
        /// Called by <see cref="GodotMcpRuntimeBuilder.Build"/>; not invoked directly.
        /// </summary>
        internal static GodotMcpRuntimeHandle Build(GodotMcpRuntimeBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            // 1) Resolve config. Start from a fresh GodotMcpConfig (NO persisted user:// auto-load — that is
            //    an editor-only convenience; a shipped build must not silently read a saved config). The
            //    config still reads GODOT_MCP_* process env + project .env LIVE in its getters, so a game can
            //    be configured out-of-band without code. Then apply the developer's WithConfig mutations on
            //    top (highest-precedence in-code layer).
            var config = new GodotMcpConfig();
            builder.ApplyConfig(config);

            // 2) Guarantee a main-thread dispatcher in the running SceneTree (unless the developer owns one),
            //    and route ReflectorNet's MainThread.Instance through it, so tool handlers can marshal Godot
            //    API calls onto the main thread via MainThread.Instance.Run(...). MUST happen before any tool
            //    runs; doing it at build time keeps the contract simple.
            if (builder.InstallDispatcher)
                EnsureMainThreadDispatcher();

            // 3) Build the reflector with the Godot value-type / ref converters and publish it as the ambient
            //    one so tool handlers share the exact converter set (mirrors GodotMcpConnection.Start). The
            //    editor ResourceLoader resolver (Tool_Resource.InstallReflectionResolver) is intentionally
            //    NOT installed — it is #if TOOLS editor-only and absent from a game build.
            Reflector reflector = GodotReflectorFactory.CreateDefaultReflector();
            GodotMcpReflector.Current = reflector;

            var version = new McpVersion
            {
                Api = com.IvanMurzak.McpPlugin.Common.Consts.ApiVersion,
                Plugin = ResolvePluginVersion(),
                Environment = $"Godot {Engine.GetVersionInfo()["string"]} (runtime)"
            };

            var mcpBuilder = new McpPluginBuilder(version)
                .SetConfig(config);

            // 4) Register ONLY the developer's opted-in tools. ZERO tools by default: with nothing opted in,
            //    no WithTools* call is made and the plugin builds with an empty tool set (exactly Unity's
            //    model). Editor tool families are #if TOOLS and don't compile into a game build, so even a
            //    WithToolsFromAssembly over the addon assembly can't pull them in.
            if (builder.ToolAssemblies.Count > 0)
                mcpBuilder.WithToolsFromAssembly(builder.ToolAssemblies);

            if (builder.ToolTypes.Count > 0)
                mcpBuilder.WithTools(builder.ToolTypes.ToArray());

            var plugin = mcpBuilder.Build(reflector);

            return new GodotMcpRuntimeHandle(plugin, config);
        }

        /// <summary>
        /// Resolve the addon version reported in the MCP handshake from
        /// <c>res://addons/godot_mcp/plugin.cfg</c> (the single source of truth the release workflow tags),
        /// via the pure-managed <see cref="GodotMcpServerView.ParsePluginVersion"/> parser, with a safe
        /// fallback. Mirrors <c>GodotMcpConnection.ResolvePluginVersion</c> but kept local so the runtime
        /// entry point does not depend on the editor-coupled connection class. Never throws — a read/parse
        /// failure degrades to <see cref="FallbackPluginVersion"/>.
        /// </summary>
        static string ResolvePluginVersion()
        {
            try
            {
                var path = ProjectSettings.GlobalizePath("res://addons/godot_mcp/plugin.cfg");
                if (System.IO.File.Exists(path))
                {
                    var parsed = GodotMcpServerView.ParsePluginVersion(System.IO.File.ReadAllText(path));
                    if (!string.IsNullOrEmpty(parsed))
                        return parsed!;
                }
            }
            catch
            {
                // Fall through to the fallback — never let a config read break the runtime build path.
            }

            return FallbackPluginVersion;
        }

        /// <summary>
        /// Version reported when <c>plugin.cfg</c> is unreadable. Bump alongside <c>plugin.cfg</c>'s
        /// <c>version=</c> (the live parsed value above is the source of truth; this is only the degraded
        /// fallback). Mirrors <c>GodotMcpConnection.FallbackPluginVersion</c>.
        /// </summary>
        const string FallbackPluginVersion = "0.7.0";

        /// <summary>
        /// Ensure a <see cref="MainThreadDispatcher"/> Node exists in the running game's
        /// <see cref="SceneTree"/> and that <see cref="GodotMainThread"/> is installed as ReflectorNet's
        /// <c>MainThread.Instance</c>.
        ///
        /// <para>
        /// In a running game the active <see cref="MainLoop"/> is a <see cref="SceneTree"/>; the dispatcher
        /// is added under <see cref="SceneTree.Root"/> (the autoload-equivalent injection point) so it
        /// receives <see cref="Node._Process"/> ticks for the lifetime of the game — no editor-frame
        /// dependency. Idempotent: a no-op when a dispatcher is already in the tree (e.g. a second
        /// <c>Initialize().Build()</c>, or a developer-installed autoload dispatcher). Defensive — if the
        /// SceneTree is not yet available (called too early, before the tree exists) it installs
        /// <see cref="GodotMainThread"/> anyway and logs a warning; the developer can re-run after the tree
        /// is up.
        /// </para>
        /// </summary>
        static void EnsureMainThreadDispatcher()
        {
            // GodotMainThread.Install() is idempotent and pure-managed — safe to call first.
            GodotMainThread.Install();

            if (MainThreadDispatcher.Instance != null)
                return; // already pumped (editor plugin, a prior runtime init, or a developer autoload).

            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            {
                GD.PushWarning(
                    "[Godot-MCP] runtime dispatcher bootstrap: no live SceneTree yet — tool handlers cannot " +
                    "marshal to the main thread until a MainThreadDispatcher is in the tree. Call " +
                    "GodotMcpRuntime.Initialize(...).Build() once the SceneTree is available (e.g. from an " +
                    "autoload _Ready), or install your own dispatcher.");
                return;
            }

            var dispatcher = new MainThreadDispatcher { Name = DispatcherNodeName };

            // Add under the tree root via CallDeferred so the add lands on a safe frame boundary regardless
            // of the calling thread or whether we are mid-tree-iteration. (A direct AddChild is never safe to
            // take here: MainThreadDispatcher.MainThreadId stays -1 until a dispatcher's _EnterTree runs, so
            // IsMainThread is false on this first-install path even on the engine main thread — and a second
            // Initialize().Build() already returned at the Instance != null guard above.)
            tree.Root.CallDeferred(Node.MethodName.AddChild, dispatcher);

            GD.Print($"[Godot-MCP] runtime main-thread dispatcher scheduled ('{DispatcherNodeName}').");
        }
    }
}
