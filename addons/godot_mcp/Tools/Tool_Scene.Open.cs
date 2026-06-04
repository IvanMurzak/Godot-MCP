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
        public const string SceneOpenToolId = "scene-open";

        [AiTool
        (
            SceneOpenToolId,
            Title = "Scene / Open"
        )]
        [Description("Open a Godot scene asset (a res://*.tscn / *.scn PackedScene) in the editor and make " +
            "it the active/edited scene. Pass 'resourcePath' as the res:// path. Returns the opened scene's " +
            "structured data (the now-active scene). Use 'scene-list-opened' to see all open scenes afterwards.")]
        public SceneData Open
        (
            [Description("res:// path of the scene file to open, e.g. 'res://levels/level_1.tscn'.")]
            string resourcePath
        )
        {
            if (string.IsNullOrEmpty(resourcePath))
                throw new ArgumentException("resourcePath cannot be null or empty.", nameof(resourcePath));

            return MainThread.Instance.Run(() =>
            {
                if (!resourcePath.StartsWith("res://", StringComparison.Ordinal))
                    throw new ArgumentException($"resourcePath must be a 'res://' path; got '{resourcePath}'.", nameof(resourcePath));
                if (!ResourceLoader.Exists(resourcePath))
                    throw new ArgumentException($"No scene resource exists at '{resourcePath}'.", nameof(resourcePath));

                EditorInterface.Singleton.OpenSceneFromPath(resourcePath);

                var editedRoot = EditorInterface.Singleton.GetEditedSceneRoot();
                if (editedRoot == null)
                    throw new Exception($"Opened '{resourcePath}' but the editor has no edited scene root afterwards.");

                return ToActiveSceneData(editedRoot);
            });
        }
    }
}
#endif
