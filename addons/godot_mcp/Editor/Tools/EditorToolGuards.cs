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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Shared guard helpers for the editor-only (<c>#if TOOLS</c>) tool families. These centralize the
    /// small, repeated precondition checks several <c>Tool_*</c> handlers previously performed inline:
    /// resolving the edited scene root / editor resource filesystem (throwing a consistent error when the
    /// editor is in a state that makes the operation impossible), asserting a resource exists, validating a
    /// ClassDB class name before instantiating it, and creating a target directory on demand. Behavior is
    /// identical to the inlined checks they replace — this is a pure de-duplication.
    ///
    /// <para>
    /// Every helper touches a Godot editor/native API (<see cref="EditorInterface"/>, <see cref="ClassDB"/>,
    /// <see cref="DirAccess"/>, <see cref="ResourceLoader"/>), so the whole class lives behind
    /// <c>#if TOOLS</c> and is exercised via the headless Godot smoke (test.md Suite 3), not the plain-xUnit
    /// host. Callers must already be on the editor main thread (the <c>Tool_*</c> handlers invoke these from
    /// inside their <c>MainThread.Instance.Run(...)</c> body).
    /// </para>
    /// </summary>
    internal static class EditorToolGuards
    {
        /// <summary>
        /// The currently-edited scene root, or throw when no scene is open. Pass <paramref name="message"/>
        /// to override the default "no edited scene" text for a call site with a more specific intent.
        /// Main-thread only.
        /// </summary>
        internal static Node GetEditedSceneRootOrThrow(string? message = null)
            => EditorInterface.Singleton.GetEditedSceneRoot()
                ?? throw new Exception(message ?? "No scene is currently being edited.");

        /// <summary>
        /// The editor's resource filesystem, or throw when it is unavailable. Main-thread only.
        /// </summary>
        internal static EditorFileSystem GetResourceFileSystemOrThrow()
            => EditorInterface.Singleton.GetResourceFilesystem()
                ?? throw new Exception("Editor resource filesystem is not available.");

        /// <summary>
        /// Throw an <see cref="ArgumentException"/> (attributed to <paramref name="paramName"/>) when no
        /// resource exists at <paramref name="resPath"/>. Pass <paramref name="message"/> to override the
        /// default text for a call site that phrases the miss differently. Main-thread only.
        /// </summary>
        internal static void RequireResourceExists(string resPath, string paramName, string? message = null)
        {
            if (!ResourceLoader.Exists(resPath))
                throw new ArgumentException(message ?? $"No resource exists at '{resPath}'.", paramName);
        }

        /// <summary>
        /// Validate a Godot <paramref name="className"/> and instantiate it as <typeparamref name="T"/>,
        /// throwing an <see cref="ArgumentException"/> (attributed to <paramref name="paramName"/>) when the
        /// class is unknown, cannot be instantiated, does not derive from
        /// <paramref name="requireParentClass"/> (when supplied), or does not instantiate to
        /// <typeparamref name="T"/>. Returns the validated, freshly-instantiated instance. Main-thread only.
        /// </summary>
        internal static T ValidateClassDbInstantiation<[MustBeVariant] T>(
            string className, string paramName, string? requireParentClass = null)
            where T : GodotObject
        {
            if (!ClassDB.ClassExists(className))
                throw new ArgumentException($"Unknown Godot class '{className}'.", paramName);
            if (!ClassDB.CanInstantiate(className))
                throw new ArgumentException($"Godot class '{className}' cannot be instantiated (abstract/virtual).", paramName);
            if (requireParentClass != null && !ClassDB.IsParentClass(className, requireParentClass))
                throw new ArgumentException($"Godot class '{className}' does not derive from {requireParentClass}.", paramName);

            return ClassDB.Instantiate(className).As<T>()
                ?? throw new ArgumentException($"Godot class '{className}' did not instantiate to a {typeof(T).Name}.", paramName);
        }

        /// <summary>
        /// Ensure <paramref name="absDir"/> exists, creating it recursively when missing and throwing on
        /// failure. Mirrors the "ResourceSaver/FileAccess does not create parent dirs" guard the create-style
        /// tools share. Main-thread only.
        /// </summary>
        internal static void EnsureDirectoryExists(string absDir)
        {
            if (!DirAccess.DirExistsAbsolute(absDir))
            {
                var mkErr = DirAccess.MakeDirRecursiveAbsolute(absDir);
                if (mkErr != Error.Ok)
                    throw new Exception($"Failed to create target directory '{absDir}': {mkErr}.");
            }
        }
    }
}
#endif
