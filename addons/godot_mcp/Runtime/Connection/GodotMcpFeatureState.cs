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
    /// One persisted MCP-feature visibility entry — a feature item's name plus whether it is listed to MCP clients
    /// (tools/list, prompts/list, resources/list). Visibility is NOT an access control: a hidden tool still runs
    /// through tools/call. The Godot analog of Unity-MCP's per-feature visibility flags persisted by the
    /// tool/prompt/resource managers. Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the persistence /
    /// merge logic is unit-testable in the plain-xUnit host.
    ///
    /// <para>
    /// Persistence policy: only entries the user has actually TOUCHED are stored (an absent name means
    /// "use the live default", which for the McpPlugin managers is visible). Storing a name with
    /// <see cref="Visible"/> <c>false</c> is therefore how a user's hide survives a restart.
    /// </para>
    /// <para>
    /// Written as <c>"visible"</c>. A config saved before the rename carries <c>"enabled"</c>: it is still read
    /// (<see cref="LegacyEnabled"/>), and <c>"visible"</c> wins when both are present.
    /// </para>
    /// </summary>
    public sealed class GodotMcpFeatureState
    {
        /// <summary>The feature item's unique name (tool/prompt/resource name as the manager reports it).</summary>
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        bool? _visible;
        bool? _legacyEnabled;

        /// <summary>Whether this feature item is listed to MCP clients. Serialized so a user's hide persists.</summary>
        [JsonPropertyName("visible")]
        public bool Visible
        {
            get => _visible ?? _legacyEnabled ?? true;
            set => _visible = value;
        }

        /// <summary>
        /// The pre-rename key <c>"enabled"</c>: read so an older config keeps its hidden items, never written
        /// (its getter returns <see langword="null"/>).
        /// </summary>
        [JsonPropertyName("enabled")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public bool? LegacyEnabled
        {
            get => null;
            set => _legacyEnabled = value;
        }

        public GodotMcpFeatureState() { }

        public GodotMcpFeatureState(string name, bool visible)
        {
            Name = name;
            Visible = visible;
        }
    }
}
