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
using System.IO;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the <c>GODOT_MCP_SERVER_PATH</c> override resolver
    /// (<see cref="GodotMcpServerPathOverride"/>) — the dev/CI escape hatch that makes the editor launch a
    /// caller-supplied <c>gamedev-mcp-server</c> instead of the pinned release, skipping the download and the
    /// cached-version match.
    ///
    /// <para>
    /// This class is the ONLY automated gate for those decisions: the manager methods that consume them
    /// (<c>GodotMcpServerManager.ExecutableFullPath</c> / <c>IsVersionMatches</c> /
    /// <c>KillOrphanedServerProcesses</c>) are <c>#if TOOLS</c> and reach
    /// <c>ProjectSettings.GlobalizePath</c>, which faults in this binary-less xUnit host — so the decisions
    /// were factored into this pure resolver precisely so they could be pinned here.
    /// </para>
    ///
    /// <para>
    /// Every existence check goes through <see cref="RecordingFileExists"/>, which also RECORDS the exact
    /// string it was asked about. Several tests assert that recorded argument rather than only the
    /// <c>null</c>/non-<c>null</c> result: an absence assertion alone cannot tell "the gate rejected the
    /// value" from "the value never arrived", and the recorded probe is the positive artifact that does.
    /// </para>
    /// </summary>
    public class GodotMcpServerPathOverrideTests
    {
        /// <summary>A <c>fileExists</c> double that answers from a fixed set AND records every probe.</summary>
        sealed class RecordingFileExists
        {
            readonly HashSet<string> _existing;

            public RecordingFileExists(params string[] existing)
                => _existing = new HashSet<string>(existing, StringComparer.Ordinal);

            public List<string> Probes { get; } = new();

            public bool Exists(string path)
            {
                Probes.Add(path);
                return _existing.Contains(path);
            }

            public Func<string, bool> Delegate => Exists;
        }

        static string ExePath(params string[] segments)
        {
            var parts = new List<string> { Path.GetTempPath(), "godot-mcp-server-path-override-tests" };
            parts.AddRange(segments);
            return Path.Combine(parts.ToArray());
        }

        // --- (a) an existing file resolves to exactly that path ------------------------------------------

        [Fact]
        public void Resolve_ExistingFile_ReturnsThatPath()
        {
            var exe = ExePath("chain", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(exe);

            Assert.Equal(exe, GodotMcpServerPathOverride.Resolve(exe, files.Delegate));
            Assert.Equal(new[] { exe }, files.Probes);
        }

        // --- (b) set, but naming no existing file, falls THROUGH (Unreal's ResolveBinaryPath rule) --------

        [Fact]
        public void Resolve_SetButMissingFile_ReturnsNull()
        {
            var missing = ExePath("chain", "not-published-yet", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(/* nothing exists */);

            Assert.Null(GodotMcpServerPathOverride.Resolve(missing, files.Delegate));

            // Positive artifact: the value DID reach the existence gate with its full path, so the null above
            // is the gate refusing a missing file — not the value being dropped before the gate.
            Assert.Equal(new[] { missing }, files.Probes);
        }

        // --- (c) unset / blank values resolve to null and never touch the filesystem ----------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\t\r\n")]
        public void Resolve_NullEmptyOrWhitespace_ReturnsNullWithoutProbing(string? raw)
        {
            var files = new RecordingFileExists(ExePath("chain", "gamedev-mcp-server.exe"));

            Assert.Null(GodotMcpServerPathOverride.Resolve(raw, files.Delegate));
            Assert.Empty(files.Probes);
        }

        [Fact]
        public void Normalize_NullEmptyOrWhitespace_ReturnsNull()
        {
            Assert.Null(GodotMcpServerPathOverride.Normalize(null));
            Assert.Null(GodotMcpServerPathOverride.Normalize(""));
            Assert.Null(GodotMcpServerPathOverride.Normalize("   "));
        }

        // --- (d) surrounding quotes + whitespace are trimmed, the .env layer's convention -----------------

        [Fact]
        public void Resolve_DoubleQuotedValue_TrimsQuotesBeforeTheExistenceGate()
        {
            var exe = ExePath("chain", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(exe);

            Assert.Equal(exe, GodotMcpServerPathOverride.Resolve("\"" + exe + "\"", files.Delegate));

            // The gate must be asked about the UNQUOTED path; asking about the quoted one would silently
            // ignore a perfectly good override exported as GODOT_MCP_SERVER_PATH="C:/.../server.exe".
            Assert.Equal(new[] { exe }, files.Probes);
        }

        [Fact]
        public void Resolve_SingleQuotedValue_TrimsQuotesBeforeTheExistenceGate()
        {
            var exe = ExePath("chain", "gamedev-mcp-server");
            var files = new RecordingFileExists(exe);

            Assert.Equal(exe, GodotMcpServerPathOverride.Resolve("'" + exe + "'", files.Delegate));
            Assert.Equal(new[] { exe }, files.Probes);
        }

        [Fact]
        public void Resolve_SurroundingWhitespace_IsTrimmed()
        {
            var exe = ExePath("chain", "gamedev-mcp-server");
            var files = new RecordingFileExists(exe);

            Assert.Equal(exe, GodotMcpServerPathOverride.Resolve("  " + exe + "\t", files.Delegate));
            Assert.Equal(new[] { exe }, files.Probes);
        }

        [Fact]
        public void Normalize_TrimsWhitespaceThenOnePairOfQuotes()
        {
            Assert.Equal("/srv/gamedev-mcp-server", GodotMcpServerPathOverride.Normalize("  \"/srv/gamedev-mcp-server\"  "));
            Assert.Equal("/srv/gamedev-mcp-server", GodotMcpServerPathOverride.Normalize(" '/srv/gamedev-mcp-server' "));
            Assert.Equal("/srv/gamedev-mcp-server", GodotMcpServerPathOverride.Normalize("/srv/gamedev-mcp-server"));
        }

        // --- (e) precedence: the PROCESS value beats the project .env value -------------------------------

        [Fact]
        public void Resolve_BothLayersSet_ProcessValueWins()
        {
            var fromProcess = ExePath("process", "gamedev-mcp-server.exe");
            var fromEnvFile = ExePath("dotenv", "gamedev-mcp-server.exe");
            // BOTH exist, so the existence gate cannot be what picks the winner — only precedence can.
            var files = new RecordingFileExists(fromProcess, fromEnvFile);

            Assert.Equal(fromProcess, GodotMcpServerPathOverride.Resolve(fromProcess, fromEnvFile, files.Delegate));
            Assert.Equal(new[] { fromProcess }, files.Probes);
        }

        [Fact]
        public void Resolve_ProcessValueBlank_FallsBackToEnvFileValue()
        {
            var fromEnvFile = ExePath("dotenv", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(fromEnvFile);

            Assert.Equal(fromEnvFile, GodotMcpServerPathOverride.Resolve("   ", fromEnvFile, files.Delegate));
            Assert.Equal(new[] { fromEnvFile }, files.Probes);
        }

        [Fact]
        public void Resolve_NeitherLayerSet_ReturnsNull()
        {
            var files = new RecordingFileExists(ExePath("dotenv", "gamedev-mcp-server.exe"));

            Assert.Null(GodotMcpServerPathOverride.Resolve(null, "", files.Delegate));
            Assert.Empty(files.Probes);
        }

        [Fact]
        public void SelectRaw_AppliesPrecedenceAndNormalization()
        {
            Assert.Equal("/a/server", GodotMcpServerPathOverride.SelectRaw("\"/a/server\"", "/b/server"));
            Assert.Equal("/b/server", GodotMcpServerPathOverride.SelectRaw(" ", " '/b/server' "));
            Assert.Null(GodotMcpServerPathOverride.SelectRaw(null, "   "));
        }

        // --- the single "override in force" predicate every consumer reads --------------------------------

        [Fact]
        public void IsActive_OnlyForANonEmptyResolvedOverride()
        {
            Assert.True(GodotMcpServerPathOverride.IsActive("/srv/gamedev-mcp-server"));
            Assert.False(GodotMcpServerPathOverride.IsActive(null));
            Assert.False(GodotMcpServerPathOverride.IsActive(""));
        }

        // --- what the manager LAUNCHES (GodotMcpServerManager.ExecutableFullPath) --------------------------

        [Fact]
        public void ExecutablePath_OverrideActive_LaunchesTheOverride()
        {
            var overridePath = ExePath("chain", "gamedev-mcp-server.exe");
            var cached = ExePath("cache", "win-x64", "gamedev-mcp-server.exe");

            Assert.Equal(overridePath, GodotMcpServerPathOverride.ExecutablePath(overridePath, cached));
        }

        [Fact]
        public void ExecutablePath_NoOverride_LaunchesTheCachedBinary()
        {
            var cached = ExePath("cache", "win-x64", "gamedev-mcp-server.exe");

            Assert.Equal(cached, GodotMcpServerPathOverride.ExecutablePath(null, cached));
            Assert.Equal(cached, GodotMcpServerPathOverride.ExecutablePath("", cached));
        }

        // --- the child process's working directory ---------------------------------------------------------

        [Fact]
        public void WorkingDirectory_IsTheDirectoryOfTheResolvedExecutable()
        {
            var overrideDir = ExePath("chain", "win-x64");
            var overrideExe = Path.Combine(overrideDir, "gamedev-mcp-server.exe");
            var cacheDir = ExePath("cache", "win-x64");

            // Not the cache folder: an override binary must run beside ITS OWN sidecar files.
            Assert.Equal(overrideDir, GodotMcpServerPathOverride.WorkingDirectory(overrideExe, cacheDir));
        }

        [Fact]
        public void WorkingDirectory_NoOverride_IsStillTheCacheFolder()
        {
            var cacheDir = ExePath("cache", "win-x64");
            var cachedExe = Path.Combine(cacheDir, "gamedev-mcp-server.exe");

            // The historical value is preserved because the cache folder IS the cached executable's directory.
            Assert.Equal(cacheDir, GodotMcpServerPathOverride.WorkingDirectory(cachedExe, cacheDir));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("gamedev-mcp-server")]
        public void WorkingDirectory_PathWithoutADirectoryComponent_FallsBack(string? executablePath)
        {
            var cacheDir = ExePath("cache", "win-x64");

            Assert.Equal(cacheDir, GodotMcpServerPathOverride.WorkingDirectory(executablePath, cacheDir));
        }

        // --- misuse ----------------------------------------------------------------------------------------

        [Fact]
        public void Resolve_NullFileExistsDelegate_Throws()
        {
            Assert.Throws<ArgumentNullException>(
                () => GodotMcpServerPathOverride.Resolve("/srv/gamedev-mcp-server", null!));
        }
    }
}
