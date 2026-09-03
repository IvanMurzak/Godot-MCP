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

        // --- an existing file resolves to exactly that path ------------------------------------------

        [Fact]
        public void Resolve_ExistingFile_ReturnsThatPath()
        {
            var exe = ExePath("chain", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(exe);

            Assert.Equal(exe, GodotMcpServerPathOverride.Resolve(exe, files.Delegate));
            Assert.Equal(new[] { exe }, files.Probes);
        }

        // --- set, but naming no existing file, falls THROUGH (Unreal's ResolveBinaryPath rule) --------

        [Fact]
        public void Resolve_SetButMissingFile_ReturnsNull()
        {
            var missing = ExePath("chain", "not-published-yet", "gamedev-mcp-server.exe");
            var files = new RecordingFileExists(/* nothing exists */);

            var resolved = GodotMcpServerPathOverride.Resolve(missing, files.Delegate);

            // The probe assertion comes FIRST deliberately. Positive artifact: the value DID reach the
            // existence gate with its full path, so the null below is the gate refusing a missing file — not
            // the value being dropped before the gate. Ordering it first also keeps the two gate mutations
            // tellable apart: REMOVING the gate fails here (no probe was ever made), whereas INVERTING it
            // probes normally and fails on the null instead.
            Assert.Equal(new[] { missing }, files.Probes);
            Assert.Null(resolved);
        }

        // --- unset / blank values resolve to null and never touch the filesystem ----------------------

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

        // --- surrounding quotes + whitespace are trimmed, the .env layer's convention -----------------

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

            // Documents the contract, but do NOT read it as coverage of Normalize's own leading Trim():
            // GodotMcpConfig.NormalizeEnv trims too, so this case stays green with the local trim removed.
            // The whitespace case that is actually load-bearing is the QUOTED-and-spaced one in
            // Normalize_TrimsWhitespaceThenOnePairOfQuotes — without the local trim the leading space means
            // the value no longer starts with a quote and the single-quote branch is skipped entirely.
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

        [Fact]
        public void Normalize_StripsSingleQuotesBEFORETheSharedDoubleQuoteNormalizer()
        {
            // The ONLY inputs that can tell the two orders apart are NESTED pairs — every single-convention
            // value normalizes identically either way, which is why the ordering claim in Normalize's
            // docstring needs a case of its own rather than riding on the cases above.
            //
            // Current order (single strip, THEN GodotMcpConfig.NormalizeEnv's Trim('"')): the outer double
            // quotes come off and the inner single quotes survive. The swapped order would strip both and
            // return "/srv/x". This matches GodotMcpEnvFile.Sanitize, which is the compatibility contract:
            // a value must normalize the same whether it arrived from the process env or from res://.env.
            Assert.Equal("'/srv/x'", GodotMcpServerPathOverride.Normalize("\"'/srv/x'\""));
        }

        // --- precedence: the PROCESS value beats the project .env value -------------------------------

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

        // --- the two under-override decisions the manager itself cannot pin --------------------------------
        //
        // GodotMcpServerManager is #if TOOLS and reaches ProjectSettings.GlobalizePath, so it does not compile
        // into this host and its call sites can never be asserted here. These are the decisions themselves,
        // factored out so the POLARITY of each is pinned rather than only the predicate they read.

        [Fact]
        public void VersionMatchesOrOverridden_TrueUnderOverrideWithoutConsultingTheCachedVersion()
        {
            var consulted = false;
            Func<bool> cached = () => { consulted = true; return false; };

            Assert.True(GodotMcpServerPathOverride.VersionMatchesOrOverridden("/srv/gamedev-mcp-server", cached));

            // Positive artifact for the short-circuit: reading the cache's `version` marker is file I/O and
            // that folder routinely does not exist under an override, so it must not be reached at all.
            Assert.False(consulted);
        }

        [Fact]
        public void VersionMatchesOrOverridden_NoOverride_DefersToTheCachedVersionVerdict()
        {
            Assert.True(GodotMcpServerPathOverride.VersionMatchesOrOverridden(null, () => true));
            Assert.False(GodotMcpServerPathOverride.VersionMatchesOrOverridden(null, () => false));
            Assert.False(GodotMcpServerPathOverride.VersionMatchesOrOverridden("", () => false));
        }

        [Fact]
        public void ShouldKillOrphans_OnlyWithoutAnOverride()
        {
            Assert.True(GodotMcpServerPathOverride.ShouldKillOrphans(null));
            Assert.True(GodotMcpServerPathOverride.ShouldKillOrphans(""));

            // The skip is a correctness requirement, not a convenience: ownership matches on the containing
            // directory, and an override binary is shared by design.
            Assert.False(GodotMcpServerPathOverride.ShouldKillOrphans("/srv/gamedev-mcp-server"));
        }

        // --- "supplied, but ignored" — the state that is otherwise invisible -------------------------------

        [Fact]
        public void IsIgnoredValue_TrueOnlyWhenAValueWasSuppliedAndDidNotResolve()
        {
            // Supplied, but the existence gate refused it: the addon silently downloads the pinned release,
            // so this is the state the boot site must warn about.
            Assert.True(GodotMcpServerPathOverride.IsIgnoredValue("/srv/not-built-yet", null));

            // Nothing supplied — indistinguishable in the RESOLVED value, which is exactly why the raw value
            // is carried separately; it must NOT produce a warning.
            Assert.False(GodotMcpServerPathOverride.IsIgnoredValue(null, null));
            Assert.False(GodotMcpServerPathOverride.IsIgnoredValue("", null));

            // Supplied and resolved: the override is in force, nothing to warn about.
            Assert.False(GodotMcpServerPathOverride.IsIgnoredValue("/srv/built", "/srv/built"));
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

            // Asserted first so that "the predicate is stuck on" and "the launch path ignores the predicate"
            // fail on DIFFERENT lines with different text, rather than both landing on the Equal below.
            Assert.False(GodotMcpServerPathOverride.IsActive(null));

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

            // In PRODUCTION the manager passes CachePlatformPath() as the fallback, so with no override both
            // arms of this method return the same string and the assertion could not fail. Passing a fallback
            // the answer must NOT be is what gives the test discriminating power: the returned cacheDir can
            // then only have come from the executable path, so a mutation that always returns the fallback
            // reddens here. The claim is unchanged — with no override the answer is the cache platform folder.
            var fallbackThatMustNotBeUsed = ExePath("fallback-never-used");

            Assert.Equal(cacheDir, GodotMcpServerPathOverride.WorkingDirectory(cachedExe, fallbackThatMustNotBeUsed));
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
