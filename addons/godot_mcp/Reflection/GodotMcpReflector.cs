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
using com.IvanMurzak.ReflectorNet;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Ambient accessor for the process-wide <see cref="Reflector"/> that the MCP connection built
    /// (with the Godot type converters registered). The Godot analog of Unity-MCP's
    /// <c>UnityMcpPluginEditor.Instance.Reflector</c>.
    ///
    /// <para>
    /// Tool handlers that need a reflector at call time — e.g. <c>node-modify</c>, which routes through
    /// <c>Reflector.TryModify</c>/<c>TryModifyAt</c>/<c>TryPatch</c> — read <see cref="Current"/> rather
    /// than constructing a fresh reflector, so they share the exact converter set the connection
    /// registered. <see cref="GodotMcpConnection.Start"/> assigns <see cref="Current"/> when it builds
    /// the plugin reflector; if a handler runs before a connection exists (e.g. a direct unit call),
    /// <see cref="GetOrCreate"/> falls back to a freshly-built default reflector so the tool layer never
    /// hard-depends on the editor lifecycle.
    /// </para>
    ///
    /// This is engine-runtime code (no Godot editor API, no <c>#if TOOLS</c>), so it is unit-testable in
    /// the plain xUnit host.
    /// </summary>
    public static class GodotMcpReflector
    {
        /// <summary>
        /// The reflector the active connection built, or <c>null</c> before <see cref="GodotMcpConnection.Start"/>
        /// has run / after the connection was disposed.
        /// </summary>
        public static Reflector? Current { get; set; }

        /// <summary>
        /// Return <see cref="Current"/> when set, otherwise a freshly-built default reflector (with the
        /// Godot converters registered). Never returns <c>null</c>, so tool handlers can call it without
        /// a null-guard. Does NOT cache the fallback into <see cref="Current"/> — the connection remains
        /// the single owner of the shared instance.
        /// </summary>
        public static Reflector GetOrCreate()
            => Current ?? GodotReflectorFactory.CreateDefaultReflector();
    }
}
