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
        public const string SceneGetDataToolId = "scene-get-data";

        [AiTool
        (
            SceneGetDataToolId,
            Title = "Scene / Get Data",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Get the scene-tree of the currently edited Godot scene as a structured Node hierarchy " +
            "rooted at the scene's root Node. 'hierarchyDepth' bounds how deep the tree is walked: 0 = the " +
            "root only, 1 = root + direct children, etc.; -1 walks the entire tree. Each entry carries the " +
            "Node's instanceId, name, path, type, attached script, and child count.")]
        public NodeData GetData
        (
            [Description("Depth of the scene tree to include. 0 = root only; N > 0 = N layers of children; " +
                "-1 = the entire tree.")]
            int hierarchyDepth = -1
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var root = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited.");

                // Walk the whole tree when -1 is requested; otherwise honor the explicit bound. A very
                // large sentinel is used for "unbounded" so Tool_Node.ToNodeData can stay depth-counted.
                var depth = hierarchyDepth < 0 ? int.MaxValue : hierarchyDepth;
                return Tool_Node.ToNodeData(root, depth);
            });
        }
    }
}
#endif
