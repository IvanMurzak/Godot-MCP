/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Data
{
    /// <summary>
    /// Structured snapshot of the Godot editor's run/play state — the Godot analog of Unity-MCP's
    /// <c>EditorStatsData</c>. Where Unity exposes a single in-editor "playmode" toggle
    /// (<c>EditorApplication.isPlaying</c>), Godot's editor launches the game in a SEPARATE OS process
    /// (<see cref="global::Godot.EditorInterface.PlayMainScene"/> /
    /// <see cref="global::Godot.EditorInterface.StopPlayingScene"/>); this model captures whether such a
    /// run is currently active (<see cref="IsPlaying"/>), the <c>res://</c> path of the scene being run
    /// (<see cref="PlayingScene"/>), and the editor's version string. Godot has no editor-side "paused"
    /// or "compiling" play state to mirror, so those Unity fields have no analog here.
    ///
    /// Pure-managed (no Godot API surface, no <c>#if TOOLS</c>), so it is unit-testable in the plain
    /// xUnit host. The editor handler fills it from <see cref="global::Godot.EditorInterface"/> on the
    /// main thread.
    /// </summary>
    [System.Serializable]
    [Description("Snapshot of the Godot editor run/play state: whether a play-run is active, the res:// " +
        "path of the scene being run (if any), and the editor version string.")]
    public class EditorStateData
    {
        [JsonInclude, JsonPropertyName("isPlaying")]
        [Description("True when the editor is currently running a scene in a separate game process " +
            "(EditorInterface.IsPlayingScene()).")]
        public bool IsPlaying { get; set; } = false;

        [JsonInclude, JsonPropertyName("playingScene")]
        [Description("res:// path of the scene currently being run, or null when not playing " +
            "(EditorInterface.GetPlayingScene()).")]
        public string? PlayingScene { get; set; } = null;

        [JsonInclude, JsonPropertyName("editorVersion")]
        [Description("Godot editor version string, e.g. '4.5.1.stable.mono' (Engine.GetVersionInfo()['string']).")]
        public string EditorVersion { get; set; } = string.Empty;

        public EditorStateData() { }

        public override string ToString()
            => $"Editor play={IsPlaying} scene='{PlayingScene ?? "(none)"}' version='{EditorVersion}'";
    }
}
