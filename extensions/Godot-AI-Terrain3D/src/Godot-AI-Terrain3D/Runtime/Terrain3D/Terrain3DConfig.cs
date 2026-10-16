/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// Pure-managed, serializable recommended starter configuration for a Terrain3D node — the structured
    /// result the pure-managed <c>terrain3d-defaults</c> tool returns. Holds ONLY primitives + strings (no
    /// Godot native types), so it is CI-unit-testable with no Godot binary and serializes cleanly through
    /// ReflectorNet. The LLM can pass these values on to <c>terrain3d-create</c> / <c>terrain3d-set-region-size</c>.
    /// </summary>
    public sealed class Terrain3DConfig
    {
        /// <summary>The preset this config was built for (<c>"small"</c> / <c>"medium"</c> / <c>"large"</c>).</summary>
        public string Preset { get; set; } = string.Empty;

        /// <summary>Recommended Terrain3D region size (a valid <c>RegionSize</c> value: 64..2048).</summary>
        public int RegionSize { get; set; }

        /// <summary>Recommended number of clipmap mesh LOD levels.</summary>
        public int MeshLods { get; set; }

        /// <summary>Recommended clipmap mesh size.</summary>
        public int MeshSize { get; set; }

        /// <summary>Recommended world-space vertex spacing.</summary>
        public double VertexSpacing { get; set; }

        /// <summary>Recommended <c>res://</c> data directory where Terrain3D persists region data on disk.</summary>
        public string DataDirectory { get; set; } = string.Empty;

        /// <summary>Whether to persist heightmaps at 16-bit precision (Terrain3D <c>save_16_bit</c>).</summary>
        public bool Save16Bit { get; set; }
    }
}
