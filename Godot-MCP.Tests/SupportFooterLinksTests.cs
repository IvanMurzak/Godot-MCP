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
using com.IvanMurzak.Godot.MCP.UI;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed support-footer link constants (<see cref="SupportFooterLinks"/>) — the static
    /// URLs the dock footer opens via <c>OS.ShellOpen</c>. The editor Control wiring (<c>SupportFooter.cs</c>,
    /// <c>#if TOOLS</c>) instantiates live Godot nodes and is verified via the headless Godot smoke
    /// (test.md Suite 3) — NOT here. Guards against a stale/typo'd URL (e.g. pointing at the wrong repo)
    /// silently shipping.
    /// </summary>
    public class SupportFooterLinksTests
    {
        [Fact]
        public void IssuesUrl_targets_the_godot_mcp_repo_issues_page()
        {
            Assert.Equal("https://github.com/IvanMurzak/Godot-MCP/issues", SupportFooterLinks.IssuesUrl);
        }

        [Fact]
        public void RepositoryUrl_targets_the_godot_mcp_repo()
        {
            Assert.Equal("https://github.com/IvanMurzak/Godot-MCP", SupportFooterLinks.RepositoryUrl);
        }

        [Fact]
        public void DiscordUrl_is_the_shared_invite()
        {
            Assert.Equal("https://discord.gg/cfbdMZX99G", SupportFooterLinks.DiscordUrl);
        }

        [Theory]
        [InlineData("Discord")]
        [InlineData("Issues")]
        [InlineData("Repository")]
        public void All_urls_are_absolute_https(string which)
        {
            var url = which switch
            {
                "Discord" => SupportFooterLinks.DiscordUrl,
                "Issues" => SupportFooterLinks.IssuesUrl,
                _ => SupportFooterLinks.RepositoryUrl
            };

            Assert.True(Uri.TryCreate(url, UriKind.Absolute, out var parsed), $"{which} URL must be absolute: {url}");
            Assert.Equal(Uri.UriSchemeHttps, parsed!.Scheme);
        }

        [Fact]
        public void Copy_strings_are_non_empty()
        {
            Assert.False(string.IsNullOrWhiteSpace(SupportFooterLinks.PromptText));
            Assert.False(string.IsNullOrWhiteSpace(SupportFooterLinks.ThanksText));
        }
    }
}
