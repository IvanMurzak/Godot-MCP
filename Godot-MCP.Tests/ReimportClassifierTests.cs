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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for <c>filesystem-reimport</c>'s routing:
    /// <list type="bullet">
    /// <item>issue #310 — native <c>.tscn</c> scenes were queued for import while the editor logged
    /// <c>ERROR: BUG: File queued for import, but can't be imported, importer for type '' not found.</c>;</item>
    /// <item>D32 — a brand-new <c>.png</c> (no <c>.import</c> sidecar yet) was classified native, refreshed with
    /// <c>EditorFileSystem.UpdateFile</c> (which never imports), and the tool still reported success.</item>
    /// </list>
    ///
    /// <para>
    /// Every probe (sidecar existence, the importer's extension list, the native-loader check) is injected,
    /// so the partition is fully testable without a Godot filesystem. The editor-side effect (register, then
    /// <c>ReimportFiles</c>, then verify) lives in the <c>#if TOOLS</c> handler and is verified on the
    /// headless Godot testbed.
    /// </para>
    /// </summary>
    public class ReimportClassifierTests
    {
        /// <summary>
        /// What <c>ResourceLoader.GetRecognizedExtensionsForType("")</c> returns in a real editor: the
        /// importer's extensions AND the native loaders' extensions, mixed. Spelled the way Godot does
        /// (lower-case, no dot).
        /// </summary>
        static readonly HashSet<string> NativeExtensions = new HashSet<string>(StringComparer.Ordinal)
        {
            "tscn", "tres", "res", "gd", "cs", "gdshader", "json",
        };

        static readonly string[] EditorRecognizedExtensions =
            new[] { "png", "jpg", "webp", "svg", "wav", "ogg", "glb", "ttf" }.Concat(NativeExtensions).ToArray();

        static readonly IReadOnlyDictionary<string, ImportOutcome> NoOutcomes = new Dictionary<string, ImportOutcome>();

        /// <summary>A fake project in which only the listed paths have an import sidecar.</summary>
        static Func<string, bool> ProjectWithImported(params string[] importedFiles)
        {
            var sidecars = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in importedFiles)
                sidecars.Add(ReimportClassifier.ImportSidecarPath(file));
            return path => sidecars.Contains(path);
        }

        /// <summary>The editor's native-loader probe: a native loader claims every native extension.</summary>
        static bool LoadsNatively(string path) => NativeExtensions.Contains(ReimportClassifier.ExtensionOf(path));

        static ReimportPlan Plan(IEnumerable<string> files, Func<string, bool> sidecarExists)
            => ReimportClassifier.Plan(files, sidecarExists, EditorRecognizedExtensions, LoadsNatively);

        static Dictionary<string, ImportOutcome> AllImported(params string[] paths)
        {
            var outcomes = new Dictionary<string, ImportOutcome>(StringComparer.Ordinal);
            foreach (var p in paths)
                outcomes[p] = ImportOutcome.Success();
            return outcomes;
        }

        [Fact]
        public void ImportSidecarPath_AppendsTheGodotSuffix()
        {
            Assert.Equal("res://art/hero.png.import", ReimportClassifier.ImportSidecarPath("res://art/hero.png"));
        }

        [Theory]
        [InlineData("res://art/hero.png", "png")]
        [InlineData("res://art/Hero.PNG", "png")]
        [InlineData("res://art/hero.tar.gz", "gz")]
        [InlineData("res://v1.2/readme", "")]
        [InlineData("res://LICENSE", "")]
        public void ExtensionOf_FollowsGodotGetExtension(string path, string expected)
        {
            Assert.Equal(expected, ReimportClassifier.ExtensionOf(path));
        }

        // ---- D32: a brand-new importable file ------------------------------------------------------

        [Fact]
        public void Plan_NewPngWithoutSidecar_IsNewImportable_NotNative()
        {
            // The owner's probe: a freshly written PNG has no sidecar. It must NOT be routed to UpdateFile
            // alone (which never imports it); it is an import candidate.
            var plan = Plan(new[] { "res://generated-images/cat.png" }, ProjectWithImported());

            Assert.Equal(new[] { "res://generated-images/cat.png" }, plan.NewImportable);
            Assert.Empty(plan.Native);
            Assert.Empty(plan.Importable);
        }

        [Fact]
        public void Plan_NewFile_ExtensionMatchIsCaseInsensitive_AndAcceptsDottedExtensions()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://art/HERO.PNG" },
                ProjectWithImported(),
                new[] { ".Png" },
                _ => false);

            Assert.Equal(new[] { "res://art/HERO.PNG" }, plan.NewImportable);
        }

        [Fact]
        public void Plan_NativeSceneWithoutSidecar_StaysNative_EvenThoughGodotListsItsExtension()
        {
            // GetRecognizedExtensionsForType("") contains "tscn" too — the native-loader guard keeps #310 fixed.
            var plan = Plan(new[] { "res://levels/01.tscn" }, ProjectWithImported());

            Assert.Empty(plan.NewImportable);
            Assert.Equal(new[] { "res://levels/01.tscn" }, plan.Native);
        }

        [Fact]
        public void Plan_UnknownExtensionWithoutSidecar_IsNative()
        {
            var plan = Plan(new[] { "res://notes/todo.txt", "res://bin/blob.xyz", "res://LICENSE" }, ProjectWithImported());

            Assert.Empty(plan.NewImportable);
            Assert.Equal(new[] { "res://notes/todo.txt", "res://bin/blob.xyz", "res://LICENSE" }, plan.Native);
        }

        [Fact]
        public void Plan_FileWithSidecar_IsImportable_WhateverItsExtension()
        {
            // The sidecar is authoritative — a plugin importer may own an extension the set does not list.
            var plan = Plan(new[] { "res://data/level.ldtk" }, ProjectWithImported("res://data/level.ldtk"));

            Assert.Equal(new[] { "res://data/level.ldtk" }, plan.Importable);
            Assert.Empty(plan.NewImportable);
            Assert.Empty(plan.Native);
        }

        // ---- #310: native files are never queued for import -----------------------------------------

        [Fact]
        public void Plan_NativeScenes_AreNeverQueuedForImport()
        {
            // The exact repro from #310: two native .tscn scenes, no importer, previously reported as
            // "Reimported 2 file(s)" while Godot logged the BUG error.
            var files = new[] { "res://levels/01.tscn", "res://levels/02.tscn" };

            var plan = Plan(files, ProjectWithImported());

            Assert.Empty(plan.Importable);
            Assert.Empty(plan.NewImportable);
            Assert.Equal(files, plan.Native);
        }

        [Fact]
        public void Plan_ImportedAssets_GoToReimport()
        {
            var plan = Plan(
                new[] { "res://art/hero.png", "res://audio/step.wav" },
                ProjectWithImported("res://art/hero.png", "res://audio/step.wav"));

            Assert.Equal(new[] { "res://art/hero.png", "res://audio/step.wav" }, plan.Importable);
            Assert.Empty(plan.NewImportable);
            Assert.Empty(plan.Native);
        }

        [Fact]
        public void Plan_MixedRequest_IsSplit_PreservingOrder()
        {
            var plan = Plan(
                new[]
                {
                    "res://levels/01.tscn", "res://new/b.png", "res://art/hero.png", "res://Player.gd",
                    "res://new/a.png", "res://audio/step.wav",
                },
                ProjectWithImported("res://art/hero.png", "res://audio/step.wav"));

            Assert.Equal(new[] { "res://art/hero.png", "res://audio/step.wav" }, plan.Importable);
            Assert.Equal(new[] { "res://new/b.png", "res://new/a.png" }, plan.NewImportable);
            Assert.Equal(new[] { "res://levels/01.tscn", "res://Player.gd" }, plan.Native);
        }

        [Fact]
        public void Plan_DuplicatePaths_AreCollapsed()
        {
            var plan = Plan(
                new[] { "res://art/hero.png", "res://art/hero.png", "res://a.tscn", "res://a.tscn", "res://n.png", "res://n.png" },
                ProjectWithImported("res://art/hero.png"));

            Assert.Single(plan.Importable);
            Assert.Single(plan.NewImportable);
            Assert.Single(plan.Native);
        }

        [Fact]
        public void Plan_ProbesTheSidecarPath_NotTheFileItself()
        {
            var probed = new List<string>();
            ReimportClassifier.Plan(new[] { "res://art/hero.png" }, path =>
            {
                probed.Add(path);
                return false;
            }, EditorRecognizedExtensions, LoadsNatively);

            Assert.Equal(new[] { "res://art/hero.png.import" }, probed);
        }

        [Fact]
        public void Plan_NullArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(null!, _ => true, EditorRecognizedExtensions, LoadsNatively));
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(Array.Empty<string>(), null!, EditorRecognizedExtensions, LoadsNatively));
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(Array.Empty<string>(), _ => true, null!, LoadsNatively));
            // The native-loader probe is what keeps #310 fixed — it may not be omitted.
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(Array.Empty<string>(), _ => true, EditorRecognizedExtensions, null!));
            Assert.Throws<ArgumentNullException>(() => Plan(Array.Empty<string>(), ProjectWithImported()).Describe(null!));
        }

        // ---- Sidecar verification ------------------------------------------------------------------

        [Fact]
        public void SidecarRecordsValidImport_SuccessfulImport_IsValid()
        {
            // Shape of a sidecar Godot 4.5 writes for a good PNG.
            const string text = "[remap]\n\nimporter=\"texture\"\ntype=\"CompressedTexture2D\"\n" +
                "uid=\"uid://b3k4\"\npath=\"res://.godot/imported/cat.png-abc.ctex\"\n" +
                "metadata={\n\"vram_texture\": false\n}\n\n[deps]\n\nsource_file=\"res://cat.png\"\n";

            Assert.True(ReimportClassifier.SidecarRecordsValidImport(text));
        }

        [Fact]
        public void SidecarRecordsValidImport_FailedImport_IsNotValid()
        {
            // Godot writes the sidecar even when the importer fails, and marks it 'valid=false'.
            const string text = "[remap]\r\n\r\nimporter=\"texture\"\r\ntype=\"CompressedTexture2D\"\r\n" +
                "uid=\"uid://b3k4\"\r\nvalid=false\r\n\r\n[deps]\r\n\r\nsource_file=\"res://broken.png\"\r\n";

            Assert.False(ReimportClassifier.SidecarRecordsValidImport(text));
        }

        [Fact]
        public void SidecarRecordsValidImport_ValidKeyOutsideRemap_IsIgnored()
        {
            const string text = "[remap]\nimporter=\"texture\"\n[params]\nvalid=false\n";

            Assert.True(ReimportClassifier.SidecarRecordsValidImport(text));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void SidecarRecordsValidImport_EmptySidecar_IsNotValid(string? text)
        {
            Assert.False(ReimportClassifier.SidecarRecordsValidImport(text));
        }

        [Theory]
        [InlineData("[remap]\nimporter=\"texture\"\nvalid = false\n")]
        [InlineData("[remap]\nimporter=\"texture\"\nvalid=False\n")]
        public void SidecarRecordsValidImport_ToleratesSpacingAndCase(string text)
        {
            Assert.False(ReimportClassifier.SidecarRecordsValidImport(text));
        }

        // ---- Did THIS request import the file? -----------------------------------------------------

        const string GoodSidecar = "[remap]\n\nimporter=\"texture\"\npath=\"res://.godot/imported/a.ctex\"\n";
        const string FailedSidecar = "[remap]\n\nimporter=\"texture\"\nvalid=false\n";
        const ulong RequestStarted = 1_791_000_000;

        [Fact]
        public void JudgeImport_ValidSidecarWrittenDuringTheRequest_IsImported()
        {
            Assert.True(ReimportClassifier.JudgeImport(GoodSidecar, RequestStarted + 1, RequestStarted).Imported);
            // Same second as the request start (GetModifiedTime has whole-second resolution) still counts.
            Assert.True(ReimportClassifier.JudgeImport(GoodSidecar, RequestStarted, RequestStarted).Imported);
        }

        [Fact]
        public void JudgeImport_ValidSidecarFromBeforeTheRequest_IsNotImported()
        {
            // ReimportFiles returns nothing and silently skips a file it cannot find, so an UNTOUCHED valid
            // sidecar from an earlier import must not be reported as this request's success.
            var outcome = ReimportClassifier.JudgeImport(GoodSidecar, RequestStarted - 1, RequestStarted);

            Assert.False(outcome.Imported);
            Assert.Equal(ReimportClassifier.StaleSidecarReason, outcome.Reason);
        }

        [Fact]
        public void JudgeImport_NoSidecar_IsNotImported()
        {
            var outcome = ReimportClassifier.JudgeImport(null, 0, RequestStarted);

            Assert.False(outcome.Imported);
            Assert.Equal(ReimportClassifier.NoSidecarReason, outcome.Reason);
        }

        [Fact]
        public void JudgeImport_FreshSidecarMarkedInvalid_ReportsTheImporterFailure()
        {
            var outcome = ReimportClassifier.JudgeImport(FailedSidecar, RequestStarted + 1, RequestStarted);

            Assert.False(outcome.Imported);
            Assert.Equal(ReimportClassifier.ImporterFailedReason, outcome.Reason);
        }

        [Fact]
        public void JudgeImport_UnreadableSidecar_IsNotImported_WithItsOwnReason()
        {
            // Godot's FileAccess.GetFileAsString returns "" when the read fails.
            var outcome = ReimportClassifier.JudgeImport(string.Empty, RequestStarted + 1, RequestStarted);

            Assert.False(outcome.Imported);
            Assert.Equal(ReimportClassifier.UnreadableSidecarReason, outcome.Reason);
        }

        [Fact]
        public void FailureReasons_DoNotContainTheListSeparator()
        {
            // Describe joins failed files with "; " — a reason containing it would split one entry in two.
            foreach (var reason in new[]
            {
                ReimportClassifier.NoSidecarReason, ReimportClassifier.StaleSidecarReason,
                ReimportClassifier.UnreadableSidecarReason, ReimportClassifier.ImporterFailedReason,
                ImportOutcome.NoOutcomeReason,
            })
                Assert.DoesNotContain(";", reason);
        }

        [Fact]
        public void Plan_IgnoresBlankEntriesInTheExtensionList()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://a.png", "res://noext" },
                ProjectWithImported(),
                new[] { null!, " ", "", "png" },
                _ => false);

            Assert.Equal(new[] { "res://a.png" }, plan.NewImportable);
            Assert.Equal(new[] { "res://noext" }, plan.Native);
        }

        // ---- The status string the agent reads ----------------------------------------------------

        [Fact]
        public void Describe_MixedRequest_NamesBothGroups()
        {
            var plan = Plan(new[] { "res://a.tscn", "res://art/hero.png" }, ProjectWithImported("res://art/hero.png"));

            var described = plan.Describe(AllImported("res://art/hero.png"));

            Assert.Contains("Reimported 1 file(s)", described);
            Assert.Contains("refreshed 1 native file(s)", described);
            Assert.Contains("UpdateFile", described);
            Assert.DoesNotContain("NOT imported", described);
        }

        [Fact]
        public void Describe_NativeOnlyRequest_DoesNotClaimAnImportHappened()
        {
            // This is the heart of #310: the caller must not read "Reimported 2 file(s)" for a request that
            // imported nothing.
            var plan = Plan(new[] { "res://levels/01.tscn", "res://levels/02.tscn" }, ProjectWithImported());

            var described = plan.Describe(NoOutcomes);

            Assert.DoesNotContain("Reimported", described);
            Assert.DoesNotContain("Imported", described);
            Assert.Contains("Refreshed 2 native file(s)", described);
            Assert.Contains("no importer", described);
        }

        [Fact]
        public void Describe_ImportOnlyRequest_ReportsTheImport()
        {
            var plan = Plan(new[] { "res://art/hero.png" }, ProjectWithImported("res://art/hero.png"));

            Assert.Equal("Reimported 1 file(s)", plan.Describe(AllImported("res://art/hero.png")));
        }

        [Fact]
        public void Describe_NewFileImported_NamesIt()
        {
            var plan = Plan(new[] { "res://generated-images/cat.png" }, ProjectWithImported());

            Assert.Equal(
                "Imported 1 new file(s): res://generated-images/cat.png",
                plan.Describe(AllImported("res://generated-images/cat.png")));
        }

        [Fact]
        public void Describe_NewFileNotImported_LeadsWithTheFailureAndReason_AndNeverClaimsSuccess()
        {
            // D32: the tool must never answer success for a file that was not imported.
            var plan = Plan(new[] { "res://generated-images/broken.png", "res://ok.png" }, ProjectWithImported());
            var outcomes = new Dictionary<string, ImportOutcome>(StringComparer.Ordinal)
            {
                ["res://generated-images/broken.png"] = ImportOutcome.Failure("the importer failed"),
                ["res://ok.png"] = ImportOutcome.Success(),
            };

            var described = plan.Describe(outcomes);

            Assert.StartsWith("NOT imported 1 file(s): res://generated-images/broken.png (the importer failed)", described);
            Assert.Contains("Imported 1 new file(s): res://ok.png", described);
            Assert.DoesNotContain("broken.png", described.Substring(described.IndexOf("Imported 1 new", StringComparison.Ordinal)));
        }

        [Fact]
        public void Describe_RoutedFileWithoutAnOutcome_IsReportedAsNotImported()
        {
            // Without outcomes nothing was observed, so nothing may be claimed — for new AND existing imports.
            var plan = Plan(new[] { "res://new.png", "res://art/hero.png" }, ProjectWithImported("res://art/hero.png"));

            var described = plan.Describe(NoOutcomes);

            Assert.StartsWith("NOT imported 2 file(s)", described);
            Assert.Contains("res://new.png (" + ImportOutcome.NoOutcomeReason + ")", described);
            Assert.Contains("res://art/hero.png (" + ImportOutcome.NoOutcomeReason + ")", described);
            Assert.DoesNotContain("Reimported", described);
        }

        [Fact]
        public void Describe_FailedReimportOfAnExistingAsset_IsReported()
        {
            var plan = Plan(new[] { "res://art/hero.png" }, ProjectWithImported("res://art/hero.png"));
            var outcomes = new Dictionary<string, ImportOutcome>(StringComparer.Ordinal)
            {
                ["res://art/hero.png"] = ImportOutcome.Failure("valid=false"),
            };

            Assert.Equal("NOT imported 1 file(s): res://art/hero.png (valid=false)", plan.Describe(outcomes));
        }

        [Fact]
        public void ImportOutcome_FailureWithoutReason_FallsBackToTheDefaultReason()
        {
            var outcome = ImportOutcome.Failure("  ");

            Assert.False(outcome.Imported);
            Assert.Equal(ImportOutcome.NoOutcomeReason, outcome.Reason);
        }
    }
}
