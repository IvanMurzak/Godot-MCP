/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Keeps the plugin-owned local MCP server process across a C# assembly reload without pinning the reloaded
    /// assembly. Pure BCL (no Godot types), so the xUnit host tests it against a real collectible load context.
    ///
    /// <para>
    /// Godot loads this addon into a collectible <c>AssemblyLoadContext</c> and unloads it on every rebuild. Anything
    /// that outlives the unload and still references a delegate or object of this assembly keeps it alive, and the
    /// reload fails ("Failed to unload assemblies", plugin offline). A running server <see cref="Process"/> with our
    /// <c>Exited</c> / <c>OutputDataReceived</c> handlers is exactly that: its pending pipe reads and exit wait hold the
    /// handlers until the server exits. So the server's output is drained by BCL code only (<see cref="Drain"/>), and
    /// at reload the manager removes its <c>Exited</c> handler and parks the bare <see cref="Process"/> in the
    /// AppDomain's data store (<see cref="Park"/>), where the next plugin instance takes it back
    /// (<see cref="TakeParked"/>). The user's server keeps running through every rebuild.
    /// </para>
    /// </summary>
    public static class GodotMcpLocalServerHandoff
    {
        /// <summary>AppDomain data key of the parked server process.</summary>
        public const string ParkedProcessKey = "com.IvanMurzak.Godot.MCP.LocalServerProcess";

        /// <summary>
        /// Read and discard the server's redirected stdout and stderr until it exits, so it never blocks on a full pipe.
        /// The server echoes its launch arguments (the token included), so nothing is logged. Only BCL code runs the
        /// copy, and the execution context does not flow into it, so it holds nothing of this assembly.
        /// </summary>
        public static void Drain(Process process)
        {
            using (ExecutionContext.SuppressFlow())
            {
                _ = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
                _ = process.StandardError.BaseStream.CopyToAsync(Stream.Null);
            }
        }

        /// <summary>
        /// Hand <paramref name="process"/> to the next plugin instance. The caller must have removed every handler it
        /// added to the process; a process parked earlier and never taken is replaced (and left running).
        /// </summary>
        public static void Park(Process process) => AppDomain.CurrentDomain.SetData(ParkedProcessKey, process);

        /// <summary>
        /// The process a previous plugin instance parked, or null. Clears the slot. A process that has exited meanwhile
        /// is disposed and not returned.
        /// </summary>
        public static Process? TakeParked()
        {
            var domain = AppDomain.CurrentDomain;
            if (domain.GetData(ParkedProcessKey) is not Process process)
                return null;
            domain.SetData(ParkedProcessKey, null);

            bool exited;
            try { exited = process.HasExited; }
            catch (InvalidOperationException) { exited = true; } // disposed, or never started
            if (!exited)
                return process;

            try { process.Dispose(); } catch { /* best effort */ }
            return null;
        }
    }
}
#endif
