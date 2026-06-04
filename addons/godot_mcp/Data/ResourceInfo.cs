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
    /// Lightweight identity record for a resource on disk — returned by <c>resource-find</c> and as the
    /// confirmation payload of <c>resource-create</c>/<c>resource-move</c>/<c>resource-delete</c>. Carries
    /// the resource's <c>res://</c> path, its <c>uid://</c> (when assigned), and the Godot type the
    /// importer recorded for it. The Godot analog of a single <c>assets-find</c> hit (path + GUID + type).
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("Identity of a resource on disk: its res:// path, uid:// (when assigned), and Godot type.")]
    public class ResourceInfo
    {
        [JsonInclude, JsonPropertyName("resourcePath")]
        [Description("res:// path of the resource, e.g. 'res://materials/wood.tres'.")]
        public string ResourcePath { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("uid")]
        [Description("uid:// identifier of the resource (e.g. 'uid://abc123'), or null when the resource has no uid.")]
        public string? Uid { get; set; } = null;

        [JsonInclude, JsonPropertyName("type")]
        [Description("Godot type recorded for the resource by the import pipeline (e.g. 'PackedScene', " +
            "'Texture2D', 'Resource'), or null when unknown.")]
        public string? Type { get; set; } = null;

        public ResourceInfo() { }

        public override string ToString()
            => $"Resource '{ResourcePath}' uid='{Uid ?? "(none)"}' type='{Type ?? "(unknown)"}'";
    }
}
