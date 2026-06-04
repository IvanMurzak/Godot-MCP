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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Resource
    {
        public const string ResourceFindToolId = "resource-find";

        [AiTool
        (
            ResourceFindToolId,
            Title = "Resource / Find",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Find resources in the Godot project's res:// filesystem. Combine any of:\n" +
            "  - 'resourcePath' — an exact res:// path to resolve (also accepts a 'uid://' which is mapped " +
            "to its path).\n" +
            "  - 'uid' — a 'uid://' identifier to resolve to its res:// path.\n" +
            "  - 'typeFilter' — a Godot type name (e.g. 'PackedScene', 'Texture2D', 'Resource'); only files " +
            "whose importer-assigned type equals or derives from it are returned.\n" +
            "  - 'directory' — a res:// directory to scope the type-filtered scan (defaults to the whole project).\n" +
            "With 'resourcePath' or 'uid', returns that single resource (a structured error if it does not " +
            "exist). With 'typeFilter' (and optional 'directory'), recursively scans the editor filesystem " +
            "and returns every match. Each hit carries res:// path, uid, and type.\n" +
            "NOTE: 'resourcePath' and 'uid' are mutually exclusive — when both are supplied 'uid' takes " +
            "precedence and 'resourcePath' is ignored. For a direct path/uid lookup, 'typeFilter' and " +
            "'directory' are ignored (they only apply to the type-filtered scan).")]
        public ResourceFindResult Find
        (
            [Description("Optional exact res:// path (or uid://) to resolve to a single resource.")]
            string? resourcePath = null,
            [Description("Optional uid:// identifier to resolve to a single resource.")]
            string? uid = null,
            [Description("Optional Godot type name to filter by (e.g. 'PackedScene', 'Texture2D'). Matches " +
                "the file's importer-assigned type, including subclasses.")]
            string? typeFilter = null,
            [Description("Optional res:// directory to scope a type-filtered scan. Defaults to the project root.")]
            string? directory = null
        )
        {
            var hasPath = !string.IsNullOrWhiteSpace(resourcePath);
            var hasUid = !string.IsNullOrWhiteSpace(uid);
            var hasType = !string.IsNullOrWhiteSpace(typeFilter);

            if (!hasPath && !hasUid && !hasType)
                throw new ArgumentException(
                    $"At least one of '{nameof(resourcePath)}', '{nameof(uid)}', or '{nameof(typeFilter)}' is required.");

            return MainThread.Instance.Run(() =>
            {
                var result = new ResourceFindResult();
                var efs = EditorInterface.Singleton.GetResourceFilesystem()
                    ?? throw new Exception("Editor resource filesystem is not available.");

                // 1) Direct lookup by uid (mapped to a res:// path).
                if (hasUid)
                {
                    var resolved = ResolveUidToPath(uid!)
                        ?? throw new ArgumentException($"uid '{uid}' does not resolve to any resource.", nameof(uid));
                    AddSingle(result, resolved);
                    return result;
                }

                // 2) Direct lookup by path (a 'uid://' path is mapped first).
                if (hasPath)
                {
                    var resPath = resourcePath!.StartsWith("uid://", StringComparison.Ordinal)
                        ? (ResolveUidToPath(resourcePath!) ?? throw new ArgumentException($"uid '{resourcePath}' does not resolve to any resource.", nameof(resourcePath)))
                        : resourcePath!;

                    if (!ResourceLoader.Exists(resPath))
                        throw new ArgumentException($"No resource exists at '{resPath}'.", nameof(resourcePath));

                    AddSingle(result, resPath);
                    return result;
                }

                // 3) Type-filtered recursive scan over the editor filesystem.
                var scope = ResPathNormalizer.NormalizeDir(directory);
                var rootDir = efs.GetFilesystemPath(scope)
                    ?? throw new ArgumentException($"Directory '{scope}' was not found in the project filesystem.", nameof(directory));

                CollectByType(rootDir, typeFilter!, result.Resources);
                result.Count = result.Resources.Count;
                return result;
            });
        }

        /// <summary>Add a single resolved path to <paramref name="result"/>. Main-thread only.</summary>
        static void AddSingle(ResourceFindResult result, string resPath)
        {
            result.Resources.Add(ToResourceInfo(resPath));
            result.Count = result.Resources.Count;
        }

        /// <summary>uid:// text → res:// path, or null when the uid is unknown. Main-thread only.</summary>
        static string? ResolveUidToPath(string uidText)
        {
            var id = ResourceUid.TextToId(uidText);
            if (id == ResourceUid.InvalidId || !ResourceUid.HasId(id))
                return null;
            var path = ResourceUid.GetIdPath(id);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        /// <summary>
        /// Recursively walk <paramref name="dir"/> collecting every file whose importer-assigned type
        /// matches <paramref name="typeFilter"/> (exact or subclass via <see cref="ClassDB.IsParentClass"/>).
        /// Main-thread only.
        /// </summary>
        static void CollectByType(EditorFileSystemDirectory dir, string typeFilter, List<ResourceInfo> sink)
        {
            var fileCount = dir.GetFileCount();
            for (int i = 0; i < fileCount; i++)
            {
                var fileType = dir.GetFileType(i).ToString();
                if (TypeMatches(fileType, typeFilter))
                    sink.Add(ToResourceInfo(dir.GetFilePath(i)));
            }

            var subCount = dir.GetSubdirCount();
            for (int i = 0; i < subCount; i++)
            {
                var sub = dir.GetSubdir(i);
                if (sub != null)
                    CollectByType(sub, typeFilter, sink);
            }
        }

        /// <summary>
        /// True when <paramref name="fileType"/> equals <paramref name="typeFilter"/> or derives from it.
        /// Uses <see cref="ClassDB.IsParentClass"/> when both are known engine classes; otherwise falls
        /// back to a case-sensitive equality so custom/script types still match exactly.
        /// </summary>
        static bool TypeMatches(string fileType, string typeFilter)
        {
            if (string.IsNullOrEmpty(fileType))
                return false;
            if (fileType == typeFilter)
                return true;
            if (ClassDB.ClassExists(fileType) && ClassDB.ClassExists(typeFilter))
                return ClassDB.IsParentClass(fileType, typeFilter);
            return false;
        }
    }
}
#endif
