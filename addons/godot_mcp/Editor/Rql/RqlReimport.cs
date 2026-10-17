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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// <c>editor | call Reimport(files?)</c> — the logic of the replaced <c>filesystem-reimport</c> tool, unchanged: with
    /// files, each is routed by type (an imported asset is reimported, a new importable file is registered and imported,
    /// Godot's native formats are refreshed with UpdateFile) and every import is verified; without files, a full
    /// EditorFileSystem scan. RQL calls run on the editor main thread, so a brand-new file in a folder the editor never
    /// scanned is reported as NOT imported (run Reimport() without files first) instead of waiting for editor frames.
    /// </summary>
    internal static class RqlReimport
    {
        /// <summary>Reimport <paramref name="files"/> (res:// paths), or scan the whole filesystem when null / empty.</summary>
        public static string Run(IReadOnlyList<string>? files) =>
            files != null && files.Count > 0 ? ReimportFiles(files.ToList()) : MainThread.Instance.Run(FullScan);

        // Bounded settle tunables. A short Thread.Sleep on the editor main thread yields without re-entrancy
        // issues; a wait that needs the editor to run FRAMES (a scan's results are applied on the main loop)
        // sleeps on the caller's thread instead, between main-thread probes.
        const int ReimportSleepMs = 25;
        const int ReimportMaxWaits = 200;          // 200 * 25ms = 5s settle ceiling
        const int ReimportPrimeWaits = 40;         // 40 * 25ms = 1s ceiling to observe the scan START
        const int RegisterPollMs = 50;
        const int RegisterTimeoutMs = 10_000;      // ceiling for a new file to become known to the editor

        /// <summary>The targeted (<c>files</c>) form. Runs on the caller's thread, hopping to the main thread per step.</summary>
        static string ReimportFiles(List<string> files)
        {
            // When we are already ON the main thread no frame can run until we return, so a scan started now
            // could never be applied within this call; only wait for registration when we can yield frames.
            var canWaitForFrames = !MainThread.Instance.IsMainThread;

            // Step 1 (main thread): validate, classify, refresh the native files, and register the files routed
            // to the importer with the editor filesystem.
            var (plan, startedUnix, needsRegistration) =
                MainThread.Instance.Run(() => StartTargetedReimport(files, canWaitForFrames));

            // Step 2: a file is only importable once EditorFileSystem knows it. UpdateFile registers it when its
            // folder is already known; a file in a folder the editor has never scanned needs a Scan(), whose
            // result is applied on a later main-loop FRAME — so wait OFF the main thread, probing on it, so the
            // editor can run those frames.
            if (needsRegistration && canWaitForFrames)
            {
                var rescanned = false;
                var clock = Stopwatch.StartNew();
                while (clock.ElapsedMilliseconds < RegisterTimeoutMs)
                {
                    var pending = MainThread.Instance.Run(() =>
                    {
                        var efs = Tools.EditorToolGuards.GetResourceFileSystemOrThrow();
                        if (efs.IsScanning())
                            return true;

                        // UpdateFile is a no-op while a scan runs, and a scan that walked the folder before the
                        // file was written misses it — so retry the registration once the editor is idle, and
                        // fall back to one more scan of our own.
                        RegisterRoutedFiles(efs, plan);
                        if (!AnyUnregistered(efs, plan) || rescanned)
                            return false;
                        efs.Scan();
                        rescanned = true;
                        return true;
                    });
                    if (!pending)
                        break;
                    Thread.Sleep(RegisterPollMs);
                }
            }

            // Step 3 (main thread): import, verify every routed file, then drain any tail scan.
            return MainThread.Instance.Run(() => FinishTargetedReimport(plan, startedUnix));
        }

        static (Tools.ReimportPlan Plan, ulong StartedUnix, bool NeedsRegistration) StartTargetedReimport(
            List<string> files, bool canWaitForFrames)
        {
            var efs = Tools.EditorToolGuards.GetResourceFileSystemOrThrow();

            // Every import this request claims must have rewritten its sidecar at or after this instant.
            var startedUnix = (ulong)Math.Floor(Time.GetUnixTimeFromSystem());

            // Validate every path up front so a single bad entry is a clean error, not a partial refresh.
            // Collect the normalized/trimmed paths and act on THOSE — passing the raw 'files' (which may carry
            // surrounding whitespace) would not match a known res:// file and both ReimportFiles and UpdateFile
            // would silently no-op.
            var normalized = new List<string>(files.Count);
            foreach (var f in files)
            {
                var p = Tools.ResPathNormalizer.RequireResFilePath(f, nameof(files));
                if (!FileAccess.FileExists(p))
                    throw new ArgumentException($"No file exists at '{p}'.", nameof(files));
                normalized.Add(p);
            }

            // Never queue a NATIVE file (.tscn/.tres/.gd/…) for import (issue #310), and never route a
            // brand-new importable file (no sidecar yet) to UpdateFile alone — that registers it but does not
            // import it (D32). GetRecognizedExtensionsForType("") lists every loader's extensions, the
            // importer's included; ResourceLoader.Exists filters the ones a native loader handles directly
            // (a sidecar-less .png has no loader until it is imported, a .tscn always has one).
            var plan = Tools.ReimportClassifier.Plan(
                normalized,
                FileAccess.FileExists,
                ResourceLoader.GetRecognizedExtensionsForType(string.Empty),
                p => ResourceLoader.Exists(p));

            foreach (var path in plan.Native)
                efs.UpdateFile(path);

            RegisterRoutedFiles(efs, plan);

            // A file whose folder the editor has never scanned stays unknown after UpdateFile — only a scan
            // registers the folder.
            var needsRegistration = AnyUnregistered(efs, plan);
            if (needsRegistration && canWaitForFrames && !efs.IsScanning())
                efs.Scan();

            return (plan, startedUnix, needsRegistration);
        }

        static string FinishTargetedReimport(Tools.ReimportPlan plan, ulong startedUnix)
        {
            var efs = Tools.EditorToolGuards.GetResourceFileSystemOrThrow();
            var outcomes = new Dictionary<string, Tools.ImportOutcome>(StringComparer.Ordinal);
            var newFiles = new HashSet<string>(plan.NewImportable, StringComparer.Ordinal);

            // A file the editor still does not know cannot be imported at all (ReimportFiles would only log
            // "Can't find file"); a NEW file a scan already imported during this request needs no second import.
            var toImport = new List<string>();
            foreach (var path in RoutedToImporter(plan))
            {
                if (!IsKnownToFileSystem(efs, path))
                    outcomes[path] = Tools.ImportOutcome.Failure(
                        "the editor filesystem never registered the file, so it could not be imported (its folder " +
                        "may not have been scanned yet, or the path's letter case differs from the file on disk) — " +
                        "run editor | call Reimport() without files for a full scan");
                else if (newFiles.Contains(path) && VerifyImport(path, startedUnix).Imported)
                    outcomes[path] = Tools.ImportOutcome.Success();
                else
                    toImport.Add(path);
            }

            if (toImport.Count > 0)
                efs.ReimportFiles(toImport.ToArray());

            // Verify: an import is only claimed when its sidecar was (re)written during this request AND records
            // a valid import (Godot writes 'valid=false' when the importer fails, e.g. on a corrupt file).
            foreach (var path in toImport)
                outcomes[path] = VerifyImport(path, startedUnix);

            // ReimportFiles/UpdateFile are synchronous; only a tail scan (if any) may still be in flight. Do
            // NOT prime here — a prime that never observes a scan would falsely report "never started". Just
            // drain whatever scan is currently running (bounded).
            return DrainScan(efs, plan.Describe(outcomes));
        }

        static string FullScan()
        {
            var efs = Tools.EditorToolGuards.GetResourceFileSystemOrThrow();

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

        /// <summary>The plan's files that go to the importer: the already-imported ones and the new ones.</summary>
        static IEnumerable<string> RoutedToImporter(Tools.ReimportPlan plan) => plan.Importable.Concat(plan.NewImportable);

        /// <summary>UpdateFile every importer-routed file the editor does not know yet. Main-thread only.</summary>
        static void RegisterRoutedFiles(EditorFileSystem efs, Tools.ReimportPlan plan)
        {
            foreach (var path in RoutedToImporter(plan))
            {
                if (!IsKnownToFileSystem(efs, path))
                    efs.UpdateFile(path);
            }
        }

        /// <summary>Whether any importer-routed file is still unknown to <c>EditorFileSystem</c>. Main-thread only.</summary>
        static bool AnyUnregistered(EditorFileSystem efs, Tools.ReimportPlan plan)
            => RoutedToImporter(plan).Any(p => !IsKnownToFileSystem(efs, p));

        /// <summary>Whether <c>EditorFileSystem</c> has an entry for <paramref name="resPath"/>. Main-thread only.</summary>
        static bool IsKnownToFileSystem(EditorFileSystem efs, string resPath)
        {
            var dir = efs.GetFilesystemPath(Tools.ResPathNormalizer.ParentDir(resPath));
            return dir != null && dir.FindFileIndex(GetLeafName(resPath)) >= 0;
        }

        /// <summary>Judge, from its sidecar, whether this request imported <paramref name="resPath"/>. Main-thread only.</summary>
        static Tools.ImportOutcome VerifyImport(string resPath, ulong startedUnix)
        {
            var sidecar = Tools.ReimportClassifier.ImportSidecarPath(resPath);
            return FileAccess.FileExists(sidecar)
                ? Tools.ReimportClassifier.JudgeImport(FileAccess.GetFileAsString(sidecar), FileAccess.GetModifiedTime(sidecar), startedUnix)
                : Tools.ReimportClassifier.JudgeImport(null, 0, startedUnix);
        }

        static string GetLeafName(string resPath)
        {
            var trimmed = resPath.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            return idx < 0 ? trimmed : trimmed.Substring(idx + 1);
        }
    }
}
#endif
