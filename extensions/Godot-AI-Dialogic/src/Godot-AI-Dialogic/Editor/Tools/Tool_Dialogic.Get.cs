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
        /// Editor-only, read-only tool — read the scalar config of a Dialogic timeline (<c>.dtl</c>: event count +
        /// text) or character (<c>.dch</c>: display name, color, description) addressed by a <c>res://</c> path.
        /// Presence-gated; main-thread-marshalled; does not modify the project.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Dialogic / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read a Dialogic timeline (.dtl) or character (.dch) resource and return its scalar config: " +
            "for a timeline, EventCount + TimelineText; for a character, DisplayName + Color + Description. The kind " +
            "is chosen by the file extension. Read-only. If the Dialogic addon is not installed, returns " +
            "Installed:false with an install hint instead of crashing.")]
        public DialogicResourceInfo Get
        (
            [Description("res:// path of a Dialogic timeline (.dtl) or character (.dch) to read.")]
            string resourcePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var gate = GateInstalled();
                if (gate != null) return gate;

                var path = NormalizeResPath(resourcePath);
                if (!FileAccess.FileExists(path))
                    throw new ArgumentException($"No file at '{path}'.", nameof(resourcePath));

                var kind = DialogicResourceKind.FromPath(path);

                if (kind == DialogicResourceKind.Timeline)
                {
                    var timelineText = ReadAllText(path);
                    return new DialogicResourceInfo
                    {
                        Installed = true,
                        ResourceKind = DialogicResourceKind.Timeline,
                        ResourcePath = path,
                        TimelineText = timelineText,
                        EventCount = DialogicTimelineText.CountEvents(timelineText),
                    };
                }

                // Character: load via the addon's auto-registered ResourceFormatLoader (fresh, no cache).
                var character = ResourceLoader.Load(path, "", ResourceLoader.CacheMode.Ignore)
                    ?? throw new InvalidOperationException($"Could not load Dialogic character '{path}'.");

                var displayName = character.Get(DialogicAddon.DisplayNameMember).AsString();
                var color = character.Get(DialogicAddon.ColorMember).AsColor();
                var description = character.Get(DialogicAddon.DescriptionMember).AsString();

                return new DialogicResourceInfo
                {
                    Installed = true,
                    ResourceKind = DialogicResourceKind.Character,
                    ResourcePath = path,
                    DisplayName = displayName,
                    Color = "#" + color.ToHtml(false).ToLowerInvariant(),
                    Description = description,
                };
            });
        }
    }
}
#endif
