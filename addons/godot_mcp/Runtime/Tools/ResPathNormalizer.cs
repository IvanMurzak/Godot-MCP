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
using System.Collections.Generic;
using System.Linq;

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
        /// True when <paramref name="path"/> names a Godot scene file (<c>.tscn</c> text or <c>.scn</c> binary).
        /// </summary>
        public static bool IsScenePath(string? path)
            => path != null
            && (path.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".scn", StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// Why a file tool must not move or delete <paramref name="resPath"/>; null when it may. Protected: the project file
        /// (<c>res://project.godot</c>: without it the folder is no longer a Godot project), Godot's own cache folder
        /// (<c>res://.godot/</c>) and this addon (<c>res://addons/godot_mcp/</c>, the tools themselves). Case-insensitive, as
        /// Windows and macOS file systems are.
        /// </summary>
        public static string? ProtectedFileProblem(string path)
        {
            if (!path.StartsWith(ResScheme, StringComparison.Ordinal)) return null;
            // "res://./project.godot" and "res://addons//godot_mcp/x" name the same files.
            var resPath = ResScheme + string.Join("/", path.Substring(ResScheme.Length).Split('/').Where(s => s.Length > 0 && s != "."));
            if (string.Equals(resPath, ResScheme + "project.godot", StringComparison.OrdinalIgnoreCase))
                return "'res://project.godot' is the project file; change settings with project | set settings[\"…\"] instead";
            if (resPath.StartsWith(ResScheme + ".godot/", StringComparison.OrdinalIgnoreCase))
                return $"'{resPath}' is in Godot's own cache folder res://.godot/";
            if (resPath.StartsWith(ResScheme + "addons/godot_mcp/", StringComparison.OrdinalIgnoreCase))
                return $"'{resPath}' belongs to the Godot-MCP addon itself";
            return null;
        }

        /// <summary>
        /// Why a move from <paramref name="src"/> to <paramref name="dst"/> would break the file; null when it would not. A
        /// different extension changes how Godot reads the file (a text .tres renamed to .res no longer loads).
        /// </summary>
        public static string? MoveProblem(string src, string dst)
        {
            var from = System.IO.Path.GetExtension(src);
            var to = System.IO.Path.GetExtension(dst);
            return string.Equals(from, to, StringComparison.OrdinalIgnoreCase)
                ? null
                : $"moving '{src}' to '{dst}' changes the file extension ('{from}' -> '{to}'), and Godot would no longer read the file; keep the extension";
        }

        /// <summary>
        /// The uid:// and the res:// path of one <c>ResourceLoader.GetDependencies</c> entry (<c>"uid://…::Type::res://path"</c>, any slice
        /// may be empty, or a bare path); null for a part the entry does not have. Godot loads by the uid first, the path is its fallback.
        /// </summary>
        public static (string? uid, string? path) DependencyParts(string? entry)
        {
            var parts = (entry ?? "").Split(new[] { "::" }, StringSplitOptions.None).Select(p => p.Trim()).ToArray();
            return (parts.FirstOrDefault(p => p.StartsWith("uid://", StringComparison.Ordinal)),
                    parts.LastOrDefault(p => p.StartsWith(ResScheme, StringComparison.Ordinal)));
        }

        /// <summary>
        /// Whether a <c>ResourceLoader.GetDependencies</c> entry names the file <paramref name="resPath"/> (or its
        /// <paramref name="uid"/>). Entries are <c>uid://…::Type::res://path</c> (Godot 4.x), <c>res://path::Type</c> or a bare path.
        /// </summary>
        public static bool DependencyNames(string entry, string resPath, string? uid)
        {
            foreach (var part in entry.Split(new[] { "::" }, StringSplitOptions.None))
                if (string.Equals(part, resPath, StringComparison.OrdinalIgnoreCase) || (!string.IsNullOrEmpty(uid) && part == uid)) return true;
            return false;
        }

        /// <summary>
        /// The <c>res://</c> paths and <c>uid://</c> ids written as strings in a text file (a script's <c>load("res://…")</c>, a
        /// scene's string property, <c>project.godot</c>'s main scene), in order, without repeats. A path ends at whitespace, a
        /// quote or a list / call delimiter; a trailing <c>.</c> or <c>:</c> (end of a sentence, a <c>::</c> separator) is not part of it.
        /// </summary>
        public static IEnumerable<string> MentionedPaths(string? text)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (System.Text.RegularExpressions.Match match in MentionPattern.Matches(text ?? ""))
            {
                var mention = match.Value.TrimEnd('.', ':');
                if (mention.Length > "res://".Length && seen.Add(mention)) yield return mention;
            }
        }

        private static readonly System.Text.RegularExpressions.Regex MentionPattern =
            new System.Text.RegularExpressions.Regex(@"(?:res|uid)://(?:[^\s""'`<>|,;()\[\]{}:]|:(?!:))+", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        /// <summary>Whether <paramref name="src"/> → <paramref name="dst"/> only changes letter case (the same file on Windows / macOS).</summary>
        public static bool IsCaseOnlyRename(string src, string dst)
            => !string.Equals(src, dst, StringComparison.Ordinal) && string.Equals(src, dst, StringComparison.OrdinalIgnoreCase);

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
