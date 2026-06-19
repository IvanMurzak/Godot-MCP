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
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The <see cref="ILoggerProvider"/> handed to <c>McpPluginBuilder</c> so the reused framework's
    /// <c>ILogger&lt;T&gt;</c> calls (ConnectionManager / hub connector connect, hub-state, version
    /// handshake, errors) reach the Godot editor Output. The Godot analog of Unity-MCP's
    /// <c>UnityLoggerProvider</c>.
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>): the level threshold is read live via the
    /// injected <see cref="Func{TResult}"/> (so the dock dropdown applies without a rebuild) and the Godot
    /// Output sink is the injected <see cref="GodotLogSink"/>. <see cref="GodotMcpConnection"/> constructs
    /// this with a <c>() =&gt; _config.ActiveLogLevel</c> level provider and a <c>GD.*</c>+collector sink.
    /// </para>
    /// </summary>
    public sealed class GodotMcpLoggerProvider : ILoggerProvider
    {
        readonly Func<GodotMcpLogLevel> _levelProvider;
        readonly GodotLogSink _sink;

        /// <summary>
        /// Construct the provider.
        /// </summary>
        /// <param name="levelProvider">Reads the LIVE configured threshold on every log call.</param>
        /// <param name="sink">Routes a formatted line to the Godot Output + log collector.</param>
        public GodotMcpLoggerProvider(Func<GodotMcpLogLevel> levelProvider, GodotLogSink sink)
        {
            _levelProvider = levelProvider ?? throw new ArgumentNullException(nameof(levelProvider));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public ILogger CreateLogger(string categoryName) =>
            new GodotMcpLogger(categoryName, _levelProvider, _sink);

        public void Dispose() { /* No resources to dispose. */ }
    }
}
