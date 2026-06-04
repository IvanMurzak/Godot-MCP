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
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Editor application tool family (<c>editor-application-*</c>) — the Godot analog of Unity-MCP's
    /// <c>Tool_Editor</c>. Each tool method lives in its own partial-class file (GetState / SetState) and
    /// drives the editor's run/play lifecycle via <see cref="EditorInterface"/> through the main-thread
    /// dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: Unity toggles an IN-editor playmode (<c>EditorApplication.isPlaying</c>);
    /// Godot launches the game in a SEPARATE OS process via <see cref="EditorInterface.PlayMainScene"/> /
    /// <see cref="EditorInterface.PlayCurrentScene"/> / <see cref="EditorInterface.PlayCustomScene"/> and
    /// stops it via <see cref="EditorInterface.StopPlayingScene"/>; <see cref="EditorInterface.IsPlayingScene"/>
    /// reports whether a run is active. There is no editor-side "paused" / "compiling" play state to mirror,
    /// so those Unity fields have no Godot analog (see <see cref="EditorStateData"/>).
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>). The pure-managed <see cref="EditorStateData"/> model lives outside
    /// this guard and is unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Editor
    {
        /// <summary>
        /// Build an <see cref="EditorStateData"/> from the live editor. Main-thread only.
        /// </summary>
        internal static EditorStateData ToEditorStateData()
        {
            var ei = EditorInterface.Singleton;
            var isPlaying = ei.IsPlayingScene();
            var playingScene = isPlaying ? ei.GetPlayingScene() : null;

            return new EditorStateData
            {
                IsPlaying = isPlaying,
                PlayingScene = string.IsNullOrEmpty(playingScene) ? null : playingScene,
                EditorVersion = Engine.GetVersionInfo()["string"].AsString(),
            };
        }
    }
}
#endif
