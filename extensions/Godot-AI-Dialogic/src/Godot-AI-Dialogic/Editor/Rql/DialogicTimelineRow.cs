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
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// One Dialogic timeline file (a row of <c>dialogic.timelines</c>). A timeline is its text (one event per line), read
    /// from the file; append a text line with <c>AddText</c> (writes the file, not undoable).
    /// </summary>
    [RqlDescription("Dialogic timeline (read-only row): path name eventCount text resource; call AddText(text, speaker?)")]
    public sealed class DialogicTimelineRow
    {
        private readonly DialogicApi _api;
        private string? _text;

        internal DialogicTimelineRow(string path, DialogicApi api)
        {
            this.path = path;
            _api = api;
        }

        [RqlDescription("res:// path of the .dtl file")]
        public string path { get; }

        [RqlDisplay, RqlDescription("file name without the extension (Dialogic's timeline identifier)")]
        public string name => path.Substring(path.LastIndexOf('/') + 1).GetBaseName();

        [RqlDescription("the timeline text (Dialogic's .dtl format: one event per line, \"Speaker: text\" for a text event)")]
        public string text => _text ??= FileAccess.GetFileAsString(path);

        [RqlDescription("number of non-empty lines (events) in the text")]
        public int eventCount => DialogicTimelineText.CountEvents(text);

        [RqlReference, RqlDescription("the DialogicTimeline resource (loaded on access)")]
        public Resource? resource => ResourceLoader.Load(path);

        [RqlCallable("append a text event (\"Speaker: text\"; speaker optional, colons escaped) to the timeline file; not undoable; returns the new event count")]
        public int AddText(string text, string? speaker = null)
        {
            _api.RequireInstalled();
            var combined = DialogicTimelineText.Append(FileAccess.GetFileAsString(path), DialogicTimelineText.BuildTextLine(speaker, text));
            _api.SaveTimeline(path, combined);
            _text = combined;
            return DialogicTimelineText.CountEvents(combined);
        }
    }
}
#endif
