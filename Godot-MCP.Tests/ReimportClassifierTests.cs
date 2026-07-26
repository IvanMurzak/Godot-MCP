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
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for issue #310 — <c>filesystem-reimport</c> reporting success for native <c>.tscn</c>
    /// scenes while the editor logged
    /// <c>ERROR: BUG: File queued for import, but can't be imported, importer for type '' not found.</c>
    ///
    /// <para>
    /// The decision is "does a <c>&lt;file&gt;.import</c> sidecar exist", so the sidecar probe is injected
    /// and the partition is fully testable without a Godot filesystem. The editor-side effect (routing the
    /// native group through <c>EditorFileSystem.UpdateFile</c> instead of <c>ReimportFiles</c>) lives in the
    /// <c>#if TOOLS</c> handler and is verified by the headless Godot smoke.
    /// </para>
    /// </summary>
    public class ReimportClassifierTests
    {
        /// <summary>A fake project in which only the listed paths have an import sidecar.</summary>
        static Func<string, bool> ProjectWithImported(params string[] importedFiles)
        {
            var sidecars = new HashSet<string>(StringComparer.Ordinal);
            foreach (var file in importedFiles)
                sidecars.Add(ReimportClassifier.ImportSidecarPath(file));
            return path => sidecars.Contains(path);
        }

        [Fact]
        public void ImportSidecarPath_AppendsTheGodotSuffix()
        {
            Assert.Equal("res://art/hero.png.import", ReimportClassifier.ImportSidecarPath("res://art/hero.png"));
        }

        [Fact]
        public void Plan_NativeScenes_AreNeverQueuedForImport()
        {
            // The exact repro from #310: two native .tscn scenes, no importer, previously reported as
            // "Reimported 2 file(s)" while Godot logged the BUG error.
            var files = new[] { "res://levels/01.tscn", "res://levels/02.tscn" };

            var plan = ReimportClassifier.Plan(files, ProjectWithImported());

            Assert.Empty(plan.Importable);
            Assert.Equal(files, plan.Native);
        }

        [Fact]
        public void Plan_ImportedAssets_GoToReimport()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://art/hero.png", "res://audio/step.wav" },
                ProjectWithImported("res://art/hero.png", "res://audio/step.wav"));

            Assert.Equal(new[] { "res://art/hero.png", "res://audio/step.wav" }, plan.Importable);
            Assert.Empty(plan.Native);
        }

        [Fact]
        public void Plan_MixedRequest_IsSplit_PreservingOrder()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://levels/01.tscn", "res://art/hero.png", "res://Player.gd", "res://audio/step.wav" },
                ProjectWithImported("res://art/hero.png", "res://audio/step.wav"));

            Assert.Equal(new[] { "res://art/hero.png", "res://audio/step.wav" }, plan.Importable);
            Assert.Equal(new[] { "res://levels/01.tscn", "res://Player.gd" }, plan.Native);
        }

        [Fact]
        public void Plan_DuplicatePaths_AreCollapsed()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://art/hero.png", "res://art/hero.png", "res://a.tscn", "res://a.tscn" },
                ProjectWithImported("res://art/hero.png"));

            Assert.Single(plan.Importable);
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
            });

            Assert.Equal(new[] { "res://art/hero.png.import" }, probed);
        }

        [Fact]
        public void Plan_NullArguments_AreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(null!, _ => true));
            Assert.Throws<ArgumentNullException>(() => ReimportClassifier.Plan(Array.Empty<string>(), null!));
        }

        // ---- The status string the agent reads ----------------------------------------------------

        [Fact]
        public void Describe_MixedRequest_NamesBothGroups()
        {
            var plan = ReimportClassifier.Plan(
                new[] { "res://a.tscn", "res://art/hero.png" },
                ProjectWithImported("res://art/hero.png"));

            var described = plan.Describe();

            Assert.Contains("Reimported 1 file(s)", described);
            Assert.Contains("refreshed 1 native file(s)", described);
            Assert.Contains("UpdateFile", described);
        }

        [Fact]
        public void Describe_NativeOnlyRequest_DoesNotClaimAnImportHappened()
        {
            // This is the heart of #310: the caller must not read "Reimported 2 file(s)" for a request that
            // imported nothing.
            var plan = ReimportClassifier.Plan(
                new[] { "res://levels/01.tscn", "res://levels/02.tscn" },
                ProjectWithImported());

            var described = plan.Describe();

            Assert.DoesNotContain("Reimported", described);
            Assert.Contains("Refreshed 2 native file(s)", described);
            Assert.Contains("no importer", described);
        }

        [Fact]
        public void Describe_ImportOnlyRequest_ReportsTheImport()
        {
            var plan = ReimportClassifier.Plan(new[] { "res://art/hero.png" }, ProjectWithImported("res://art/hero.png"));

            Assert.Equal("Reimported 1 file(s)", plan.Describe());
        }
    }
}
