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
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Structured result of a <c>filesystem-list</c> call: the listed directory's <c>res://</c> path plus
    /// its immediate sub-directories and files (each a <see cref="FileSystemEntry"/>). The Godot analog of
    /// a single-directory <c>assets-find</c> result.
    ///
    /// <para>
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain xUnit
    /// host.
    /// </para>
    /// </summary>
    [System.Serializable]
    [Description("A res:// directory listing: the directory's path, its immediate sub-directories, and its " +
        "immediate files.")]
    public class FileSystemListing
    {
        [JsonInclude, JsonPropertyName("path")]
        [Description("res:// path of the listed directory (always ends with '/'), e.g. 'res://' or 'res://materials/'.")]
        public string Path { get; set; } = string.Empty;

        [JsonInclude, JsonPropertyName("directoryCount")]
        [Description("Number of immediate sub-directories in this directory.")]
        public int DirectoryCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("fileCount")]
        [Description("Number of immediate files in this directory.")]
        public int FileCount { get; set; } = 0;

        [JsonInclude, JsonPropertyName("entries")]
        [Description("Immediate children of the directory (sub-directories first, then files). Each entry " +
            "carries its res:// path, kind, and — for files — resource type and uid.")]
        public List<FileSystemEntry> Entries { get; set; } = new();

        public FileSystemListing() { }

        public override string ToString()
            => $"Listing '{Path}' ({DirectoryCount} dirs, {FileCount} files)";
    }
}
