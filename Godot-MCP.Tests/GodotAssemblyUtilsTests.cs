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
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Loader;
using com.IvanMurzak.Godot.MCP.Reflection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the scan-set contract of <see cref="GodotAssemblyUtils.AllAssemblies"/> (issue #102).
    ///
    /// <para>
    /// The Godot editor loads the project assembly — the one hosting the addon and every
    /// <c>[AiToolType]</c> tool class — into its own collectible non-default
    /// <see cref="AssemblyLoadContext"/>. The scan set must therefore span ALL load contexts; when it
    /// enumerated only <see cref="AssemblyLoadContext.Default"/>, the <c>McpPluginBuilder</c> attribute
    /// scan never saw the tool assembly and the plugin registered zero tools (empty <c>tools/list</c>,
    /// <c>ping</c> not found) with no error anywhere. The non-default-context test below simulates
    /// Godot's plugin context with a collectible ALC; it fails against the
    /// <see cref="AssemblyLoadContext.Default"/>-only implementation and passes against
    /// <see cref="AppDomain.GetAssemblies"/>. Pure-BCL, CI-friendly (no Godot binary needed).
    /// </para>
    /// </summary>
    public class GodotAssemblyUtilsTests
    {
        [Fact]
        public void AllAssemblies_IncludesAssemblyLoadedInNonDefaultContext()
        {
            // Simulate Godot's plugin context: load a copy of this test assembly into a fresh
            // collectible ALC (a copy, so the path differs and the load is not deduplicated into
            // an already-loaded assembly identity).
            var sourcePath = typeof(GodotAssemblyUtilsTests).Assembly.Location;
            var copyPath = Path.Combine(Path.GetTempPath(), $"alc-probe-{Guid.NewGuid():N}.dll");
            File.Copy(sourcePath, copyPath);

            var alc = new AssemblyLoadContext($"test-plugin-context-{Guid.NewGuid():N}", isCollectible: true);
            try
            {
                var loaded = alc.LoadFromAssemblyPath(copyPath);
                Assert.NotEqual(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(loaded));

                Assert.Contains(loaded, GodotAssemblyUtils.AllAssemblies);
            }
            finally
            {
                alc.Unload();
                try { File.Delete(copyPath); } catch (IOException) { /* still mapped; temp cleanup */ }
            }
        }

        [Fact]
        public void AllAssemblies_IncludesTheCallingAssemblyItself()
        {
            // The addon assembly must always be in its own scan set — the ping tool lives there.
            Assert.Contains(typeof(GodotAssemblyUtilsTests).Assembly, GodotAssemblyUtils.AllAssemblies);
        }

        [Fact]
        public void AllAssemblies_ExcludesDynamicAssemblies()
        {
            var dynamic = AssemblyBuilder.DefineDynamicAssembly(
                new AssemblyName($"dynamic-probe-{Guid.NewGuid():N}"), AssemblyBuilderAccess.Run);
            // Materialize a type so the dynamic assembly is fully realized in the AppDomain.
            dynamic.DefineDynamicModule("m");

            Assert.DoesNotContain(dynamic, GodotAssemblyUtils.AllAssemblies);
        }
    }
}
