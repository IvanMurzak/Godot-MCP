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
            // Re-seed the deps.json resolver from the test assembly (idempotent Install() may have
            // already pinned a different anchor in another test run, so go through the seam directly).
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
    }
}
