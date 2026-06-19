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
    public partial class Tool_Editor
    {
        public const string EditorApplicationSetStateToolId = "editor-application-set-state";

        [AiTool
        (
            EditorApplicationSetStateToolId,
            Title = "Editor / Application / Set State",
            IdempotentHint = true
        )]
        [Description("Start or stop the Godot editor's play-run. Unlike Unity (an in-editor playmode " +
            "toggle), Godot launches the game in a SEPARATE process. Use 'editor-application-get-state' to " +
            "inspect the current state first.\n" +
            "Inputs:\n" +
            "  - 'isPlaying' (default false): true starts a run, false stops any active run.\n" +
            "  - 'scene' (default 'main'): which scene to run when starting — 'main' runs the project's main " +
            "scene (EditorInterface.PlayMainScene), 'current' runs the currently-edited scene " +
            "(EditorInterface.PlayCurrentScene), or a res:// path runs that specific scene " +
            "(EditorInterface.PlayCustomScene). Ignored when 'isPlaying' is false.\n" +
            "Returns the post-change EditorStateData snapshot.")]
        public EditorStateData SetState
        (
            [Description("If true, start a play-run; if false, stop any active run.")]
            bool isPlaying = false,
            [Description("Which scene to run when starting: 'main' (project main scene), 'current' (the " +
                "currently-edited scene), or a res:// path to a specific .tscn. Ignored when isPlaying is false.")]
            string scene = "main"
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var ei = EditorInterface.Singleton;

                if (!isPlaying)
                {
                    ei.StopPlayingScene();
                    return ToEditorStateData();
                }

                var which = string.IsNullOrWhiteSpace(scene) ? "main" : scene.Trim();

                if (which.Equals("main", StringComparison.OrdinalIgnoreCase))
                {
                    ei.PlayMainScene();
                }
                else if (which.Equals("current", StringComparison.OrdinalIgnoreCase))
                {
                    var editedRoot = ei.GetEditedSceneRoot();
                    if (editedRoot == null)
                        throw new Exception("Cannot run the current scene: no scene is currently being edited.");
                    ei.PlayCurrentScene();
                }
                else
                {
                    // Treat anything else as a res:// scene path.
                    if (!which.StartsWith("res://", StringComparison.Ordinal))
                        throw new Exception($"'scene' must be 'main', 'current', or a res:// path; got '{scene}'.");
                    if (!ResourceLoader.Exists(which))
                        throw new Exception($"Scene not found at '{which}'.");
                    ei.PlayCustomScene(which);
                }

                return ToEditorStateData();
            });
        }
    }
}
#endif
