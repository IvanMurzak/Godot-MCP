/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_FileSystem
    {
        public const string FileSystemReimportToolId = "filesystem-reimport";

        // Bounded settle tunables. A short Thread.Sleep on the editor main thread yields without re-entrancy
        // issues; a wait that needs the editor to run FRAMES (a scan's results are applied on the main loop)
        // sleeps on the caller's thread instead, between main-thread probes.
        const int ReimportSleepMs = 25;
        const int ReimportMaxWaits = 200;          // 200 * 25ms = 5s settle ceiling
        const int ReimportPrimeWaits = 40;         // 40 * 25ms = 1s ceiling to observe the scan START
        const int RegisterPollMs = 50;
        const int RegisterMaxPolls = 200;          // 200 * 50ms = 10s ceiling for a new file to become known

        [AiTool
        (
            FileSystemReimportToolId,
            Title = "FileSystem / Reimport",
            IdempotentHint = true
        )]
        [Description("Re-scan the Godot project's res:// filesystem and/or refresh specific files, then " +
            "wait for the import to settle before returning. The Godot analog of Unity's AssetDatabase.Refresh. " +
            "Two modes:\n" +
            "  - Pass 'files' (a list of res:// paths) to refresh exactly those files — use this after editing " +
            "a file's bytes outside the editor, or after adding a new file.\n" +
            "  - Omit 'files' (or pass an empty list) to trigger a full EditorFileSystem.Scan — use this after " +
            "adding/removing many files on disk so Godot picks up the change.\n" +
            "With 'files', each file is routed by type: an already-imported asset (it has a '.import' sidecar) " +
            "is reimported via EditorFileSystem.ReimportFiles; a NEW file with an importer extension (e.g. a " +
            ".png that has never been imported) is first registered with the editor filesystem and then " +
            "imported; Godot's NATIVE formats (.tscn/.tres/.gd/.cs/.gdshader) have no importer and are " +
            "refreshed with EditorFileSystem.UpdateFile instead — queueing them for import would make the editor " +
            "log \"importer for type '' not found\".\n" +
            "Every import is VERIFIED afterwards (a valid '.import' sidecar must exist). The returned status " +
            "lists each file that was NOT imported, with the reason, before anything else — never treat a " +
            "result that starts with 'NOT imported' as success.\n" +
            "The call blocks until the work completes (bounded), so a subsequent resource-find/get-data sees " +
            "the settled state. Returns a short status string.")]
        public string Reimport
        (
            [Description("Optional list of res:// file paths to refresh. An already-imported asset (it has a " +
                "'.import' sidecar) is reimported; a new file with an importer extension (e.g. a fresh .png) is " +
                "registered and imported for the first time; Godot's native formats (.tscn/.tres/.gd/.cs/...) " +
                "have no importer and are refreshed via EditorFileSystem.UpdateFile. " +
                "When omitted/empty, a full filesystem scan is run instead.")]
            List<string>? files = null
        )
        {
            return files != null && files.Count > 0
                ? ReimportFiles(files)
                : MainThread.Instance.Run(FullScan);
        }

        /// <summary>The targeted (<c>files</c>) form. Runs on the caller's thread, hopping to the main thread per step.</summary>
        static string ReimportFiles(List<string> files)
        {
            // Step 1 (main thread): validate, classify, refresh the native files, and register the new
            // importable ones with the editor filesystem.
            var (plan, needsRegistration) = MainThread.Instance.Run(() => StartTargetedReimport(files));

            // Step 2: a brand-new file is only importable once EditorFileSystem knows it. UpdateFile registers
            // it when its folder is already known; a file in a folder the editor has never scanned needs a
            // Scan(), whose result is applied on a later main-loop FRAME — so wait OFF the main thread,
            // probing on it, so the editor can run those frames. (When we were called ON the main thread no
            // frame can run until we return; the unregistered files are then reported as not imported.)
            if (needsRegistration && !MainThread.Instance.IsMainThread)
            {
                for (var polls = 0; polls < RegisterMaxPolls; polls++)
                {
                    var pending = MainThread.Instance.Run(() =>
                    {
                        var efs = EditorToolGuards.GetResourceFileSystemOrThrow();
                        return efs.IsScanning() || AnyUnregistered(efs, plan);
                    });
                    if (!pending)
                        break;
                    Thread.Sleep(RegisterPollMs);
                }
            }

            // Step 3 (main thread): import, verify every routed file, then drain any tail scan.
            return MainThread.Instance.Run(() => FinishTargetedReimport(plan));
        }

        static (ReimportPlan Plan, bool NeedsRegistration) StartTargetedReimport(List<string> files)
        {
            var efs = EditorToolGuards.GetResourceFileSystemOrThrow();

            // Validate every path up front so a single bad entry is a clean error, not a partial refresh.
            // Collect the normalized/trimmed paths and act on THOSE — passing the raw 'files' (which may carry
            // surrounding whitespace) would not match a known res:// file and both ReimportFiles and UpdateFile
            // would silently no-op.
            var normalized = new List<string>(files.Count);
            foreach (var f in files)
            {
                var p = ResPathNormalizer.RequireResFilePath(f, nameof(files));
                if (!FileAccess.FileExists(p))
                    throw new ArgumentException($"No file exists at '{p}'.", nameof(files));
                normalized.Add(p);
            }

            // Never queue a NATIVE file (.tscn/.tres/.gd/…) for import (issue #310), and never route a
            // brand-new importable file (no sidecar yet) to UpdateFile alone — that registers it but does not
            // import it (D32). GetRecognizedExtensionsForType("") lists every loader's extensions, the
            // importer's included; ResourceLoader.Exists filters the ones a native loader handles directly
            // (a sidecar-less .png has no loader until it is imported, a .tscn always has one).
            var plan = ReimportClassifier.Plan(
                normalized,
                FileAccess.FileExists,
                ResourceLoader.GetRecognizedExtensionsForType(string.Empty),
                p => ResourceLoader.Exists(p));

            foreach (var path in plan.Native.Concat(plan.NewImportable))
                efs.UpdateFile(path);

            // A new file whose folder the editor has never scanned stays unknown after UpdateFile — only a
            // scan registers the folder.
            var needsRegistration = AnyUnregistered(efs, plan);
            if (needsRegistration && !efs.IsScanning())
                efs.Scan();

            return (plan, needsRegistration);
        }

        static string FinishTargetedReimport(ReimportPlan plan)
        {
            var efs = EditorToolGuards.GetResourceFileSystemOrThrow();
            var outcomes = new Dictionary<string, ImportOutcome>(StringComparer.Ordinal);

            // A new file that a scan already imported needs no second import; one the editor still does not
            // know cannot be imported at all (ReimportFiles would only log "Can't find file").
            var toImport = new List<string>(plan.Importable);
            foreach (var path in plan.NewImportable)
            {
                if (VerifyImport(path).Imported)
                    outcomes[path] = ImportOutcome.Success();
                else if (!IsKnownToFileSystem(efs, path))
                    outcomes[path] = ImportOutcome.Failure(
                        "the editor filesystem never registered the file, so it could not be imported — " +
                        "its folder may not have been scanned yet; run filesystem-reimport without 'files' " +
                        "for a full scan");
                else
                    toImport.Add(path);
            }

            if (toImport.Count > 0)
                efs.ReimportFiles(toImport.ToArray());

            // Verify: an import is only claimed when a sidecar exists afterwards AND records a valid import
            // (Godot writes 'valid=false' when the importer fails, e.g. on a corrupt file).
            foreach (var path in toImport)
                outcomes[path] = VerifyImport(path);

            // ReimportFiles/UpdateFile are synchronous; only a tail scan (if any) may still be in flight. Do
            // NOT prime here — a prime that never observes a scan would falsely report "never started". Just
            // drain whatever scan is currently running (bounded).
            return DrainScan(efs, plan.Describe(outcomes));
        }

        static string FullScan()
        {
            var efs = EditorToolGuards.GetResourceFileSystemOrThrow();

            // Full scan. Scan() runs the index ASYNCHRONOUSLY, so IsScanning() can still be false on the
            // first check (the scan has not begun yet). A naive `while (IsScanning())` would exit
            // immediately and falsely report "settled" though nothing was indexed — defeating the tool's
            // purpose. So PRIME first: poll until the scan is observed running at least once (bounded),
            // and only then poll until it clears.
            efs.Scan();
            const string action = "Full filesystem scan";

            for (var primePolls = 0; primePolls < ReimportPrimeWaits && !efs.IsScanning(); primePolls++)
                Thread.Sleep(ReimportSleepMs);

            if (!efs.IsScanning())
                // The scan never started within the prime window. Report it explicitly rather than
                // silently claiming "settled" — the caller should not assume the index was refreshed.
                return $"{action}; scan did not start within {ReimportPrimeWaits * ReimportSleepMs}ms — filesystem may be unchanged or busy (NOT confirmed settled).";

            // Scan observed running; now drain it (bounded).
            return DrainScan(efs, action);
        }

        /// <summary>
        /// Wait (bounded) for any running scan to finish and suffix <paramref name="action"/> with whether the
        /// filesystem settled. Main-thread only.
        /// </summary>
        static string DrainScan(EditorFileSystem efs, string action)
        {
            for (var waits = 0; waits < ReimportMaxWaits && efs.IsScanning(); waits++)
                Thread.Sleep(ReimportSleepMs);

            return !efs.IsScanning()
                ? $"{action}; filesystem settled."
                : $"{action}; filesystem still scanning after {ReimportMaxWaits * ReimportSleepMs}ms (progress={efs.GetScanningProgress():0.00}).";
        }

        /// <summary>Whether any of the plan's new files is still unknown to <c>EditorFileSystem</c>. Main-thread only.</summary>
        static bool AnyUnregistered(EditorFileSystem efs, ReimportPlan plan)
            => plan.NewImportable.Any(p => !IsKnownToFileSystem(efs, p));

        /// <summary>Whether <c>EditorFileSystem</c> has an entry for <paramref name="resPath"/>. Main-thread only.</summary>
        static bool IsKnownToFileSystem(EditorFileSystem efs, string resPath)
        {
            var dir = efs.GetFilesystemPath(ResPathNormalizer.ParentDir(resPath));
            return dir != null && dir.FindFileIndex(GetLeafName(resPath)) >= 0;
        }

        static ImportOutcome VerifyImport(string resPath)
        {
            var sidecar = ReimportClassifier.ImportSidecarPath(resPath);
            if (!FileAccess.FileExists(sidecar))
                return ImportOutcome.Failure("Godot wrote no '.import' sidecar, so the file was not imported — see the editor log");

            return ReimportClassifier.SidecarRecordsValidImport(FileAccess.GetFileAsString(sidecar))
                ? ImportOutcome.Success()
                : ImportOutcome.Failure("Godot's importer failed (the '.import' sidecar is marked valid=false) — " +
                    "the file may be corrupt or in an unsupported format; see the editor log");
        }
    }
}
#endif
