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
    public partial class Tool_FileSystem
    {
        public const string FileSystemListToolId = "filesystem-list";

        [AiTool
        (
            FileSystemListToolId,
            Title = "FileSystem / List",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Browse the Godot project's res:// filesystem one directory at a time. Pass a 'path' " +
            "(a res:// directory, e.g. 'res://materials' — a trailing slash is optional); omit it (or pass " +
            "'res://') to list the project root. Returns the directory's immediate sub-directories and files. " +
            "Each file entry includes the importer-assigned resource type (e.g. 'PackedScene', 'Texture2D') " +
            "and uid (when assigned) — read straight from the editor's filesystem index, so no resource is " +
            "loaded. A non-existent or non-res:// path yields a structured error.")]
        public FileSystemListing List
        (
            [Description("res:// directory to list (a trailing slash is optional). Omit or pass 'res://' for the project root.")]
            string? path = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var dirPath = ResPathNormalizer.NormalizeDir(path);

                var efs = EditorInterface.Singleton.GetResourceFilesystem()
                    ?? throw new Exception("Editor resource filesystem is not available.");

                var dir = efs.GetFilesystemPath(dirPath)
                    ?? throw new ArgumentException($"Directory '{dirPath}' was not found in the project filesystem.", nameof(path));

                var listing = new FileSystemListing { Path = dirPath };

                // Sub-directories first.
                var subDirCount = dir.GetSubdirCount();
                for (int i = 0; i < subDirCount; i++)
                {
                    var sub = dir.GetSubdir(i);
                    if (sub == null)
                        continue;

                    var subPath = sub.GetPath(); // already a 'res://.../' trailing-slash form
                    listing.Entries.Add(new FileSystemEntry
                    {
                        Name = GetLeafName(subPath), // GetLeafName already strips the trailing slash
                        Path = subPath,
                        IsDirectory = true,
                    });
                }

                // Then files.
                var fileCount = dir.GetFileCount();
                for (int i = 0; i < fileCount; i++)
                {
                    var filePath = dir.GetFilePath(i);
                    var fileType = dir.GetFileType(i).ToString(); // GetFileType(int) returns a StringName

                    // Resolve the uid when the importer assigned one (Godot 4.3: ResourceLoader maps res://->uid).
                    var uidText = GodotUidForPath(filePath);
                    var uid = string.IsNullOrEmpty(uidText) ? null : uidText;

                    listing.Entries.Add(new FileSystemEntry
                    {
                        Name = GetLeafName(filePath),
                        Path = filePath,
                        IsDirectory = false,
                        ResourceType = string.IsNullOrEmpty(fileType) ? null : fileType,
                        Uid = uid,
                    });
                }

                listing.DirectoryCount = subDirCount;
                listing.FileCount = fileCount;
                return listing;
            });
        }

        /// <summary>
        /// res:// → uid:// text for a file, or empty when the file has no registered uid. Main-thread only
        /// (touches <see cref="ResourceLoader"/>). Returns Godot's <c>uid://</c> text form.
        /// </summary>
        static string GodotUidForPath(string resPath)
        {
            var id = ResourceLoader.GetResourceUid(resPath);
            return id == ResourceUid.InvalidId ? string.Empty : ResourceUid.IdToText(id);
        }

        /// <summary>Leaf (last segment) of a res:// path; a trailing slash on a directory path is preserved by the caller.</summary>
        static string GetLeafName(string resPath)
        {
            var trimmed = resPath.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            return idx < 0 ? trimmed : trimmed.Substring(idx + 1);
        }
    }
}
#endif
