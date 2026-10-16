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
        public const string ResourceDeleteToolId = "resource-delete";

        [AiTool
        (
            ResourceDeleteToolId,
            Title = "Resource / Delete"
        )]
        [Description("Delete a resource file from the res:// filesystem. 'resourcePath' must be a res:// file " +
            "path. The resource's identity (path, uid, type) is captured BEFORE deletion and returned for " +
            "confirmation. The resource file is removed with DirAccess, along with its '.import' / '.uid' " +
            "sidecars when present, then the editor filesystem is rescanned. WARNING: other resources holding " +
            "a hard reference to this file will break — verify with '" + ResourceFindToolId + "' first. A " +
            "loaded copy is detached from the path, so an open scene still using it embeds it on its next save; a " +
            "deleted scene that is the edited scene is closed (Godot 4.4+), so a later save cannot recreate it.")]
        public ResourceInfo Delete
        (
            [Description("res:// path of the resource file to delete.")]
            string resourcePath
        )
        {
            if (string.IsNullOrEmpty(resourcePath))
                throw new ArgumentException("resourcePath cannot be null or empty.", nameof(resourcePath));

            return MainThread.Instance.Run(() =>
            {
                var resPath = ResPathNormalizer.RequireResFilePath(resourcePath, nameof(resourcePath));

                if (!FileAccess.FileExists(resPath))
                    throw new ArgumentException($"No file exists at '{resPath}'.", nameof(resourcePath));

                // Snapshot identity BEFORE removing the file (path/uid/type are unrecoverable afterwards).
                var info = ToResourceInfo(resPath);

                var rmErr = DirAccess.RemoveAbsolute(resPath);
                if (rmErr != Error.Ok)
                    throw new Exception($"Failed to delete '{resPath}': {rmErr}.");

                // From here the resource file is already gone. This is a MULTI-FILE op (resource + .import/.uid
                // sidecars): if the sidecar removal throws below, the resource itself is still deleted. Rescan
                // in a 'finally' regardless so the editor index drops the removed entry even on partial
                // failure — never leave a stale index pointing at a file that no longer exists.
                try
                {
                    EditorToolGuards.RebindCachedResource(resPath, newPath: null);
                    if (ResPathNormalizer.IsScenePath(resPath))
                        EditorToolGuards.CloseDeletedScene(resPath);

                    // Remove the '.import' / '.uid' sidecars too, so no orphaned metadata is left behind.
                    foreach (var sidecar in EditorToolGuards.Sidecars)
                    {
                        var sidecarPath = resPath + sidecar;
                        if (!FileAccess.FileExists(sidecarPath))
                            continue;
                        var rmSidecarErr = DirAccess.RemoveAbsolute(sidecarPath);
                        if (rmSidecarErr != Error.Ok)
                            throw new Exception(
                                $"Inconsistent state: resource '{resPath}' was deleted but its '{sidecar}' " +
                                $"sidecar '{sidecarPath}' failed to remove ({rmSidecarErr}). The resource itself " +
                                "is already gone; remove the orphaned sidecar manually.");
                    }
                }
                finally
                {
                    // Rescan so the editor filesystem drops the removed entry — runs even when the sidecar
                    // removal threw, so the index never lists a resource that no longer exists on disk.
                    EditorInterface.Singleton.GetResourceFilesystem()?.Scan();
                }

                return info;
            });
        }
    }
}
#endif
