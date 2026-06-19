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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Scene tool family (<c>scene-*</c>) — the Godot analog of Unity-MCP's <c>Tool_Scene</c>. Each tool
    /// method lives in its own partial-class file (Open / Save / Create / ListOpened / GetData) and drives
    /// the Godot editor's scene set via <see cref="EditorInterface"/> through the main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: a Godot scene is a <see cref="PackedScene"/> on disk (<c>res://*.tscn</c>)
    /// whose instanced root <see cref="Node"/> is what the editor edits. Godot 4.3's editor API exposes
    /// the open-scene set as a flat list of <c>res://</c> paths (<see cref="EditorInterface.GetOpenScenes"/>)
    /// plus the single currently-edited root (<see cref="EditorInterface.GetEditedSceneRoot"/>); there is
    /// no per-open-scene root accessor in 4.3, so <see cref="ListOpened"/> reports paths and flags which
    /// one is active.
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>). The pure-managed <see cref="SceneData"/> model lives outside this
    /// guard and is unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Scene
    {
        /// <summary>
        /// Build a <see cref="SceneData"/> for the editor's currently-edited scene from its root node.
        /// Main-thread only. Returns a model with <see cref="SceneData.IsActive"/> set true.
        /// </summary>
        internal static SceneData ToActiveSceneData(Node editedRoot)
        {
            var path = editedRoot.GetSceneFilePath();
            return new SceneData
            {
                ResourcePath = string.IsNullOrEmpty(path) ? null : path,
                RootName = editedRoot.Name.ToString(),
                RootInstanceId = editedRoot.GetInstanceId(),
                IsActive = true,
            };
        }

        /// <summary>
        /// Build a path-only <see cref="SceneData"/> for an open scene that is NOT the active one (Godot
        /// 4.3 exposes no root accessor for non-active open scenes). Main-thread only.
        /// </summary>
        internal static SceneData ToOpenSceneData(string resourcePath, bool isActive)
            => new SceneData
            {
                ResourcePath = string.IsNullOrEmpty(resourcePath) ? null : resourcePath,
                RootName = null,
                RootInstanceId = 0,
                IsActive = isActive,
            };
    }
}
#endif
