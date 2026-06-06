/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// The thin IO seam over the CONSUMER's game <c>.csproj</c> — the file an extension install reads, transforms
    /// via the pure <see cref="ExtensionInstallPlanner"/>, and writes back. Kept behind this interface so the
    /// planner stays pure (no Godot / no IO) and so the install flow is unit-testable with an in-memory fake. The
    /// real editor implementation (<c>ConsumerProjectFile</c>, <c>#if TOOLS</c>) globalizes <c>res://</c> and
    /// locates the project <c>.csproj</c>; the in-memory test double exercises the same flow with no filesystem.
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so it lives in the CI-unit-testable layer alongside
    /// the planner/detector.
    /// </para>
    /// </summary>
    public interface IConsumerProjectFile
    {
        /// <summary>
        /// True when a consumer <c>.csproj</c> could be located (a real project, not a published-addon-only context).
        /// When false, the dock disables the install buttons + shows a "no .csproj found" notice rather than writing.
        /// </summary>
        bool Exists { get; }

        /// <summary>The absolute path of the located consumer <c>.csproj</c> (for the status line), or null when none.</summary>
        string? Path { get; }

        /// <summary>Read the current <c>.csproj</c> text, or null when no consumer <c>.csproj</c> could be located / read.</summary>
        string? Read();

        /// <summary>Write <paramref name="csprojText"/> back to the consumer <c>.csproj</c>. Returns true on success.</summary>
        bool Write(string csprojText);
    }
}
