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

            RejectParentTraversal(path, rawPath, nameof(rawPath));

            if (!path.EndsWith("/", StringComparison.Ordinal))
                path += "/";

            return path;
        }

        /// <summary>
        /// Reject a '..' path segment so that, e.g., <c>res://a/../a/b.tres</c> and <c>res://a/b.tres</c>
        /// cannot denote the same file while comparing as unequal strings (which would let a caller bypass a
        /// string-equality src==dst guard). Bounded by Godot's res:// sandbox so this is not a traversal
        /// vulnerability — it just locks the path-equality guards. A bare <c>.</c> segment is harmless and
        /// allowed; only the parent-escape <c>..</c> is rejected.
        /// </summary>
        static void RejectParentTraversal(string normalized, string? original, string paramName)
        {
            // Split on '/' and look for a literal '..' segment. The 'res://' prefix yields empty segments
            // ("res:", "", "") which never equal "..", so it is unaffected.
            foreach (var segment in normalized.Split('/'))
            {
                if (segment == "..")
                    throw new ArgumentException(
                        $"Path must not contain a '..' parent-directory segment; got '{original}'.", paramName);
            }
        }

        /// <summary>
        /// True when <paramref name="path"/> is a non-empty <c>res://</c> path.
        /// </summary>
        public static bool IsResPath(string? path)
            => !string.IsNullOrEmpty(path) && path!.StartsWith(ResScheme, StringComparison.Ordinal);

        /// <summary>
        /// Derive the parent directory (trailing-slash form) of a <c>res://</c> file path. For
        /// <c>res://scenes/level.tscn</c> this returns <c>res://scenes/</c>; for a file directly under the
        /// project root (<c>res://thing.tres</c>) it returns the bare scheme <c>res://</c>. Pure string logic
        /// (no Godot API) so the slash math is unit-testable; the editor handlers pass the result to
        /// <c>DirAccess.MakeDirRecursiveAbsolute</c> before saving so a nested target dir is created instead
        /// of failing with <c>CantOpen</c>. Throws for a non-<c>res://</c> or directory path.
        /// </summary>
        public static string ParentDir(string? filePath)
        {
            var p = (filePath ?? string.Empty).Trim();
            if (!IsResPath(p))
                throw new ArgumentException($"Path must be a '{ResScheme}' path; got '{filePath}'.", nameof(filePath));
            if (p.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException($"Path must be a file path, not a directory; got '{filePath}'.", nameof(filePath));

            // The last '/' lies at or after the scheme's own slashes ("res://" ends at index 5), so the
            // substring up to and including it is the parent dir in trailing-slash form. A file directly under
            // the root has its last '/' inside the scheme, so this yields the bare scheme 'res://'.
            var lastSlash = p.LastIndexOf('/');
            return p.Substring(0, lastSlash + 1);
        }

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
            // The bare scheme 'res://' is the project root, not a file — give an accurate message rather than
            // the misleading "not a directory" one the trailing-slash check below would otherwise produce.
            if (p == ResScheme)
                throw new ArgumentException(
                    $"Path must name a file under '{ResScheme}', not the bare project root '{ResScheme}'.", paramName);
            if (p.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException($"Path must be a file path, not a directory; got '{path}'.", paramName);
            RejectParentTraversal(p, path, paramName);
            return p;
        }
    }
}
