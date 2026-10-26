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
using SerializedMember = com.IvanMurzak.ReflectorNet.Model.SerializedMember;
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
            "the code printed: `Console` always; `GD.Print`, `GD.PushWarning` / `GD.PushError` when the project builds with Godot.NET.Sdk 4.5+. " +
            "Both are capped (20000 characters each, 2000 per output line; the cut is noted): return a count, a summary or a slice " +
            "rather than a whole data set. A lazy sequence (LINQ query, iterator) is enumerated, at most 1000 items (one that never yields an item would block the editor).\n\n" +
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

        /// <summary>
        /// Most characters of the result returned to the agent. Code-Exec bounds logs, errors and text values; the object
        /// this plugin's value converter returns is the one thing it cannot measure, so it is cut here with its limit.
        /// </summary>
        public const int MaxValueChars = OutputLimits.DefaultValueChars;
        /// <summary>Most items of a lazy sequence (LINQ query, iterator) enumerated for the result.</summary>
        public const int MaxSequenceItems = 1_000;

        /// <summary>Captured output (already bounded by Code-Exec) as lines: <c>message</c> for info, <c>level: message</c> otherwise.</summary>
        public static List<string> LogLines(IEnumerable<LogEntry> logs) =>
            logs.Select(l => l.Level == "info" ? l.Message : l.Level + ": " + l.Message).ToList();

        /// <summary>
        /// The result as returned: a value whose serialized form is longer than <see cref="MaxValueChars"/> becomes that form
        /// as text, clipped the way Code-Exec clips everything else (<c>... [truncated: N of M characters shown; hint]</c>).
        /// </summary>
        public static SerializedMember CapValue(SerializedMember member)
        {
            // A plain text value is measured and cut as text; anything else as its serialized form.
            string full;
            if (member.fields == null && member.props == null && member.valueJsonElement is { ValueKind: System.Text.Json.JsonValueKind.String } element)
                full = element.GetString() ?? "";
            else
            {
                try { full = System.Text.Json.JsonSerializer.Serialize(member, CapJson); }
                catch (Exception) { return member; } // not measurable as JSON: leave it to the transport
            }
            return full.Length <= MaxValueChars
                ? member
                : TextMember(member.name, member.typeName, OutputLimits.Clip(full, MaxValueChars, "return a count, a summary or a slice"));
        }

        /// <summary>A result member holding <paramref name="text"/> as its value.</summary>
        public static SerializedMember TextMember(string? name, string typeName, string? text) =>
            new SerializedMember { name = name, typeName = typeName }.SetJsonValue(System.Text.Json.JsonSerializer.Serialize(text));

        static readonly System.Text.Json.JsonSerializerOptions CapJson = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };

        /// <summary>
        /// A lazy sequence: enumerable, but neither a string nor a collection (a LINQ query, an iterator). Its own members
        /// (Current, the source iterator) say nothing about its items, so it is enumerated first (<see cref="Materialize"/>).
        /// </summary>
        public static bool IsLazySequence(Type type) =>
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            && type != typeof(string)
            && !typeof(System.Collections.ICollection).IsAssignableFrom(type)
            && !type.GetInterfaces().Any(i => i.IsGenericType && (i.GetGenericTypeDefinition() == typeof(ICollection<>) || i.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>)));

        /// <summary>
        /// The first <see cref="MaxSequenceItems"/> items of a sequence (an endless one stops there, with a last item saying so),
        /// each passed through <paramref name="item"/> (the caller's per-value rules); items of the snippet's own types become
        /// their text, as a snippet-typed result does (<see cref="IsSnippetType"/>).
        /// </summary>
        public static List<object?> Materialize(System.Collections.IEnumerable sequence, Func<object?, object?>? item = null)
        {
            var items = new List<object?>();
            var e = sequence.GetEnumerator();
            try
            {
                // Past the cap only one more item is asked for, to tell whether the sequence has more.
                while (items.Count < MaxSequenceItems && e.MoveNext())
                {
                    var value = e.Current;
                    items.Add(value != null && IsSnippetType(value.GetType()) ? value.ToString() : item != null ? item(value) : value);
                }
                if (items.Count == MaxSequenceItems && e.MoveNext())
                    items.Add($"… (the first {MaxSequenceItems} items; the sequence has more)");
            }
            finally { (e as IDisposable)?.Dispose(); }
            return items;
        }

        // Godot value types the reflector has no converter for (it would print "{}"): their ToString text is the value.
        static readonly HashSet<string> GodotTextValues = new HashSet<string>(StringComparer.Ordinal)
        {
            "Godot.Basis", "Godot.Transform2D", "Godot.Transform3D", "Godot.Quaternion", "Godot.Aabb", "Godot.Rect2", "Godot.Rect2I",
            "Godot.Plane", "Godot.Projection", "Godot.Vector2I", "Godot.Vector3I", "Godot.Vector4", "Godot.Vector4I", "Godot.Rid",
            "Godot.StringName",
        };

        /// <summary>A Godot value returned as its text (e.g. a Basis as "[X: (1, 0, 0), Y: …]").</summary>
        public static bool IsGodotTextValue(Type type) => type.FullName != null && GodotTextValues.Contains(type.FullName);

        /// <summary>
        /// The error text of a failed run (every part bounded by Code-Exec): diagnostics in the snippet's coordinates, then
        /// auto-imports, what Code-Exec cut, and output.
        /// </summary>
        public static string Describe(ExecuteResult result)
        {
            var text = result.ErrorKind == ErrorKind.Compile
                ? "Compilation failed:\n" + string.Join("\n", result.Diagnostics.Select(d => d.ToString()))
                : $"Execution failed ({result.ErrorKind}): {result.Error}";
            if (result.AutoImported.Count > 0)
                text += "\nAuto-imported: " + string.Join(", ", result.AutoImported);
            if (result.Truncated.Count > 0)
                text += "\nTruncated: " + string.Join(", ", result.Truncated);
            if (result.Logs.Count > 0)
                text += "\nOutput:\n" + string.Join("\n", LogLines(result.Logs));
            return text;
        }
    }
}
#endif
