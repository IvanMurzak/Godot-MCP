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
using System.Linq;
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Runtime;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the pure-managed surface of the runtime (in-game) entry point — the Godot analog of
    /// Unity-MCP's <c>UnityMcpPluginRuntime</c>:
    /// <list type="bullet">
    ///   <item><see cref="GodotMcpRuntime.Initialize"/> returns a configurable builder.</item>
    ///   <item><b>Zero tools by default</b> — a builder with no <c>WithTools*</c> call registers nothing.</item>
    ///   <item>Manual tool registration via <see cref="GodotMcpRuntimeBuilder.WithToolsFromAssembly"/> /
    ///   <see cref="GodotMcpRuntimeBuilder.WithTools"/> accumulates the opted-in set.</item>
    ///   <item><see cref="GodotMcpRuntimeBuilder.WithConfig"/> mutations compose onto a fresh
    ///   <see cref="GodotMcpConfig"/> with NO persisted-config auto-load.</item>
    /// </list>
    ///
    /// <para>
    /// These tests stop at the builder / config layer on purpose. The full <c>.Build()</c> path constructs
    /// the reused SignalR client and touches native Godot (<c>Engine.GetMainLoop</c>, <c>GD.Print</c>),
    /// which faults in this binary-less xUnit host (an <c>AccessViolationException</c> would crash the
    /// runner). That end-to-end "builds an empty-tool-set connection without an editor" proof is the
    /// ExportRelease (TOOLS-undefined) compile gate plus the headless Godot runtime smoke (T3) — not a unit
    /// test. Here we verify everything that decides WHAT the build will register, deterministically.
    /// </para>
    /// </summary>
    public class GodotMcpRuntimeBuilderTests
    {
        [Fact]
        public void Initialize_WithNullConfigure_ReturnsBuilder_WithNoTools()
        {
            var builder = GodotMcpRuntime.Initialize();

            Assert.NotNull(builder);
            Assert.True(builder.HasNoTools);
            Assert.Empty(builder.ToolAssemblies);
            Assert.Empty(builder.ToolTypes);
        }

        [Fact]
        public void Initialize_InvokesConfigureCallback()
        {
            var invoked = false;
            GodotMcpRuntime.Initialize(b =>
            {
                invoked = true;
                Assert.NotNull(b);
            });

            Assert.True(invoked);
        }

        [Fact]
        public void Builder_ByDefault_RegistersZeroTools()
        {
            // The core "exactly Unity's model" invariant: nothing opted in → empty tool set.
            var builder = GodotMcpRuntime.Initialize(b => b.WithConfig(c => c.Host = "http://localhost:9999"));

            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void WithToolsFromAssembly_AccumulatesAndDeduplicates()
        {
            var asm = typeof(Tool_Ping).Assembly;
            var builder = GodotMcpRuntime.Initialize(b =>
            {
                b.WithToolsFromAssembly(asm);
                b.WithToolsFromAssembly(asm); // duplicate — must be de-duplicated
            });

            Assert.False(builder.HasNoTools);
            Assert.Single(builder.ToolAssemblies);
            Assert.Same(asm, builder.ToolAssemblies[0]);
        }

        [Fact]
        public void WithTools_AccumulatesTypes_IgnoresNullsAndDuplicates()
        {
            var builder = GodotMcpRuntime.Initialize(b =>
                b.WithTools(typeof(Tool_Ping), typeof(Tool_Ping), null!));

            Assert.False(builder.HasNoTools);
            Assert.Single(builder.ToolTypes);
            Assert.Equal(typeof(Tool_Ping), builder.ToolTypes[0]);
        }

        [Fact]
        public void WithToolsFromAssembly_Null_Throws()
        {
            var builder = GodotMcpRuntime.Initialize();
            Assert.Throws<ArgumentNullException>(() => builder.WithToolsFromAssembly(null!));
        }

        [Fact]
        public void WithConfig_Null_Throws()
        {
            var builder = GodotMcpRuntime.Initialize();
            Assert.Throws<ArgumentNullException>(() => builder.WithConfig(null!));
        }

        // --- Prompts: mirror the tools tests exactly (accumulate + dedup + null-guard + empty-by-default).
        //     Prompt/resource registration is INDEPENDENTLY optional from tools; a builder can register
        //     prompts while leaving HasNoTools true. The accumulation logic is type-agnostic, so the
        //     CI-friendly Tool_Ping plus arbitrary BCL/test types double as distinct Type/Assembly fixtures
        //     here (the editor-gated tool families are #if TOOLS and not compiled into this xUnit assembly).

        [Fact]
        public void Builder_ByDefault_RegistersZeroPrompts()
        {
            var builder = GodotMcpRuntime.Initialize();

            Assert.Empty(builder.PromptAssemblies);
            Assert.Empty(builder.PromptTypes);
        }

        [Fact]
        public void WithPromptsFromAssembly_AccumulatesAndDeduplicates()
        {
            var asm = typeof(Tool_Ping).Assembly;
            var builder = GodotMcpRuntime.Initialize(b =>
            {
                b.WithPromptsFromAssembly(asm);
                b.WithPromptsFromAssembly(asm); // duplicate — must be de-duplicated
            });

            Assert.Single(builder.PromptAssemblies);
            Assert.Same(asm, builder.PromptAssemblies[0]);
            // Prompts are independent of tools — registering a prompt assembly must NOT flip HasNoTools.
            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void WithPrompts_AccumulatesTypes_IgnoresNullsAndDuplicates()
        {
            var builder = GodotMcpRuntime.Initialize(b =>
                b.WithPrompts(typeof(Tool_Ping), typeof(Tool_Ping), null!));

            Assert.Single(builder.PromptTypes);
            Assert.Equal(typeof(Tool_Ping), builder.PromptTypes[0]);
            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void WithPromptsFromAssembly_Null_Throws()
        {
            var builder = GodotMcpRuntime.Initialize();
            Assert.Throws<ArgumentNullException>(() => builder.WithPromptsFromAssembly(null!));
        }

        [Fact]
        public void WithPrompts_NullArray_IsNoOp()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithPrompts(null!));
            Assert.Empty(builder.PromptTypes);
        }

        // --- Resources: mirror the prompts tests exactly.

        [Fact]
        public void Builder_ByDefault_RegistersZeroResources()
        {
            var builder = GodotMcpRuntime.Initialize();

            Assert.Empty(builder.ResourceAssemblies);
            Assert.Empty(builder.ResourceTypes);
        }

        [Fact]
        public void WithResourcesFromAssembly_AccumulatesAndDeduplicates()
        {
            var asm = typeof(Tool_Ping).Assembly;
            var builder = GodotMcpRuntime.Initialize(b =>
            {
                b.WithResourcesFromAssembly(asm);
                b.WithResourcesFromAssembly(asm); // duplicate — must be de-duplicated
            });

            Assert.Single(builder.ResourceAssemblies);
            Assert.Same(asm, builder.ResourceAssemblies[0]);
            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void WithResources_AccumulatesTypes_IgnoresNullsAndDuplicates()
        {
            var builder = GodotMcpRuntime.Initialize(b =>
                b.WithResources(typeof(Tool_Ping), typeof(Tool_Ping), null!));

            Assert.Single(builder.ResourceTypes);
            Assert.Equal(typeof(Tool_Ping), builder.ResourceTypes[0]);
            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void WithResourcesFromAssembly_Null_Throws()
        {
            var builder = GodotMcpRuntime.Initialize();
            Assert.Throws<ArgumentNullException>(() => builder.WithResourcesFromAssembly(null!));
        }

        [Fact]
        public void WithResources_NullArray_IsNoOp()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithResources(null!));
            Assert.Empty(builder.ResourceTypes);
        }

        // --- Prompts + resources + tools accumulate INDEPENDENTLY in a single builder.

        [Fact]
        public void Tools_Prompts_Resources_AccumulateIndependently()
        {
            // Distinct arbitrary Type fixtures — the accumulation buckets are type-agnostic; only Tool_Ping
            // is compiled into this xUnit assembly, so use BCL/test types for the prompt/resource slots.
            var toolType = typeof(Tool_Ping);
            var promptType = typeof(string);
            var resourceType = typeof(GodotMcpRuntimeBuilderTests);
            var asm = toolType.Assembly;
            var builder = GodotMcpRuntime.Initialize(b =>
            {
                b.WithTools(toolType);
                b.WithPrompts(promptType);
                b.WithResources(resourceType);
                b.WithPromptsFromAssembly(asm);
                b.WithResourcesFromAssembly(asm);
            });

            Assert.False(builder.HasNoTools);
            Assert.Single(builder.ToolTypes);
            Assert.Equal(toolType, builder.ToolTypes[0]);
            Assert.Single(builder.PromptTypes);
            Assert.Equal(promptType, builder.PromptTypes[0]);
            Assert.Single(builder.ResourceTypes);
            Assert.Equal(resourceType, builder.ResourceTypes[0]);
            Assert.Single(builder.PromptAssemblies);
            Assert.Single(builder.ResourceAssemblies);
        }

        [Fact]
        public void WithTools_NullArray_IsNoOp()
        {
            var builder = GodotMcpRuntime.Initialize(b => b.WithTools(null!));
            Assert.True(builder.HasNoTools);
        }

        [Fact]
        public void ApplyConfig_ComposesMutationsInOrder_OnFreshConfig()
        {
            var builder = GodotMcpRuntime.Initialize(b =>
            {
                b.WithConfig(c => c.ConnectionMode = GodotMcpConnectionMode.Custom);
                b.WithConfig(c => c.CustomHost = "http://localhost:8080");
                b.WithConfig(c =>
                {
                    c.AuthOption = McpServerConsts.AuthOption.token;
                    c.CustomToken = "secret-token";
                });
            });

            // A fresh config — exactly what GodotMcpRuntime.Build seeds (NO persisted user:// auto-load).
            var config = new GodotMcpConfig();
            InvokeApplyConfig(builder, config);

            Assert.Equal(GodotMcpConnectionMode.Custom, config.ConnectionMode);
            Assert.Equal("http://localhost:8080", config.CustomHost);
            Assert.Equal(McpServerConsts.AuthOption.token, config.AuthOption);
            Assert.Equal("secret-token", config.CustomToken);
        }

        [Fact]
        public void FreshConfig_HasSecureRuntimeDefaults_NoPersistedLoad()
        {
            // GodotMcpRuntime.Build starts from `new GodotMcpConfig()` and never reads the persisted
            // user:// config. A no-WithConfig builder therefore yields the built-in defaults verbatim:
            // Cloud mode, no custom token, no custom host beyond the default. (The connection still stays
            // OFF until Connect() — proven structurally by GodotMcpRuntimeHandle, not exercised here.)
            var builder = GodotMcpRuntime.Initialize();
            var config = new GodotMcpConfig();
            InvokeApplyConfig(builder, config);

            Assert.Equal(GodotMcpConnectionMode.Cloud, config.ConnectionMode);
            Assert.Null(config.CustomToken);
            Assert.Equal(GodotMcpConfig.DefaultCustomHost, config.CustomHost);
        }

        [Fact]
        public void WithoutMainThreadDispatcher_FlipsInstallDispatcherFlag()
        {
            // InstallDispatcher is internal; reach it reflectively to pin the opt-out contract.
            var on = GodotMcpRuntime.Initialize();
            var off = GodotMcpRuntime.Initialize(b => b.WithoutMainThreadDispatcher());

            Assert.True(ReadInstallDispatcher(on));
            Assert.False(ReadInstallDispatcher(off));
        }

        // --- helpers reaching the builder's internal surface (same assembly, but the members are internal
        //     so the public test stays readable) ----------------------------------------------------------

        static void InvokeApplyConfig(GodotMcpRuntimeBuilder builder, GodotMcpConfig config)
        {
            var m = typeof(GodotMcpRuntimeBuilder).GetMethod(
                "ApplyConfig", BindingFlags.Instance | BindingFlags.NonPublic)!;
            m.Invoke(builder, new object[] { config });
        }

        static bool ReadInstallDispatcher(GodotMcpRuntimeBuilder builder)
        {
            var p = typeof(GodotMcpRuntimeBuilder).GetProperty(
                "InstallDispatcher", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (bool)p.GetValue(builder)!;
        }
    }
}
