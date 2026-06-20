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
    public class GodotAssemblyUtilsTests : IClassFixture<GodotAssemblyUtilsTests.AlcProbeTempDir>
    {
        private readonly AlcProbeTempDir _tempDir;

        public GodotAssemblyUtilsTests(AlcProbeTempDir tempDir)
        {
            _tempDir = tempDir;
        }

        [Fact]
        public void AllAssemblies_IncludesAssemblyLoadedInNonDefaultContext()
        {
            // Simulate Godot's plugin context: load a copy of this test assembly into a fresh
            // collectible ALC (a copy, so the path differs and the load is not deduplicated into
            // an already-loaded assembly identity).
            //
            // The copy lands in a per-run temp SUBDIRECTORY (not bare %TEMP%), so that any DLL the
            // OS still has memory-mapped after alc.Unload() is reaped wholesale at class teardown
            // (AlcProbeTempDir.Dispose) and %TEMP% never accumulates orphaned alc-probe-*.dll.
            var sourcePath = typeof(GodotAssemblyUtilsTests).Assembly.Location;
            var copyPath = Path.Combine(_tempDir.Path, $"alc-probe-{Guid.NewGuid():N}.dll");
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
                BestEffortDeleteAfterUnload(copyPath);
            }
        }

        /// <summary>
        /// Best-effort delete of the ALC-loaded probe DLL after <see cref="AssemblyLoadContext.Unload"/>.
        /// <para>
        /// <c>alc.Unload()</c> is ASYNCHRONOUS — the collectible context is only torn down once the GC
        /// reclaims it, so immediately after the call the copied DLL is typically still memory-mapped.
        /// On Windows a delete of a mapped file throws <see cref="UnauthorizedAccessException"/> (a SIBLING
        /// of <see cref="IOException"/>, NOT a subclass — so the original <c>catch (IOException)</c> let it
        /// escape and fail the test). We force the collection that frees the ALC, then retry the delete a
        /// few times, and swallow BOTH exception types: cleanup is best-effort and must NEVER fail the test.
        /// Whatever survives here is reaped at class teardown by <see cref="AlcProbeTempDir.Dispose"/>.
        /// </para>
        /// </summary>
        private static void BestEffortDeleteAfterUnload(string copyPath)
        {
            // Force the GC pass that actually frees the collectible ALC and unmaps the DLL.
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            for (var attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    File.Delete(copyPath);
                    return;
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Still mapped (Windows: UnauthorizedAccessException after an async Unload). Retry;
                    // if it never unmaps in-process, the temp-dir teardown reaps it on a later run.
                    System.Threading.Thread.Sleep(20);
                }
            }
        }

        /// <summary>
        /// xUnit class fixture: owns a per-run temp subdirectory for the ALC probe DLLs and best-effort
        /// deletes the WHOLE directory at class teardown. A DLL the OS still has memory-mapped when the
        /// individual delete in <see cref="BestEffortDeleteAfterUnload"/> gave up is reaped here (or, if
        /// still locked, on the next run that recreates the dir) — so %TEMP% never grows alc-probe-*.dll.
        /// </summary>
        public sealed class AlcProbeTempDir : IDisposable
        {
            public string Path { get; }

            public AlcProbeTempDir()
            {
                Path = System.IO.Path.Combine(
                    System.IO.Path.GetTempPath(), $"godot-mcp-alc-probe-{Guid.NewGuid():N}");
                Directory.CreateDirectory(Path);
            }

            public void Dispose()
            {
                // Best-effort: a still-mapped DLL must not throw out of teardown.
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                try
                {
                    Directory.Delete(Path, recursive: true);
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
                {
                    // Leave it for the next run / OS temp cleaner; never fail the suite on teardown.
                }
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
