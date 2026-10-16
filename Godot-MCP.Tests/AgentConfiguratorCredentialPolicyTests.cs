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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.McpPlugin.AgentConfig;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed credential-surfacing policy of the AI-agent configurators (mcp-authorize e1 · PR 5,
    /// design <c>06-engine-plugins.md</c>): the <see cref="AgentConfiguratorCredentialPolicy"/> decisions AND —
    /// end-to-end through the real shared <c>com.IvanMurzak.McpPlugin.AgentConfig</c> 7.0 configurators — that the
    /// DEFAULT (OAuth) path writes a URL-only config with no token in the file OR the rendered manual-config
    /// command, while the "Advanced: use access token" escape hatch writes the legacy Bearer-header shape.
    ///
    /// <para>
    /// The <c>#if TOOLS</c> panel/factory adapters (<c>AgentConfiguratorSettingsFactory</c> reads Godot
    /// <c>ProjectSettings</c>; <c>AgentConfiguratorsPanel</c> / <c>ConnectionPanel</c> build live Godot Controls)
    /// are verified via the headless Godot smoke (test.md Suite 3) — here we replicate exactly the settings the
    /// factory builds (token / authOption via the policy) and drive the shared writer, which is pure-managed.
    /// </para>
    /// </summary>
    public class AgentConfiguratorCredentialPolicyTests
    {
        // A representative Godot-visible configurator with a writable HTTP config file (claude-code). The pin
        // hash is derived from this string; no filesystem access happens, so any stable value works cross-OS.
        const string ProjectRoot = "/home/dev/projects/MyGame";
        const string SecretToken = "SECRET_TOKEN_9f8e7d6c";

        static AgentConfiguratorSettings BuildSettings(
            HttpCredentialMode mode, string? rawConfigToken, ConnectionMode connMode = ConnectionMode.Cloud) =>
            AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRoot,
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: connMode == ConnectionMode.Cloud ? "https://ai-game.dev/mcp" : "http://localhost:26610",
                token: AgentConfiguratorCredentialPolicy.ResolveSettingsToken(mode, rawConfigToken),
                connectionMode: connMode,
                authOption: AgentConfiguratorCredentialPolicy.ResolveSettingsAuthOption(mode),
                serverExecutableName: "gamedev-mcp-server",
                serverVersion: "9.0.0",
                dockerImage: "aigamedeveloper/mcp-server");

        static AiAgentConfigurator Claude()
        {
            var c = GodotAgentConfigurators.GetByAgentId("claude-code");
            Assert.NotNull(c);
            return c!;
        }

        // --- ResolveCredentialMode: default OAuth; user opt-in or a non-OAuth configurator forces AccessToken ---

        [Theory]
        [InlineData(true, false, HttpCredentialMode.Oauth)]        // OAuth-capable, default -> URL-only
        [InlineData(true, true, HttpCredentialMode.AccessToken)]   // OAuth-capable + "Advanced" opt-in -> token
        [InlineData(false, false, HttpCredentialMode.AccessToken)] // non-OAuth -> forced token (no default path)
        [InlineData(false, true, HttpCredentialMode.AccessToken)]  // non-OAuth stays token regardless
        public void ResolveCredentialMode_DefaultsToOAuth_ForcesTokenWhenNeeded(
            bool supportsOAuth, bool useAccessToken, HttpCredentialMode expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ResolveCredentialMode(supportsOAuth, useAccessToken));
        }

        // --- ResolveCredentialMode (launch-aware overload): a Custom+token local server forces AccessToken ---

        [Theory]
        // Custom + token (Bearer-gated local server) FORCES AccessToken regardless of the OAuth toggle.
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.token, true, false, HttpCredentialMode.AccessToken)]
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.token, true, true, HttpCredentialMode.AccessToken)]
        // Custom + none / oauth fall through to the base policy (URL-only OAuth unless the toggle/non-OAuth forces it).
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.none, true, false, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.oauth, true, false, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.oauth, true, true, HttpCredentialMode.AccessToken)]
        // Cloud mode is never forced by the local auth option — the base policy governs.
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.token, true, false, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.token, false, false, HttpCredentialMode.AccessToken)]
        public void ResolveCredentialMode_LaunchAware_ForcesAccessTokenOnlyForCustomToken(
            GodotMcpConnectionMode activeMode, AuthOption activeAuthOption, bool supportsOAuth, bool useAccessToken,
            HttpCredentialMode expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                activeMode, activeAuthOption, supportsOAuth, useAccessToken));
        }

        [Theory]
        [InlineData(true, true)]    // OAuth-capable -> the toggle is offered
        [InlineData(false, false)]  // non-OAuth -> no toggle (the token is mandatory)
        public void ShowAdvancedToggle_OnlyForOAuthCapable(bool supportsOAuth, bool expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ShowAdvancedToggle(supportsOAuth));
        }

        [Theory]
        [InlineData(true, false, false)]  // default OAuth path hides the token field
        [InlineData(true, true, true)]    // "Advanced" opt-in reveals it
        [InlineData(false, false, true)]  // a non-OAuth configurator always shows it
        public void ShowAccessTokenField_TracksResolvedMode(bool supportsOAuth, bool useAccessToken, bool expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ShowAccessTokenField(supportsOAuth, useAccessToken));
        }

        [Fact]
        public void ResolveSettingsToken_OAuthDropsToken_AccessTokenCarriesIt()
        {
            // OAuth default: never carry the token, even when the connection has one stored.
            Assert.Equal(string.Empty, AgentConfiguratorCredentialPolicy.ResolveSettingsToken(HttpCredentialMode.Oauth, SecretToken));
            // AccessToken: carry the live token (empty when none is stored).
            Assert.Equal(SecretToken, AgentConfiguratorCredentialPolicy.ResolveSettingsToken(HttpCredentialMode.AccessToken, SecretToken));
            Assert.Equal(string.Empty, AgentConfiguratorCredentialPolicy.ResolveSettingsToken(HttpCredentialMode.AccessToken, null));
        }

        [Fact]
        public void ResolveSettingsAuthOption_OAuthIsNone_AccessTokenIsRequired()
        {
            Assert.Equal(AuthOption.none, AgentConfiguratorCredentialPolicy.ResolveSettingsAuthOption(HttpCredentialMode.Oauth));
            Assert.Equal(AuthOption.required, AgentConfiguratorCredentialPolicy.ResolveSettingsAuthOption(HttpCredentialMode.AccessToken));
        }

        // --- End-to-end through the real shared configurator: DEFAULT path writes URL-only (no token) ---

        [Fact]
        public void DefaultOAuthPath_WritesUrlOnlyConfig_NoTokenNoBearer()
        {
            var settings = BuildSettings(HttpCredentialMode.Oauth, SecretToken);
            var config = Claude().GetHttpConfig(settings, NullLogger.Instance, HttpCredentialMode.Oauth);
            var content = config.ExpectedFileContent ?? string.Empty;

            Assert.DoesNotContain(SecretToken, content);
            Assert.DoesNotContain("Authorization", content);
            Assert.DoesNotContain("Bearer", content);
            // The URL-only config still points at the project-pinned URL (the /p/<pin> path).
            Assert.Contains("/p/", content);
        }

        [Fact]
        public void DefaultOAuthPath_ManualConfigCommand_OmitsTheRealToken()
        {
            // On the default path the settings carry NO token, so no real access token appears anywhere in the
            // rendered DTO — not in the primary config and not in the "Manual Configuration Steps" command. (In
            // Cloud mode the shared 7.0 lib still renders a generic "Bearer <token>" PLACEHOLDER in the manual
            // fallback — a frozen-pin artifact, driven by connectionMode=Cloud, never the real secret.)
            var settings = BuildSettings(HttpCredentialMode.Oauth, SecretToken);
            var description = Claude().Describe(settings, TransportMethod.streamableHttp, NullLogger.Instance);
            var allText = string.Join("\n", description.Sections.SelectMany(s => s.Items).Select(i => i.Text ?? string.Empty));

            Assert.DoesNotContain(SecretToken, allText);
        }

        [Fact]
        public void DefaultOAuthPath_LocalMode_ManualConfigCommand_IsFullyUrlOnly()
        {
            // In Local (self-hosted) mode the OAuth default (authOption=none) drops the Bearer header from the
            // manual-config command entirely — the URL-only golden path, no token surface at all.
            var settings = BuildSettings(HttpCredentialMode.Oauth, SecretToken, ConnectionMode.Local);
            var description = Claude().Describe(settings, TransportMethod.streamableHttp, NullLogger.Instance);
            var allText = string.Join("\n", description.Sections.SelectMany(s => s.Items).Select(i => i.Text ?? string.Empty));

            Assert.DoesNotContain(SecretToken, allText);
            Assert.DoesNotContain("Bearer", allText);
        }

        // --- End-to-end: the "Advanced: use access token" escape hatch writes the legacy Bearer-header shape ---

        [Fact]
        public void AdvancedAccessTokenPath_WritesBearerHeaderConfig()
        {
            var settings = BuildSettings(HttpCredentialMode.AccessToken, SecretToken);
            var config = Claude().GetHttpConfig(settings, NullLogger.Instance, HttpCredentialMode.AccessToken);
            var content = config.ExpectedFileContent ?? string.Empty;

            Assert.Contains(SecretToken, content);
            Assert.Contains("Bearer", content);
        }

        [Fact]
        public void BearerGatedLocalServer_ManualConfigCommand_ShowsBearerToken()
        {
            // The manual-steps text follows what Configure WRITES (McpPlugin 8.5 WritesHttpBearer): a local server
            // in the offline `token` auth mode is Bearer-gated, so its command carries the real local secret.
            var settings = AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRoot,
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: "http://localhost:26610",
                token: SecretToken,
                connectionMode: ConnectionMode.Local,
                authOption: AuthOption.token,
                serverExecutableName: "gamedev-mcp-server",
                serverVersion: "9.0.0",
                dockerImage: "aigamedeveloper/mcp-server");
            var description = Claude().Describe(settings, TransportMethod.streamableHttp, NullLogger.Instance);
            var allText = string.Join("\n", description.Sections.SelectMany(s => s.Items).Select(i => i.Text ?? string.Empty));

            Assert.Contains("Bearer", allText);
            Assert.Contains(SecretToken, allText);
        }

        // --- Cloud project key (project-keys contract §7) ---

        const string ProjectKey = "agd_pk_REALSECRET_4b3a2c1d";

        /// <summary>The snapshot <c>AgentConfiguratorSettingsFactory.Create</c> builds when a project key is in use.</summary>
        static AgentConfiguratorSettings BuildProjectKeySettings(string? cloudToken = SecretToken)
        {
            var mode = AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                GodotMcpConnectionMode.Cloud, AuthOption.none, supportsOAuth: true, useAccessToken: false, hasProjectKey: true);
            return AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRoot,
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: "https://ai-game.dev/mcp",
                token: string.Empty, // the factory never feeds the short-lived Cloud token alongside a project key
                connectionMode: ConnectionMode.Cloud,
                authOption: AgentConfiguratorCredentialPolicy.ResolveSettingsAuthOption(mode),
                serverExecutableName: "gamedev-mcp-server",
                serverVersion: "9.0.0",
                dockerImage: "aigamedeveloper/mcp-server").WithProjectKey(ProjectKey);
        }

        [Theory]
        // Cloud + a project key ⇒ EVERY agent writes the Bearer shape (owner ruling 2026-09-23).
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.none, true, false, true, HttpCredentialMode.AccessToken)]
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.none, false, false, true, HttpCredentialMode.AccessToken)]
        // Cloud without a key (signed out / mint failed) ⇒ URL-only OAuth — the "Advanced" opt-in and a
        // non-OAuth configurator no longer force the short-lived access token into the file.
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.none, true, false, false, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.none, true, true, false, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Cloud, AuthOption.token, false, false, false, HttpCredentialMode.Oauth)]
        // Local-server mode is unchanged — a key is never applied there.
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.none, true, false, true, HttpCredentialMode.Oauth)]
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.oauth, true, true, true, HttpCredentialMode.AccessToken)]
        [InlineData(GodotMcpConnectionMode.Custom, AuthOption.token, true, false, true, HttpCredentialMode.AccessToken)]
        public void ResolveCredentialMode_ProjectKey_CloudFollowsTheKey_LocalUnchanged(
            GodotMcpConnectionMode activeMode, AuthOption activeAuthOption, bool supportsOAuth, bool useAccessToken,
            bool hasProjectKey, HttpCredentialMode expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                activeMode, activeAuthOption, supportsOAuth, useAccessToken, hasProjectKey));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void CloudMode_PanelModeEqualsTheSharedSettingsResolution(bool hasProjectKey)
        {
            // The panel's mode must equal the shared snapshot's own resolution, so Configure's write, the status
            // check and the manual-steps text (driven by WritesHttpBearer) can never disagree.
            var settings = hasProjectKey ? BuildProjectKeySettings() : BuildSettings(HttpCredentialMode.Oauth, SecretToken);
            Assert.Equal(
                settings.ResolveHttpCredentialMode(),
                AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                    GodotMcpConnectionMode.Cloud, AuthOption.none, supportsOAuth: true, useAccessToken: false, hasProjectKey));
        }

        [Theory]
        [InlineData(AuthOption.none)]
        [InlineData(AuthOption.oauth)]
        [InlineData(AuthOption.token)]
        public void LocalMode_PanelModeEqualsTheSharedSettingsResolution(AuthOption authOption)
        {
            // Custom (local server): what Configure writes must match the manual steps McpPlugin renders from
            // the snapshot's own resolution (WritesHttpBearer) — no panel-only override exists any more.
            var settings = AgentConfiguratorSettings.CreateForHost(
                projectRootPath: ProjectRoot,
                executableFullPath: string.Empty,
                port: 8080,
                timeoutMs: 10000,
                host: "http://localhost:26610",
                token: SecretToken,
                connectionMode: ConnectionMode.Local,
                authOption: authOption,
                serverExecutableName: "gamedev-mcp-server",
                serverVersion: "9.0.0",
                dockerImage: "aigamedeveloper/mcp-server");
            Assert.Equal(
                settings.ResolveHttpCredentialMode(),
                AgentConfiguratorCredentialPolicy.ResolveCredentialMode(
                    GodotMcpConnectionMode.Custom, authOption, supportsOAuth: true, useAccessToken: false, hasProjectKey: false));
        }

        [Fact]
        public void ProjectKey_EveryAgentWritesPinnedUrlPlusBearerProjectKey()
        {
            var settings = BuildProjectKeySettings();
            foreach (var agent in GodotAgentConfigurators.All.Where(a => a is not com.IvanMurzak.McpPlugin.AgentConfig.Impl.CustomConfigurator))
            {
                var config = agent.GetHttpConfig(settings, NullLogger.Instance, settings.ResolveHttpCredentialMode());
                var content = config.ExpectedFileContent ?? string.Empty;
                Assert.True(content.Contains("Bearer " + ProjectKey), $"{agent.AgentId}: no project-key header in\n{content}");
                Assert.True(content.Contains("/mcp/p/"), $"{agent.AgentId}: URL is not pinned");
                Assert.DoesNotContain(SecretToken, content);
                Assert.DoesNotContain("GAME_DEV_AUTH_TOKEN", content);
            }
        }

        [Fact]
        public void ProjectKey_ManualConfigPreview_ShowsTheHeaderShape_ButNeverTheKey()
        {
            var display = BuildProjectKeySettings().ForDisplay();
            var description = Claude().Describe(display, TransportMethod.streamableHttp, NullLogger.Instance);
            var allText = string.Join("\n", description.Sections.SelectMany(s => s.Items).Select(i => i.Text ?? string.Empty));

            Assert.DoesNotContain(ProjectKey, allText);
            Assert.Contains(AgentConfiguratorSettings.ProjectKeyDisplayPlaceholder, allText);
        }

        [Theory]
        [InlineData(GodotMcpConnectionMode.Custom, true, false, true, false, false, ProjectKeyStatus.NotApplicable)]
        [InlineData(GodotMcpConnectionMode.Cloud, false, false, false, false, false, ProjectKeyStatus.SignedOut)]
        [InlineData(GodotMcpConnectionMode.Cloud, true, true, true, false, false, ProjectKeyStatus.Working)]
        [InlineData(GodotMcpConnectionMode.Cloud, true, false, false, false, false, ProjectKeyStatus.None)]
        [InlineData(GodotMcpConnectionMode.Cloud, true, false, true, false, false, ProjectKeyStatus.InUse)]
        [InlineData(GodotMcpConnectionMode.Cloud, true, false, false, true, false, ProjectKeyStatus.Unavailable)]
        [InlineData(GodotMcpConnectionMode.Cloud, true, false, true, false, true, ProjectKeyStatus.RegenerateFailed)]
        public void ResolveProjectKeyStatus_Matrix(
            GodotMcpConnectionMode mode, bool signedIn, bool busy, bool hasKey, bool lastFailed, bool regenFailed,
            ProjectKeyStatus expected)
        {
            Assert.Equal(expected, AgentConfiguratorCredentialPolicy.ResolveProjectKeyStatus(
                mode, signedIn, busy, hasKey, lastFailed, regenFailed));
        }

        [Fact]
        public void ProjectKeyStatus_TextAndRegenerateAvailability()
        {
            Assert.Null(AgentConfiguratorCredentialPolicy.DescribeProjectKeyStatus(ProjectKeyStatus.NotApplicable));
            foreach (var status in System.Enum.GetValues(typeof(ProjectKeyStatus)).Cast<ProjectKeyStatus>())
            {
                if (status != ProjectKeyStatus.NotApplicable)
                    Assert.False(string.IsNullOrWhiteSpace(AgentConfiguratorCredentialPolicy.DescribeProjectKeyStatus(status)));
            }
            // Signed out ⇒ a hint to sign in; no git/.gitignore talk anywhere (owner ruling 2026-09-23).
            Assert.Contains("Sign in", AgentConfiguratorCredentialPolicy.DescribeProjectKeyStatus(ProjectKeyStatus.SignedOut));
            Assert.All(System.Enum.GetValues(typeof(ProjectKeyStatus)).Cast<ProjectKeyStatus>(),
                st => Assert.DoesNotContain("git", AgentConfiguratorCredentialPolicy.DescribeProjectKeyStatus(st) ?? string.Empty));

            Assert.False(AgentConfiguratorCredentialPolicy.CanRegenerateKey(ProjectKeyStatus.NotApplicable));
            Assert.False(AgentConfiguratorCredentialPolicy.CanRegenerateKey(ProjectKeyStatus.SignedOut));
            Assert.False(AgentConfiguratorCredentialPolicy.CanRegenerateKey(ProjectKeyStatus.Working));
            Assert.True(AgentConfiguratorCredentialPolicy.CanRegenerateKey(ProjectKeyStatus.InUse));
            Assert.True(AgentConfiguratorCredentialPolicy.CanRegenerateKey(ProjectKeyStatus.None));
        }

        [Fact]
        public void EveryAgentPanelButton_HasATooltipNamingActionAndTarget()
        {
            Assert.Contains("Regenerate", AgentConfiguratorCredentialPolicy.RegenerateKeyTooltip);
            Assert.Contains("key", AgentConfiguratorCredentialPolicy.RegenerateKeyTooltip);
            Assert.Contains("Claude Code", AgentConfiguratorCredentialPolicy.ConfigureTooltip("Claude Code"));
            Assert.Contains("Claude Code", AgentConfiguratorCredentialPolicy.RemoveTooltip("Claude Code"));
        }
    }
}
