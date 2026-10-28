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
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    // Reads collectible-context lifetimes through forced GCs and owns the process-wide parked-server slot.
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class LocalServerHandoffCollection
    {
        public const string Name = "Local server handoff (serial)";
    }

    /// <summary>
    /// <see cref="GodotMcpLocalServerHandoff"/>: the plugin-owned local server survives a C# assembly reload without
    /// keeping the unloading assembly alive. Godot loads the addon into a collectible context; these tests start the
    /// "server" (a long-running child process) from a collectible copy of this assembly, the way the addon does.
    /// </summary>
    [Collection(LocalServerHandoffCollection.Name)]
    public class GodotMcpLocalServerHandoffTests
    {
        [Fact]
        public void ServerWithPluginHandlers_PinsTheCollectibleContextUntilItExits()
        {
            // The shape before the fix (Exited + OutputDataReceived handlers and async line reads): the running process
            // holds the handlers, so the context cannot unload while the server runs. This is the reload failure.
            var alcRef = StartFromCollectibleContext(nameof(ServerProcessProbe.StartWithPluginHandlers), out var pid);
            using var server = Process.GetProcessById(pid);
            try
            {
                Assert.False(WaitForCollection(alcRef, attempts: 10), "a server process with the plugin's handlers did not pin the collectible context; the test cannot discriminate");
            }
            finally
            {
                Kill(server);
            }
            Assert.True(WaitForCollection(alcRef), "the collectible context stayed pinned after the server exited");
        }

        [Fact]
        public void ParkedServer_DoesNotPinTheCollectibleContext_AndIsTakenBackRunning()
        {
            ClearSlot();
            var alcRef = StartFromCollectibleContext(nameof(ServerProcessProbe.StartDrainAndPark), out var pid);
            try
            {
                Assert.True(WaitForCollection(alcRef), "the parked server process pinned the collectible context");

                using var parked = GodotMcpLocalServerHandoff.TakeParked();
                Assert.NotNull(parked);
                Assert.Equal(pid, parked!.Id);
                Assert.False(parked.HasExited);
                Assert.Null(GodotMcpLocalServerHandoff.TakeParked()); // the slot is cleared by the take
                Kill(parked);
            }
            finally
            {
                ClearSlot();
                KillIfRunning(pid);
            }
        }

        [Fact]
        public void TakeParked_ReturnsNull_WhenNothingIsParkedOrTheProcessExited()
        {
            ClearSlot();
            Assert.Null(GodotMcpLocalServerHandoff.TakeParked());

            var process = Process.Start(ServerProcessProbe.Chatty(seconds: 60))!;
            Kill(process);
            GodotMcpLocalServerHandoff.Park(process);
            Assert.Null(GodotMcpLocalServerHandoff.TakeParked());
            Assert.Null(AppDomain.CurrentDomain.GetData(GodotMcpLocalServerHandoff.ParkedProcessKey));
        }

        [Fact]
        public void Drain_KeepsAVerboseServerFromBlockingOnAFullPipe()
        {
            // Megabytes of output, far beyond a pipe buffer: without a reader the child blocks and never exits.
            using var process = Process.Start(ServerProcessProbe.Flood())!;
            GodotMcpLocalServerHandoff.Drain(process);
            var exited = process.WaitForExit(60_000);
            if (!exited)
                Kill(process);
            Assert.True(exited, "the child blocked on its output pipe");
        }

        static void ClearSlot() => AppDomain.CurrentDomain.SetData(GodotMcpLocalServerHandoff.ParkedProcessKey, null);

        /// <summary>
        /// Load a second copy of this test assembly into a collectible context, run <paramref name="probeMethod"/> of
        /// its <see cref="ServerProcessProbe"/> (starts the server, returns its PID), unload the context and hand back
        /// only a weak reference. NoInlining so no strong reference survives in the caller's frame.
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static WeakReference StartFromCollectibleContext(string probeMethod, out int pid)
        {
            var alc = new AssemblyLoadContext("local-server-handoff-test", isCollectible: true);
            var alcRef = new WeakReference(alc, trackResurrection: true);
            pid = InvokeProbe(alc, probeMethod);
            alc.Unload();
            return alcRef;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int InvokeProbe(AssemblyLoadContext alc, string probeMethod)
        {
            var assembly = alc.LoadFromAssemblyPath(typeof(ServerProcessProbe).Assembly.Location);
            var probe = assembly.GetType(typeof(ServerProcessProbe).FullName!, throwOnError: true)!;
            return (int)probe.GetMethod(probeMethod)!.Invoke(null, null)!;
        }

        /// <summary>Bounded forced-GC loop; true when the context was collected (unload finishes through finalizers).</summary>
        static bool WaitForCollection(WeakReference reference, int attempts = 200)
        {
            for (int i = 0; i < attempts; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                if (!reference.IsAlive)
                    return true;
                Thread.Sleep(50);
            }
            return !reference.IsAlive;
        }

        static void Kill(Process process)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { /* already exited */ }
            process.WaitForExit(10_000);
        }

        static void KillIfRunning(int pid)
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                Kill(process);
            }
            catch (ArgumentException) { /* gone */ }
        }
    }

    /// <summary>
    /// Starts a stand-in for the local server. Public so the tests can call it in the collectible copy of this assembly.
    /// </summary>
    public static class ServerProcessProbe
    {
        /// <summary>A child that prints a line about every 0.1-1 s for <paramref name="seconds"/>, its output redirected.</summary>
        public static ProcessStartInfo Chatty(int seconds) => Redirected(OperatingSystem.IsWindows()
            ? ("cmd.exe", $"/c ping -n {seconds} 127.0.0.1")
            : ("/bin/sh", $"-c \"i=0; while [ $i -lt {seconds * 10} ]; do echo chatter; sleep 0.1; i=$((i+1)); done\""));

        /// <summary>A child that writes several megabytes to stdout and stderr, then exits.</summary>
        public static ProcessStartInfo Flood() => Redirected(OperatingSystem.IsWindows()
            ? ("cmd.exe", "/c \"for /L %i in (1,1,20000) do @echo 0123456789012345678901234567890123456789012345678901234567890123456789 & @echo 0123456789 1>&2\"")
            : ("/bin/sh", "-c \"head -c 4000000 /dev/zero; head -c 1000000 /dev/zero 1>&2\""));

        static ProcessStartInfo Redirected((string file, string args) command) => new(command.file, command.args)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        /// <summary>How the server manager wired its server before the fix.</summary>
        public static int StartWithPluginHandlers()
        {
            var process = new Process { StartInfo = Chatty(seconds: 120), EnableRaisingEvents = true };
            process.Exited += OnExited;
            process.OutputDataReceived += Discard;
            process.ErrorDataReceived += Discard;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process.Id;
        }

        /// <summary>How the server manager starts its server and hands it over at a reload now.</summary>
        public static int StartDrainAndPark()
        {
            var process = new Process { StartInfo = Chatty(seconds: 120), EnableRaisingEvents = true };
            process.Exited += OnExited;
            process.Start();
            GodotMcpLocalServerHandoff.Drain(process);

            process.Exited -= OnExited; // DetachForReload
            GodotMcpLocalServerHandoff.Park(process);
            return process.Id;
        }

        static void OnExited(object? sender, EventArgs e) { }

        static void Discard(object sender, DataReceivedEventArgs e) { }
    }
}
