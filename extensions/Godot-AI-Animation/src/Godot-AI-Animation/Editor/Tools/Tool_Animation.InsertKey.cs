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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    public partial class Tool_Animation
    {
        /// <summary>
        /// Editor-only tool — inserts a keyframe on a track of an existing <c>Animation</c>. For a generic
        /// <c>value</c> track the scalar <c>value</c> is keyed; for the dedicated 3D transform tracks the
        /// <c>x</c>/<c>y</c>/<c>z</c> components are keyed (rotation interprets them as Euler degrees). The
        /// keyframe <c>time</c> is clamped to &gt;= 0 by the pure-managed defaults. Main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            InsertKeyToolId,
            Title = "Animation / Insert Key"
        )]
        [Description("Insert a keyframe on a track of an existing Animation (player addressed by 'nodePath' " +
            "relative to the edited scene root; 'animationName' is the qualified clip name; 'trackIndex' is the " +
            "0-based track). 'time' is the keyframe time in seconds (clamped to >= 0). For a 'value' track, " +
            "'value' is the keyed scalar. For a 3D transform track (position/rotation/scale), 'x'/'y'/'z' are " +
            "the keyed components (rotation is in Euler degrees; scale defaults to 1 when a component is " +
            "omitted). Returns the animation's updated config.")]
        public AnimationClipInfo InsertKey
        (
            [Description("Node path (relative to the edited scene root) of the AnimationPlayer.")]
            string nodePath,
            [Description("Qualified animation name ('name' for the global library, or 'library/name').")]
            string animationName,
            [Description("0-based index of the track to key (from 'animation-add-track').")]
            int trackIndex,
            [Description("Keyframe time in seconds (Godot key time); clamped to >= 0. Defaults to 0.")]
            double time = 0.0,
            [Description("Keyed scalar value for a 'value' track (ignored for 3D transform tracks). Defaults to 0.")]
            double value = 0.0,
            [Description("X component for a 3D transform track (Euler degrees for rotation). Defaults to 0/1.")]
            double? x = null,
            [Description("Y component for a 3D transform track (Euler degrees for rotation). Defaults to 0/1.")]
            double? y = null,
            [Description("Z component for a 3D transform track (Euler degrees for rotation). Defaults to 0/1.")]
            double? z = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var player = ResolveAnimationPlayerOrThrow(nodePath);
                var animation = ResolveAnimationOrThrow(player, animationName);
                ValidateTrackIndexOrThrow(animation, trackIndex);

                double t = AnimationDefaults.ClampTime(time);
                var trackType = (int)animation.TrackGetType(trackIndex);
                var kind = AnimationTrackKinds.FromGodotTrackType(trackType);

                switch (kind)
                {
                    case AnimationTrackKind.Position3D:
                        animation.PositionTrackInsertKey(trackIndex, t,
                            new Vector3((float)(x ?? 0.0), (float)(y ?? 0.0), (float)(z ?? 0.0)));
                        break;
                    case AnimationTrackKind.Scale3D:
                        animation.ScaleTrackInsertKey(trackIndex, t,
                            new Vector3((float)(x ?? 1.0), (float)(y ?? 1.0), (float)(z ?? 1.0)));
                        break;
                    case AnimationTrackKind.Rotation3D:
                        var euler = new Vector3(
                            Mathf.DegToRad((float)(x ?? 0.0)),
                            Mathf.DegToRad((float)(y ?? 0.0)),
                            Mathf.DegToRad((float)(z ?? 0.0)));
                        animation.RotationTrackInsertKey(trackIndex, t, Quaternion.FromEuler(euler));
                        break;
                    default:
                        // Value track (or any other type addressed generically): key the scalar value.
                        animation.TrackInsertKey(trackIndex, t, Variant.From(value));
                        break;
                }

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadClipInfo(animationName, animation);
            });
        }
    }
}
#endif
