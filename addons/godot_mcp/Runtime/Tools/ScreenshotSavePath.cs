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
using System.IO;
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The optional <c>savePath</c> of the <c>screenshot-*</c> tools: which file a request names, and writing it. Pure .NET
    /// (no Godot types) so every refusal is unit-testable with a temp folder standing in for the project.
    /// <para>
    /// The request is judged segment by segment as given, never through a path-collapsing API: <c>"."</c>,
    /// <c>"Shots/."</c> and <c>"Shots/Sub/.."</c> name folders however a runtime would collapse them, and Windows drops a
    /// trailing dot or space from a segment when it opens a file, so <c>".. /x.png"</c> climbs out while reading as a name.
    /// </para>
    /// </summary>
    public static class ScreenshotSavePath
    {
        /// <summary>Appended to the description of every screenshot tool.</summary>
        public const string ToolDescriptionNote =
            " The image otherwise exists only in this conversation: when the user wants the picture as a file, pass " +
            "'savePath' and the PNG is also written inside the project (the result states 'Saved to <absolute path>').";

        /// <summary>The description of the <c>savePath</c> parameter of every screenshot tool.</summary>
        public const string ParameterDescription =
            "Optional file to also write the PNG to when the user wants the picture as a file, inside the project: " +
            "'res://Screenshots/shot.png', 'Screenshots/shot.png' (relative to the project root) or an absolute path under it. " +
            "Missing folders are created and .png is added when there is no extension. Refused: anything outside the project, " +
            "user://, res://.godot/, this addon's folder, another extension, an existing file (it never overwrites). " +
            "The image is still returned. Godot imports the PNG, so a .import file appears beside it.";

        const string Example = "res://Screenshots/shot.png";
        const string ForbiddenSegmentChars = "<>:\"|?*";

        // Case-insensitive like the file systems of Windows and macOS (see ResPathNormalizer.ProtectedFileProblem).
        static StringComparison PathComparison => OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        static bool IsDriveRooted(string path) =>
            path.Length >= 3 && path[1] == ':' && path[2] == '/' && ((path[0] >= 'A' && path[0] <= 'Z') || (path[0] >= 'a' && path[0] <= 'z'));

        /// <summary>
        /// Resolve a request against the project folder. True with a null <paramref name="fullPath"/> when no save was asked
        /// for (null or blank); true with the absolute path of a file that may be written; false with <paramref name="error"/>
        /// saying what to do instead. Reads the file system (links, an existing file) and writes nothing.
        /// </summary>
        public static bool TryResolve(string? requested, string projectRoot, out string? fullPath, out string? error)
        {
            fullPath = null;
            error = null;
            if (string.IsNullOrWhiteSpace(requested))
                return true;

            var root = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            var problem = Check(requested.Trim().Replace('\\', '/'), root, out var resolved);
            if (problem != null)
            {
                error = $"Cannot save to '{requested}': {problem}";
                return false;
            }
            fullPath = resolved;
            return true;
        }

        static string? Check(string path, string root, out string? resolved)
        {
            resolved = null;
            var outside = $"it must stay inside the project folder {root}; use a path such as {Example}.";

            string relative;
            if (ResPathNormalizer.IsResPath(path))
            {
                relative = path.Substring(ResPathNormalizer.ResScheme.Length);
                if (relative.StartsWith("/", StringComparison.Ordinal))
                    return $"a res:// path is relative to the project root; use a path such as {Example}.";
            }
            else if (path.Contains("://"))
                return $"only a res:// path is accepted, because user:// and other locations are outside the project; use a path such as {Example}.";
            else if (path.StartsWith("/", StringComparison.Ordinal) || IsDriveRooted(path))
            {
                if (!path.StartsWith(root + "/", PathComparison))
                    return outside;
                relative = path.Substring(root.Length + 1);
            }
            else
                relative = path;

            var requestedSegments = relative.Split('/');
            if (requestedSegments[requestedSegments.Length - 1] is "" or "." or "..")
                return $"it names a folder; add a file name, such as {Example}.";

            var segments = new List<string>();
            foreach (var segment in requestedSegments)
            {
                if (segment is "" or ".")
                    continue;
                if (segment == "..")
                {
                    if (segments.Count == 0)
                        return outside;
                    segments.RemoveAt(segments.Count - 1);
                    continue;
                }
                if (segment.EndsWith(".", StringComparison.Ordinal) || segment.EndsWith(" ", StringComparison.Ordinal))
                    return $"the name '{segment}' ends in a dot or a space, which Windows drops when it opens a file; rename it.";
                if (segment.Any(c => c < ' ' || ForbiddenSegmentChars.IndexOf(c) >= 0))
                    return $"the name '{segment}' has a character a file name cannot use ({ForbiddenSegmentChars} or a control character); rename it.";
                if (IsWindowsDeviceName(segment))
                    return $"the name '{segment}' is reserved for a device on Windows; rename it.";
                segments.Add(segment);
            }

            var fileName = segments[segments.Count - 1];
            if (Path.GetFileNameWithoutExtension(fileName).Trim('.').Length == 0)
                return $"it has no file name, only an extension; use a name such as {Example}.";

            var extension = Path.GetExtension(fileName);
            if (extension.Length == 0)
                segments[segments.Count - 1] = fileName + ".png";
            else if (!extension.Equals(".png", StringComparison.OrdinalIgnoreCase))
                return "the capture is a PNG, so the file name must end in .png.";

            var joined = string.Join("/", segments);
            if (ResPathNormalizer.ProtectedFileProblem(ResPathNormalizer.ResScheme + joined) is { } isProtected)
                return $"{isProtected}; use a path such as {Example}.";

            var full = root + "/" + joined;
            if (LinkOnPath(root, segments) is { } link)
                return $"'{link}' is a symlink or junction, and a capture is only written through real folders of the project; use a path such as {Example}.";
            if (File.Exists(full) || Directory.Exists(full))
                return "the file already exists and a capture never overwrites one; pick another name.";

            resolved = full;
            return null;
        }

        // CON, PRN, AUX, NUL, COM0-9, LPT0-9, with any extension: older Windows opens the device, so a save would claim a file
        // it never wrote, and on any Windows such a folder is awkward to delete.
        static bool IsWindowsDeviceName(string segment)
        {
            var stem = segment.Split('.')[0].ToUpperInvariant();
            return stem is "CON" or "PRN" or "AUX" or "NUL"
                || (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '0' && stem[3] <= '9');
        }

        // The first symlink or junction on the way to the file (or the part that cannot be read), null when there is none. Any
        // link is refused rather than resolved: on Unix ResolveLinkTarget follows only the last component, so a target that
        // reads as inside the project can still lead out through another link, and an internal link could reach res://.godot/.
        // The project root's own path may hold links; only what is below it is judged.
        static string? LinkOnPath(string root, List<string> segments)
        {
            var current = root;
            var relative = "";
            foreach (var segment in segments)
            {
                current += "/" + segment;
                relative += relative.Length == 0 ? segment : "/" + segment;
                try
                {
                    if (new FileInfo(current).LinkTarget != null)
                        return relative;
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    return relative;
                }
            }
            return null;
        }

        /// <summary>
        /// Create the folders and write the PNG, failing if the file exists by now (it can appear while the capture renders).
        /// Removes a partial file it created. Returns false with <paramref name="error"/>; nothing is then saved.
        /// </summary>
        public static bool TryWrite(string fullPath, byte[] png, out string? error)
        {
            error = null;
            var created = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                using (var file = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    file.Write(png, 0, png.Length);
                }
                return true;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                if (created)
                {
                    try { File.Delete(fullPath); }
                    catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { /* the write error below says enough */ }
                }
                error = !created && File.Exists(fullPath)
                    ? $"{fullPath} already exists and a capture never overwrites one; nothing was saved. Pick another name."
                    : $"Could not write the capture to {fullPath} ({e.Message}); nothing was saved. Pick another folder or free disk space.";
                return false;
            }
        }

        /// <summary>The sentence the result text gains after a save.</summary>
        public static string SavedNote(string fullPath) =>
            $"Saved to {fullPath}. Godot imports a PNG in the project, so '{Path.GetFileName(fullPath)}.import' appears beside it " +
            "after the filesystem scan requested now (not in a dot-named folder or one with a .gdignore file).";
    }
}
