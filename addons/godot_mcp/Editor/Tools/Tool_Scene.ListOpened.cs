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
using System.Collections.Generic;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Scene
    {
        public const string SceneListOpenedToolId = "scene-list-opened";

        [AiTool
        (
            SceneListOpenedToolId,
            Title = "Scene / List Opened",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("List every scene currently open in the Godot editor as a shallow snapshot " +
            "(res:// path, and whether it is the active/edited scene). The active scene additionally " +
            "carries its root Node's name and instanceId. Use 'scene-get-data' for the deep scene-tree view.")]
        public List<SceneData> ListOpened(string? nothing = null)
        {
            return MainThread.Instance.Run(() =>
            {
                var result = new List<SceneData>();

                var editedRoot = EditorInterface.Singleton.GetEditedSceneRoot();
                var activePath = editedRoot?.GetSceneFilePath() ?? string.Empty;

                var openPaths = EditorInterface.Singleton.GetOpenScenes();
                var sawActive = false;

                foreach (var path in openPaths)
                {
                    var isActive = !string.IsNullOrEmpty(activePath) && path == activePath;
                    if (isActive && editedRoot != null)
                    {
                        result.Add(ToActiveSceneData(editedRoot));
                        sawActive = true;
                    }
                    else
                    {
                        result.Add(ToOpenSceneData(path, isActive: false));
                    }
                }

                // A freshly-created-but-unsaved active scene has no path, so GetOpenScenes() may not list
                // it; surface it explicitly so the active scene is never missing from the result.
                if (!sawActive && editedRoot != null)
                    result.Add(ToActiveSceneData(editedRoot));

                return result;
            });
        }
    }
}
#endif
