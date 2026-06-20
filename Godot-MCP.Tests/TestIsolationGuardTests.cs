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
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Durable drift-guard for issue #195: a meta-test that scans the test project's own <c>.cs</c> SOURCE
    /// files and FAILS if any test class WRITES one of the known process-wide-static mutators yet lacks a
    /// <c>[Collection(...)]</c> attribute. Without serialization those writers run in xUnit's default parallel
    /// pool and can clobber a sibling class that snapshots the same static — the exact flaky-test race this
    /// task hardened.
    ///
    /// <para>
    /// WHY SOURCE-SCAN, NOT REFLECTION: reflection sees a type's members (methods/fields), but NOT the IL
    /// bodies of those methods, so it cannot tell whether a method assigns <c>SomeStatic.Current = ...</c>.
    /// A robust check therefore reads the test <c>.cs</c> files at test time and regex-matches the static
    /// writes, mapping each to its enclosing class. The files are reliably reachable: <see cref="ThisDir"/> is
    /// resolved from <c>[CallerFilePath]</c> (baked in at compile time, valid in the CI checkout where
    /// <c>dotnet test</c> runs from the same tree). This needs no Godot binary and is fully CI-runnable.
    /// </para>
    ///
    /// <para>
    /// The guarded statics are the four <c>*.Current</c> setters plus the <c>RuntimeErrorCapture</c>
    /// install/uninstall state mutators (see <see cref="GuardedWritePattern"/>). A future contributor who adds
    /// a 5th unguarded mutator gets a failing test whose message names the offending class + static and the
    /// fix (join a <c>DisableParallelization = true</c> collection).
    /// </para>
    /// </summary>
    public class TestIsolationGuardTests
    {
        /// <summary>Absolute path to the directory containing THIS source file = the test project's source root.</summary>
        static string ThisDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;

        /// <summary>
        /// Matches a WRITE to one of the process-wide statics this task serialized. Anchored on an assignment
        /// (<c>=</c> not followed by <c>=</c>) so a READ (<c>var x = Foo.Current;</c>) is NOT flagged — only a
        /// mutation that creates the race. <c>RuntimeErrorCapture.Install()</c> / <c>.Uninstall()</c> are
        /// state mutators (they register/drop the engine logger + flip IsInstalled), so a call to either also
        /// counts as a mutation even without an <c>=</c>.
        /// </summary>
        static readonly Regex GuardedWritePattern = new(
            @"\b(?:GodotLogCollector|RuntimeErrorCollector|ScriptErrorCapture|GodotMcpReflector)\.Current\s*=(?!=)"
            + @"|\bRuntimeErrorCapture\.(?:Install|Uninstall)\s*\(",
            RegexOptions.Compiled);

        /// <summary>Matches a top-level <c>... class Name ...</c> declaration, capturing the class name.</summary>
        static readonly Regex ClassDeclPattern = new(
            @"^\s*(?:public|internal|sealed|abstract|static|partial|\s)*\bclass\s+([A-Za-z_][A-Za-z0-9_]*)",
            RegexOptions.Compiled);

        /// <summary>Matches a <c>[Collection(...)]</c> attribute on a class.</summary>
        static readonly Regex CollectionAttrPattern = new(@"\[\s*Collection\s*\(", RegexOptions.Compiled);

        /// <summary>Matches an xUnit test method marker so we only enforce on real test classes.</summary>
        static readonly Regex FactOrTheoryPattern = new(@"\[\s*(?:Fact|Theory)\b", RegexOptions.Compiled);

        [Fact]
        public void EveryProcessWideStaticMutator_RunsInASerialCollection()
        {
            var dir = ThisDir();
            Assert.True(Directory.Exists(dir), $"Test source dir not found: {dir}");

            var files = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
                // Skip generated build output if any slipped into the tree.
                .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
                .OrderBy(f => f, StringComparer.Ordinal)
                .ToList();

            Assert.NotEmpty(files);

            var violations = new List<string>();
            var sawAnyGuardedWrite = false;

            foreach (var file in files)
            {
                foreach (var cls in EnumerateClasses(file))
                {
                    if (!cls.HasGuardedWrite)
                        continue;

                    sawAnyGuardedWrite = true;

                    // Only test classes (those with [Fact]/[Theory]) participate in xUnit parallelization, so
                    // only they can race. A plain helper/model class with a guarded write is not a test class.
                    if (!cls.IsTestClass)
                        continue;

                    if (!cls.HasCollectionAttribute)
                    {
                        violations.Add(
                            $"  - {cls.Name}  (in {Path.GetFileName(file)})  writes {cls.FirstGuardedStatic} "
                            + "but has NO [Collection(...)] attribute.");
                    }
                }
            }

            // Sanity: the scanner must actually be finding the known mutators. If this trips, the regex or the
            // source layout drifted and the guard is silently inert — fail loudly so it gets fixed.
            Assert.True(sawAnyGuardedWrite,
                "Drift-guard found ZERO process-wide-static writes across the test sources — the scanner is "
                + "likely broken (regex or source layout changed). Expected to see writes to "
                + "GodotLogCollector.Current / RuntimeErrorCollector.Current / ScriptErrorCapture.Current / "
                + "GodotMcpReflector.Current / RuntimeErrorCapture.Install|Uninstall.");

            Assert.True(violations.Count == 0,
                "Process-wide-static mutator(s) found WITHOUT a [Collection(...)] serial collection — these "
                + "run in xUnit's default parallel pool and can clobber a sibling class that snapshots the same "
                + "static (issue #195 flaky-test race). Fix: tag the class with "
                + "[Collection(\"<Static>.Current (serial)\")] joined to a "
                + "[CollectionDefinition(..., DisableParallelization = true)], snapshotting/restoring the static "
                + "in the ctor/Dispose. Offending class(es):\n" + string.Join("\n", violations));
        }

        /// <summary>
        /// Cheap brace-depth class splitter: walks the file line-by-line, tracks <c>{ }</c> nesting to attribute
        /// each line to the innermost open top-level (namespace-direct) class, and records whether that class's
        /// body contains a guarded static WRITE, a <c>[Collection]</c> attribute, and a <c>[Fact]/[Theory]</c>.
        /// Block/line comments are stripped before matching so a <c>// Foo.Current = ...</c> or a doc-comment
        /// <c>&lt;see cref="...Current"/&gt;</c> never false-positives.
        /// </summary>
        static IEnumerable<ClassScan> EnumerateClasses(string file)
        {
            var raw = File.ReadAllText(file);
            var code = StripComments(raw);
            var lines = code.Split('\n');

            var scans = new List<ClassScan>();
            // Open classes as (index-into-scans, braceDepthAtDecl, entered). `entered` flips true once brace
            // depth rises ABOVE the declaration depth (i.e. the class body's `{` was seen) — only THEN may the
            // class close when depth falls back to its declaration depth. Without the `entered` gate a class
            // whose `{` is on the NEXT line (the common `public class Foo\n{` shape) would be popped on its own
            // declaration line (depth still == decl depth), so its body would never be scanned.
            var open = new List<(int idx, int depth, bool entered)>();
            // Pending attribute lines accumulate before a class decl so the [Collection] right above the class
            // is attributed to it.
            var pendingHasCollection = false;
            int braceDepth = 0;

            foreach (var line in lines)
            {
                // Detect a class declaration on this line BEFORE counting its braces.
                var m = ClassDeclPattern.Match(line);
                if (m.Success)
                {
                    var scan = new ClassScan { Name = m.Groups[1].Value, HasCollectionAttribute = pendingHasCollection };
                    scans.Add(scan);
                    open.Add((scans.Count - 1, braceDepth, false));
                    pendingHasCollection = false;
                }
                else if (CollectionAttrPattern.IsMatch(line))
                {
                    // A [Collection(...)] attribute line preceding the next class decl.
                    pendingHasCollection = true;
                }

                // Attribute the line's content to the innermost open class (if any).
                if (open.Count > 0)
                {
                    var top = scans[open[^1].idx];
                    if (FactOrTheoryPattern.IsMatch(line))
                        top.IsTestClass = true;
                    var gw = GuardedWritePattern.Match(line);
                    if (gw.Success)
                    {
                        top.HasGuardedWrite = true;
                        top.FirstGuardedStatic ??= gw.Value.Trim();
                    }
                }

                // Update brace depth, mark classes whose body we've now entered, then close any whose body
                // brace has balanced back out.
                braceDepth += CountChar(line, '{') - CountChar(line, '}');
                for (int k = 0; k < open.Count; k++)
                {
                    if (!open[k].entered && braceDepth > open[k].depth)
                        open[k] = (open[k].idx, open[k].depth, true);
                }
                while (open.Count > 0 && open[^1].entered && braceDepth <= open[^1].depth)
                    open.RemoveAt(open.Count - 1);
            }

            return scans;
        }

        sealed class ClassScan
        {
            public string Name = "";
            public bool HasGuardedWrite;
            public bool HasCollectionAttribute;
            public bool IsTestClass;
            public string? FirstGuardedStatic;
        }

        static int CountChar(string s, char c)
        {
            int n = 0;
            foreach (var ch in s)
                if (ch == c) n++;
            return n;
        }

        /// <summary>Strips <c>/* ... */</c> block comments and <c>// ...</c> line comments (no string-literal
        /// awareness needed — the guarded patterns never appear inside a string literal in this suite, and
        /// stripping comments is what prevents doc-comment <c>&lt;see cref&gt;</c> false-positives).</summary>
        static string StripComments(string src)
        {
            // Block comments first (handles multi-line doc/banner blocks), then single-line.
            src = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\n]*", " ");
            return src;
        }
    }
}
