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
        public static readonly DialogicApi Instance = new DialogicApi();

        [RqlDescription("whether the Dialogic addon is installed (its DialogicTimeline class is registered)")]
        public bool installed => DialogicScripts.Has(DialogicNames.TimelineClass);

        [RqlDescription("how to install the addon when it is missing, else null")]
        public string? hint => installed ? null : DialogicNames.InstallHint;

        [RqlDescription("every .dtl timeline of the project (res://, addons/ and hidden folders skipped): path name eventCount text resource; call AddText")]
        public List<DialogicTimelineRow> timelines
        {
            get
            {
                var list = new List<DialogicTimelineRow>();
                if (installed)
                    foreach (var path in Files(DialogicNames.TimelineExtension)) list.Add(new DialogicTimelineRow(path));
                return list;
            }
        }

        [RqlDescription("every .dch character of the project (res://, addons/ and hidden folders skipped): path name displayName color description resource")]
        public List<DialogicCharacterRow> characters
        {
            get
            {
                var list = new List<DialogicCharacterRow>();
                if (installed)
                    foreach (var path in Files(DialogicNames.CharacterExtension)) list.Add(new DialogicCharacterRow(path));
                return list;
            }
        }

        [RqlCallable("create a timeline file (res://….dtl; .dtl is appended when missing) with one text line (speaker optional; no text: a two-line starter); fails if the file exists; not undoable; returns its path")]
        public string CreateTimeline(string path, string? text = null, string? speaker = null)
        {
            RequireInstalled();
            var resPath = NewFilePath(path, DialogicNames.TimelineExtension);
            SaveTimeline(resPath, string.IsNullOrWhiteSpace(text) ? DialogicNames.StarterTimelineText() : DialogicTimelineText.BuildTextLine(speaker, text));
            return resPath;
        }

        [RqlCallable("create a character file (res://….dch; .dch is appended when missing) with a display name, a color (\"#rrggbb\", default \"#3aa0ff\") and a description; fails if the file exists; not undoable; returns its path")]
        public string CreateCharacter(string path, string displayName, string? color = null, string? description = null)
        {
            RequireInstalled();
            Check(DialogicRules.DisplayName(displayName));
            Check(DialogicRules.Color(color, out var hex));
            var resPath = NewFilePath(path, DialogicNames.CharacterExtension);
            var character = DialogicScripts.New(DialogicNames.CharacterClass);
            character.Set(DialogicNames.DisplayName, displayName.Trim());
            character.Set(DialogicNames.Color, global::Godot.Color.FromHtml(hex));
            character.Set(DialogicNames.Description, description ?? string.Empty);
            Save(character, resPath);
            return resPath;
        }

        /// <summary>Write a timeline's text to its file through Dialogic's saver and make the saved resource the cached one.</summary>
        internal static void SaveTimeline(string resPath, string text)
        {
            var timeline = DialogicScripts.New(DialogicNames.TimelineClass);
            timeline.Call(DialogicNames.FromTextMethod, text);
            timeline.SetMeta(DialogicNames.NotSavedMeta, true);
            Save(timeline, resPath);
        }

        internal static void RequireInstalled()
        {
            if (!DialogicScripts.Has(DialogicNames.TimelineClass)) throw new InvalidOperationException(DialogicNames.InstallHint);
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

        private static string NewFilePath(string path, string extension)
        {
            Check(DialogicRules.ResourcePath(path, extension, out var resPath));
            if (FileAccess.FileExists(resPath))
                throw new ArgumentException($"'{resPath}' already exists (read it from dialogic.{(extension == DialogicNames.TimelineExtension ? "timelines" : "characters")})");
            return resPath;
        }

        private static void Check(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }

        /// <summary>The res:// files with the extension, sorted, skipping hidden folders and res://addons.</summary>
        private static List<string> Files(string extension)
        {
            var found = new List<string>();
            var pending = new Stack<string>();
            pending.Push("res://");
            while (pending.Count > 0)
            {
                var dir = pending.Pop();
                var prefix = dir.EndsWith("/", StringComparison.Ordinal) ? dir : dir + "/";
                foreach (var file in DirAccess.GetFilesAt(dir))
                    if (file.EndsWith("." + extension, StringComparison.OrdinalIgnoreCase)) found.Add(prefix + file);
                foreach (var sub in DirAccess.GetDirectoriesAt(dir))
                    if (!sub.StartsWith(".", StringComparison.Ordinal) && !(dir == "res://" && sub == "addons")) pending.Push(prefix + sub);
            }
            found.Sort(StringComparer.Ordinal);
            return found;
        }
    }
}
#endif
