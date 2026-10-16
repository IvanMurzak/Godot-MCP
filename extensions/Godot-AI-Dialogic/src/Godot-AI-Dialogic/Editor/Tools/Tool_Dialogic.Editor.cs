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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// Editor-only shared helpers for the <c>dialogic-*</c> tools (behind <c>#if TOOLS</c>: they touch Godot
    /// static facades + live <c>Resource</c>s). Every method here is invoked ONLY from inside a
    /// <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor main thread.
    ///
    /// <para>
    /// The Dialogic addon's types are referenced by STRING NAME only (via <see cref="AddonInterop"/> and the
    /// <see cref="DialogicAddon"/> constants) — the package never names a Dialogic type, so it compiles with the
    /// addon absent. The presence gate (<see cref="GateInstalled"/>) is the mandatory first line of every tool.
    /// </para>
    /// </summary>
    public partial class Tool_Dialogic
    {
        /// <summary>
        /// The Class-B presence gate: returns null when the Dialogic addon is installed, or a structured
        /// <c>Installed: false</c> result when it is not. Must be the FIRST line of every editor tool's
        /// main-thread lambda.
        /// </summary>
        static DialogicResourceInfo? GateInstalled()
        {
            if (AddonInterop.GlobalClassExists(DialogicAddon.PresenceClass))
                return null;

            return DialogicResourceInfo.NotInstalled(
                AddonGateResult.NotInstalled(
                    DialogicAddon.AddonName, DialogicAddon.PresenceClass, DialogicAddon.InstallHint));
        }

        /// <summary>Normalize a user/LLM path into a clean <c>res://</c> path.</summary>
        static string NormalizeResPath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A resource path is required.", nameof(path));

            var p = path!.Trim().Replace('\\', '/');
            if (!p.StartsWith("res://"))
                p = "res://" + p.TrimStart('/');
            return p;
        }

        /// <summary>Ensure the path ends with the given (dotless) extension.</summary>
        static string EnsureExtension(string path, string ext)
        {
            return path.ToLowerInvariant().EndsWith("." + ext) ? path : path + "." + ext;
        }

        static string NormalizeTimelinePath(string? path) =>
            EnsureExtension(NormalizeResPath(path), DialogicAddon.TimelineExtension);

        static string NormalizeCharacterPath(string? path) =>
            EnsureExtension(NormalizeResPath(path), DialogicAddon.CharacterExtension);

        /// <summary>Read a project file as text (used for the plain-text <c>.dtl</c> timeline format).</summary>
        static string ReadAllText(string resPath)
        {
            using var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read);
            if (file == null)
                throw new InvalidOperationException(
                    $"Could not open '{resPath}' for reading ({FileAccess.GetOpenError()}).");
            return file.GetAsText();
        }

        /// <summary>
        /// Instantiate a Dialogic timeline from plain text and save it to <paramref name="path"/> via the addon's
        /// auto-registered <c>ResourceFormatSaver</c> (which only writes when the <c>timeline_not_saved</c> meta
        /// is set). Returns the structured timeline result.
        /// </summary>
        static DialogicResourceInfo SaveTimelineFromText(string path, string timelineText)
        {
            var timeline = AddonInterop.InstantiateScriptResource(DialogicAddon.TimelineClass)
                ?? throw new InvalidOperationException(
                    $"Could not instantiate '{DialogicAddon.TimelineClass}' — is the Dialogic addon installed?");

            timeline.Call(DialogicAddon.FromTextMethod, timelineText);
            timeline.SetMeta(DialogicAddon.TimelineNotSavedMeta, true);

            var err = ResourceSaver.Save(timeline, path);
            if (err != Error.Ok)
                throw new InvalidOperationException($"ResourceSaver.Save failed for '{path}': {err}.");

            return new DialogicResourceInfo
            {
                Installed = true,
                ResourceKind = DialogicResourceKind.Timeline,
                ResourcePath = path,
                TimelineText = timelineText,
                EventCount = DialogicTimelineText.CountEvents(timelineText),
            };
        }
    }
}
#endif
