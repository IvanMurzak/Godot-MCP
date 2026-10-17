/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Collections.Generic;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// The <c>dialogic</c> source: whether the addon is installed, the project's Dialogic files as read-only rows
    /// (<c>dialogic.timelines</c>, <c>dialogic.characters</c>) and the calls that create them. Dialogic works on resource
    /// files (<c>.dtl</c> timelines, <c>.dch</c> characters), not scene nodes. A character row holds the loaded resource in
    /// <c>resource</c>: <c>get resource | set props["…"]</c> writes it and the core saves the file (Dialogic's own saver).
    /// A timeline is its text, so lines are appended with the row's <c>AddText</c>. Creating and appending write files
    /// directly: they are not undoable (like <c>editor | call CreateResource</c>). Without the addon the lists are empty,
    /// <c>hint</c> says how to install it and the calls fail with that hint.
    /// </summary>
    [RqlDescription("Dialogic: installed, hint, timelines, characters; calls CreateTimeline, CreateCharacter")]
    public sealed class DialogicApi
    {
        // The source creates one instance per query: the class table and one walk of the editor filesystem (both file
        // kinds) are read once and shared by installed / hint / timelines / characters and the calls.
        private DialogicScripts? _scripts;
        private Dictionary<string, List<string>>? _files;

        internal DialogicScripts Scripts => _scripts ??= new DialogicScripts();

        [RqlDescription("whether the Dialogic addon is installed (its DialogicTimeline class is registered)")]
        public bool installed => Scripts.Has(DialogicNames.TimelineClass);

        [RqlDescription("how to install the addon when it is missing, else null")]
        public string? hint => installed ? null : DialogicNames.InstallHint;

        [RqlDescription("every .dtl timeline of the project (res://addons skipped): path name eventCount text resource; call AddText")]
        public List<DialogicTimelineRow> timelines => Rows(DialogicNames.TimelineExtension, path => new DialogicTimelineRow(path, this));

        [RqlDescription("every .dch character of the project (res://addons skipped): path name displayName color description resource")]
        public List<DialogicCharacterRow> characters => Rows(DialogicNames.CharacterExtension, path => new DialogicCharacterRow(path));

        [RqlCallable("create a timeline file (res://….dtl; .dtl is appended when missing) with one text line (speaker optional; no text: a two-line starter); fails if the file exists; not undoable; returns its path")]
        public string CreateTimeline(string path, string? text = null, string? speaker = null)
        {
            RequireInstalled();
            var resPath = NewFilePath(path, DialogicNames.TimelineExtension, "timelines");
            SaveTimeline(resPath, string.IsNullOrWhiteSpace(text) ? DialogicNames.StarterTimelineText() : DialogicTimelineText.BuildTextLine(speaker, text));
            return resPath;
        }

        [RqlCallable("create a character file (res://….dch; .dch is appended when missing) with a display name, a color (\"#rrggbb\", default \"#3aa0ff\") and a description; fails if the file exists; not undoable; returns its path")]
        public string CreateCharacter(string path, string displayName, string? color = null, string? description = null)
        {
            RequireInstalled();
            Check(DialogicRules.DisplayName(displayName));
            Check(DialogicRules.Color(color, out var hex));
            var resPath = NewFilePath(path, DialogicNames.CharacterExtension, "characters");
            var character = Scripts.New(DialogicNames.CharacterClass);
            character.Set(DialogicNames.DisplayName, displayName.Trim());
            character.Set(DialogicNames.Color, global::Godot.Color.FromHtml(hex));
            character.Set(DialogicNames.Description, description ?? string.Empty);
            Save(character, resPath);
            return resPath;
        }

        /// <summary>Write a timeline's text to its file through Dialogic's saver and make the saved resource the cached one.</summary>
        internal void SaveTimeline(string resPath, string text)
        {
            var timeline = Scripts.New(DialogicNames.TimelineClass);
            timeline.Call(DialogicNames.FromTextMethod, text);
            timeline.SetMeta(DialogicNames.NotSavedMeta, true);
            Save(timeline, resPath);
        }

        internal void RequireInstalled()
        {
            if (!installed) throw new InvalidOperationException(DialogicNames.InstallHint);
        }

        private List<T> Rows<T>(string extension, Func<string, T> make)
        {
            var list = new List<T>();
            if (installed)
                foreach (var path in Files(extension)) list.Add(make(path));
            return list;
        }

        private static void Save(Resource resource, string resPath)
        {
            var dir = resPath.Substring(0, resPath.LastIndexOf('/'));
            if (dir != "res:/" && !DirAccess.DirExistsAbsolute(dir))
            {
                var made = DirAccess.MakeDirRecursiveAbsolute(dir);
                if (made != Error.Ok) throw new InvalidOperationException($"could not create the folder '{dir}': {made}");
            }
            var error = ResourceSaver.Save(resource, resPath);
            if (error != Error.Ok) throw new InvalidOperationException($"could not save '{resPath}': {error}");
            resource.TakeOverPath(resPath);
            EditorInterface.Singleton.GetResourceFilesystem()?.UpdateFile(resPath);
        }

        private static string NewFilePath(string path, string extension, string list)
        {
            Check(DialogicRules.ResourcePath(path, extension, out var resPath));
            if (FileAccess.FileExists(resPath)) throw new ArgumentException($"'{resPath}' already exists (read it from dialogic.{list})");
            return resPath;
        }

        private static void Check(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }

        /// <summary>
        /// The res:// files with the extension, sorted, from the editor filesystem index (one walk for both extensions per
        /// query; res://addons skipped).
        /// </summary>
        private List<string> Files(string extension)
        {
            if (_files == null)
            {
                _files = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    [DialogicNames.TimelineExtension] = new List<string>(),
                    [DialogicNames.CharacterExtension] = new List<string>(),
                };
                if (EditorInterface.Singleton.GetResourceFilesystem()?.GetFilesystem() is { } root) Collect(root, _files, isRoot: true);
                foreach (var found in _files.Values) found.Sort(StringComparer.Ordinal);
            }
            return _files[extension];
        }

        private static void Collect(EditorFileSystemDirectory dir, Dictionary<string, List<string>> files, bool isRoot)
        {
            var fileCount = dir.GetFileCount();
            for (int i = 0; i < fileCount; i++)
            {
                var path = dir.GetFilePath(i);
                var dot = path.LastIndexOf('.');
                if (dot >= 0 && files.TryGetValue(path.Substring(dot + 1), out var list)) list.Add(path);
            }
            var subCount = dir.GetSubdirCount();
            for (int i = 0; i < subCount; i++)
                if (dir.GetSubdir(i) is { } sub && !(isRoot && sub.GetName() == "addons")) Collect(sub, files, isRoot: false);
        }
    }
}
#endif
