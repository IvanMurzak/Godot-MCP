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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    public partial class Tool_Dialogic
    {
        /// <summary>
        /// Editor-only tool — create + save a Dialogic timeline resource (<c>.dtl</c>) at a <c>res://</c> path,
        /// seeded with one text event. Presence-gated; all Godot access is main-thread-marshalled. The Dialogic
        /// addon's timeline type is resolved by string name, so the package never names a Dialogic type.
        /// </summary>
        [AiTool
        (
            TimelineCreateToolId,
            Title = "Dialogic / Timeline Create"
        )]
        [Description("Create a Dialogic timeline resource (.dtl) at the given res:// path, seeded with one text " +
            "event. '.dtl' is appended to 'resourcePath' if missing. Pass 'text' (and optional 'speaker') for the " +
            "first line; when 'text' is omitted a 2-line starter timeline is written. Returns the structured " +
            "result (Installed, ResourcePath, EventCount, TimelineText). If the Dialogic addon is not installed, " +
            "returns Installed:false with an install hint instead of crashing.")]
        public DialogicResourceInfo TimelineCreate
        (
            [Description("res:// path for the new timeline, e.g. 'res://timelines/intro.dtl' ('.dtl' appended if missing).")]
            string resourcePath,
            [Description("Optional text for the first text event. When omitted, a starter timeline is written.")]
            string? text = null,
            [Description("Optional speaking character name for the first line. When omitted, the line has no speaker.")]
            string? speaker = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var gate = GateInstalled();
                if (gate != null) return gate;

                var path = NormalizeTimelinePath(resourcePath);
                var timelineText = string.IsNullOrWhiteSpace(text)
                    ? DialogicDefaults.StarterTimelineText()
                    : DialogicTimelineText.BuildTextLine(speaker, text);

                return SaveTimelineFromText(path, timelineText);
            });
        }
    }
}
#endif
