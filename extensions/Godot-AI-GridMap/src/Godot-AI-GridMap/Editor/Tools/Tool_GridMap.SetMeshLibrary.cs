/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    public partial class Tool_GridMap
    {
        /// <summary>
        /// Editor-only tool — assigns a <c>MeshLibrary</c> resource (loaded from a <c>res://</c> path) to an
        /// existing <c>GridMap</c> node. The MeshLibrary supplies the meshes that <c>gridmap-set-cell</c> item
        /// ids reference. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetMeshLibraryToolId,
            Title = "GridMap / Set Mesh Library"
        )]
        [Description("Assign a MeshLibrary resource to an existing GridMap node, addressed by 'nodePath' " +
            "(relative to the edited scene root). 'resourcePath' is the res:// path of the MeshLibrary resource " +
            "(e.g. 'res://tiles.meshlib' or a '.tres'); it must exist and be a MeshLibrary. The MeshLibrary's " +
            "item ids are what gridmap-set-cell places. Returns the GridMap's updated config.")]
        public GridMapNodeInfo SetMeshLibrary
        (
            [Description("Node path (relative to the edited scene root) of the GridMap node.")]
            string nodePath,
            [Description("res:// path of the MeshLibrary resource to assign (must exist and be a MeshLibrary).")]
            string resourcePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (string.IsNullOrWhiteSpace(resourcePath))
                    throw new ArgumentException("A resource path is required.", nameof(resourcePath));

                var grid = ResolveGridMapOrThrow(nodePath);

                var res = ResourceLoader.Load(resourcePath);
                if (res == null)
                    throw new ArgumentException($"No resource found at '{resourcePath}'.", nameof(resourcePath));
                if (res is not MeshLibrary meshLib)
                    throw new ArgumentException(
                        $"Resource at '{resourcePath}' is a {res.GetClass()}, not a MeshLibrary.", nameof(resourcePath));

                grid.MeshLibrary = meshLib;

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(grid);
            });
        }
    }
}
#endif
