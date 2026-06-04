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
        public const string SceneSaveToolId = "scene-save";

        [AiTool
        (
            SceneSaveToolId,
            Title = "Scene / Save",
            IdempotentHint = true
        )]
        [Description("Save the currently edited Godot scene. With no 'path', saves back to the scene's " +
            "existing res:// file (fails if the scene has never been saved — pass a 'path' in that case). " +
            "With a 'path' (a res://*.tscn), saves the scene to that new location (save-as). Returns the " +
            "saved scene's structured data.")]
        public SceneData Save
        (
            [Description("Optional res:// destination path ending in '.tscn'/'.scn' for a save-as. When " +
                "omitted, the scene is saved back to its existing file.")]
            string? path = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var editedRoot = EditorInterface.Singleton.GetEditedSceneRoot()
                    ?? throw new Exception("No scene is currently being edited; nothing to save.");

                if (!string.IsNullOrEmpty(path))
                {
                    if (!path!.StartsWith("res://", StringComparison.Ordinal))
                        throw new ArgumentException($"path must be a 'res://' path; got '{path}'.", nameof(path));
                    if (!EndsWithSceneExt(path))
                        throw new ArgumentException($"path must end with '.tscn' or '.scn'; got '{path}'.", nameof(path));

                    // SaveSceneAs returns void (no Error), so a silent failure (e.g. an unwritable target
                    // dir) would otherwise be reported as success. Confirm the edited scene's file path now
                    // points at the requested destination; a mismatch means the save did not land.
                    EditorInterface.Singleton.SaveSceneAs(path);

                    var savedPath = EditorInterface.Singleton.GetEditedSceneRoot()?.GetSceneFilePath();
                    if (savedPath != path)
                        throw new Exception(
                            $"Save-as to '{path}' did not take effect (edited scene path is now '{savedPath ?? "<none>"}').");
                }
                else
                {
                    var existingPath = editedRoot.GetSceneFilePath();
                    if (string.IsNullOrEmpty(existingPath))
                        throw new Exception("The edited scene has never been saved; provide a 'path' to save it for the first time.");

                    var err = EditorInterface.Singleton.SaveScene();
                    if (err != Error.Ok)
                        throw new Exception($"Failed to save scene '{existingPath}': {err}.");
                }

                // Re-read the edited root (SaveSceneAs re-points the scene's file path).
                var rootAfter = EditorInterface.Singleton.GetEditedSceneRoot() ?? editedRoot;
                return ToActiveSceneData(rootAfter);
            });
        }

        static bool EndsWithSceneExt(string path)
            => path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase);
    }
}
#endif
