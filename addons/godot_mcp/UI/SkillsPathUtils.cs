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
using System.IO;

namespace com.IvanMurzak.Godot.MCP.UI
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) path-display + path-safety helpers for the
    /// dock's Skills card. These two utilities survived the #142 convergence onto the shared
    /// <c>com.IvanMurzak.McpPlugin.AgentConfig</c> module: they are presentation/validation concerns of the
    /// Godot dock (not agent-config WRITE logic, which the shared module now owns), and the retired
    /// <c>AgentConfigPaths</c> was their only previous home. Kept here so the Skills card stays
    /// CI-unit-testable in the plain-xUnit host.
    /// </summary>
    public static class SkillsPathUtils
    {
        /// <summary>
        /// Render <paramref name="absolutePath"/> for DISPLAY relative to <paramref name="projectRoot"/>: returns the
        /// project-relative form (e.g. <c>.claude/skills</c>) when the path is inside the project root, <c>"."</c> when
        /// it equals the project root, and the original absolute path unchanged when it is OUTSIDE the project (or on
        /// any error — safety fallback). Separators are normalized to <c>'/'</c> and trailing slashes trimmed before the
        /// ordinal containment check. The skills card shows the short relative path while keeping the full absolute
        /// path in the tooltip.
        /// </summary>
        public static string ToDisplayPath(string absolutePath, string projectRoot)
        {
            try
            {
                if (string.IsNullOrEmpty(absolutePath) || string.IsNullOrEmpty(projectRoot))
                    return absolutePath;

                var normalizedPath = absolutePath.Replace('\\', '/').TrimEnd('/');
                var normalizedRoot = projectRoot.Replace('\\', '/').TrimEnd('/');

                if (string.Equals(normalizedPath, normalizedRoot, StringComparison.Ordinal))
                    return ".";

                var prefix = normalizedRoot + "/";
                if (normalizedPath.StartsWith(prefix, StringComparison.Ordinal))
                    return normalizedPath.Substring(prefix.Length);

                // Outside the project root (or empty root) — return the original absolute path untouched.
                return absolutePath;
            }
            catch
            {
                return absolutePath;
            }
        }

        /// <summary>
        /// Validate a user-or-config-supplied skills path is a SAFE in-project relative path: rejects an absolute /
        /// rooted path and any <c>..</c> traversal segment. Returns <c>true</c> when <paramref name="relativePath"/> is
        /// null/empty (the resolver falls back to the per-agent default) OR a clean relative path; <c>false</c> when it
        /// is rooted or escapes the project root. Pure-managed (no IO, no Godot types) so it is unit-testable.
        /// </summary>
        public static bool IsSafeRelativeSkillsPath(string? relativePath)
        {
            if (string.IsNullOrEmpty(relativePath))
                return true;

            // Reject absolute / rooted forms with a PURELY STRING-BASED test so the result is identical on every
            // host OS. System.IO.Path.IsPathRooted is platform-dependent — on Linux it returns false for a Windows
            // drive-letter path like "C:\Windows" (a drive letter is not a Linux root), which would let such a path
            // slip past the guard on a Linux CI runner while being rejected on Windows. Cover all absolute shapes:
            //   - POSIX absolute:        leading '/'
            //   - Windows UNC / rooted:  leading '\' (single or "\\server\share")
            //   - Windows drive-letter:  "<letter>:" prefix, with '\' OR '/' or nothing after (C:\, C:/, C:foo)
            var normalized = relativePath!.Replace('\\', '/');

            if (normalized.StartsWith("/"))
                return false;

            if (normalized.Length >= 2 &&
                normalized[1] == ':' &&
                ((normalized[0] >= 'A' && normalized[0] <= 'Z') || (normalized[0] >= 'a' && normalized[0] <= 'z')))
                return false;

            // Belt-and-suspenders: still honour the platform check too (catches any rooted form not covered above).
            if (Path.IsPathRooted(relativePath))
                return false;

            // Reject '..' traversal segments (after backslash normalization above).
            if (normalized == ".." || normalized.StartsWith("../") || normalized.Contains("/../") || normalized.EndsWith("/.."))
                return false;

            return true;
        }
    }
}
