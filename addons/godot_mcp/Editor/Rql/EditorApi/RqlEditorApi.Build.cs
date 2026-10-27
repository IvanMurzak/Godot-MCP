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
using System.Diagnostics;
using System.IO;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        /// <summary>Longest a build may take before it is stopped.</summary>
        public const int BuildTimeoutMs = 300_000;

        /// <summary>
        /// <c>editor | call BuildProject()</c>: <c>dotnet build</c> of the project's C# (what the editor's Build button runs), so a
        /// C# script written with script-create / script-update — a new [GlobalClass] resource type, a changed node script —
        /// loads; the editor reloads the assembly by itself once the build output changed. Runs on the main thread on
        /// purpose: the reload (which rebuilds this plugin) can only start after the call has answered.
        /// </summary>
        [RqlCallable("build the project's C# (dotnet build, like the editor's Build button) so new or changed C# scripts and [GlobalClass] " +
            "types load; the editor reloads the assembly a few seconds later. Returns the outcome; fails with the compiler errors")]
        public string BuildProject()
        {
            var csproj = new Extensions.ConsumerProjectFile().Path ?? throw new InvalidOperationException("this project has no C# project (.csproj) to build");
            var dir = Path.GetDirectoryName(csproj)!;
            var start = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = dir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            foreach (var arg in new[] { "build", csproj, "-c", "Debug", "-nologo", "-v", "q" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("could not start dotnet (is the .NET SDK installed and on PATH?)");
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(BuildTimeoutMs))
            {
                try { process.Kill(entireProcessTree: true); } catch (Exception) { /* already gone */ }
                throw new TimeoutException($"the build did not finish in {BuildTimeoutMs / 1000} s and was stopped");
            }
            process.WaitForExit(); // flush the redirected streams
            return BuildOutputText.Summarize(process.ExitCode, stdout.Result + "\n" + stderr.Result, dir);
        }
    }
}
#endif
