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
    /// One entry in a <c>res://</c> directory listing — either a sub-directory or an importable file.
    /// The Godot analog of a single row of Unity-MCP's <c>assets-find</c> result, built from Godot's
    /// <see cref="global::Godot.EditorFileSystemDirectory"/> on the main thread and then serialized off it.
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>): it holds plain strings/bools captured
    /// from the editor filesystem, so it is unit-testable in the plain xUnit host.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("One entry in a res:// directory listing: a sub-directory or a file, with its res:// path, " +
        "name, kind, and (for files) resource type + uid.")]
    public class FileSystemEntry
    {
        [JsonInclude, JsonPropertyName("name")]
        [Description("The entry's leaf name (last path segment), e.g. 'wood.tres' or 'materials'.")]
        public string Name { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("path")]
        [Description("Full res:// path of the entry. Directories end with a trailing '/', e.g. 'res://materials/'.")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("isDirectory")]
        [Description("True when this entry is a directory; false when it is a file.")]
        public bool IsDirectory { get; set; } = false;

        [JsonInclude, JsonPropertyName("resourceType")]
        [Description("For files: the Godot resource type the importer assigned (e.g. 'PackedScene', " +
            "'Texture2D', 'Resource'), or null for directories / unimported files.")]
        public string? ResourceType { get; set; } = null;

        [JsonInclude, JsonPropertyName("uid")]
        [Description("For files: the resource UID string (e.g. 'uid://abc123') when the file has one, else null. " +
            "Directories have no uid.")]
        public string? Uid { get; set; } = null;

        public FileSystemEntry() { }

        public override string ToString()
            => IsDirectory ? $"[dir] {Path}" : $"[file] {Path} ({ResourceType ?? "?"})";
    }
}
