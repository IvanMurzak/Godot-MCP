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
using System.Linq;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// The <c>editor</c> source: editor state and the operations that have no natural owner object — the replacement of the
    /// <c>editor-application-*</c> / <c>editor-selection-*</c>, <c>scene-*</c>, <c>resource-create/move/delete</c> and
    /// <c>filesystem-reimport</c> tools, e.g. <c>editor | call NewScene("res://levels/a.tscn", "Node3D")</c>,
    /// <c>editor | set selection = [godot.find("Player")]</c>. Calls go through rql-mutate (mutation policy, dry-run,
    /// receipts).
    /// </summary>
    public sealed partial class RqlEditorApi
    {
        public static readonly RqlEditorApi Instance = new RqlEditorApi();

        private static EditorInterface Ei => EditorInterface.Singleton;

        [RqlDescription("true while a scene is running from the editor (a separate game process); assign true to run the main scene, false to stop")]
        public bool isPlaying
        {
            get => Ei.IsPlayingScene();
            set { if (value) Ei.PlayMainScene(); else Ei.StopPlayingScene(); }
        }

        [RqlDescription("res:// path of the running scene, or null")]
        public string? playingScene => Ei.IsPlayingScene() && Ei.GetPlayingScene() is { Length: > 0 } s ? s : null;

        [RqlDescription("res:// path of the scene being edited, or null (no scene open, or never saved)")]
        public string? editedScene => RqlGodot.EditedRoot() is { } root && root.SceneFilePath is { Length: > 0 } p ? p : null;

        [RqlDescription("Godot version string, e.g. \"4.4.1.stable.mono\"")]
        public string godotVersion => Engine.GetVersionInfo()["string"].AsString();

        [RqlDescription("absolute path of the project folder (res://)")]
        public string projectPath => ProjectSettings.GlobalizePath("res://");

        [RqlDescription("nodes selected in the editor; assign a list of nodes (refs, godot.find(…), paths) to change the selection")]
        public Node[] selection
        {
            get => RqlGodot.SelectedNodes().ToArray();
            set
            {
                var editorSelection = Ei.GetSelection();
                editorSelection.Clear();
                foreach (var node in value ?? Array.Empty<Node>())
                    if (node != null && GodotObject.IsInstanceValid(node)) editorSelection.AddNode(node);
            }
        }

        [RqlCallable("run a scene in a separate game process: a res:// path, or null for the scene being edited; returns the running scene's path")]
        public string? PlayScene(string? path = null)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                if (RqlGodot.EditedRoot() == null) throw new InvalidOperationException("no scene is being edited; pass a res:// scene path");
                Ei.PlayCurrentScene();
            }
            else
            {
                if (!ResourceLoader.Exists(path)) throw new ArgumentException($"no scene at '{path}'");
                Ei.PlayCustomScene(path);
            }
            return playingScene;
        }

        [RqlCallable("stop the scene running from the editor")]
        public void StopScene() => Ei.StopPlayingScene();

        [RqlCallable("clear the captured editor log (the console source)")]
        public void ClearConsole() => Tools.GodotLogCollector.Current?.Clear();
    }
}
#endif
