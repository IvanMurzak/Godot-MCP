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
using System.IO;
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the editor-runtime NuGet-dependency resolution logic in
    /// <see cref="GodotMcpAssemblyResolver"/>. This is the exact mechanism that lets the Godot editor
    /// find ReflectorNet/McpPlugin at runtime (where Godot's default AssemblyLoadContext otherwise
    /// throws <c>FileNotFoundException</c> because it does not probe the project's <c>*.deps.json</c>).
    ///
    /// <para>
    /// We seed the resolver from THIS test assembly's location: the test project itself
    /// <c>PackageReference</c>s <c>com.IvanMurzak.ReflectorNet</c> and <c>com.IvanMurzak.McpPlugin</c>,
    /// so its sibling <c>*.deps.json</c> describes the same dependency graph the addon assembly has.
    /// Pure-BCL, no Godot native types — CI-friendly (no Godot binary needed).
    /// </para>
    /// </summary>
    public class AssemblyResolverTests
    {
        static string TestAssemblyPath => typeof(AssemblyResolverTests).Assembly.Location;

        [Theory]
        [InlineData("ReflectorNet")]
        [InlineData("McpPlugin")]
        [InlineData("McpPlugin.Common")]
        public void ResolvePath_ResolvesTransitiveNuGetDependency_ViaDepsJson(string simpleName)
        {
            // Ensure the resolver is installed and seeded from the test assembly's sibling deps.json.
            // NOTE: Install() latches on the first call (the _installed flag), so this is the seeding
            // call only if no prior test installed it first; otherwise it is a no-op and the resolver is
            // already seeded from the test assembly (every test in this class anchors on TestAssemblyPath,
            // so the seeded anchor is the same either way).
            GodotMcpAssemblyResolver.Install(TestAssemblyPath);

            var resolved = GodotMcpAssemblyResolver.ResolvePath(new AssemblyName(simpleName));

            Assert.False(string.IsNullOrEmpty(resolved), $"expected a resolved path for '{simpleName}'");
            Assert.True(File.Exists(resolved), $"resolved path should exist on disk: {resolved}");
            Assert.Equal(simpleName + ".dll", Path.GetFileName(resolved));
        }

        [Fact]
        public void ResolvePath_ReturnsNull_ForUnknownAssembly()
        {
            GodotMcpAssemblyResolver.Install(TestAssemblyPath);

            var resolved = GodotMcpAssemblyResolver.ResolvePath(
                new AssemblyName("Definitely.Not.A.Real.Assembly.Name.42"));

            Assert.True(string.IsNullOrEmpty(resolved));
        }

        [Theory]
        [InlineData("ReflectorNet")]
        [InlineData("McpPlugin")]
        [InlineData("McpPlugin.Common")]
        public void BuildDepsJsonIndex_MapsAssembliesToExistingNuGetCachePaths(string simpleName)
        {
            // This is the strategy that actually carries in the Godot editor, where no
            // *.runtimeconfig.dev.json (NuGet probing paths) is emitted next to the project assembly,
            // so AssemblyDependencyResolver returns empty. The deps.json index resolves package
            // {id}/{version}/{relative} against the NuGet global packages folder directly.
            var depsJsonPath = Path.ChangeExtension(TestAssemblyPath, ".deps.json");
            Assert.True(File.Exists(depsJsonPath), $"test deps.json should exist: {depsJsonPath}");

            var index = GodotMcpAssemblyResolver.BuildDepsJsonIndex(depsJsonPath);

            Assert.NotNull(index);
            Assert.True(index!.TryGetValue(simpleName, out var path),
                $"deps.json index should contain '{simpleName}'");
            Assert.True(File.Exists(path), $"indexed path should exist on disk: {path}");
            Assert.Equal(simpleName + ".dll", Path.GetFileName(path));
        }

        [Fact]
        public void BuildDepsJsonIndex_ReturnsNull_ForMissingFile()
        {
            var index = GodotMcpAssemblyResolver.BuildDepsJsonIndex(
                Path.Combine(Path.GetTempPath(), "definitely-missing.deps.json"));

            Assert.Null(index);
        }

        [Fact]
        public void Install_IsIdempotent_DoesNotThrowOnRepeatedCalls()
        {
            // Multiple installs (plugin toggled off/on, domain reload) must be safe.
            GodotMcpAssemblyResolver.Install(TestAssemblyPath);
            GodotMcpAssemblyResolver.Install(TestAssemblyPath);
            GodotMcpAssemblyResolver.Install();

            // Still resolves after repeated installs.
            var resolved = GodotMcpAssemblyResolver.ResolvePath(new AssemblyName("ReflectorNet"));
            Assert.False(string.IsNullOrEmpty(resolved));
        }

        [Fact]
        public void Install_LatchesOnFirstCall_SecondInstallWithDifferentAnchorIsNoOp()
        {
            // Proves the idempotency latch behaviorally (rather than merely "does not throw"): once the
            // resolver is installed, a SECOND Install() seeded from a DIFFERENT (here, deliberately
            // bogus) anchor must NOT re-seed or change resolution behavior — the _installed latch makes
            // the second call a no-op, so the original (good) seeding stays in effect. If the latch ever
            // regressed and the resolver re-seeded from this bogus anchor, ResolvePath would stop finding
            // the real dependency and this test would fail.
            GodotMcpAssemblyResolver.Install(TestAssemblyPath);

            var before = GodotMcpAssemblyResolver.ResolvePath(new AssemblyName("ReflectorNet"));
            Assert.False(string.IsNullOrEmpty(before), "precondition: resolver must resolve ReflectorNet after the seeding install");

            // A path that does not exist and has no sibling deps.json. If this re-seeded the resolver,
            // strategies 1 and 2 would lose their good probe dir / deps.json index.
            var bogusAnchor = Path.Combine(Path.GetTempPath(), "godot-mcp-nonexistent-anchor", "Bogus.dll");
            GodotMcpAssemblyResolver.Install(bogusAnchor);

            var after = GodotMcpAssemblyResolver.ResolvePath(new AssemblyName("ReflectorNet"));
            Assert.Equal(before, after);
        }
    }

    /// <summary>
    /// Pins the pure-managed core of the assembly-unload teardown hook (issue #132): on a Godot
    /// "Build Project" hot-reload the addon's collectible <see cref="System.Runtime.Loader.AssemblyLoadContext"/>
    /// unloads WITHOUT the editor calling the plugin's <c>_ExitTree</c>, so the plugin arms
    /// <see cref="GodotMcpAssemblyResolver.ReloadTeardown"/> and the resolver's private
    /// <c>OnAssemblyUnloading</c> handler invokes it when the ALC's <c>Unloading</c> event fires.
    ///
    /// <para>
    /// These tests exercise the handler directly via reflection (it is a private static taking an
    /// <see cref="System.Runtime.Loader.AssemblyLoadContext"/>). They are pure-BCL and CI-friendly — no
    /// Godot binary, no real ALC unload (which cannot be forced deterministically in-process). The two
    /// load-bearing properties: (1) the handler runs the registered teardown, and (2) a throwing teardown
    /// is swallowed so the handler NEVER throws — an exception escaping an <c>Unloading</c> handler would
    /// abort the very unload it exists to enable.
    /// </para>
    /// </summary>
    public class AssemblyUnloadingHookTests
    {
        static void InvokeOnAssemblyUnloading()
        {
            var method = typeof(GodotMcpAssemblyResolver).GetMethod(
                "OnAssemblyUnloading",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(method);

            // The handler reads only the registered ReloadTeardown; the AssemblyLoadContext argument is
            // unused by it, so the default (collectible) context for this test assembly is a fine stand-in.
            var ctx = System.Runtime.Loader.AssemblyLoadContext.GetLoadContext(
                typeof(AssemblyUnloadingHookTests).Assembly);
            method!.Invoke(null, new object?[] { ctx });
        }

        [Fact]
        public void OnAssemblyUnloading_InvokesRegisteredReloadTeardown()
        {
            var saved = GodotMcpAssemblyResolver.ReloadTeardown;
            try
            {
                var ran = 0;
                GodotMcpAssemblyResolver.ReloadTeardown = () => ran++;

                InvokeOnAssemblyUnloading();

                Assert.Equal(1, ran);
            }
            finally
            {
                GodotMcpAssemblyResolver.ReloadTeardown = saved;
            }
        }

        [Fact]
        public void OnAssemblyUnloading_IsNoOp_WhenNoTeardownRegistered()
        {
            var saved = GodotMcpAssemblyResolver.ReloadTeardown;
            try
            {
                GodotMcpAssemblyResolver.ReloadTeardown = null;

                // No teardown registered (CI/xUnit host where the plugin never booted) → must not throw.
                var ex = Record.Exception(InvokeOnAssemblyUnloading);
                Assert.Null(ex);
            }
            finally
            {
                GodotMcpAssemblyResolver.ReloadTeardown = saved;
            }
        }

        [Fact]
        public void OnAssemblyUnloading_SwallowsThrowingTeardown_NeverPropagates()
        {
            var saved = GodotMcpAssemblyResolver.ReloadTeardown;
            try
            {
                var ran = false;
                GodotMcpAssemblyResolver.ReloadTeardown = () =>
                {
                    ran = true;
                    throw new System.InvalidOperationException("boom — must be swallowed by the unload handler");
                };

                // Reflection wraps a thrown exception in TargetInvocationException; if the handler let the
                // teardown's exception escape, this would be non-null. The contract is that it must NOT.
                var ex = Record.Exception(InvokeOnAssemblyUnloading);

                Assert.True(ran, "the teardown should have been invoked before throwing");
                Assert.Null(ex);
            }
            finally
            {
                GodotMcpAssemblyResolver.ReloadTeardown = saved;
            }
        }
    }
}
