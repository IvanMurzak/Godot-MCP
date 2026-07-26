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
            "a file's bytes outside the editor.\n" +
            "  - Omit 'files' (or pass an empty list) to trigger a full EditorFileSystem.Scan — use this after " +
            "adding/removing files on disk so Godot picks up the change.\n" +
            "Imported assets (textures, meshes, audio, fonts — anything with a '.import' sidecar) go through " +
            "EditorFileSystem.ReimportFiles. Godot's NATIVE formats (.tscn/.tres/.gd/.cs/.gdshader) have no " +
            "importer and are refreshed with EditorFileSystem.UpdateFile instead — queueing them for import " +
            "would make the editor log \"importer for type '' not found\" while this tool reported success. " +
            "The returned status names both groups.\n" +
            "The call blocks until scanning completes (bounded), so a subsequent resource-find/get-data sees " +
            "the settled state. Returns a short status string.")]
        public string Reimport
        (
            [Description("Optional list of res:// file paths to refresh. Each is reimported when it is an " +
                "imported asset (it has a '.import' sidecar) and refreshed via EditorFileSystem.UpdateFile " +
                "when it is one of Godot's native formats (.tscn/.tres/.gd/.cs/...), which have no importer. " +
                "When omitted/empty, a full filesystem scan is run instead.")]
            List<string>? files = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var efs = EditorToolGuards.GetResourceFileSystemOrThrow();

                var hasFiles = files != null && files.Count > 0;

                // Bounded settle loop tunables. The MainThread dispatcher already executes us on the editor
                // main thread, so a short Thread.Sleep here yields without re-entrancy issues.
                const int sleepMs = 25;
                const int maxWaits = 200;          // 200 * 25ms = 5s settle ceiling
                const int primeWaits = 40;         // 40 * 25ms = 1s ceiling to observe the scan START

                string action;
                if (hasFiles)
                {
                    // Validate every path up front so a single bad entry is a clean error, not a partial
                    // refresh. Collect the normalized/trimmed paths and act on THOSE — passing the raw
                    // 'files' (which may carry surrounding whitespace) would not match a known res:// file
                    // and both ReimportFiles and UpdateFile would silently no-op.
                    var normalized = new List<string>(files!.Count);
                    foreach (var f in files!)
                    {
                        var p = ResPathNormalizer.RequireResFilePath(f, nameof(files));
                        if (!FileAccess.FileExists(p))
                            throw new ArgumentException($"No file exists at '{p}'.", nameof(files));
                        normalized.Add(p);
                    }

                    // Never queue a NATIVE file (.tscn/.tres/.gd/…) for import: it has no importer, so the
                    // editor logs "importer for type '' not found" while ReimportFiles reports nothing and
                    // this tool used to answer "success" (issue #310). Split on the '.import' sidecar and
                    // refresh the native ones through UpdateFile instead, which is the operation the caller
                    // actually wants for a file whose bytes changed on disk.
                    var plan = ReimportClassifier.Plan(normalized, FileAccess.FileExists);

                    if (plan.Importable.Count > 0)
                        efs.ReimportFiles(plan.Importable.ToArray());

                    foreach (var nativePath in plan.Native)
                        efs.UpdateFile(nativePath);

                    action = plan.Describe();

                    // ReimportFiles/UpdateFile are synchronous; only a tail scan (if any) may still be in
                    // flight. Do NOT prime here — a prime that never observes a scan would falsely report
                    // "never started". Just drain whatever scan is currently running (bounded).
                    var tailWaits = 0;
                    while (efs.IsScanning() && tailWaits < maxWaits)
                    {
                        Thread.Sleep(sleepMs);
                        tailWaits++;
                    }

                    var tailSettled = !efs.IsScanning();
                    return tailSettled
                        ? $"{action}; filesystem settled."
                        : $"{action}; filesystem still scanning after {maxWaits * sleepMs}ms (progress={efs.GetScanningProgress():0.00}).";
                }

                // Full scan. Scan() runs the index ASYNCHRONOUSLY, so IsScanning() can still be false on the
                // first check (the scan has not begun yet). A naive `while (IsScanning())` would exit
                // immediately and falsely report "settled" though nothing was indexed — defeating the tool's
                // purpose. So PRIME first: poll until the scan is observed running at least once (bounded),
                // and only then poll until it clears.
                efs.Scan();
                action = "Full filesystem scan";

                var primed = false;
                var primePolls = 0;
                while (primePolls < primeWaits)
                {
                    if (efs.IsScanning())
                    {
                        primed = true;
                        break;
                    }
                    Thread.Sleep(sleepMs);
                    primePolls++;
                }

                if (!primed)
                    // The scan never started within the prime window. Report it explicitly rather than
                    // silently claiming "settled" — the caller should not assume the index was refreshed.
                    return $"{action}; scan did not start within {primeWaits * sleepMs}ms — filesystem may be unchanged or busy (NOT confirmed settled).";

                // Scan observed running; now drain it (bounded).
                var waits = 0;
                while (efs.IsScanning() && waits < maxWaits)
                {
                    Thread.Sleep(sleepMs);
                    waits++;
                }

                var settled = !efs.IsScanning();
                return settled
                    ? $"{action}; filesystem settled."
                    : $"{action}; filesystem still scanning after {maxWaits * sleepMs}ms (progress={efs.GetScanningProgress():0.00}).";
            });
        }
    }
}
#endif
