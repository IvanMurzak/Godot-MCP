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
using System.Linq;
using Godot;
// The extension's root namespace ends in '.Animation', which shadows the Godot type 'Godot.Animation'
// (an unqualified 'Animation' binds to the namespace, not the type). Alias the Godot type so the wrapped
// 'Animation' resource is unambiguous everywhere it is named as a type.
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation
{
    /// <summary>
    /// Editor-only shared helpers for the <c>animation-*</c> tools (behind <c>#if TOOLS</c>: they touch
    /// <c>EditorInterface</c> and live <c>Node</c>/<c>Resource</c>s). Every method here is invoked ONLY from
    /// inside a <c>MainThread.Instance.Run(...)</c> delegate by the tool methods, so it runs on the editor
    /// main thread.
    ///
    /// <para>
    /// The reads/writes use the strongly-typed <c>AnimationPlayer</c> / <c>AnimationLibrary</c> /
    /// <c>Animation</c> API on purpose — that typed surface (resolved from the consumer's own GodotSharp) is
    /// exactly what the source-only packaging recipe must compile against cross-version. The wrapped property
    /// and method names are stable across Godot 4.3 … 4.5.
    /// </para>
    /// </summary>
    public partial class Tool_Animation
    {
        /// <summary>The edited scene root, or throw a clear error when no scene is open.</summary>
        static Node GetEditedSceneRootOrThrow()
        {
            var root = EditorInterface.Singleton.GetEditedSceneRoot();
            if (root == null)
                throw new InvalidOperationException(
                    "No scene is currently being edited; open or create a scene first.");
            return root;
        }

        /// <summary>
        /// Resolve <paramref name="nodePath"/> (relative to the edited scene root) to an
        /// <c>AnimationPlayer</c>, throwing a clear error when the path is empty, the node is missing, or the
        /// node is not an <c>AnimationPlayer</c>.
        /// </summary>
        static AnimationPlayer ResolveAnimationPlayerOrThrow(string? nodePath)
        {
            if (string.IsNullOrWhiteSpace(nodePath))
                throw new ArgumentException("A node path is required.", nameof(nodePath));

            var root = GetEditedSceneRootOrThrow();
            var node = root.GetNodeOrNull(new NodePath(nodePath));
            if (node == null)
                throw new ArgumentException($"No node found at path '{nodePath}'.", nameof(nodePath));

            if (node is not AnimationPlayer player)
                throw new ArgumentException(
                    $"Node at '{nodePath}' is a {node.GetClass()}, not an AnimationPlayer.", nameof(nodePath));

            return player;
        }

        /// <summary>
        /// Resolve the <c>AnimationLibrary</c> named <paramref name="libraryName"/> on the player, creating and
        /// adding an empty one when it does not exist (so <c>animation-create</c> works without a prior
        /// <c>animation-library-add</c> for the global/default library). The empty string is the global library.
        /// </summary>
        static AnimationLibrary GetOrAddLibrary(AnimationPlayer player, string libraryName)
        {
            if (player.HasAnimationLibrary(libraryName))
                return player.GetAnimationLibrary(libraryName);

            var library = new AnimationLibrary();
            var err = player.AddAnimationLibrary(libraryName, library);
            if (err != Error.Ok)
                throw new InvalidOperationException(
                    $"Failed to add animation library '{libraryName}' ({err}).");
            return library;
        }

        /// <summary>
        /// Resolve the fully-qualified <paramref name="animationName"/> (<c>"library/name"</c> or <c>"name"</c>
        /// for the global library) to a live <c>Animation</c> on the player, throwing when it does not exist.
        /// </summary>
        static GodotAnimation ResolveAnimationOrThrow(AnimationPlayer player, string? animationName)
        {
            if (string.IsNullOrWhiteSpace(animationName))
                throw new ArgumentException("An animation name is required.", nameof(animationName));

            if (!player.HasAnimation(animationName))
                throw new ArgumentException(
                    $"No animation '{animationName}' on the player. Create it with 'animation-create' first.",
                    nameof(animationName));

            return player.GetAnimation(animationName);
        }

        /// <summary>Throw a clear error when <paramref name="trackIndex"/> is out of range for the animation.</summary>
        static void ValidateTrackIndexOrThrow(GodotAnimation animation, int trackIndex)
        {
            var count = animation.GetTrackCount();
            if (trackIndex < 0 || trackIndex >= count)
                throw new ArgumentOutOfRangeException(nameof(trackIndex),
                    $"Track index {trackIndex} is out of range (the animation has {count} track(s)).");
        }

        /// <summary>Split a qualified animation name into (library, name); a name with no '/' is in the global library.</summary>
        static (string Library, string Name) SplitQualified(string qualifiedName)
        {
            var slash = qualifiedName.IndexOf('/');
            return slash < 0
                ? (string.Empty, qualifiedName)
                : (qualifiedName.Substring(0, slash), qualifiedName.Substring(slash + 1));
        }

        /// <summary>Build a pure-managed <see cref="AnimationPlayerInfo"/> snapshot from a live player node.</summary>
        static AnimationPlayerInfo ReadPlayerInfo(AnimationPlayer player)
        {
            var libraries = player.GetAnimationLibraryList().Select(s => s.ToString()).ToList();
            var animations = player.GetAnimationList().Select(s => s.ToString()).ToList();
            return new AnimationPlayerInfo
            {
                NodePath = player.GetPath().ToString(),
                TypeName = player.GetClass(),
                Libraries = libraries,
                LibraryCount = libraries.Count,
                Animations = animations,
                AnimationCount = animations.Count
            };
        }

        /// <summary>Build a pure-managed <see cref="AnimationClipInfo"/> snapshot from a live animation resource.</summary>
        static AnimationClipInfo ReadClipInfo(string qualifiedName, GodotAnimation animation)
        {
            var (library, name) = SplitQualified(qualifiedName);
            var info = new AnimationClipInfo
            {
                Name = name,
                Library = library,
                QualifiedName = qualifiedName,
                Length = animation.Length,
                LoopMode = AnimationLoopModes.FromGodotValue((int)animation.LoopMode).ToLabel(),
                TrackCount = animation.GetTrackCount()
            };

            for (int i = 0; i < info.TrackCount; i++)
                info.Tracks.Add(ReadTrackInfo(animation, i));

            return info;
        }

        /// <summary>Build a pure-managed <see cref="AnimationTrackInfo"/> snapshot from one track of an animation.</summary>
        static AnimationTrackInfo ReadTrackInfo(GodotAnimation animation, int trackIndex)
        {
            var trackType = (int)animation.TrackGetType(trackIndex);
            var kind = AnimationTrackKinds.FromGodotTrackType(trackType);
            return new AnimationTrackInfo
            {
                Index = trackIndex,
                Kind = kind?.ToLabel() ?? animation.TrackGetType(trackIndex).ToString(),
                Path = animation.TrackGetPath(trackIndex).ToString(),
                KeyCount = animation.TrackGetKeyCount(trackIndex)
            };
        }
    }
}
#endif
