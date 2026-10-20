/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// script-execute: the editor, a game built with the in-game opt-in (docs/ARCHITECTURE.md), and the xUnit suite.
#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using com.IvanMurzak.CodeExec;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The pure-managed half of <c>script-execute</c> (no Godot API, so it is unit-testable): mode detection, where the
    /// game assembly lives on disk, which results must be flattened before their snippet unloads, and error text.
    /// Mirrors Unity-MCP's Script.Execute.cs.
    /// </summary>
    public static class ScriptExecuteSource
    {
        /// <summary>How long one snippet may run (ms); below the server's <c>GodotMcpServerView.ToolTimeoutMs</c>.</summary>
        public const int RunTimeoutMs = 120_000;

        /// <summary>Usings every snippet gets; Godot's namespace on top of Unity-MCP's System set.</summary>
        public static readonly IReadOnlyList<string> DefaultUsings = new[]
        {
            "System", "System.Collections", "System.Collections.Generic", "System.Linq", "System.Threading.Tasks", "Godot",
        };

        /// <summary>
        /// Full mode when the code declares class <paramref name="className"/> (namespace-qualified or not) with a static
        /// method <paramref name="methodName"/>: returns its runtime type name. Null = run the code as a snippet.
        /// Code with top-level statements is always a snippet (its own helper types may reuse the name); code with only
        /// type declarations and none of them the entry point is an error, since as a snippet nothing would run.
        /// </summary>
        public static string? FullSourceEntryType(string code, string className, string methodName)
        {
            // Parsed as a script, the way the snippet writer reads it: anything but a type or namespace is snippet code.
            var script = (CompilationUnitSyntax)CSharpSyntaxTree.ParseText(code, ScriptParseOptions).GetRoot();
            if (script.Members.Any(m => !(m is BaseNamespaceDeclarationSyntax || m is BaseTypeDeclarationSyntax || m is DelegateDeclarationSyntax)))
                return null;
            var declaresTypes = false;
            foreach (var type in script.DescendantNodes(n => n is CompilationUnitSyntax || n is BaseNamespaceDeclarationSyntax || n is TypeDeclarationSyntax)
                .OfType<TypeDeclarationSyntax>())
            {
                declaresTypes = true;
                var fullName = RuntimeName(type);
                if (type.Identifier.Text != className && fullName != className)
                    continue;
                if (type.Members.OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.Text == methodName && m.Modifiers.Any(SyntaxKind.StaticKeyword)))
                    return fullName;
                throw new Exception($"Class '{className}' is declared, but it has no static method '{methodName}'.");
            }
            if (!declaresTypes)
                return null;
            throw new Exception($"'csharpCode' declares no class '{className}' and has no statements to run. " +
                "Set 'className' / 'methodName' to the entry point, or write plain statements.");
        }

        // Tool text shared by the editor (Tool_Script.Execute) and the in-game (Tool_GameScript) script-execute.
        internal const string SkillSnippetModeTail =
            " Own `using` lines, local classes and functions are allowed; " +
            "`await` works. Usings for System, LINQ and Godot are already there; an unknown type name (also one of the game's own " +
            "classes) is imported automatically when exactly one namespace has it.\n" +
            "- **Full code**: when `csharpCode` declares class `className` with a static method `methodName`, that method runs.\n" +
            "- `isMethodBody=true` forces snippet mode.\n\n";
        internal const string SkillOutputAndUsings =
            "## Output\n\n" +
            "`result` holds the value (type name and serialized value; `Success` when nothing is returned). `logs` holds what " +
            "the code printed: `Console` always; `GD.Print`, `GD.PushWarning` / `GD.PushError` when the project builds with Godot.NET.Sdk 4.5+.\n\n" +
            "## usings / include\n\n" +
            "- `usings`: extra namespaces, e.g. `[\"MyGame.Gameplay\"]`.\n";
        internal const string SkillParameters =
            "## Parameters\n\n" +
            "Parameters become the snippet's variables (or the method's arguments, in order); each has `name`, `typeName` and a value.";
        internal const string ClassNameDescription = "Full mode: the class whose static method runs (namespace-qualified or not).";
        internal const string MethodNameDescription = "Full mode: the static method to run.";
        internal const string ParametersDescription = "Parameters, each with 'name' and 'typeName': the snippet's variables or the method's arguments in order. " +
            "Leave empty when none.";
        internal const string IsMethodBodyDescription = "Force snippet mode even when the code declares 'className'.";
        internal const string UsingsDescription = "Extra namespaces for snippet mode, e.g. [\"MyGame.Gameplay\"].";

        static readonly CSharpParseOptions ScriptParseOptions = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.None, SourceCodeKind.Script);

        static string RuntimeName(TypeDeclarationSyntax type)
        {
            var name = type.Identifier.Text;
            for (var parent = type.Parent; parent != null; parent = parent.Parent)
            {
                if (parent is TypeDeclarationSyntax outer)
                    name = outer.Identifier.Text + "+" + name;
                else if (parent is BaseNamespaceDeclarationSyntax ns)
                    name = ns.Name + "." + name;
            }
            return name;
        }

        /// <summary>
        /// The folder snippet paths (<c>include</c>) are relative to: the Godot project when the process runs from an
        /// editor build (the editor itself, or a game started from it), else the folder of the game executable. An
        /// exported game has no project folder; its <c>res://</c> is inside the pack.
        /// </summary>
        public static string ProjectRoot(bool editorBuild, string globalizedResRoot, string executablePath)
        {
            if (editorBuild || string.IsNullOrEmpty(executablePath))
                return globalizedResRoot;
            return Path.GetDirectoryName(Path.GetFullPath(executablePath)) ?? globalizedResRoot;
        }

        /// <summary>
        /// The DLL an assembly loaded from memory was built to. Godot loads the game assembly from a stream (empty
        /// <c>Location</c>), so snippets compile against <c>res://.godot/mono/temp/bin/&lt;configuration&gt;/&lt;name&gt;.dll</c>:
        /// the editor's own <c>Debug</c> build, else the newest configuration. Null when there is none.
        /// </summary>
        public static string? BuiltAssemblyPath(string binRoot, string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName) || !Directory.Exists(binRoot))
                return null;
            var editorBuild = Path.Combine(binRoot, "Debug", assemblyName + ".dll");
            if (File.Exists(editorBuild))
                return editorBuild;
            return Directory.EnumerateDirectories(binRoot)
                .Select(dir => Path.Combine(dir, assemblyName + ".dll"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }

        /// <summary>
        /// A type of the snippet's own assembly (or built from one, like <c>List&lt;SnippetType&gt;</c>). Such a result is
        /// turned into text: serializing it would cache the type and keep the snippet from unloading.
        /// </summary>
        public static bool IsSnippetType(Type type)
        {
            if (type.Assembly.FullName?.StartsWith(CodeRunner.SnippetAssemblyPrefix, StringComparison.Ordinal) == true)
                return true;
            if (type.HasElementType && type.GetElementType() is { } element)
                return IsSnippetType(element);
            return type.IsGenericType && type.GetGenericArguments().Any(IsSnippetType);
        }

        /// <summary>Captured output as lines: <c>message</c> for info, <c>level: message</c> otherwise.</summary>
        public static List<string> LogLines(IEnumerable<LogEntry> logs) =>
            logs.Select(l => l.Level == "info" ? l.Message : l.Level + ": " + l.Message).ToList();

        /// <summary>The error text of a failed run: diagnostics in the snippet's coordinates, then auto-imports and output.</summary>
        public static string Describe(ExecuteResult result)
        {
            var text = result.ErrorKind == ErrorKind.Compile
                ? "Compilation failed:\n" + string.Join("\n", result.Diagnostics.Select(d => d.ToString()))
                : $"Execution failed ({result.ErrorKind}): {result.Error}";
            if (result.AutoImported.Count > 0)
                text += "\nAuto-imported: " + string.Join(", ", result.AutoImported);
            if (result.Logs.Count > 0)
                text += "\nOutput:\n" + string.Join("\n", LogLines(result.Logs));
            return text;
        }
    }
}
#endif
