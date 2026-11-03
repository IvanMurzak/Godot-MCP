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
        static readonly string[] HintNames =
        {
            nameof(AiToolAttribute.ReadOnlyHint),
            nameof(AiToolAttribute.DestructiveHint),
            nameof(AiToolAttribute.IdempotentHint),
            nameof(AiToolAttribute.OpenWorldHint),
        };

        record ToolAnnotations(string Where, string? Name, string? Title, IReadOnlyDictionary<string, bool?> Hints);

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
            Assert.True(tools.Count >= 25, $"The source scan found only {tools.Count} [AiTool] attributes; the scan is broken.");
            AssertAnnotated(tools);

            // The source scan must see every tool reflection sees, with the same values: proves its constant evaluation.
            foreach (var compiled in CompiledTools())
            {
                var match = tools.FirstOrDefault(t => t.Name == compiled.Name && t.Title == compiled.Title);
                Assert.True(match != null, $"The source scan missed '{compiled.Name}' ({compiled.Where}).");
                foreach (var hint in HintNames)
                    Assert.Equal(compiled.Hints[hint], match!.Hints[hint]);
            }
        }

        static void AssertAnnotated(IEnumerable<ToolAnnotations> tools)
        {
            var problems = new List<string>();
            foreach (var tool in tools)
            {
                if (string.IsNullOrWhiteSpace(tool.Name))
                    problems.Add($"{tool.Where}: tool name is not a constant string");
                if (string.IsNullOrWhiteSpace(tool.Title))
                    problems.Add($"{tool.Where} ({tool.Name}): missing Title");
                foreach (var hint in HintNames)
                    if (tool.Hints[hint] == null)
                        problems.Add($"{tool.Where} ({tool.Name}): {hint} not set explicitly");
                if (tool.Hints[nameof(AiToolAttribute.ReadOnlyHint)] == true && tool.Hints[nameof(AiToolAttribute.DestructiveHint)] == true)
                    problems.Add($"{tool.Where} ({tool.Name}): ReadOnlyHint and DestructiveHint are both true");
            }
            Assert.True(problems.Count == 0, "Tool annotation problems:\n" + string.Join("\n", problems));
        }

        /// <summary>The addon tools compiled into this host (test fakes in this namespace excluded).</summary>
        static List<ToolAnnotations> CompiledTools() => typeof(Tool_Ping).Assembly.GetTypes()
            .Where(type => type.Namespace?.StartsWith(typeof(ToolAnnotationsTests).Namespace!, StringComparison.Ordinal) != true)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Select(method => (method, attribute: method.GetCustomAttribute<AiToolAttribute>()))
                .Where(x => x.attribute != null)
                .Select(x => new ToolAnnotations($"{type.Name}.{x.method.Name}", x.attribute!.Name, x.attribute.Title,
                    new Dictionary<string, bool?>
                    {
                        [nameof(AiToolAttribute.ReadOnlyHint)] = x.attribute.ReadOnlyHintValue,
                        [nameof(AiToolAttribute.DestructiveHint)] = x.attribute.DestructiveHintValue,
                        [nameof(AiToolAttribute.IdempotentHint)] = x.attribute.IdempotentHintValue,
                        [nameof(AiToolAttribute.OpenWorldHint)] = x.attribute.OpenWorldHintValue,
                    })))
            .ToList();

        /// <summary>Every <c>[AiTool]</c> in <c>addons/godot_mcp/</c> and <c>extensions/*/src/</c>, from source.</summary>
        static List<ToolAnnotations> SourceTools()
        {
            var root = FindRepoRoot();
            var files = Directory.EnumerateFiles(Path.Combine(root, "addons", "godot_mcp"), "*.cs", SearchOption.AllDirectories)
                .Concat(Directory.EnumerateDirectories(Path.Combine(root, "extensions"))
                    .Select(dir => Path.Combine(dir, "src"))
                    .Where(Directory.Exists)
                    .SelectMany(src => Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)))
                .ToList();

            // TOOLS: editor tools sit behind #if TOOLS; inactive regions are trivia and would hide their attributes.
            var parseOptions = new CSharpParseOptions(LanguageVersion.Latest,
                preprocessorSymbols: new[] { "TOOLS", "GODOT_MCP_CODE_EXEC_IN_GAME" });
            var trees = files.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), parseOptions, f)).ToList();

            // Constant folding needs only the declaring types, which are all in source; the loaded assemblies cover the rest
            // (missing editor-only references just leave unrelated errors). This test assembly is left out: it compiles
            // the same addon types, and two copies would make every reference to them ambiguous.
            var self = typeof(ToolAnnotationsTests).Assembly;
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => a != self && !a.IsDynamic && !string.IsNullOrEmpty(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location));
            var compilation = CSharpCompilation.Create("ToolAnnotationsScan", trees, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

            var tools = new List<ToolAnnotations>();
            foreach (var tree in trees)
            {
                var model = compilation.GetSemanticModel(tree);
                foreach (var attribute in tree.GetRoot().DescendantNodes().OfType<AttributeSyntax>())
                {
                    var name = attribute.Name.ToString();
                    if (name != "AiTool" && name != "AiToolAttribute" && !name.EndsWith(".AiTool", StringComparison.Ordinal)
                        && !name.EndsWith(".AiToolAttribute", StringComparison.Ordinal))
                        continue;

                    var args = attribute.ArgumentList?.Arguments ?? default;
                    var positional = args.Where(a => a.NameEquals == null && a.NameColon == null).ToList();
                    object? Named(string argName)
                    {
                        var arg = args.FirstOrDefault(a => a.NameEquals?.Name.Identifier.Text == argName
                            || a.NameColon?.Name.Identifier.Text == argName.ToLowerInvariant());
                        return arg == null ? null : Constant(model, arg.Expression);
                    }

                    var toolName = Named("name") ?? (positional.Count > 0 ? Constant(model, positional[0].Expression) : null);
                    var title = Named("Title") ?? (positional.Count > 1 ? Constant(model, positional[1].Expression) : null);
                    var line = tree.GetLineSpan(attribute.Span).StartLinePosition.Line + 1;
                    tools.Add(new ToolAnnotations($"{Path.GetRelativePath(root, tree.FilePath)}:{line}", toolName as string, title as string,
                        HintNames.ToDictionary(h => h, h => Named(h) as bool?)));
                }
            }
            return tools;
        }

        static object? Constant(SemanticModel model, ExpressionSyntax expression)
        {
            var value = model.GetConstantValue(expression);
            return value.HasValue ? value.Value : null;
        }

        static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
            {
                if (Directory.Exists(Path.Combine(dir.FullName, "addons", "godot_mcp"))
                    && Directory.Exists(Path.Combine(dir.FullName, "extensions")))
                    return dir.FullName;
            }
            throw new DirectoryNotFoundException("Godot-MCP repo root (addons/godot_mcp + extensions) not found above " + AppContext.BaseDirectory);
        }
    }
}
