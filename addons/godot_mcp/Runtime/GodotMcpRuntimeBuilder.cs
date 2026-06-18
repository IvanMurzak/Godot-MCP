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
using System.Collections.Generic;
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;

namespace com.IvanMurzak.Godot.MCP.Runtime
{
    /// <summary>
    /// Fluent configuration surface for the runtime (in-game) MCP connection, handed to the game
    /// developer's <c>configure</c> callback by <see cref="GodotMcpRuntime.Initialize"/>. The Godot analog
    /// of Unity-MCP's <c>UnityMcpPluginBuilder</c>.
    ///
    /// <para>
    /// <b>Zero tools by default — strictly manual.</b> A freshly-built runtime registers NO MCP tools.
    /// The developer opts their own <c>[AiToolType]</c>/<c>[AiTool]</c> classes in explicitly via
    /// <see cref="WithTools(Type[])"/> or <see cref="WithToolsFromAssembly(Assembly)"/>; with none
    /// registered the connection still builds and connects with an empty tool set (exactly Unity's model).
    /// The addon's own editor tool families are gated by <c>#if TOOLS</c> and do not compile into an
    /// exported game build, so they can never leak into a runtime registration even via an
    /// <see cref="WithToolsFromAssembly(Assembly)"/> over the addon assembly.
    /// </para>
    ///
    /// <para>
    /// <b>Security posture.</b> The builder applies NO persisted-config layer (no <c>user://</c> config
    /// file is auto-loaded in a build — that is an editor-only convenience). The developer supplies host/
    /// token either in code via <see cref="WithConfig(Action{GodotMcpConfig})"/> or out-of-band through the
    /// <c>GODOT_MCP_*</c> process-environment / project <c>.env</c> overrides that <see cref="GodotMcpConfig"/>
    /// reads live. The connection is <b>default OFF</b> — <see cref="GodotMcpRuntimeHandle.Connect"/> must be
    /// called explicitly; nothing auto-connects.
    /// </para>
    ///
    /// This builder only accumulates intent; the actual plugin build + dispatcher bootstrap happen in
    /// <see cref="GodotMcpRuntime.Build"/> when the developer calls <c>.Build()</c> on the returned handle.
    /// </summary>
    public sealed class GodotMcpRuntimeBuilder
    {
        readonly List<Action<GodotMcpConfig>> _configActions = new();
        readonly List<Assembly> _toolAssemblies = new();
        readonly List<Type> _toolTypes = new();

        /// <summary>
        /// Whether <see cref="GodotMcpRuntime.Initialize"/> should guarantee a
        /// <see cref="MainThreadDispatch.MainThreadDispatcher"/> Node in the running <c>SceneTree</c> and
        /// install <see cref="MainThreadDispatch.GodotMainThread"/>. ON by default — tool handlers marshal
        /// Godot API calls onto the main thread through it. A developer who already owns a dispatcher (e.g.
        /// installed via an autoload) can turn this off via <see cref="WithoutMainThreadDispatcher"/>.
        /// </summary>
        internal bool InstallDispatcher { get; private set; } = true;

        internal GodotMcpRuntimeBuilder() { }

        /// <summary>
        /// Register a mutation applied to the connection's <see cref="GodotMcpConfig"/> at build time
        /// (e.g. set <c>Host</c> / <c>Token</c> / <c>ConnectionMode</c>). Multiple calls compose in order.
        /// Returns <c>this</c> for chaining.
        /// </summary>
        /// <example>
        /// <code>
        /// builder.WithConfig(c =>
        /// {
        ///     c.ConnectionMode = GodotMcpConnectionMode.Custom;
        ///     c.Host = "http://localhost:8080";
        ///     c.AuthOption = GodotMcpAuthOption.Required;
        ///     c.Token = "...";
        /// });
        /// </code>
        /// </example>
        public GodotMcpRuntimeBuilder WithConfig(Action<GodotMcpConfig> configure)
        {
            if (configure == null)
                throw new ArgumentNullException(nameof(configure));

            _configActions.Add(configure);
            return this;
        }

        /// <summary>
        /// Opt every <c>[AiToolType]</c>/<c>[AiTool]</c> class declared in <paramref name="assembly"/> into
        /// the runtime tool set. The common case is <c>Assembly.GetExecutingAssembly()</c> so a game
        /// registers its own tools. Multiple calls accumulate; duplicate assemblies are de-duplicated at
        /// build time. Returns <c>this</c> for chaining.
        /// </summary>
        public GodotMcpRuntimeBuilder WithToolsFromAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            if (!_toolAssemblies.Contains(assembly))
                _toolAssemblies.Add(assembly);
            return this;
        }

        /// <summary>
        /// Opt specific <c>[AiToolType]</c> classes into the runtime tool set (when a whole-assembly scan
        /// is too broad). Null entries are ignored; duplicates are de-duplicated at build time. Returns
        /// <c>this</c> for chaining.
        /// </summary>
        public GodotMcpRuntimeBuilder WithTools(params Type[] toolTypes)
        {
            if (toolTypes == null)
                return this;

            foreach (var type in toolTypes)
            {
                if (type != null && !_toolTypes.Contains(type))
                    _toolTypes.Add(type);
            }
            return this;
        }

        /// <summary>
        /// Skip the automatic <see cref="MainThreadDispatch.MainThreadDispatcher"/> bootstrap. Use this only
        /// when the game already installs a dispatcher itself (e.g. as a Godot autoload) AND has called
        /// <see cref="MainThreadDispatch.GodotMainThread.Install"/>. With the bootstrap skipped and no
        /// dispatcher present, tool handlers that marshal to the main thread will fault — so leave it ON
        /// (the default) unless you know you have your own. Returns <c>this</c> for chaining.
        /// </summary>
        public GodotMcpRuntimeBuilder WithoutMainThreadDispatcher()
        {
            InstallDispatcher = false;
            return this;
        }

        /// <summary>
        /// Finalize this configuration: build the reused MCP plugin (reflector + opted-in tools + resolved
        /// config), bootstrap the main-thread dispatcher in the running <c>SceneTree</c> (unless
        /// <see cref="WithoutMainThreadDispatcher"/> was called), and return the connection handle. The
        /// handle is <b>default OFF</b> — call <see cref="GodotMcpRuntimeHandle.Connect"/> to open the
        /// connection. The Godot analog of Unity-MCP's <c>UnityMcpPluginRuntime.Initialize(...).Build()</c>.
        /// </summary>
        public GodotMcpRuntimeHandle Build() => GodotMcpRuntime.Build(this);

        /// <summary>Apply the accumulated <see cref="WithConfig"/> mutations onto <paramref name="config"/>.</summary>
        internal void ApplyConfig(GodotMcpConfig config)
        {
            foreach (var action in _configActions)
                action(config);
        }

        /// <summary>The assemblies the developer opted in via <see cref="WithToolsFromAssembly"/>.</summary>
        internal IReadOnlyList<Assembly> ToolAssemblies => _toolAssemblies;

        /// <summary>The individual tool types the developer opted in via <see cref="WithTools"/>.</summary>
        internal IReadOnlyList<Type> ToolTypes => _toolTypes;

        /// <summary>True when the developer registered no tools at all (the empty-tool-set default).</summary>
        internal bool HasNoTools => _toolAssemblies.Count == 0 && _toolTypes.Count == 0;
    }
}
