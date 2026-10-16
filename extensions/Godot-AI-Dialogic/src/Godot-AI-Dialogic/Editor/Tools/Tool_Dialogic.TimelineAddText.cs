/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    public partial class Tool_Dialogic
    {
        /// <summary>
        /// Editor-only tool — append a text event to an existing Dialogic timeline (<c>.dtl</c>). Reads the
        /// timeline's plain text, appends one <c>Speaker: text</c> (or plain <c>text</c>) line via the
        /// version-stable text helper, then re-saves through the addon's timeline resource. Presence-gated;
        /// main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            TimelineAddTextToolId,
            Title = "Dialogic / Timeline Add Text"
        )]
        [Description("Append a text event to an existing Dialogic timeline (.dtl). 'resourcePath' is the timeline " +
            "to extend ('.dtl' appended if missing); 'text' is the new line and 'speaker' (optional) names the " +
            "speaking character. Returns the structured result (Installed, ResourcePath, EventCount, TimelineText). " +
            "Errors if the timeline does not exist. If the Dialogic addon is not installed, returns Installed:false " +
            "with an install hint instead of crashing.")]
        public DialogicResourceInfo TimelineAddText
        (
            [Description("res:// path of the existing timeline to extend, e.g. 'res://timelines/intro.dtl'.")]
            string resourcePath,
            [Description("The text of the event to append.")]
            string text,
            [Description("Optional speaking character name. When omitted, the appended line has no speaker.")]
            string? speaker = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var gate = GateInstalled();
                if (gate != null) return gate;

                var path = NormalizeTimelinePath(resourcePath);
                if (!FileAccess.FileExists(path))
                    throw new ArgumentException(
                        $"No Dialogic timeline at '{path}'. Create it first with '{TimelineCreateToolId}'.",
                        nameof(resourcePath));

                var existing = ReadAllText(path);
                var newLine = DialogicTimelineText.BuildTextLine(speaker, text);
                var combined = DialogicTimelineText.Append(existing, newLine);

                return SaveTimelineFromText(path, combined);
            });
        }
    }
}
#endif
