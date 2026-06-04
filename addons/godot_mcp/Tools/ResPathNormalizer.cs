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
using System;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Pure-string validation/normalization of <c>res://</c> paths shared by the filesystem and resource
    /// tool families. Extracted from the editor-only (<c>#if TOOLS</c>) handlers — like
    /// <see cref="NodePathNormalizer"/> — so the slash/prefix logic, the part most prone to off-by-one
    /// bugs, is unit-testable in the plain xUnit host with no live Godot filesystem.
    ///
    /// <para>
    /// Godot addresses every project asset under the <c>res://</c> virtual root and every directory by its
    /// trailing-slash form (so <c>EditorFileSystem.GetFilesystemPath("res://materials/")</c> resolves the
    /// directory). These helpers enforce the <c>res://</c> scheme and the directory trailing-slash
    /// convention without touching any Godot API.
    /// </para>
    /// </summary>
    public static class ResPathNormalizer
    {
        public const string ResScheme = "res://";

        /// <summary>
        /// Normalize a user-supplied directory argument to a Godot <c>res://</c> directory path
        /// (trailing-slash form). An empty/null/<c>"res://"</c> input maps to the project root
        /// (<c>"res://"</c>); any other input must be a <c>res://</c> path and is given a trailing slash.
        /// Throws <see cref="ArgumentException"/> for a non-<c>res://</c> input.
        /// </summary>
        public static string NormalizeDir(string? rawPath)
        {
            var path = (rawPath ?? string.Empty).Trim();

            if (path.Length == 0 || path == ResScheme)
                return ResScheme;

            if (!path.StartsWith(ResScheme, StringComparison.Ordinal))
                throw new ArgumentException(
                    $"Directory path must be a '{ResScheme}' path (or empty for the project root); got '{rawPath}'.");

            if (!path.EndsWith("/", StringComparison.Ordinal))
                path += "/";

            return path;
        }

        /// <summary>
        /// True when <paramref name="path"/> is a non-empty <c>res://</c> path.
        /// </summary>
        public static bool IsResPath(string? path)
            => !string.IsNullOrEmpty(path) && path!.StartsWith(ResScheme, StringComparison.Ordinal);

        /// <summary>
        /// Validate that <paramref name="path"/> is a <c>res://</c> file path, throwing
        /// <see cref="ArgumentException"/> (with <paramref name="paramName"/>) otherwise. Returns the
        /// trimmed path on success.
        /// </summary>
        public static string RequireResFilePath(string? path, string paramName)
        {
            var p = (path ?? string.Empty).Trim();
            if (!IsResPath(p))
                throw new ArgumentException($"Path must be a '{ResScheme}' path; got '{path}'.", paramName);
            if (p.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException($"Path must be a file path, not a directory; got '{path}'.", paramName);
            return p;
        }
    }
}
