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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>The project sources of the <c>editor</c> API: <c>files</c> (the editor filesystem) and <c>classes</c> (ClassDB).</summary>
    public sealed partial class RqlEditorApi
    {
        /// <summary>
        /// The <c>files</c> source: every file the editor filesystem knows, depth-first, at most
        /// <see cref="RqlGodot.MaxFiles"/>; lazy, so <c>files | first 5</c> walks only that far.
        /// </summary>
        public static IEnumerable<RqlFile> Files() =>
            RqlGodot.WalkFiles(EditorToolGuards.GetResourceFileSystemOrThrow().GetFilesystem())
                .Select(f => new RqlFile(f.path, f.type))
                .Take(RqlGodot.MaxFiles);

        /// <summary>The <c>classes</c> source: every Godot class (ClassDB), sorted by name (the list is read once per session).</summary>
        public static List<RqlClass> Classes() => GodotTypes.GodotClasses().Select(c => new RqlClass(c)).ToList();
    }

    /// <summary>One file of the project (the <c>files</c> source).</summary>
    public sealed class RqlFile
    {
        public RqlFile(string path, string type) { this.path = path; this.type = type; }

        [RqlDescription("res:// path")]
        public string path { get; }

        [RqlDescription("file name with extension")]
        public string name => path.Substring(path.LastIndexOf('/') + 1);

        [RqlDescription("res:// folder (with a trailing slash)")]
        public string dir => ResPathNormalizer.ParentDir(path);

        [RqlDescription("extension without the dot, lower case (\"tscn\", \"png\")")]
        public string ext => ReimportClassifier.ExtensionOf(path);

        [RqlDescription("resource type the importer assigned (\"PackedScene\", \"Texture2D\" …), empty when unknown")]
        public string type { get; }

        [RqlDescription("uid:// of the file, or null")]
        public string? uid => RqlGodot.UidOf(path);
    }

    /// <summary>One Godot class (the <c>classes</c> source).</summary>
    public sealed class RqlClass
    {
        public RqlClass(string name) { this.name = name; }

        [RqlDescription("class name, e.g. \"OmniLight3D\"")]
        public string name { get; }

        [RqlDescription("parent class name (empty for Object)")]
        public string parent => ClassDB.GetParentClass(name).ToString();

        [RqlDescription("can be created (AddChild / CreateResource)")]
        public bool canInstantiate => ClassDB.CanInstantiate(name);

        [RqlDescription("a Node class")]
        public bool isNode => ClassDB.IsParentClass(name, "Node");

        [RqlDescription("a Resource class")]
        public bool isResource => ClassDB.IsParentClass(name, "Resource");
    }
}
#endif
