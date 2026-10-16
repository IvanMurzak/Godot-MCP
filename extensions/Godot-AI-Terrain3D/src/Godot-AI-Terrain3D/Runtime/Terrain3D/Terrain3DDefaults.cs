/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the recommended Terrain3D
    /// starter configuration. The pure-managed <c>terrain3d-defaults</c> tool returns <see cref="For"/>; the
    /// editor tools reuse <see cref="RegionSizes.Snap"/> so an LLM-supplied region size can never reach the
    /// live node un-snapped. Keeping this logic pure-managed means it is verified by fast xUnit tests with no
    /// Godot binary.
    /// </summary>
    public static class Terrain3DDefaults
    {
        /// <summary>The recommended starter region size (a good balance of detail vs. memory).</summary>
        public const int DefaultRegionSize = Terrain3DNames.RegionSize1024;

        /// <summary>The recommended number of clipmap mesh LOD levels.</summary>
        public const int DefaultMeshLods = 7;

        /// <summary>The recommended clipmap mesh size.</summary>
        public const int DefaultMeshSize = 48;

        /// <summary>The recommended world-space vertex spacing.</summary>
        public const double DefaultVertexSpacing = 1.0;

        /// <summary>
        /// The recommended <c>res://</c> data directory. Terrain3D persists region data to a folder on disk
        /// (not just the scene), so a WRITABLE directory is required before sculpting.
        /// </summary>
        public const string RecommendedDataDirectory = "res://terrain_data";

        /// <summary>The recommended <c>save_16_bit</c> setting (false = 32-bit heightmaps for full precision).</summary>
        public const bool DefaultSave16Bit = false;

        /// <summary>
        /// A recommended starter configuration. <paramref name="preset"/> (case/whitespace-insensitive) selects
        /// the region size: <c>"small"</c> → 256, <c>"medium"</c> → 1024 (default when null/empty),
        /// <c>"large"</c> → 2048. Throws <see cref="ArgumentException"/> on any other value.
        /// </summary>
        public static Terrain3DConfig For(string? preset = null)
        {
            var (label, regionSize) = ParsePreset(preset);
            return new Terrain3DConfig
            {
                Preset = label,
                RegionSize = regionSize,
                MeshLods = DefaultMeshLods,
                MeshSize = DefaultMeshSize,
                VertexSpacing = DefaultVertexSpacing,
                DataDirectory = RecommendedDataDirectory,
                Save16Bit = DefaultSave16Bit
            };
        }

        /// <summary>
        /// Parse a preset name into its (label, region size). Null/empty → the default "medium" preset.
        /// Throws <see cref="ArgumentException"/> on an unrecognized value.
        /// </summary>
        public static (string Label, int RegionSize) ParsePreset(string? preset)
        {
            if (string.IsNullOrWhiteSpace(preset))
                return ("medium", DefaultRegionSize);

            switch (preset!.Trim().ToLowerInvariant())
            {
                case "small": return ("small", Terrain3DNames.RegionSize256);
                case "medium": return ("medium", Terrain3DNames.RegionSize1024);
                case "large": return ("large", Terrain3DNames.RegionSize2048);
                default:
                    throw new ArgumentException(
                        $"Unrecognized Terrain3D preset '{preset}'. Use 'small', 'medium', or 'large'.",
                        nameof(preset));
            }
        }
    }
}
