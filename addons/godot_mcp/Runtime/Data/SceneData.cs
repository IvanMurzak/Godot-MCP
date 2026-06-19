/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Shallow, structured snapshot of an open Godot scene (the Godot analog of Unity-MCP's
    /// <c>SceneDataShallow</c>). A Godot "scene" is a <see cref="global::Godot.PackedScene"/> on disk
    /// whose instanced root <see cref="global::Godot.Node"/> is edited in the editor; this model
    /// captures the scene's <c>res://</c> path, root-node identity, and whether it is the editor's
    /// currently-edited (active) scene.
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain
    /// xUnit host.
    /// </summary>
    [System.Serializable]
    [Description("Shallow snapshot of an open Godot scene: res:// path, root-node name/instanceId, and " +
        "whether it is the editor's active (edited) scene.")]
    public class SceneData
    {
        [JsonInclude, JsonPropertyName("resourcePath")]
        [Description("res:// path of the scene's .tscn file, or null for an unsaved/new scene.")]
        public string? ResourcePath { get; set; } = null;

        [JsonInclude, JsonPropertyName("rootName")]
        [Description("Name of the scene's root Node, or null when the scene has no root yet.")]
        public string? RootName { get; set; } = null;

        [JsonInclude, JsonPropertyName("rootInstanceId")]
        [Description("Instance id of the scene's root Node (0 when no root / not currently instanced in the editor).")]
        public ulong RootInstanceId { get; set; } = 0;

        [JsonInclude, JsonPropertyName("isActive")]
        [Description("True when this scene is the editor's currently-edited (active) scene.")]
        public bool IsActive { get; set; } = false;

        public SceneData() { }

        public override string ToString()
            => $"Scene '{ResourcePath ?? "(unsaved)"}' root='{RootName}' active={IsActive}";
    }
}
