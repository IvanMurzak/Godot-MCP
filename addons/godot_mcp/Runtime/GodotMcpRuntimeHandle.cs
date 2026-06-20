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
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Runtime
{
    /// <summary>
    /// The runtime MCP connection handle returned by <see cref="GodotMcpRuntime.Build"/> — the Godot analog
    /// of the object Unity-MCP's <c>UnityMcpPluginRuntime.Initialize(...).Build()</c> returns. Wraps the
    /// built reused <see cref="IMcpPlugin"/> (SignalR client) and exposes the minimal in-game surface:
    /// <see cref="Connect"/> / <see cref="Disconnect"/>.
    ///
    /// <para>
    /// <b>Default OFF.</b> Building a handle does NOT open a connection. The game must call
    /// <see cref="Connect"/> explicitly — the security-required opt-in. Auto-reconnect/backoff (when the
    /// resolved <see cref="GodotMcpConfig.KeepConnected"/> is true) is handled inside the reused client, not
    /// reimplemented here.
    /// </para>
    /// </summary>
    public sealed class GodotMcpRuntimeHandle : IDisposable
    {
        readonly IMcpPlugin _plugin;
        readonly GodotMcpConfig _config;
        readonly bool _uninstallErrorCaptureOnDispose;
        int _disposed;

        internal GodotMcpRuntimeHandle(IMcpPlugin plugin, GodotMcpConfig config,
            bool uninstallErrorCaptureOnDispose = false)
        {
            _plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _uninstallErrorCaptureOnDispose = uninstallErrorCaptureOnDispose;
        }

        /// <summary>The resolved connection config (host / token / mode) this handle connects with.</summary>
        public GodotMcpConfig Config => _config;

        /// <summary>The built reused MCP plugin instance (SignalR client + managers).</summary>
        public IMcpPlugin Plugin => _plugin;

        /// <summary>
        /// Open the MCP connection to the configured server. Idempotent in the sense the reused client
        /// guards re-entry; when <see cref="GodotMcpConfig.KeepConnected"/> is true the client also re-arms
        /// its own auto-reconnect intent here. Returns <c>true</c> when the initial connect succeeded
        /// (the client keeps retrying afterwards if <c>KeepConnected</c>).
        /// </summary>
        public Task<bool> Connect(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _plugin.Connect(cancellationToken);
        }

        /// <summary>
        /// Close the MCP connection and stop auto-reconnect (the reused client flips its own
        /// <c>KeepConnected</c> to false inside <see cref="IConnection.Disconnect"/>). The handle remains
        /// reusable — a subsequent <see cref="Connect"/> re-arms the connection.
        /// </summary>
        public Task Disconnect(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _plugin.Disconnect(cancellationToken);
        }

        /// <summary>
        /// Tear down the handle: synchronously kill the live connection (immediate disconnect — cancels the
        /// reconnect loop and fire-and-forgets the transport teardown) and dispose the reused plugin. Safe
        /// to call multiple times. Call this on game shutdown / when the connection is no longer needed.
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            try { _plugin.DisconnectImmediate(); }
            catch { /* best-effort: never throw out of Dispose */ }

            try { _plugin.Dispose(); }
            catch { /* best-effort: never throw out of Dispose */ }

            // Uninstall in-game runtime error capture IFF this handle's Build installed it (opt-in via
            // WithRuntimeErrorCapture). Removes the engine logger + the AppDomain/TaskScheduler fault hooks and
            // clears RuntimeErrorCollector.Current. Idempotent + defensive in RuntimeErrorCapture.Uninstall.
            if (_uninstallErrorCaptureOnDispose)
            {
                try { RuntimeErrorCapture.Uninstall(); }
                catch { /* best-effort: never throw out of Dispose */ }
            }
        }

        void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
                throw new ObjectDisposedException(nameof(GodotMcpRuntimeHandle));
        }
    }
}
