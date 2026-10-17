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
using System.Collections.Generic;
using System.Linq;
using Godot;
// The root namespace ends in '.Animation', which shadows the type 'Godot.Animation': alias it.
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// The Godot operations behind the adapters' calls. Each one checks every argument (<see cref="AnimationRules"/>, the
    /// label parsers, existing names) before it changes anything, so a rejected call changes nothing and Godot never logs
    /// an error for it. Runs on the editor main thread (every RQL query does).
    /// </summary>
    internal static class AnimationOps
    {
        public static AnimationLibrary AddLibrary(AnimationMixer mixer, string name)
        {
            Require(AnimationRules.LibraryName(name));
            if (mixer.HasAnimationLibrary(name))
                throw new ArgumentException($"{mixer.Name} already has a library named '{name}'");
            var library = new AnimationLibrary();
            var error = mixer.AddAnimationLibrary(name, library);
            if (error != Error.Ok) throw new InvalidOperationException($"Godot refused to add library '{name}' ({error})");
            return library;
        }

        /// <summary>Create an animation in <paramref name="library"/> of <paramref name="mixer"/>, adding the library when it is missing.</summary>
        public static GodotAnimation CreateAnimation(AnimationMixer mixer, string name, double length, string? loopMode, string library)
        {
            Require(AnimationRules.LibraryName(library));
            var mode = CheckNew(name, length, loopMode);
            var existing = mixer.HasAnimationLibrary(library) ? mixer.GetAnimationLibrary(library) : null;
            if (existing != null) RequireFree(existing, name);
            return Add(existing ?? AddLibrary(mixer, library), name, length, mode);
        }

        public static GodotAnimation CreateAnimation(AnimationLibrary library, string name, double length, string? loopMode)
        {
            var mode = CheckNew(name, length, loopMode);
            RequireFree(library, name);
            return Add(library, name, length, mode);
        }

        private static void RequireFree(AnimationLibrary library, string name)
        {
            if (library.HasAnimation(name)) throw new ArgumentException($"the library already has an animation named '{name}'");
        }

        public static int AddTrack(GodotAnimation animation, string kindLabel, string path)
        {
            var kind = AnimationTrackKinds.Parse(kindLabel);
            Require(AnimationRules.TrackPath(kind, path));
            int index = animation.AddTrack((GodotAnimation.TrackType)kind.ToGodotTrackType());
            animation.TrackSetPath(index, new NodePath(path));
            return index;
        }

        public static int InsertKey(GodotAnimation animation, int track, double time, Variant value)
        {
            var kind = CheckKey(animation, track, time);
            if (kind != GodotAnimation.TrackType.Value)
                throw new ArgumentException(IsTransform(kind)
                    ? $"track {track} is a {Label(kind)} track: key it with InsertTransformKey(track, time, \"(x, y, z)\")"
                    : $"track {track} is a {Label(kind)} track; InsertKey keys value tracks only");
            return animation.TrackInsertKey(track, time, value);
        }

        /// <summary>Key a 3D transform track: a position, a scale, or a rotation given as Euler angles in degrees.</summary>
        public static int InsertTransformKey(GodotAnimation animation, int track, double time, Vector3 value)
        {
            var kind = CheckKey(animation, track, time);
            switch (kind)
            {
                case GodotAnimation.TrackType.Position3D:
                    return animation.PositionTrackInsertKey(track, time, value);
                case GodotAnimation.TrackType.Scale3D:
                    return animation.ScaleTrackInsertKey(track, time, value);
                case GodotAnimation.TrackType.Rotation3D:
                    var radians = new Vector3(Mathf.DegToRad(value.X), Mathf.DegToRad(value.Y), Mathf.DegToRad(value.Z));
                    return animation.RotationTrackInsertKey(track, time, Quaternion.FromEuler(radians));
                default:
                    throw new ArgumentException($"track {track} is a {Label(kind)} track: InsertTransformKey keys position-3d, rotation-3d and scale-3d tracks (InsertKey keys value tracks)");
            }
        }

        public static List<AnimationTrackRow> Tracks(GodotAnimation animation)
        {
            var count = animation.GetTrackCount();
            var rows = new List<AnimationTrackRow>(count);
            for (int i = 0; i < count; i++)
                rows.Add(new AnimationTrackRow(i, Label(animation.TrackGetType(i)), animation.TrackGetPath(i).ToString(), animation.TrackGetKeyCount(i)));
            return rows;
        }

        public static IEnumerable<string> LibraryNames(AnimationMixer mixer) => mixer.GetAnimationLibraryList().Select(n => n.ToString());

        public static IEnumerable<string> AnimationNames(AnimationMixer mixer) => mixer.GetAnimationList().Select(n => n.ToString());

        public static IEnumerable<string> AnimationNames(AnimationLibrary library) => library.GetAnimationList().Select(n => n.ToString());

        private static bool IsTransform(GodotAnimation.TrackType kind) =>
            kind is GodotAnimation.TrackType.Position3D or GodotAnimation.TrackType.Rotation3D or GodotAnimation.TrackType.Scale3D;

        private static string Label(GodotAnimation.TrackType type) =>
            AnimationTrackKinds.FromGodotTrackType((int)type)?.ToLabel() ?? type.ToString();

        private static AnimationLoopMode CheckNew(string name, double length, string? loopMode)
        {
            Require(AnimationRules.AnimationName(name));
            Require(AnimationRules.Length(length));
            return string.IsNullOrWhiteSpace(loopMode) ? AnimationLoopMode.None : AnimationLoopModes.Parse(loopMode);
        }

        private static GodotAnimation.TrackType CheckKey(GodotAnimation animation, int track, double time)
        {
            Require(AnimationRules.TrackIndex(track, animation.GetTrackCount()));
            Require(AnimationRules.KeyTime(time));
            return animation.TrackGetType(track);
        }

        private static GodotAnimation Add(AnimationLibrary library, string name, double length, AnimationLoopMode mode)
        {
            var animation = new GodotAnimation
            {
                Length = (float)length,
                LoopMode = (GodotAnimation.LoopModeEnum)mode.ToGodotValue(),
            };
            var error = library.AddAnimation(name, animation);
            if (error != Error.Ok) throw new InvalidOperationException($"Godot refused to add animation '{name}' ({error})");
            return animation;
        }

        private static void Require(string? reason)
        {
            if (reason != null) throw new ArgumentException(reason);
        }
    }
}
#endif
