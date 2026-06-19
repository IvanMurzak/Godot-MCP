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
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// One persisted MCP-feature enable/disable entry — a feature item's name plus whether the user has it
    /// enabled. The Godot analog of Unity-MCP's per-feature enabled flags persisted by the tool/prompt/
    /// resource managers. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the persistence /
    /// merge logic is unit-testable in the plain-xUnit host.
    ///
    /// <para>
    /// Persistence policy: only entries the user has actually TOUCHED are stored (an absent name means
    /// "use the live default", which for the McpPlugin managers is enabled). Storing a name with
    /// <see cref="Enabled"/> <c>false</c> is therefore how a user disable survives a restart.
    /// </para>
    /// </summary>
    public sealed class GodotMcpFeatureState
    {
        /// <summary>The feature item's unique name (tool/prompt/resource name as the manager reports it).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>Whether this feature item is enabled. Serialized so a user disable persists.</summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        public GodotMcpFeatureState() { }

        public GodotMcpFeatureState(string name, bool enabled)
        {
            Name = name;
            Enabled = enabled;
        }
    }
}
