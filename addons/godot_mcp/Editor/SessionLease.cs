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
using System.Text.Json;
using com.IvanMurzak.McpPlugin.AgentConfig;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// The CLI's editor-session lease, <c>&lt;project&gt;/.ai-game-dev/session.json</c> (written by
    /// <c>@baizor/gamedev-cli-core</c> EditorSession when it starts an editor, with the same nonce in
    /// <c>AIGD_SESSION_NONCE</c>). The editor removes it when it exits, but only its own: a lease with another
    /// nonce belongs to another editor. Pure-managed.
    /// </summary>
    public static class SessionLease
    {
        public static string PathFor(string projectRoot) => Path.Combine(projectRoot, ProjectMarker.DirectoryName, "session.json");

        /// <summary>Delete the lease when its nonce is <paramref name="nonce"/>. Never throws; true when deleted.</summary>
        public static bool DeleteIfOwned(string projectRoot, string? nonce)
        {
            if (string.IsNullOrEmpty(nonce))
                return false;
            var path = PathFor(projectRoot);
            try
            {
                if (!File.Exists(path))
                    return false;
                using (var document = JsonDocument.Parse(File.ReadAllText(path)))
                {
                    if (!document.RootElement.TryGetProperty("nonce", out var value)
                        || value.ValueKind != JsonValueKind.String
                        || !string.Equals(value.GetString(), nonce, StringComparison.Ordinal))
                        return false;
                }
                File.Delete(path);
                return true;
            }
            catch (Exception)
            {
                return false; // unreadable or concurrently replaced: leave it to the CLI's stale-lease check
            }
        }
    }
}
