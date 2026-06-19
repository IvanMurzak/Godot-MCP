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
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// FileSystem tool family (<c>filesystem-*</c>) — the Godot analog of the read-only "browse the
    /// project" half of Unity-MCP's <c>Tool_Assets</c> (<c>assets-find</c>). It walks the editor's
    /// <see cref="EditorFileSystem"/> — the project-aware index of the <c>res://</c> tree — through the
    /// main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: <see cref="EditorFileSystem"/> ↔ Unity's <c>AssetDatabase</c>; an
    /// <see cref="EditorFileSystemDirectory"/> ↔ a folder of asset GUIDs; the importer-assigned
    /// per-file type ↔ a Unity asset's main type. The <see cref="EditorFileSystem"/> already knows each
    /// file's resource type + uid without loading the resource, so listing is cheap and side-effect-free.
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>): the handlers touch <see cref="EditorInterface"/> and
    /// <see cref="EditorFileSystem"/>, neither of which exists in a plain (non-editor) build. The
    /// pure-managed result models (<see cref="Data.FileSystemEntry"/>/<see cref="Data.FileSystemListing"/>)
    /// live outside this guard and are unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_FileSystem
    {
    }
}
#endif
