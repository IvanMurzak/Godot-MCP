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
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The addon and the extensions ship as source and compile inside the consumer's project, so they inherit its
    /// <c>&lt;ImplicitUsings&gt;enable&lt;/ImplicitUsings&gt;</c> global usings (issue #474). A bare Godot type name that
    /// also exists in one of those namespaces (<c>Timer</c>, <c>FileAccess</c>, …) then fails with CS0104; the source
    /// must alias it per file (<c>using FileAccess = Godot.FileAccess;</c>). This compiles every shipped source with the
    /// SDK's implicit global usings, for the editor and for an exported game, and fails on any CS0104. Other
    /// diagnostics are ignored: this host lacks GodotSharpEditor and the newer GodotSharp APIs some sources use.
    /// </summary>
    public class ImplicitUsingsAmbiguityTests
    {
        // The global usings Microsoft.NET.Sdk (and so Godot.NET.Sdk) generates for ImplicitUsings=enable.
        const string ImplicitGlobalUsings = @"
global using global::System;
global using global::System.Collections.Generic;
global using global::System.IO;
global using global::System.Linq;
global using global::System.Net.Http;
global using global::System.Threading;
global using global::System.Threading.Tasks;
";

        public static TheoryData<string, string[]> Builds => new()
        {
            { "editor", new[] { "TOOLS", "GODOT", "GODOT4", "GODOT4_5_OR_GREATER", "GODOT4_6_OR_GREATER" } },
            { "editor-4.3", new[] { "TOOLS", "GODOT", "GODOT4" } },
            { "export", new[] { "GODOT", "GODOT4" } },
        };

        [Theory]
        [MemberData(nameof(Builds))]
        public void ShippedSources_CompileWithoutAmbiguity_UnderImplicitUsings(string build, string[] symbols)
        {
            var repoRoot = ToolAnnotationsTests.RepoRoot();

            var sourceRoots = new[] { Path.Combine(repoRoot, "addons", "godot_mcp") }
                .Concat(Directory.EnumerateDirectories(Path.Combine(repoRoot, "extensions"), "Godot-AI-*")
                    .Select(module => Path.Combine(module, "src")))
                .Append(Path.Combine(repoRoot, "templates", "extension", "src"));
            var files = sourceRoots
                .SelectMany(root => Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Any(part => part is "bin" or "obj"))
                .ToList();
            // The scan must really see the sources that once hit CS0104, or the check below passes on nothing.
            Assert.Contains(files, f => f.EndsWith("ReloadGateDriver.cs", StringComparison.Ordinal));
            Assert.Contains(files, f => f.EndsWith("DialogicApi.cs", StringComparison.Ordinal));

            var options = new CSharpParseOptions(LanguageVersion.Latest, preprocessorSymbols: symbols);
            var trees = files
                .Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), options, f))
                .Append(CSharpSyntaxTree.ParseText(ImplicitGlobalUsings, options, "GlobalUsings.g.cs"))
                .ToList();

            var compilation = CSharpCompilation.Create(
                $"ImplicitUsingsProbe_{build}",
                trees,
                References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));
            // Without GodotSharp a bare Timer would bind to System.Threading.Timer alone and the check would pass on nothing.
            Assert.NotNull(compilation.GetTypeByMetadataName("Godot.FileAccess"));

            var ambiguous = compilation.GetDiagnostics()
                .Where(d => d.Id == "CS0104")
                .Select(d => Path.GetRelativePath(repoRoot, d.Location.SourceTree?.FilePath ?? "?")
                    + $"({d.Location.GetLineSpan().StartLinePosition.Line + 1}): {d.GetMessage()}")
                .Distinct()
                .ToList();
            Assert.True(ambiguous.Count == 0,
                $"Ambiguous names under ImplicitUsings ({build}); add a file-local alias such as "
                + "`using FileAccess = Godot.FileAccess;`:\n" + string.Join("\n", ambiguous));
        }

        // The BCL plus every assembly beside the test (GodotSharp, McpPlugin, ReflectorNet, RQL, Code-Exec, …).
        static IEnumerable<MetadataReference> References()
        {
            var platform = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty)
                .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
            var local = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll");
            return platform.Concat(local)
                .GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .Where(IsManagedAssembly)
                .Select(path => MetadataReference.CreateFromFile(path));
        }

        static bool IsManagedAssembly(string path)
        {
            try
            {
                System.Reflection.AssemblyName.GetAssemblyName(path);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
