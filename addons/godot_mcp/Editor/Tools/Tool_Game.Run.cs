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
        // A game that prints every frame for a minute writes tens of thousands of lines; the result shows a few dozen.
        const int MaxCollectedLines = 20_000;

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
            [Description("res:// path of the scene to run; omit for the project's main scene.")]
            string? scene = null,
            [Description("Game time to run, in seconds (fixed 60 frames per second, so a light game finishes faster than real " +
                "time). Default 3, at most 60.")]
            double seconds = DefaultSeconds
        )
        {
            if (double.IsNaN(seconds) || seconds <= 0 || seconds > MaxSeconds)
                throw new ArgumentException($"'seconds' must be more than 0 and at most {MaxSeconds}.", nameof(seconds));
            var path = string.IsNullOrWhiteSpace(scene) ? null : scene!.Trim();
            var (executable, projectDir) = MainThread.Instance.Run(() =>
            {
                if (path != null)
                    EditorToolGuards.RequireResourceExists(path, nameof(scene), $"no scene at '{path}'");
                else if (string.IsNullOrEmpty(ProjectSettings.GetSetting("application/run/main_scene").AsString()))
                    throw new InvalidOperationException("the project has no main scene; pass 'scene' (a res:// .tscn path)");
                return (OS.GetExecutablePath(), ProjectSettings.GlobalizePath("res://"));
            });

            var args = new List<string> { "--headless", "--path", projectDir, "--fixed-fps", FramesPerSecond.ToString(CultureInfo.InvariantCulture),
                "--quit-after", ((int)Math.Ceiling(seconds * FramesPerSecond)).ToString(CultureInfo.InvariantCulture) };
            if (path != null) args.Add(path);
            // A light game runs its game time faster than real time; a heavy or blocked one is stopped after this.
            var limit = Math.Max(30, seconds * 4);
            var (exitCode, lines, dropped) = await RunProcess(executable, projectDir, args, TimeSpan.FromSeconds(limit)).ConfigureAwait(false);
            return GameRunOutput.Format(path ?? "the main scene", seconds, exitCode, limit, lines, dropped);
        }

        /// <summary>Run a process, collecting stdout and stderr in arrival order; null exit code when it hit the limit and was killed.</summary>
        static async Task<(int? exitCode, List<string> lines, int dropped)> RunProcess(string executable, string workingDir,
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

            var lines = new List<string>();
            var dropped = 0;
            void Collect(object _, DataReceivedEventArgs e)
            {
                if (e.Data == null) return;
                lock (lines)
                {
                    if (lines.Count < MaxCollectedLines) lines.Add(e.Data);
                    else dropped++;
                }
            }
            using var process = new Process { StartInfo = psi };
            process.OutputDataReceived += Collect;
            process.ErrorDataReceived += Collect;
            if (!process.Start())
                throw new InvalidOperationException($"could not start '{executable}'");
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var killed = false;
            using (var timeout = new CancellationTokenSource(limit))
            {
                try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException)
                {
                    killed = true;
                    try { process.Kill(entireProcessTree: true); }
                    catch (InvalidOperationException) { /* exited meanwhile */ }
                }
            }
            await process.WaitForExitAsync().ConfigureAwait(false); // also drains the redirected streams: no more Collect calls
            return (killed ? null : process.ExitCode, lines, dropped);
        }
    }
}
#endif
