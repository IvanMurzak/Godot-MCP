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
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The persisted enable-map for the three MCP feature kinds (tools / prompts / resources). Each list
    /// holds the user-touched <see cref="GodotMcpFeatureState"/> entries for that kind; an empty list means
    /// "everything at its live default" (which for the McpPlugin managers is enabled). Serialized as part of
    /// <see cref="GodotMcpConfig"/> under the <c>features</c> key so a user's per-item disable survives a
    /// restart. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) — the merge/apply/capture logic in
    /// <see cref="GodotMcpFeatureStateMerge"/> is unit-testable in the plain-xUnit host.
    /// </summary>
    public sealed class GodotMcpFeatureMap
    {
        /// <summary>Persisted tool enable/disable entries (user-touched only).</summary>
        [JsonPropertyName("tools")]
        public List<GodotMcpFeatureState> Tools { get; set; } = new();

        /// <summary>Persisted prompt enable/disable entries (user-touched only).</summary>
        [JsonPropertyName("prompts")]
        public List<GodotMcpFeatureState> Prompts { get; set; } = new();

        /// <summary>Persisted resource enable/disable entries (user-touched only).</summary>
        [JsonPropertyName("resources")]
        public List<GodotMcpFeatureState> Resources { get; set; } = new();

        /// <summary>
        /// The list for a given <see cref="GodotMcpFeatureKind"/>, so callers can address one kind generically
        /// (the panel/window are parameterized by kind). Returns the live backing list (mutations persist).
        /// </summary>
        public List<GodotMcpFeatureState> For(GodotMcpFeatureKind kind) => kind switch
        {
            GodotMcpFeatureKind.Tools => Tools,
            GodotMcpFeatureKind.Prompts => Prompts,
            _ => Resources
        };

        /// <summary>Replace the entries for one kind in-place (used when capturing live state back into the map).</summary>
        public void Set(GodotMcpFeatureKind kind, List<GodotMcpFeatureState> entries)
        {
            switch (kind)
            {
                case GodotMcpFeatureKind.Tools:
                    Tools = entries;
                    break;
                case GodotMcpFeatureKind.Prompts:
                    Prompts = entries;
                    break;
                default:
                    Resources = entries;
                    break;
            }
        }
    }

    /// <summary>Which MCP feature kind a panel row / list window addresses.</summary>
    public enum GodotMcpFeatureKind
    {
        /// <summary>MCP tools (<c>IToolManager</c>).</summary>
        Tools,

        /// <summary>MCP prompts (<c>IPromptManager</c>).</summary>
        Prompts,

        /// <summary>MCP resources (<c>IResourceManager</c>).</summary>
        Resources
    }
}
