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
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Resource
    {
        public const string ResourceMoveToolId = "resource-move";

        [AiTool
        (
            ResourceMoveToolId,
            Title = "Resource / Move",
            IdempotentHint = false
        )]
        [Description("Move or rename a resource file in the res:// filesystem. Both 'sourcePath' and " +
            "'destinationPath' must be res:// file paths. The resource file is moved with DirAccess; its " +
            "sidecar '.import' metadata (when present) is moved alongside it so the import pipeline stays " +
            "consistent. The editor filesystem is then rescanned so the new location is indexed. Returns the " +
            "moved resource's identity at its new path. NOTE: hard-coded res:// path references in OTHER " +
            "resources are not rewritten — prefer addressing assets by uid where stable references matter.")]
        public ResourceInfo Move
        (
            [Description("res:// path of the resource to move (the existing file).")]
            string sourcePath,
            [Description("res:// destination path (the new file location/name).")]
            string destinationPath
        )
        {
            if (string.IsNullOrEmpty(sourcePath))
                throw new ArgumentException("sourcePath cannot be null or empty.", nameof(sourcePath));
            if (string.IsNullOrEmpty(destinationPath))
                throw new ArgumentException("destinationPath cannot be null or empty.", nameof(destinationPath));

            return MainThread.Instance.Run(() =>
            {
                var src = ResPathNormalizer.RequireResFilePath(sourcePath, nameof(sourcePath));
                var dst = ResPathNormalizer.RequireResFilePath(destinationPath, nameof(destinationPath));

                if (src == dst)
                    throw new ArgumentException("sourcePath and destinationPath are identical.", nameof(destinationPath));

                if (!FileAccess.FileExists(src))
                    throw new ArgumentException($"No file exists at '{src}'.", nameof(sourcePath));
                if (FileAccess.FileExists(dst))
                    throw new ArgumentException($"A file already exists at '{dst}'.", nameof(destinationPath));

                // Ensure the destination directory exists (DirAccess.Rename does not create it).
                var dstDir = dst.Substring(0, dst.LastIndexOf('/') + 1);
                if (!DirAccess.DirExistsAbsolute(dstDir))
                {
                    var mkErr = DirAccess.MakeDirRecursiveAbsolute(dstDir);
                    if (mkErr != Error.Ok)
                        throw new Exception($"Failed to create destination directory '{dstDir}': {mkErr}.");
                }

                // Move the resource file itself.
                var moveErr = DirAccess.RenameAbsolute(src, dst);
                if (moveErr != Error.Ok)
                    throw new Exception($"Failed to move '{src}' to '{dst}': {moveErr}.");

                // From here the resource file is already at 'dst'. This is a MULTI-FILE op (resource +
                // .import sidecar): if the sidecar rename throws below, the filesystem is left half-moved.
                // Rescan in a 'finally' regardless so the editor index reflects on-disk reality (resource at
                // dst) even on a partial failure — never leave a stale index pointing at the old location.
                try
                {
                    // Move the sidecar '.import' metadata if it exists (imported assets carry one; .tres
                    // usually do not). Keeping it next to the resource preserves import settings + uid mapping.
                    var srcImport = src + ".import";
                    if (FileAccess.FileExists(srcImport))
                    {
                        var importErr = DirAccess.RenameAbsolute(srcImport, dst + ".import");
                        if (importErr != Error.Ok)
                            throw new Exception(
                                $"Inconsistent state: resource moved to '{dst}' but its '.import' sidecar " +
                                $"failed to move ('{srcImport}' -> '{dst}.import': {importErr}). A manual " +
                                "reimport of the moved resource is required to repair the import metadata.");
                    }
                }
                finally
                {
                    // Rescan so the editor filesystem indexes the new location and drops the old one — runs
                    // even when the sidecar move threw, so the index never lies about where the resource is.
                    EditorInterface.Singleton.GetResourceFilesystem()?.Scan();
                }

                return ToResourceInfo(dst);
            });
        }
    }
}
#endif
