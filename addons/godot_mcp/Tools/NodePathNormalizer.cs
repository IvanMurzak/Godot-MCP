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
    /// Pure-string normalization of a user-supplied scene-tree path into a form resolvable against the
    /// edited-scene root via <c>Node.GetNodeOrNull</c>. Extracted from <c>Tool_Node</c> (which is
    /// editor-only, <c>#if TOOLS</c>) so the path logic — the part most prone to off-by-one slash bugs —
    /// is unit-testable in the plain xUnit host without a live Godot scene tree.
    ///
    /// <para>
    /// Godot's editor SceneTree window roots open scenes under <c>/root/</c>; the edited scene's own root
    /// is a child of that window. <c>GetNodeOrNull</c> on the edited root resolves paths RELATIVE to the
    /// root (it does not resolve the root itself). So this normalizer strips a leading <c>/root/</c> or
    /// <c>/</c>, and — when the remaining path names the root segment — reduces it to <c>"."</c> (meaning
    /// "the root itself") or strips the root segment so the remainder is root-relative.
    /// </para>
    /// </summary>
    public static class NodePathNormalizer
    {
        /// <summary>
        /// Normalize <paramref name="rawPath"/> against an edited root named <paramref name="editedRootName"/>.
        /// Returns <c>"."</c> when the path denotes the root itself, or a root-relative child path otherwise.
        /// </summary>
        public static string Normalize(string rawPath, string editedRootName)
        {
            var path = (rawPath ?? string.Empty).Trim();

            // Strip a leading '/root/' (the SceneTree window) — the edited scene root is a child of it.
            const string rootPrefix = "/root/";
            if (path.StartsWith(rootPrefix, StringComparison.Ordinal))
                path = path.Substring(rootPrefix.Length);
            else if (path.StartsWith("/", StringComparison.Ordinal))
                path = path.Substring(1);

            // If the path now starts with the edited root's own name, strip that segment so the remainder
            // is relative to the root (GetNodeOrNull resolves children, not the root itself).
            if (path == editedRootName)
                return ".";
            var rootSlash = editedRootName + "/";
            if (path.StartsWith(rootSlash, StringComparison.Ordinal))
                path = path.Substring(rootSlash.Length);

            return path;
        }
    }
}
