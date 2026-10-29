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
using System.Linq;
using System.Threading;
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
        /// <summary>Reimport <paramref name="files"/> (res:// paths), or scan the whole filesystem when null / empty. Main thread.</summary>
        public static string Run(IReadOnlyList<string>? files) =>
            files != null && files.Count > 0 ? ReimportFiles(files) : FullScan();

        // Bounded settle tunables: a short Thread.Sleep on the editor main thread yields without re-entrancy issues.
        const int ReimportSleepMs = 25;
        const int ReimportMaxWaits = 200;          // 200 * 25ms = 5s settle ceiling
        const int ReimportPrimeWaits = 40;         // 40 * 25ms = 1s ceiling to observe the scan START

        /// <summary>
        /// The targeted (<c>files</c>) form, on the main thread: validate, classify, refresh the native files, register
        /// the importer-routed ones, then import and verify. No editor frame runs during the call, so a file in a folder
        /// the editor never scanned stays unregistered and is reported as NOT imported.
        /// </summary>
        static string ReimportFiles(IReadOnlyList<string> files)
        {
            var efs = Tools.EditorToolGuards.GetResourceFileSystemOrThrow();

            // Every import this request claims must have rewritten its sidecar at or after this instant.
            var startedUnix = (ulong)Math.Floor(Time.GetUnixTimeFromSystem());

            // Validate every path up front so a single bad entry is a clean error, not a partial refresh, and act on the
            // normalized paths (a raw path with surrounding whitespace would match no known file and silently no-op).
            var normalized = new List<string>(files.Count);
            foreach (var f in files)
            {
                var p = Tools.ResPathNormalizer.RequireResFilePath(f, nameof(files));
                if (!FileAccess.FileExists(p))
                    throw new ArgumentException($"No file exists at '{p}'.", nameof(files));
                normalized.Add(p);
            }

            // Never queue a NATIVE file (.tscn/.tres/.gd/…) for import (issue #310), and never route a brand-new
            // importable file (no sidecar yet) to UpdateFile alone — that registers it but does not import it (D32).
            // GetRecognizedExtensionsForType("") lists every loader's extensions, the importer's included;
            // ResourceLoader.Exists filters the ones a native loader handles directly.
            var plan = Tools.ReimportClassifier.Plan(
                normalized,
                FileAccess.FileExists,
                ResourceLoader.GetRecognizedExtensionsForType(string.Empty),
                p => ResourceLoader.Exists(p));

            foreach (var path in plan.Native)
            {
                efs.UpdateFile(path);
                // A loaded resource (a .tres read by an earlier query) keeps its old data otherwise: refresh the cached
                // instance in place from the file, so later queries and everything holding it see the change.
                if (ResourceLoader.HasCached(path))
                    ResourceLoader.Load(path, string.Empty, ResourceLoader.CacheMode.Replace);
            }
            foreach (var path in RoutedToImporter(plan))
                if (!IsKnownToFileSystem(efs, path))
                    efs.UpdateFile(path);

            return FinishTargetedReimport(efs, plan, startedUnix);
        }

        static string FinishTargetedReimport(EditorFileSystem efs, Tools.ReimportPlan plan, ulong startedUnix)
        {
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

        /// <summary>
        /// Make the editor filesystem know a file just written to <paramref name="resPath"/>: <c>UpdateFile</c> when its
        /// folder is known, else a full scan (settled). <c>UpdateFile</c> ignores a file in a folder the editor never scanned
        /// (the <c>files</c> source, <c>godot.resources(type, folder)</c> and a GDScript <c>class_name</c> then never saw it),
        /// and a scan made before the write would not help: its result replaces the folder's listing a frame later.
        /// A second unknown-folder file in the same frame cannot get its own scan (the first one is swapped in only on the
        /// next frame, and a new Scan() is ignored until then): it is registered with <c>UpdateFile</c> once that scan
        /// lands. A folder the editor never lists (<c>.gdignore</c>, a dot-named folder) is not scanned for. Main-thread only.
        /// </summary>
        internal static void Register(string resPath)
        {
            var efs = EditorInterface.Singleton.GetResourceFilesystem();
            if (efs == null) return;
            var dir = Tools.ResPathNormalizer.ParentDir(resPath);
            if (IsKnownFolder(efs, dir)) efs.UpdateFile(resPath);
            else if (IsIgnoredFolder(dir)) return;
            else if (_scanFrame == Engine.GetProcessFrames()) Defer(efs, resPath);
            else FullScanFor(efs);
        }

        /// <summary>A folder created on disk becomes known to the editor filesystem (a full scan, settled, if it is not yet).</summary>
        internal static void RegisterFolder(string resDir)
        {
            var efs = EditorInterface.Singleton.GetResourceFilesystem();
            if (efs == null || IsKnownFolder(efs, resDir) || IsIgnoredFolder(resDir)) return;
            if (_scanFrame == Engine.GetProcessFrames()) Defer(efs, null);
            else FullScanFor(efs);
        }

        // The frame of the last full scan Register ran, and the files written after it in that frame.
        static ulong _scanFrame = ulong.MaxValue;
        static readonly List<string?> _deferred = new List<string?>();

        static void FullScanFor(EditorFileSystem efs)
        {
            FullScan();
            _scanFrame = Engine.GetProcessFrames();
        }

        static void Defer(EditorFileSystem efs, string? resPath)
        {
            if (_deferred.Count == 0)
                efs.Connect(EditorFileSystem.SignalName.FilesystemChanged, Callable.From(FlushDeferred), (uint)GodotObject.ConnectFlags.OneShot);
            _deferred.Add(resPath);
        }

        // The scan of the frame is in: register the files written after it (their folders are known now), or scan again.
        static void FlushDeferred()
        {
            var efs = EditorInterface.Singleton.GetResourceFilesystem();
            var pending = _deferred.ToList();
            _deferred.Clear();
            if (efs == null) return;
            var rescan = false;
            foreach (var path in pending)
            {
                if (path != null && IsKnownFolder(efs, Tools.ResPathNormalizer.ParentDir(path))) efs.UpdateFile(path);
                else rescan = true;
            }
            if (rescan) efs.Scan();
        }

        // The editor filesystem skips a folder holding a .gdignore file and dot-named folders (and everything below them).
        static bool IsIgnoredFolder(string resDir)
        {
            var dir = Tools.ResPathNormalizer.NormalizeDir(resDir).TrimEnd('/');
            while (dir.Length > "res://".Length)
            {
                var slash = dir.LastIndexOf('/');
                if (dir[slash + 1] == '.' || FileAccess.FileExists(dir + "/.gdignore")) return true;
                dir = dir.Substring(0, slash);
            }
            return false;
        }

        /// <summary>Whether the editor filesystem has scanned the folder <paramref name="resDir"/>. Main-thread only.</summary>
        internal static bool IsKnownFolder(string resDir) =>
            EditorInterface.Singleton.GetResourceFilesystem() is { } efs && IsKnownFolder(efs, resDir);

        static bool IsKnownFolder(EditorFileSystem efs, string resDir) =>
            efs.GetFilesystemPath(Tools.ResPathNormalizer.NormalizeDir(resDir)) != null;

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
