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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        /// <summary>Most entries the <c>console</c> source returns (the collector's capacity).</summary>
        public const int MaxLogEntries = GodotLogCollector.Capacity;

        /// <summary>The <c>console</c> source: the captured editor log lines, oldest first (stack traces included).</summary>
        public static List<LogEntry> ConsoleEntries() =>
            GodotLogCollector.Current is { } logs
                ? logs.Query(maxEntries: MaxLogEntries, includeStackTrace: true).OrderBy(e => e.Sequence).ToList()
                : new List<LogEntry>();

        /// <summary>The <c>files</c> source: every file the editor filesystem knows, depth-first (at most <see cref="RqlGodot.MaxFiles"/>).</summary>
        public static List<RqlFile> Files()
        {
            var list = new List<RqlFile>();
            if (Ei.GetResourceFilesystem()?.GetFilesystem() is { } root) Collect(root, list);
            return list;
        }

        private static void Collect(EditorFileSystemDirectory dir, List<RqlFile> sink)
        {
            var fileCount = dir.GetFileCount();
            for (int i = 0; i < fileCount && sink.Count < RqlGodot.MaxFiles; i++)
                sink.Add(new RqlFile(dir.GetFilePath(i), dir.GetFileType(i).ToString()));
            var subCount = dir.GetSubdirCount();
            for (int i = 0; i < subCount && sink.Count < RqlGodot.MaxFiles; i++)
                if (dir.GetSubdir(i) is { } sub) Collect(sub, sink);
        }

        /// <summary>The <c>classes</c> source: every Godot class (ClassDB), sorted by name.</summary>
        public static List<RqlClass> Classes() =>
            ClassDB.GetClassList().OrderBy(c => c, StringComparer.Ordinal).Select(c => new RqlClass(c)).ToList();
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
        public string dir => path.Substring(0, path.LastIndexOf('/') + 1);

        [RqlDescription("extension without the dot, lower case (\"tscn\", \"png\")")]
        public string ext => ReimportClassifier.ExtensionOf(path);

        [RqlDescription("resource type the importer assigned (\"PackedScene\", \"Texture2D\" …), empty when unknown")]
        public string type { get; }

        [RqlDescription("uid:// of the file, or null")]
        public string? uid
        {
            get
            {
                var id = ResourceLoader.GetResourceUid(path);
                return id == ResourceUid.InvalidId ? null : ResourceUid.IdToText(id);
            }
        }
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
