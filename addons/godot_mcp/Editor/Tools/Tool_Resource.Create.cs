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
        public const string ResourceCreateToolId = "resource-create";

        [AiTool
        (
            ResourceCreateToolId,
            Title = "Resource / Create"
        )]
        [Description("Create a new Godot Resource (.tres/.res) on disk. A fresh instance of the Godot class " +
            "named by 'typeClassName' (e.g. 'StandardMaterial3D', 'Resource', 'Curve') is instantiated, then " +
            "saved to 'resourcePath' (which must be a res:// path ending in '.tres' or '.res') via " +
            "ResourceSaver. The editor filesystem is updated so the new asset is importable immediately. " +
            "Use '" + ResourceModifyToolId + "' afterwards to set its properties. Returns the new resource's " +
            "identity (res:// path, uid, type).")]
        public ResourceInfo Create
        (
            [Description("res:// path for the new resource file, ending in '.tres' or '.res' (e.g. 'res://materials/wood.tres').")]
            string resourcePath,
            [Description("Godot class to instantiate for the resource (must derive from Resource), e.g. " +
                "'StandardMaterial3D', 'Curve', 'Resource'. Defaults to 'Resource'.")]
            string? typeClassName = null
        )
        {
            if (string.IsNullOrEmpty(resourcePath))
                throw new ArgumentException("resourcePath cannot be null or empty.", nameof(resourcePath));

            return MainThread.Instance.Run(() =>
            {
                var resPath = ResPathNormalizer.RequireResFilePath(resourcePath, nameof(resourcePath));
                if (!resPath.EndsWith(".tres", StringComparison.OrdinalIgnoreCase)
                    && !resPath.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"resourcePath must end with '.tres' or '.res'; got '{resourcePath}'.", nameof(resourcePath));

                // Guard on the file on disk (like sibling Move/Delete), not just on a LOADABLE resource:
                // ResourceLoader.Exists is false for a present-but-unimported / non-resource file, which
                // ResourceSaver.Save would then silently overwrite.
                if (FileAccess.FileExists(resPath) || ResourceLoader.Exists(resPath))
                    throw new ArgumentException($"A resource already exists at '{resPath}'. Use '{ResourceModifyToolId}' to change it, " +
                        $"or '{ResourceMoveToolId}'/'{ResourceDeleteToolId}' to relocate/remove it first.", nameof(resourcePath));

                var className = string.IsNullOrEmpty(typeClassName) ? "Resource" : typeClassName!;
                if (!ClassDB.ClassExists(className))
                    throw new ArgumentException($"Unknown Godot class '{className}'.", nameof(typeClassName));
                if (!ClassDB.CanInstantiate(className))
                    throw new ArgumentException($"Godot class '{className}' cannot be instantiated (abstract/virtual).", nameof(typeClassName));
                if (!ClassDB.IsParentClass(className, "Resource"))
                    throw new ArgumentException($"Godot class '{className}' does not derive from Resource.", nameof(typeClassName));

                var resource = ClassDB.Instantiate(className).As<Resource>()
                    ?? throw new ArgumentException($"Godot class '{className}' did not instantiate to a Resource.", nameof(typeClassName));

                // ResourceSaver.Save does NOT create missing parent directories — create them first so a
                // nested target path (e.g. 'res://materials/wood.tres') works like Unity's CreateAsset.
                var targetDir = ResPathNormalizer.ParentDir(resPath);
                if (!DirAccess.DirExistsAbsolute(targetDir))
                {
                    var mkErr = DirAccess.MakeDirRecursiveAbsolute(targetDir);
                    if (mkErr != Error.Ok)
                        throw new Exception($"Failed to create target directory '{targetDir}': {mkErr}.");
                }

                var saveErr = ResourceSaver.Save(resource, resPath);
                if (saveErr != Error.Ok)
                    throw new Exception($"Failed to save new resource to '{resPath}': {saveErr}.");

                // Make the new asset visible to the editor's resource filesystem (no full rescan needed).
                EditorInterface.Singleton.GetResourceFilesystem()?.UpdateFile(resPath);

                return ToResourceInfo(resPath, resource);
            });
        }
    }
}
#endif
