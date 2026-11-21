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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>The game as a whole, run from the editor's project: <c>game-run</c>.</summary>
    [AiToolType]
    public partial class Tool_Game
    {
        public const string GameRunToolId = "game-run";

        public const double DefaultSeconds = 3;
        public const double MaxSeconds = 60;
        const int FramesPerSecond = 60;

        [AiTool
        (
            GameRunToolId,
            Title = "Game / Run",
            ReadOnlyHint = false,
            DestructiveHint = false,
            IdempotentHint = false,
            OpenWorldHint = false
        )]
        [Description("Run the game (the main scene, or 'scene') windowless in a separate process for 'seconds' of game time " +
            "and return, in one call, its errors and warnings (with GDScript backtraces) and what it printed. Use it to " +
            "reproduce a runtime error and to check a fix; no play / wait / stop / log reading needed. It runs the files " +
            "as saved: save scene edits first (rql-mutate editor | call SaveScene()); C# changes need script-build first. " +
            "Nothing is rendered, so errors that need a GPU or a window do not show.")]
        public async Task<string> Run
        (
            [Description("res:// (or uid://) path of the scene to run; omit for the project's main scene.")]
            string? scene = null,
            [Description("Game time to run, in seconds (fixed 60 frames per second, so a light game finishes faster than real " +
                "time). Default 3, at most 60.")]
            double seconds = DefaultSeconds
        )
        {
            if (double.IsNaN(seconds) || seconds <= 0 || seconds > MaxSeconds)
                throw new ArgumentException($"'seconds' must be more than 0 and at most {MaxSeconds}.", nameof(seconds));
            var requested = string.IsNullOrWhiteSpace(scene) ? null : scene!.Trim();
            var (executable, projectDir, scenePath) = MainThread.Instance.Run(() =>
                (OS.GetExecutablePath(), ProjectSettings.GlobalizePath("res://"), ResolveScene(requested)));

            // Its own log file: the default one (user://logs/godot.log) is what the gameLog source reads for a game run
            // with Play, and a run rotates it. The file also keeps stdout and stderr in their real order.
            var logFile = Path.Combine(Path.GetTempPath(), $"godot-mcp-game-run-{Guid.NewGuid():N}.log");
            var args = new List<string> { "--headless", "--path", projectDir, "--log-file", logFile,
                "--fixed-fps", FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                "--quit-after", ((int)Math.Ceiling(seconds * FramesPerSecond)).ToString(CultureInfo.InvariantCulture), scenePath };
            // A light game runs its game time faster than real time; a heavy or blocked one is stopped after this.
            var limit = Math.Max(30, seconds * 4);
            try
            {
                var (exitCode, piped) = await RunProcess(executable, projectDir, args, TimeSpan.FromSeconds(limit)).ConfigureAwait(false);
                var output = ReadLog(logFile) ?? piped;
                return GameRunOutput.Format(requested == null ? $"the main scene ({scenePath})" : scenePath, seconds, exitCode, limit,
                    output.Lines, output.Dropped);
            }
            finally
            {
                try { File.Delete(logFile); } catch (IOException) { /* best effort */ } catch (UnauthorizedAccessException) { }
            }
        }

        /// <summary>Main thread: the res:// path of the scene to run (uid:// resolved), or an error the caller can act on.</summary>
        static string ResolveScene(string? requested)
        {
            var path = requested ?? ProjectSettings.GetSetting("application/run/main_scene").AsString();
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException("the project has no main scene; pass 'scene' (a res:// .tscn path)");
            if (path.StartsWith("uid://", StringComparison.Ordinal))
            {
                var id = ResourceUid.TextToId(path);
                if (id == ResourceUid.InvalidId || !ResourceUid.HasId(id))
                    throw new ArgumentException($"no resource has the uid '{path}'", "scene");
                path = ResourceUid.GetIdPath(id);
            }
            // Exists(path, "PackedScene") is true for any loadable file (a .gd too), and Godot runs the main scene instead
            // of a positional path that is not a scene: check the extension.
            var ext = System.IO.Path.GetExtension(path).TrimStart('.');
            if (!ResourceLoader.Exists(path) || Array.IndexOf(ResourceLoader.GetRecognizedExtensionsForType("PackedScene"), ext.ToLowerInvariant()) < 0)
                throw requested == null
                    ? new InvalidOperationException($"the main scene '{path}' is not a scene file; pass 'scene' (a res:// .tscn path)")
                    : new ArgumentException($"'{path}' is not a scene file (.tscn / .scn)", "scene");
            return path;
        }

        static GameRunLines? ReadLog(string logFile)
        {
            try
            {
                if (!File.Exists(logFile)) return null;
                var output = new GameRunLines();
                foreach (var line in File.ReadLines(logFile)) output.Add(line);
                return output.Lines.Count > 0 ? output : null;
            }
            catch (IOException) { return null; }
        }

        /// <summary>Run a process, collecting stdout and stderr; null exit code when it hit the limit and was killed.
        /// A cancelled tool call stops it too.</summary>
        static async Task<(int? exitCode, GameRunLines output)> RunProcess(string executable, string workingDir,
            List<string> args, TimeSpan limit)
        {
            var psi = new ProcessStartInfo(executable)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDir,
            };
            foreach (var arg in args) psi.ArgumentList.Add(arg);

            var output = new GameRunLines();
            void Collect(object _, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (output) output.Add(e.Data);
            }
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += Collect;
            process.ErrorDataReceived += Collect;
            if (!process.Start())
                throw new InvalidOperationException($"could not start '{executable}'");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var call = ToolCallCancellation.Token;
            var killed = false;
            using (var stop = CancellationTokenSource.CreateLinkedTokenSource(call))
            {
                stop.CancelAfter(limit);
                try { await process.WaitForExitAsync(stop.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    killed = true;
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { /* exited meanwhile */ }
                }
            }
            await process.WaitForExitAsync().ConfigureAwait(false); // also drains the redirected streams: no more Collect calls
            call.ThrowIfCancellationRequested();
            return (killed ? null : process.ExitCode, output);
        }
    }
}
#endif
