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
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.AgentConfig;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Custom + oauth: the ai-game.dev account token reaches a loopback host only; any other host gets
    /// <c>GODOT_MCP_TOKEN</c> or the connection's credential provider fails (McpPlugin's shared
    /// <see cref="CustomOAuthCredentialPolicy"/> / <see cref="HostScope"/>). Each provider is bound to the host it was
    /// handed to the connection with. Mutates process env, hence the <c>env</c> collection.
    /// </summary>
    [Collection("env")]
    public class CustomOAuthCredentialScopeTests
    {
        [Theory]
        [InlineData("http://localhost")]
        [InlineData("http://localhost:8080")]
        [InlineData("https://LOCALHOST:443/hub")]
        [InlineData("http://127.0.0.1:11111")]
        [InlineData("http://127.255.255.254")]
        [InlineData("http://[::1]")]
        [InlineData("http://[::1]:8080")]
        [InlineData("http://[::ffff:127.0.0.1]:8080")]
        public void IsLoopback_True(string url) => Assert.True(HostScope.IsLoopback(url));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("localhost:8080")]
        [InlineData("https://ai-game.dev")]
        [InlineData("http://evil.com")]
        [InlineData("http://localhost@evil.com")]
        [InlineData("http://user:pass@localhost:8080")]
        [InlineData("http://127.0.0.1.evil.com")]
        [InlineData("http://localhost.evil.com")]
        [InlineData("http://evil.localhost")]
        [InlineData("http://localhost.")]
        [InlineData("http://128.0.0.1")]
        [InlineData("http://0.0.0.0")]
        [InlineData("http://192.168.1.10:8080")]
        [InlineData("http://[::2]")]
        [InlineData("ftp://localhost")]
        public void IsLoopback_False(string? url) => Assert.False(HostScope.IsLoopback(url));

        const string Remote = "https://my-server.example.com";

        static GodotMcpConfig CustomOAuth(string host) => new()
        {
            ConnectionMode = GodotMcpConnectionMode.Custom,
            AuthOption = McpServerConsts.AuthOption.oauth,
            CustomHost = host,
            CloudToken = "account-jwt",
            CustomToken = "local-secret",
        };

        [Fact]
        public async Task Config_Loopback_SendsAccountToken()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth("http://127.0.0.1:5000");
            Assert.Equal("account-jwt", config.ResolveCustomToken());
            Assert.Equal("account-jwt", await config.CredentialProvider!());
        }

        [Fact]
        public async Task Config_Remote_WithoutExplicitToken_Refuses()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth(Remote);
            config.CustomToken = null;
            Assert.Null(config.ResolveCustomToken()); // never the account token
            var ex = await Assert.ThrowsAsync<CredentialScopeException>(() => config.CredentialProvider!());
            Assert.Equal("Refusing to send your ai-game.dev account token to my-server.example.com; " +
                         "set GODOT_MCP_TOKEN or use a localhost server.", ex.Message);
        }

        [Theory]
        [InlineData(Remote)]
        [InlineData("http://localhost@evil.com")]
        [InlineData("http://127.0.0.1.evil.com:9000")]
        public async Task Config_Remote_UsesExplicitToken(string host)
        {
            using var _ = EnvScope.Set("explicit-token");
            var config = CustomOAuth(host);
            Assert.Equal("explicit-token", config.ResolveCustomToken());
            Assert.Equal("explicit-token", await config.CredentialProvider!());
        }

        [Fact]
        public async Task Config_Remote_UsesConfiguredCustomToken()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth(Remote); // CustomToken also carries a project .env GODOT_MCP_TOKEN
            Assert.Equal("local-secret", config.ResolveCustomToken());
            Assert.Equal("local-secret", await config.CredentialProvider!());
        }

        [Fact]
        public async Task Config_Loopback_NoAccount_FallsBackToExplicitToken()
        {
            using var _ = EnvScope.Set("explicit-token");
            var config = CustomOAuth("http://localhost:5000");
            config.CloudToken = null;
            Assert.Equal("explicit-token", await config.CredentialProvider!());
        }

        [Fact]
        public async Task EditorProvider_Remote_NeverReadsSignedInAccount()
        {
            using var _ = EnvScope.Set(null);
            using var tmp = new TempDir();
            new MachineCredentialStore(tmp.Path).Write(new MachineCredentials { AccessToken = "machine-jwt", ServerTarget = "https://ai-game.dev" });
            var config = CustomOAuth(Remote);
            config.CloudToken = null;
            config.CustomToken = null;
            using var account = new GodotAccountAuth(() => "https://ai-game.dev", new MachineCredentialStore(tmp.Path), new HttpClient());
            Assert.True(account.IsSignedIn);

            var provider = account.CreateConnectionCredentialProvider(config);
            await Assert.ThrowsAsync<CredentialScopeException>(() => provider());

            config.CustomHost = "http://[::1]:5000";
            Assert.Equal("machine-jwt", await account.CreateConnectionCredentialProvider(config)());
        }

        // --- Host capture: a provider never resolves for a host other than the one it was handed over with. ---

        [Fact]
        public async Task RuntimeProvider_HostChangedToLoopbackAfterHandOff_NeverSendsAccountToken()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth(Remote);
            config.CustomToken = null;
            var handedToRemoteConnection = config.CredentialProvider!; // McpPlugin reads it once per HubConnection

            // A SignalR reconnect of the remote link calls the provider after the host became loopback.
            config.CustomHost = "http://localhost:5000";
            Assert.Null(await handedToRemoteConnection());

            // A HubConnection built now dials the loopback host and gets a provider bound to it.
            Assert.Equal("account-jwt", await config.CredentialProvider!());
        }

        [Fact]
        public async Task RuntimeProvider_HostChangedToRemoteAfterHandOff_ReturnsNull()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth("http://localhost:5000");
            var handedToLoopbackConnection = config.CredentialProvider!;
            config.CustomHost = Remote;
            Assert.Null(await handedToLoopbackConnection());
        }

        [Fact]
        public async Task RuntimeProvider_ModeChangedToCloudAfterHandOff_NeverSendsCloudToken()
        {
            using var _ = EnvScope.Set(null);
            var config = CustomOAuth(Remote);
            var handedToRemoteConnection = config.CredentialProvider!;
            config.ConnectionMode = GodotMcpConnectionMode.Cloud;
            Assert.Null(await handedToRemoteConnection());
        }

        [Fact]
        public void RuntimeProvider_ExplicitAssignment_IsKept()
        {
            Func<Task<string?>> custom = () => Task.FromResult<string?>("game-supplied");
            var config = new GodotMcpConfig { CredentialProvider = custom };
            Assert.Same(custom, config.CredentialProvider);
        }

        [Fact]
        public async Task EditorProvider_HostChangedToLoopbackAfterHandOff_NeverSendsAccountToken()
        {
            using var _ = EnvScope.Set(null);
            using var tmp = new TempDir();
            new MachineCredentialStore(tmp.Path).Write(new MachineCredentials { AccessToken = "machine-jwt", ServerTarget = "https://ai-game.dev" });
            var config = CustomOAuth(Remote);
            using var account = new GodotAccountAuth(() => "https://ai-game.dev", new MachineCredentialStore(tmp.Path), new HttpClient());
            Assert.True(account.IsSignedIn);

            var provider = account.CreateConnectionCredentialProvider(config);
            config.CustomHost = "http://127.0.0.1:5000";
            Assert.Null(await provider());
        }

        [Fact]
        public void ConnectionErrorHint_EditorNamesTheDockStopButton_RuntimeLeavesItUnset()
        {
            Assert.Contains("\"" + ConnectionPanelView.ButtonTextStop + "\"", ConnectionPanelView.ConnectionErrorHint);
            Assert.Contains("AI Game Developer", ConnectionPanelView.ConnectionErrorHint);
            Assert.DoesNotContain("<", ConnectionPanelView.ConnectionErrorHint); // plain text: Godot's Output shows markup raw
            Assert.Null(new GodotMcpConfig().ConnectionErrorHint);
        }

        sealed class EnvScope : IDisposable
        {
            static readonly string[] Names =
            {
                GodotMcpConfig.EnvToken, GodotMcpConfig.EnvAuthOption, GodotMcpConfig.EnvConnectionMode,
                GodotMcpConfig.EnvHost, GodotMcpConfig.EnvCloudUrl,
            };
            readonly string?[] _prior = Array.ConvertAll(Names, Environment.GetEnvironmentVariable);

            /// <summary>Clears the connection env and sets <c>GODOT_MCP_TOKEN</c> to <paramref name="token"/>.</summary>
            public static EnvScope Set(string? token)
            {
                var scope = new EnvScope();
                foreach (var name in Names)
                    Environment.SetEnvironmentVariable(name, null);
                Environment.SetEnvironmentVariable(GodotMcpConfig.EnvToken, token);
                return scope;
            }

            public void Dispose()
            {
                for (var i = 0; i < Names.Length; i++)
                    Environment.SetEnvironmentVariable(Names[i], _prior[i]);
            }
        }

        sealed class TempDir : IDisposable
        {
            public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "godot-mcp-scope-" + Guid.NewGuid().ToString("N"));
            public void Dispose()
            {
                try { if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true); } catch { /* best effort */ }
            }
        }
    }
}
