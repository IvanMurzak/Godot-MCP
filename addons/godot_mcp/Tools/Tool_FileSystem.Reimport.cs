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
        [Description("Re-scan the Godot project's res:// filesystem and/or reimport specific files, then " +
            "wait for the import to settle before returning. The Godot analog of Unity's AssetDatabase.Refresh. " +
            "Two modes:\n" +
            "  - Pass 'files' (a list of res:// paths) to reimport exactly those files via " +
            "EditorFileSystem.ReimportFiles — use this after editing a source asset's bytes outside the editor.\n" +
            "  - Omit 'files' (or pass an empty list) to trigger a full EditorFileSystem.Scan — use this after " +
            "adding/removing files on disk so Godot picks up the change.\n" +
            "The call blocks until scanning completes (bounded), so a subsequent resource-find/get-data sees " +
            "the settled state. Returns a short status string.")]
        public string Reimport
        (
            [Description("Optional list of res:// file paths to reimport. When omitted/empty, a full filesystem scan is run instead.")]
            List<string>? files = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var efs = EditorInterface.Singleton.GetResourceFilesystem()
                    ?? throw new Exception("Editor resource filesystem is not available.");

                var hasFiles = files != null && files.Count > 0;

                string action;
                if (hasFiles)
                {
                    // Validate every path up front so a single bad entry is a clean error, not a partial import.
                    foreach (var f in files!)
                    {
                        var p = ResPathNormalizer.RequireResFilePath(f, nameof(files));
                        if (!FileAccess.FileExists(p))
                            throw new ArgumentException($"No file exists at '{p}'.", nameof(files));
                    }

                    efs.ReimportFiles(files!.ToArray());
                    action = $"Reimported {files.Count} file(s)";
                }
                else
                {
                    efs.Scan();
                    action = "Full filesystem scan";
                }

                // Settle: Scan() runs the index asynchronously, so poll IsScanning() until it clears (with a
                // bounded number of short sleeps so a stuck scan cannot hang the tool indefinitely).
                // ReimportFiles is synchronous, but IsScanning() may still report a tail scan — the same wait
                // covers both. The MainThread dispatcher already executes us on the editor main thread, so a
                // short Thread.Sleep here yields without re-entrancy issues.
                const int maxWaits = 200;          // 200 * 25ms = 5s ceiling
                const int sleepMs = 25;
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
