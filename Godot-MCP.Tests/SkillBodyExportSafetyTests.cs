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
using System.Text;
using System.Text.RegularExpressions;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Export-build safety for the C# samples embedded in <c>[AiSkillBody]</c> markdown.
    ///
    /// <para>
    /// WHY THIS EXISTS: an <c>[AiSkillBody]</c> sample is a copy-me template — it is written into a
    /// generated <c>SKILL.md</c> precisely so an AI agent reproduces it verbatim through
    /// <c>godot-skill-create</c>. Godot compiles EVERY <c>.cs</c> under a project into ONE assembly, and
    /// <c>EditorInterface</c> / <c>EditorPlugin</c> / <c>EditorFileSystem</c> / <c>EditorScript</c> exist
    /// only in an editor build, so a sample that touches them without <c>#if TOOLS</c> hands the consumer
    /// code that BREAKS THEIR EXPORT BUILD. That regression shipped once (the sample called
    /// <c>EditorInterface</c> unguarded while its own prose, 20 lines further down, required the guard).
    /// </para>
    ///
    /// <para>
    /// The addon's own shipping sources are covered by <c>scripts/check-runtime-boundary.py</c> — but that
    /// guard blanks string literals before it scans (otherwise the sample's own text would trip it), so a
    /// sample living INSIDE a string is invisible to it by construction. This suite closes exactly that
    /// hole: it reads each skill body's real runtime value by reflection, extracts the fenced C# blocks,
    /// and applies the boundary rule to the sample code itself.
    /// </para>
    /// </summary>
    public class SkillBodyExportSafetyTests
    {
        /// <summary>
        /// Editor-only Godot APIs a sample must not use outside <c>#if TOOLS</c>. Kept identical to
        /// <c>EDITOR_TOKENS</c> in <c>scripts/check-runtime-boundary.py</c> — one rule, two enforcement points.
        /// </summary>
        static readonly string[] EditorOnlyApis = { "EditorInterface", "EditorPlugin", "EditorFileSystem", "EditorScript" };

        static readonly Regex EditorApiRe = new(@"\b(" + string.Join("|", EditorOnlyApis) + @")\b", RegexOptions.Compiled);

        /// <summary>Fenced C# blocks inside a markdown body (```csharp / ```cs / ```c#).</summary>
        static readonly Regex FenceRe = new(@"```(?:csharp|cs|c\#)\r?\n(.*?)```", RegexOptions.Singleline | RegexOptions.Compiled);

        // ---------------------------------------------------------------------------------------------
        // The checks
        // ---------------------------------------------------------------------------------------------

        [Fact]
        public void EverySkillBodySample_GuardsEditorOnlyApisWithIfTools()
        {
            var bodies = SkillBodies();
            Assert.True(bodies.Count > 0,
                "No [AiSkillBody] methods were found by reflection — this suite would pass vacuously. " +
                "Did the skill tools move out of the Godot-MCP.Tests compile set?");

            var offenders = new List<string>();
            var samplesSeen = 0;

            foreach (var (owner, body) in bodies)
            {
                foreach (Match fence in FenceRe.Matches(body))
                {
                    samplesSeen++;
                    foreach (var violation in FindUnguardedEditorApiUsages(fence.Groups[1].Value))
                        offenders.Add($"{owner}: {violation}");
                }
            }

            Assert.True(samplesSeen > 0,
                "No fenced C# sample was found in any [AiSkillBody] — the scan matched nothing, so a " +
                "regression could not be detected. Check the fence marker (```csharp).");

            Assert.True(offenders.Count == 0,
                "An [AiSkillBody] teaching sample uses an editor-only Godot API OUTSIDE `#if TOOLS`. " +
                "Godot compiles every .cs into one assembly, so an agent copying the sample verbatim " +
                "breaks the consumer's EXPORT build. Wrap the sample (and say why in its comments):\n  " +
                string.Join("\n  ", offenders));
        }

        [Fact]
        public void TheGodotSkillCreateSample_IsGuarded_AndSaysWhy()
        {
            // The specific sample the shipped regression was in — pinned by name so a rewrite that drops
            // the guard fails here with an unambiguous message, and so the "explains why" half of the fix
            // (a reader copying the template must understand the constraint) cannot be silently deleted.
            var sample = Assert.Single(FenceRe.Matches(Tool_Skills.SkillsCreateSkillBody).Cast<Match>()).Groups[1].Value;

            Assert.Contains("#if TOOLS", sample);
            Assert.Contains("#endif", sample);
            Assert.Empty(FindUnguardedEditorApiUsages(sample));

            // The guard must be explained, not merely present: the sample is read by an agent that will
            // decide whether its OWN new tool needs the guard.
            var rationale = sample.Substring(0, sample.IndexOf("#if TOOLS", StringComparison.Ordinal));
            Assert.Contains("export", rationale, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("TOOLS", rationale, StringComparison.Ordinal);
        }

        [Fact]
        public void EverySkillBodyInTheAddonIsReachableByThisSuite()
        {
            // Coverage guard. The scan above reflects over the sources Godot-MCP.Tests compiles, which is a
            // CURATED SUBSET of the addon. A new [AiSkillBody] added to a file outside that subset would be
            // invisible and this suite would keep reporting green — the same vacuous-gate failure mode the
            // sample regression itself had. Count the declaration sites on disk and require a match.
            var addonRoot = FindRepoDir("addons/godot_mcp");
            Assert.True(addonRoot != null, "Could not locate addons/godot_mcp from the test assembly.");

            var declaringFiles = Directory
                .EnumerateFiles(addonRoot!, "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains("[AiSkillBody("))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Assert.True(declaringFiles.Count > 0, "No [AiSkillBody(...)] declaration found in the addon sources.");
            Assert.True(declaringFiles.Count == SkillBodies().Count,
                $"The addon declares [AiSkillBody] in {declaringFiles.Count} file(s) but reflection found " +
                $"{SkillBodies().Count} — some skill body is not compiled into Godot-MCP.Tests, so its sample " +
                "is UNCHECKED. Add the file to Godot-MCP.Tests.csproj's <Compile Include> list:\n  " +
                string.Join("\n  ", declaringFiles.Select(Path.GetFileName)));
        }

        [Fact]
        public void TheAnalyzerIsNonVacuous_ItFlagsAnUnguardedSampleAndAcceptsTheGuardedOne()
        {
            // Negative control: the exact defect that shipped, as the analyzer sees it. Without this, a
            // broken analyzer (wrong token list, wrong directive handling) would report a clean bill of
            // health on genuinely unsafe samples.
            const string unguarded =
                "using Godot;\n" +
                "public partial class Tool_Sample\n" +
                "{\n" +
                "    public void Run() => EditorInterface.Singleton.GetEditedSceneRoot();\n" +
                "}\n";
            Assert.NotEmpty(FindUnguardedEditorApiUsages(unguarded));

            const string guarded =
                "#if TOOLS\n" +
                "using Godot;\n" +
                "public partial class Tool_Sample\n" +
                "{\n" +
                "    public void Run() => EditorInterface.Singleton.GetEditedSceneRoot();\n" +
                "}\n" +
                "#endif\n";
            Assert.Empty(FindUnguardedEditorApiUsages(guarded));

            // The `#else` arm of an `#if TOOLS` is NOT the editor build — it must still be flagged.
            const string elseArm =
                "#if TOOLS\n" +
                "// editor build\n" +
                "#else\n" +
                "public void Run() => EditorInterface.Singleton.GetEditedSceneRoot();\n" +
                "#endif\n";
            Assert.NotEmpty(FindUnguardedEditorApiUsages(elseArm));

            // A mention in a comment or a string is prose, not a call — it must NOT be flagged, or the
            // guard becomes noise and gets disabled. (The shipped sample's own rationale comment names
            // EditorInterface, outside the guard, on purpose.)
            const string proseOnly =
                "// This sample drives EditorInterface, so the whole file is guarded.\n" +
                "const string Hint = \"call EditorInterface.Singleton on the main thread\";\n";
            Assert.Empty(FindUnguardedEditorApiUsages(proseOnly));
        }

        // ---------------------------------------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------------------------------------

        /// <summary>Every <c>[AiSkillBody]</c> value compiled into this test assembly, with its owner.</summary>
        static List<(string Owner, string Body)> SkillBodies()
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var found = new List<(string, string)>();

            foreach (var type in typeof(Tool_Skills).Assembly.GetTypes())
            {
                foreach (var method in type.GetMethods(flags))
                {
                    var attr = method.GetCustomAttribute<AiSkillBodyAttribute>();
                    if (attr == null || string.IsNullOrWhiteSpace(attr.Body))
                        continue;

                    found.Add(($"{type.Name}.{method.Name}", attr.Body));
                }
            }

            return found;
        }

        /// <summary>
        /// Report every editor-only API token in <paramref name="sample"/> that is NOT inside an active
        /// <c>#if TOOLS</c> region. Comments and string/char literals are blanked first (the same
        /// discipline <c>scripts/check-runtime-boundary.py</c> uses) so prose naming an API is not a hit.
        /// </summary>
        static List<string> FindUnguardedEditorApiUsages(string sample)
        {
            var code = BlankNonCode(sample);
            var violations = new List<string>();

            // One frame per open `#if`; the value is "this arm is compiled only when TOOLS is defined".
            var guards = new List<bool>();
            var lines = code.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].TrimStart();

                if (trimmed.StartsWith("#if", StringComparison.Ordinal))
                {
                    guards.Add(IsToolsCondition(trimmed));
                    continue;
                }
                if (trimmed.StartsWith("#elif", StringComparison.Ordinal))
                {
                    if (guards.Count > 0) guards[guards.Count - 1] = IsToolsCondition(trimmed);
                    continue;
                }
                if (trimmed.StartsWith("#else", StringComparison.Ordinal))
                {
                    // The complement of `#if TOOLS` is the NON-editor build — never guarded.
                    if (guards.Count > 0) guards[guards.Count - 1] = false;
                    continue;
                }
                if (trimmed.StartsWith("#endif", StringComparison.Ordinal))
                {
                    if (guards.Count > 0) guards.RemoveAt(guards.Count - 1);
                    continue;
                }

                if (guards.Any(g => g))
                    continue;

                foreach (Match m in EditorApiRe.Matches(lines[i]))
                    violations.Add($"line {i + 1}: unguarded `{m.Value}`");
            }

            return violations;
        }

        /// <summary>True when a <c>#if</c>/<c>#elif</c> directive compiles only where <c>TOOLS</c> is defined.</summary>
        static bool IsToolsCondition(string directive)
            => Regex.IsMatch(directive, @"\bTOOLS\b") && !Regex.IsMatch(directive, @"!\s*TOOLS\b");

        /// <summary>
        /// Replace comment and string/char-literal spans with spaces, preserving every newline so line
        /// numbers and preprocessor-directive positions survive.
        /// </summary>
        static string BlankNonCode(string source)
        {
            var sb = new StringBuilder(source.Length);
            var i = 0;

            void Skip(Func<int, bool> isEnd, int endWidth)
            {
                while (i < source.Length && !isEnd(i))
                {
                    sb.Append(source[i] == '\n' ? '\n' : ' ');
                    i++;
                }
                for (var k = 0; k < endWidth && i < source.Length; k++, i++)
                    sb.Append(source[i] == '\n' ? '\n' : ' ');
            }

            while (i < source.Length)
            {
                var c = source[i];

                if (c == '/' && i + 1 < source.Length && source[i + 1] == '/')
                {
                    Skip(k => source[k] == '\n', 0);
                    continue;
                }
                if (c == '/' && i + 1 < source.Length && source[i + 1] == '*')
                {
                    sb.Append("  ");
                    i += 2;
                    Skip(k => k + 1 < source.Length && source[k] == '*' && source[k + 1] == '/', 2);
                    continue;
                }
                if (c == '@' && i + 1 < source.Length && source[i + 1] == '"')
                {
                    sb.Append("  ");
                    i += 2;
                    // A verbatim string ends at a `"` that is not doubled.
                    Skip(k => source[k] == '"' && (k + 1 >= source.Length || source[k + 1] != '"'), 1);
                    continue;
                }
                if (c == '"' || c == '\'')
                {
                    var quote = c;
                    sb.Append(' ');
                    i++;
                    while (i < source.Length && source[i] != quote)
                    {
                        if (source[i] == '\\' && i + 1 < source.Length)
                        {
                            sb.Append("  ");
                            i += 2;
                            continue;
                        }
                        sb.Append(source[i] == '\n' ? '\n' : ' ');
                        i++;
                    }
                    if (i < source.Length) { sb.Append(' '); i++; }
                    continue;
                }

                sb.Append(c);
                i++;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Walk up from the test assembly location to find a repo-relative directory, so the on-disk scan
        /// does not depend on the runner's working directory. (Same walk as
        /// <c>SystemToolsTests.FindRepoFile</c>.) Returns null when not found.
        /// </summary>
        static string? FindRepoDir(string relativePath)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (var i = 0; i < 12 && dir != null; i++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (Directory.Exists(candidate))
                    return candidate;
            }
            return null;
        }
    }
}
