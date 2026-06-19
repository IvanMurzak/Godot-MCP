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
    public partial class Tool_Scene
    {
        public const string SceneCreateToolId = "scene-create";

        [AiTool
        (
            SceneCreateToolId,
            Title = "Scene / Create"
        )]
        [Description("Create a new Godot scene asset at a res://*.tscn path and open it as the active scene. " +
            "A root Node is created (type given by 'rootTypeClassName', default 'Node'), packed into a " +
            "PackedScene, saved to 'resourcePath', then opened in the editor. Pass 'rootName' to name the " +
            "root Node. Returns the new scene's structured data. Use 'node-create' to add child nodes afterwards.")]
        public SceneData Create
        (
            [Description("res:// path for the new scene file, ending in '.tscn' (e.g. 'res://levels/level_2.tscn').")]
            string resourcePath,
            [Description("Godot class for the scene's root Node (e.g. 'Node', 'Node2D', 'Node3D'). Defaults to 'Node'.")]
            string? rootTypeClassName = null,
            [Description("Name for the root Node. Defaults to the type's default name.")]
            string? rootName = null
        )
        {
            if (string.IsNullOrEmpty(resourcePath))
                throw new ArgumentException("resourcePath cannot be null or empty.", nameof(resourcePath));

            return MainThread.Instance.Run(() =>
            {
                if (!resourcePath.StartsWith("res://", StringComparison.Ordinal))
                    throw new ArgumentException($"resourcePath must be a 'res://' path; got '{resourcePath}'.", nameof(resourcePath));
                if (!resourcePath.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
                    && !resourcePath.EndsWith(".scn", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException($"resourcePath must end with '.tscn' or '.scn'; got '{resourcePath}'.", nameof(resourcePath));

                var className = string.IsNullOrEmpty(rootTypeClassName) ? "Node" : rootTypeClassName!;
                if (!ClassDB.ClassExists(className))
                    throw new ArgumentException($"Unknown Godot class '{className}'.", nameof(rootTypeClassName));
                if (!ClassDB.CanInstantiate(className))
                    throw new ArgumentException($"Godot class '{className}' cannot be instantiated.", nameof(rootTypeClassName));

                var root = ClassDB.Instantiate(className).As<Node>()
                    ?? throw new ArgumentException($"Godot class '{className}' did not instantiate to a Node.", nameof(rootTypeClassName));

                if (!string.IsNullOrEmpty(rootName))
                    root.Name = rootName;

                try
                {
                    var packed = new PackedScene();
                    var packErr = packed.Pack(root);
                    if (packErr != Error.Ok)
                        throw new Exception($"Failed to pack new scene root: {packErr}.");

                    // ResourceSaver.Save does NOT create missing parent directories — create them first so a
                    // nested target path (e.g. 'res://levels/level_2.tscn') saves instead of failing with
                    // CantOpen, matching Tool_Resource.Create and Unity's CreateAsset behavior.
                    var targetDir = ResPathNormalizer.ParentDir(resourcePath);
                    if (!DirAccess.DirExistsAbsolute(targetDir))
                    {
                        var mkErr = DirAccess.MakeDirRecursiveAbsolute(targetDir);
                        if (mkErr != Error.Ok)
                            throw new Exception($"Failed to create target directory '{targetDir}': {mkErr}.");
                    }

                    var saveErr = ResourceSaver.Save(packed, resourcePath);
                    if (saveErr != Error.Ok)
                        throw new Exception($"Failed to save new scene to '{resourcePath}': {saveErr}.");
                }
                finally
                {
                    // The in-memory root was only needed to pack the PackedScene; free it so it does not
                    // leak. The editor re-instances its own root when the saved scene is opened below.
                    root.Free();
                }

                // Make the new asset visible to the editor's resource filesystem, then open it.
                EditorInterface.Singleton.GetResourceFilesystem().UpdateFile(resourcePath);
                EditorInterface.Singleton.OpenSceneFromPath(resourcePath);

                var editedRoot = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception($"Created '{resourcePath}' but the editor has no edited scene root afterwards.");

                return ToActiveSceneData(editedRoot);
            });
        }
    }
}
#endif
