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
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;
// '.Animation' namespace shadows 'Godot.Animation'; alias the Godot type so it is unambiguous.
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Editor-only tool — adds a track to an existing <c>Animation</c>. Supports a generic <c>value</c>
        /// track (which animates a node sub-property, addressed as <c>"node:property"</c>) plus the three
        /// dedicated 3D transform tracks (<c>position-3d</c> / <c>rotation-3d</c> / <c>scale-3d</c>, addressed
        /// by the node path alone). Returns the animation's updated config. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            AddTrackToolId,
            Title = "Animation / Add Track"
        )]
        [Description("Add a track to an existing Animation on an AnimationPlayer (addressed by 'nodePath', " +
            "relative to the edited scene root; 'animationName' is the qualified clip name). 'trackType' is " +
            "'value', 'position-3d', 'rotation-3d' or 'scale-3d'. 'targetPath' is the animated node's path " +
            "(relative to the player's root node). For a 'value' track, also pass 'property' (the animated " +
            "sub-property, e.g. 'modulate' or 'position:x') — the track path becomes 'targetPath:property'. " +
            "Returns the animation's updated config (including the new track's index).")]
        public AnimationClipInfo AddTrack
        (
            [Description("Node path (relative to the edited scene root) of the AnimationPlayer.")]
            string nodePath,
            [Description("Qualified animation name ('name' for the global library, or 'library/name').")]
            string animationName,
            [Description("Track kind: 'value', 'position-3d', 'rotation-3d' or 'scale-3d'.")]
            string trackType,
            [Description("Path of the node the track animates (relative to the player's root node).")]
            string targetPath,
            [Description("Sub-property for a 'value' track (e.g. 'modulate', 'position:x'); required for " +
                "'value' tracks, ignored for the 3D transform tracks.")]
            string? property = null
        )
        {
            var kind = AnimationTrackKinds.Parse(trackType);

            return MainThread.Instance.Run(() =>
            {
                var player = ResolveAnimationPlayerOrThrow(nodePath);
                var animation = ResolveAnimationOrThrow(player, animationName);

                if (string.IsNullOrWhiteSpace(targetPath))
                    throw new ArgumentException("A target node path is required.", nameof(targetPath));

                string path = targetPath;
                if (kind == AnimationTrackKind.Value)
                {
                    if (string.IsNullOrWhiteSpace(property))
                        throw new ArgumentException(
                            "A 'value' track requires a 'property' (the animated sub-property).",
                            nameof(property));
                    path = $"{targetPath}:{property}";
                }

                int index = animation.AddTrack((GodotAnimation.TrackType)kind.ToGodotTrackType());
                animation.TrackSetPath(index, new NodePath(path));

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadClipInfo(animationName, animation);
            });
        }
    }
}
#endif
