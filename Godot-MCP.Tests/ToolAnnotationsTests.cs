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
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Every MCP tool must carry the annotations the Claude Connectors Directory requires: a non-empty <c>title</c> and
    /// explicit <c>readOnlyHint</c>, <c>destructiveHint</c>, <c>idempotentHint</c> and <c>openWorldHint</c> (an unset
    /// hint is left out of <c>tools/list</c>, and the directory flags the tool).
    /// <para>
    /// Two scans. The reflection scan reads the real <see cref="AiToolAttribute"/> values of the tools this host compiles.
    /// Most editor tools are not compiled here (they need native Godot types), so the source scan compiles every addon
    /// and extension source with Roslyn (<c>TOOLS</c> defined) and evaluates each <c>[AiTool]</c> argument as a
    /// constant, the same way the C# compiler does for the real attribute.
    /// </para>
    /// </summary>
    public class ToolAnnotationsTests
    {
        record ToolAnnotations(string Where, string? Name, string? Title,
            bool? ReadOnlyHint, bool? DestructiveHint, bool? IdempotentHint, bool? OpenWorldHint);

        [Fact]
        public void CompiledTools_DeclareTitleAndEveryHint()
        {
            var tools = CompiledTools();
            Assert.True(tools.Count >= 8, $"Reflection found only {tools.Count} [AiTool] methods; the scan is broken.");
            AssertAnnotated(tools);
        }

        [Fact]
        public void AllAddonAndExtensionTools_DeclareTitleAndEveryHint()
        {
            var tools = SourceTools();
            // A floor, not the exact count (25 today): removing a tool must not trip it.
            Assert.True(tools.Count >= 20, $"The source scan found only {tools.Count} [AiTool] attributes; is the scan broken?");
            AssertAnnotated(tools);

            // The source scan must see every tool reflection sees, with the same values: proves its constant evaluation.
            foreach (var compiled in CompiledTools())
                Assert.Contains(tools, t => t with { Where = compiled.Where } == compiled);
        }

        [Fact]
        public void ScreenshotTools_CanWriteAFile_SoAreNotReadOnlyOrIdempotent()
        {
            var screenshots = SourceTools().Where(t => t.Name?.StartsWith("screenshot-", StringComparison.Ordinal) == true).ToList();

            Assert.Equal(new[] { "screenshot-camera", "screenshot-isolated", "screenshot-viewport" }, screenshots.Select(t => t.Name).OrderBy(n => n, StringComparer.Ordinal));
            foreach (var tool in screenshots)
            {
                Assert.False(tool.ReadOnlyHint, tool.Name);
                Assert.False(tool.IdempotentHint, tool.Name);
                Assert.False(tool.DestructiveHint, tool.Name); // it never overwrites
                Assert.False(tool.OpenWorldHint, tool.Name);
            }
        }

        static void AssertAnnotated(IEnumerable<ToolAnnotations> tools)
        {
            var problems = new List<string>();
            foreach (var tool in tools)
            {
                var id = $"{tool.Where} ({tool.Name})";
                if (string.IsNullOrWhiteSpace(tool.Name))
                    problems.Add($"{id}: tool name is not a constant string");
                if (string.IsNullOrWhiteSpace(tool.Title))
                    problems.Add($"{id}: missing Title");
                if (tool.ReadOnlyHint == null) problems.Add($"{id}: ReadOnlyHint not set explicitly");
                if (tool.DestructiveHint == null) problems.Add($"{id}: DestructiveHint not set explicitly");
                if (tool.IdempotentHint == null) problems.Add($"{id}: IdempotentHint not set explicitly");
                if (tool.OpenWorldHint == null) problems.Add($"{id}: OpenWorldHint not set explicitly");
                if (tool.ReadOnlyHint == true && tool.DestructiveHint == true)
                    problems.Add($"{id}: ReadOnlyHint and DestructiveHint are both true");
            }
            Assert.True(problems.Count == 0, "Tool annotation problems:\n" + string.Join("\n", problems));
        }

        /// <summary>The addon tools compiled into this host (test fakes in this namespace excluded).</summary>
        static List<ToolAnnotations> CompiledTools() => typeof(Tool_Ping).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(typeof(ToolAnnotationsTests).Namespace!, StringComparison.Ordinal) != true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => (method, a: method.GetCustomAttribute<AiToolAttribute>()))
                .Where(x => x.a != null)
                .Select(x => new ToolAnnotations($"{type.Name}.{x.method.Name}", x.a!.Name, x.a.Title,
                    x.a.ReadOnlyHintValue, x.a.DestructiveHintValue, x.a.IdempotentHintValue, x.a.OpenWorldHintValue)))
            .ToList();

        /// <summary>Every <c>[AiTool]</c> in <c>addons/godot_mcp/</c> and <c>extensions/*/src/</c>, from source.</summary>
        static List<ToolAnnotations> SourceTools()
        {
            var root = RepoRoot();
            var files = Directory.EnumerateFiles(Path.Combine(root, "addons", "godot_mcp"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateDirectories(Path.Combine(root, "extensions"))
                    .Select(dir => Path.Combine(dir, "src"))
                    .Where(Directory.Exists)
                    .SelectMany(src => Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)))
                .Where(f => !Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj"));

            // TOOLS: editor tools sit behind #if TOOLS; inactive regions are trivia and would hide their attributes.
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest,
                preprocessorSymbols: new[] { "TOOLS", "GODOT_MCP_CODE_EXEC_IN_GAME" });
            var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parseOptions, f)).ToList();

            // A tool under any other symbol (#else, #if !TOOLS, a version symbol) would be disabled text, invisible below.
            var hidden = trees.Where(t => t.GetRoot().DescendantTrivia()
                    .Any(trivia => trivia.IsKind(SyntaxKind.DisabledTextTrivia) && trivia.ToString().Contains("[AiTool")))
                .Select(t => Path.GetRelativePath(root, t.FilePath)).ToList();
            Assert.True(hidden.Count == 0, "[AiTool] inside an inactive #if region (only TOOLS and GODOT_MCP_CODE_EXEC_IN_GAME " +
                "are defined here); extend the scan's symbols: " + string.Join(", ", hidden));

            // The loaded assemblies plus McpPlugin and GodotSharp (named, not left to load order) bind the attribute and its constants; missing editor-only
            // references only leave unrelated errors. This test assembly is left out: it compiles the same addon types,
            // and two copies would make every reference to them ambiguous.
            var self = typeof(ToolAnnotationsTests).Assembly;
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Concat(new[] { typeof(AiToolAttribute).Assembly, typeof(global::Godot.GodotObject).Assembly })
                .Where(a => a != self && !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => a.Location)
                .Distinct()
                .Select(location => MetadataReference.CreateFromFile(location));
            var compilation = CSharpCompilation.Create("ToolAnnotationsScan", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            var tools = new List<ToolAnnotations>();
            foreach (var tree in trees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>())
                {
                    // An unbound attribute class still carries its name, so a broken binding fails below instead of
                    // silently dropping the tool.
                    var attribute = model.GetDeclaredSymbol(method)?.GetAttributes()
                        .FirstOrDefault(a => a.AttributeClass?.Name is nameof(AiToolAttribute) or "AiTool");
                    if (attribute == null)
                        continue;

                    var ctor = attribute.ConstructorArguments;
                    var named = attribute.NamedArguments.ToDictionary(a => a.Key, a => a.Value.Value);
                    var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                    tools.Add(new ToolAnnotations($"{Path.GetRelativePath(root, tree.FilePath)}:{line}",
                        ctor.Length > 0 ? ctor[0].Value as string : null,
                        (named.GetValueOrDefault(nameof(AiToolAttribute.Title)) ?? (ctor.Length > 1 ? ctor[1].Value : null)) as string,
                        named.GetValueOrDefault(nameof(AiToolAttribute.ReadOnlyHint)) as bool?,
                        named.GetValueOrDefault(nameof(AiToolAttribute.DestructiveHint)) as bool?,
                        named.GetValueOrDefault(nameof(AiToolAttribute.IdempotentHint)) as bool?,
                        named.GetValueOrDefault(nameof(AiToolAttribute.OpenWorldHint)) as bool?));
                }
            }
            return tools;
        }

        static string RepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Godot-MCP.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
