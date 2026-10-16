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

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    public partial class Tool_Navigation
    {
        /// <summary>
        /// Editor-only tool — assigns the navigation resource of an existing region: a <c>NavigationPolygon</c>
        /// on a <c>NavigationRegion2D</c>, or a <c>NavigationMesh</c> on a <c>NavigationRegion3D</c>. Pass a
        /// <c>res://</c> path to assign an existing resource (validated to match the region's dimension), or omit
        /// it to attach a fresh empty resource of the right type (ready to bake). Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            RegionSetMeshToolId,
            Title = "Navigation / Region Set Mesh"
        )]
        [Description("Assign the navigation resource of an existing NavigationRegion, addressed by 'nodePath' " +
            "(relative to the edited scene root): a NavigationPolygon for a NavigationRegion2D, or a " +
            "NavigationMesh for a NavigationRegion3D. Pass 'resourcePath' (a 'res://' path) to assign an " +
            "existing resource — it is validated to match the region's dimension. Omit 'resourcePath' to attach " +
            "a fresh empty resource of the correct type (which you can bake later). Returns the region's updated " +
            "config including the assigned resource type.")]
        public NavigationNodeInfo RegionSetMesh
        (
            [Description("Node path (relative to the edited scene root) of the NavigationRegion to set the resource on.")]
            string nodePath,
            [Description("Optional 'res://' path of an existing NavigationPolygon (2D) / NavigationMesh (3D) to " +
                "assign. When omitted, a fresh empty resource of the matching type is attached.")]
            string? resourcePath = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var node = ResolveRegionOrThrow(nodePath);

                if (node is NavigationRegion3D r3)
                {
                    r3.NavigationMesh = string.IsNullOrWhiteSpace(resourcePath)
                        ? new NavigationMesh()
                        : LoadResourceOrThrow<NavigationMesh>(resourcePath!, "NavigationMesh");
                }
                else
                {
                    var r2 = (NavigationRegion2D)node;
                    r2.NavigationPolygon = string.IsNullOrWhiteSpace(resourcePath)
                        ? new NavigationPolygon()
                        : LoadResourceOrThrow<NavigationPolygon>(resourcePath!, "NavigationPolygon");
                }

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }

        /// <summary>Load a resource at a <c>res://</c> path and verify it is of the expected type, or throw a clear error.</summary>
        static T LoadResourceOrThrow<T>(string resourcePath, string expectedTypeName) where T : Resource
        {
            var res = ResourceLoader.Load(resourcePath);
            if (res == null)
                throw new ArgumentException($"No resource found at '{resourcePath}'.", nameof(resourcePath));
            if (res is not T typed)
                throw new ArgumentException(
                    $"Resource at '{resourcePath}' is a {res.GetClass()}, not a {expectedTypeName}.", nameof(resourcePath));
            return typed;
        }
    }
}
#endif
