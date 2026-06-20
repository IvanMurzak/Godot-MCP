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
using System.Runtime.InteropServices;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed, fail-closed download-integrity logic (<see cref="GodotMcpServerView"/>'s
    /// SHA256SUMS URL builder, coreutils-format parser, digest lookup/compare, and the single
    /// <see cref="GodotMcpServerView.VerifyZipChecksum"/> verdict) that the editor manager
    /// (<c>GodotMcpServerManager.DownloadAndUnpackBinary</c>, <c>#if TOOLS</c>) calls BEFORE
    /// <c>ZipFile.ExtractToDirectory</c> / <c>Process.Start</c> — so a downloaded server zip is NEVER extracted
    /// or launched unless its SHA256 matches the release's published <c>SHA256SUMS</c> manifest (issue #192).
    /// Every assertion is a deterministic string/enum transform with NO Godot binary and NO real download, so
    /// the same assertions hold on the Linux CI runner and on a Windows dev box. The HTTP fetch + file IO that
    /// surround this verdict live in the editor-only manager and are verified via the headless Godot smoke.
    ///
    /// <para>
    /// The <see cref="LiveV8Sha256Sums"/> fixture is the VERBATIM content of the real
    /// <c>v8.0.0</c> release manifest (the version this addon pins) at
    /// https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v8.0.0/SHA256SUMS — so the parser is
    /// proven against the exact two-space coreutils format that production will see, and the win-x64 digest is
    /// the real one. Updating the pinned <c>ServerVersion</c> in future does not invalidate these tests: they
    /// assert the FORMAT contract, not a moving digest.
    /// </para>
    /// </summary>
    public class GodotMcpServerChecksumTests
    {
        /// <summary>
        /// The verbatim live <c>SHA256SUMS</c> from the GameDev-MCP-Server <c>v8.0.0</c> release — standard
        /// coreutils output: <c>&lt;64-lowercase-hex&gt;␠␠&lt;filename&gt;</c>, one line per RID zip, LF-terminated.
        /// All 7 published RIDs are present.
        /// </summary>
        const string LiveV8Sha256Sums =
            "5f17508e92812fbf9522eb552641d21dc2383fc2f6cf371f5413ad06c9820282  gamedev-mcp-server-linux-arm64.zip\n" +
            "844d4ad8cd152df44287341235ca2ae67cdb69b496252678eb6491f0bdc53319  gamedev-mcp-server-linux-x64.zip\n" +
            "ad0f50042dfa1edde26a9f26968538146ba792cc0188a47f6bfc1ae573bb513e  gamedev-mcp-server-osx-arm64.zip\n" +
            "d25993216e610401c8925716d9ad0f8ecaf3dc93443b12cfd057a75495ef9952  gamedev-mcp-server-osx-x64.zip\n" +
            "702f1d708c25dde6a58d3335c7adb92aa5fe36be618003821ceb040a9b59c51b  gamedev-mcp-server-win-arm64.zip\n" +
            "7383638dbc1cad84cf3b85617405c29c7885a51e34b1ef7b8b8864d0656814cb  gamedev-mcp-server-win-x64.zip\n" +
            "b171e1d8318d0ce4e88d30a5e86ad1cac1acea946ef1a71cd410a27f917c9799  gamedev-mcp-server-win-x86.zip\n";

        /// <summary>The real win-x64 digest from the live v8.0.0 manifest (verbatim).</summary>
        const string LiveWinX64Digest = "7383638dbc1cad84cf3b85617405c29c7885a51e34b1ef7b8b8864d0656814cb";

        // --- SHA256SUMS URL builder (sibling of the zip URL, same v-tag) ---

        [Fact]
        public void Sha256SumsUrl_IsSiblingOfZipUrlUnderSameVTag()
        {
            // The manifest MUST live under the same v<version> release tag as the zip, with the fixed
            // `SHA256SUMS` asset name — so the integrity manifest can never drift from the binary it covers.
            var sumsUrl = GodotMcpServerView.Sha256SumsUrl("8.0.0");
            Assert.Equal(
                "https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v8.0.0/SHA256SUMS",
                sumsUrl);

            // It shares the release-download directory prefix with the per-RID zip URL.
            var zipUrl = GodotMcpServerView.DownloadUrl("8.0.0", OSPlatform.Windows, Architecture.X64);
            var zipDir = zipUrl.Substring(0, zipUrl.LastIndexOf('/') + 1);
            Assert.StartsWith(zipDir, sumsUrl);
        }

        [Fact]
        public void Sha256SumsUrl_DefaultOverload_PinsServerVersion()
        {
            var sumsUrl = GodotMcpServerView.Sha256SumsUrl();
            Assert.Equal(GodotMcpServerView.Sha256SumsUrl(GodotMcpServerView.ServerVersion), sumsUrl);
            Assert.Contains($"/releases/download/v{GodotMcpServerView.ServerVersion}/SHA256SUMS", sumsUrl);
        }

        [Fact]
        public void Sha256SumsUrl_PrePrefixedVersion_NotDoublePrefixed()
        {
            Assert.Equal(
                "https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v8.0.0/SHA256SUMS",
                GodotMcpServerView.Sha256SumsUrl("v8.0.0"));
        }

        // --- Parser: the exact two-space coreutils line format ---

        [Fact]
        public void ParseSha256Sums_LiveManifest_ParsesAllSevenEntriesWithLowercaseDigests()
        {
            var map = GodotMcpServerView.ParseSha256Sums(LiveV8Sha256Sums);

            Assert.Equal(7, map.Count);
            // The RIGHT RID is selected among the 7 — win-x64 maps to ITS digest, not a neighbour's.
            Assert.Equal(LiveWinX64Digest, map["gamedev-mcp-server-win-x64.zip"]);
            Assert.Equal("844d4ad8cd152df44287341235ca2ae67cdb69b496252678eb6491f0bdc53319",
                map["gamedev-mcp-server-linux-x64.zip"]);
            Assert.Equal("b171e1d8318d0ce4e88d30a5e86ad1cac1acea946ef1a71cd410a27f917c9799",
                map["gamedev-mcp-server-win-x86.zip"]);
            // Every value is a 64-char lowercase hex digest.
            foreach (var digest in map.Values)
            {
                Assert.Equal(64, digest.Length);
                Assert.Equal(digest.ToLowerInvariant(), digest);
            }
        }

        [Fact]
        public void ParseSha256Sums_TolerantOfCrlfAndBlankLinesAndUppercaseHex()
        {
            const string text =
                "\r\n" +
                "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789  gamedev-mcp-server-win-x64.zip\r\n" +
                "\r\n";
            var map = GodotMcpServerView.ParseSha256Sums(text);
            Assert.Single(map);
            // Digest normalized to lowercase.
            Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789",
                map["gamedev-mcp-server-win-x64.zip"]);
        }

        [Fact]
        public void ParseSha256Sums_StripsBinaryModeStarMarker()
        {
            // coreutils binary mode emits `<hex> *<name>`; the '*' is not part of the filename.
            const string text =
                "7383638dbc1cad84cf3b85617405c29c7885a51e34b1ef7b8b8864d0656814cb *gamedev-mcp-server-win-x64.zip\n";
            var map = GodotMcpServerView.ParseSha256Sums(text);
            Assert.True(map.ContainsKey("gamedev-mcp-server-win-x64.zip"));
            Assert.Equal(LiveWinX64Digest, map["gamedev-mcp-server-win-x64.zip"]);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   \n  \n")]                                  // only whitespace
        [InlineData("not-a-digest  gamedev-mcp-server-win-x64.zip\n")] // first token isn't 64-hex
        [InlineData("7383638dbc1cad84cf3b85617405c29c7885a51e34b1ef7b8b8864d065681\n")] // hex too short, no filename
        [InlineData("zzz3638dbc1cad84cf3b85617405c29c7885a51e34b1ef7b8b8864d0656814cb  x.zip\n")] // non-hex chars
        public void ParseSha256Sums_MalformedOrEmpty_YieldsNoUsableEntry(string? text)
        {
            var map = GodotMcpServerView.ParseSha256Sums(text);
            Assert.Empty(map);
        }

        // --- Lookup + compare ---

        [Fact]
        public void LookupDigest_ReturnsEntryForRid_NullWhenMissing()
        {
            var map = GodotMcpServerView.ParseSha256Sums(LiveV8Sha256Sums);
            Assert.Equal(LiveWinX64Digest, GodotMcpServerView.LookupDigest(map, "gamedev-mcp-server-win-x64.zip"));
            Assert.Null(GodotMcpServerView.LookupDigest(map, "gamedev-mcp-server-freebsd-x64.zip"));
        }

        [Fact]
        public void DigestMatches_IsCaseInsensitive_AndFailsClosedOnNullOrEmpty()
        {
            Assert.True(GodotMcpServerView.DigestMatches(LiveWinX64Digest, LiveWinX64Digest.ToUpperInvariant()));
            Assert.True(GodotMcpServerView.DigestMatches("  " + LiveWinX64Digest + "  ", LiveWinX64Digest));
            Assert.False(GodotMcpServerView.DigestMatches(LiveWinX64Digest, null));
            Assert.False(GodotMcpServerView.DigestMatches(null, LiveWinX64Digest));
            Assert.False(GodotMcpServerView.DigestMatches("", ""));
            Assert.False(GodotMcpServerView.DigestMatches(LiveWinX64Digest, "deadbeef"));
        }

        // --- The single fail-closed verdict (what the editor manager calls) ---

        [Fact]
        public void VerifyZipChecksum_RealPair_IsVerified()
        {
            // The real win-x64 digest against the real live manifest → SAFE to extract/execute. This is the
            // exact pair production sees (operator-confirmable: `gh release download v8.0.0 --pattern
            // gamedev-mcp-server-win-x64.zip` then `sha256sum` equals LiveWinX64Digest).
            var verdict = GodotMcpServerView.VerifyZipChecksum(
                LiveV8Sha256Sums, "gamedev-mcp-server-win-x64.zip", LiveWinX64Digest);
            Assert.Equal(GodotMcpServerView.ChecksumVerdict.Verified, verdict);
        }

        [Fact]
        public void VerifyZipChecksum_RealPair_VerifiedWithUppercaseComputedDigest()
        {
            // BCL Convert.ToHexString emits UPPER-case; the verdict must accept it (case-insensitive compare).
            var verdict = GodotMcpServerView.VerifyZipChecksum(
                LiveV8Sha256Sums, "gamedev-mcp-server-win-x64.zip", LiveWinX64Digest.ToUpperInvariant());
            Assert.Equal(GodotMcpServerView.ChecksumVerdict.Verified, verdict);
        }

        [Fact]
        public void VerifyZipChecksum_TamperedDigest_IsRejected()
        {
            // A tampered/compromised zip whose SHA256 differs by a single nibble must be REJECTED (fail-closed).
            var tampered = "0" + LiveWinX64Digest.Substring(1);
            var verdict = GodotMcpServerView.VerifyZipChecksum(
                LiveV8Sha256Sums, "gamedev-mcp-server-win-x64.zip", tampered);
            Assert.Equal(GodotMcpServerView.ChecksumVerdict.DigestMismatch, verdict);
        }

        [Fact]
        public void VerifyZipChecksum_MissingEntryForRid_IsRejected()
        {
            // The manifest parsed fine but has no line for THIS RID's asset → fail-closed (MissingEntry).
            var verdict = GodotMcpServerView.VerifyZipChecksum(
                LiveV8Sha256Sums, "gamedev-mcp-server-freebsd-x64.zip", LiveWinX64Digest);
            Assert.Equal(GodotMcpServerView.ChecksumVerdict.MissingEntry, verdict);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage with no valid sha lines\n")]
        public void VerifyZipChecksum_UnparsableManifest_IsRejected(string? manifest)
        {
            // An empty/unparsable manifest must NEVER pass (do not execute an unverified binary).
            var verdict = GodotMcpServerView.VerifyZipChecksum(
                manifest, "gamedev-mcp-server-win-x64.zip", LiveWinX64Digest);
            Assert.Equal(GodotMcpServerView.ChecksumVerdict.ManifestUnparsable, verdict);
        }

        [Fact]
        public void ChecksumFailureReason_NamesTheActionableCause()
        {
            // The editor fail-closed log line must be actionable: it names the manifest, the asset, and the cause.
            var asset = "gamedev-mcp-server-win-x64.zip";
            Assert.Contains(asset,
                GodotMcpServerView.ChecksumFailureReason(GodotMcpServerView.ChecksumVerdict.DigestMismatch, asset));
            Assert.Contains(asset,
                GodotMcpServerView.ChecksumFailureReason(GodotMcpServerView.ChecksumVerdict.MissingEntry, asset));
            Assert.Contains("SHA256SUMS",
                GodotMcpServerView.ChecksumFailureReason(GodotMcpServerView.ChecksumVerdict.ManifestUnparsable, asset));
        }
    }
}
