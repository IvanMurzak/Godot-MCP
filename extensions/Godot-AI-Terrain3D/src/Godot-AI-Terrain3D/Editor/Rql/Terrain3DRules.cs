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
    /// The value rules the module enforces before it changes a terrain. Each returns the rejection reason, or null when the value
    /// is valid. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class Terrain3DRules
    {
        /// <summary>
        /// A region size Terrain3D accepts: 64, 128, 256, 512, 1024 or 2048 (any other value makes <c>change_region_size</c> log an
        /// error and change nothing; 0.x snapped it to the nearest one).
        /// </summary>
        public static string? RegionSize(int size) =>
            Array.IndexOf(Terrain3DNames.RegionSizes, size) >= 0
                ? null
                : $"RegionSize must be one of {string.Join(", ", Terrain3DNames.RegionSizes)} (got {size})";

        /// <summary>The Terrain3D GDExtension must be loaded (its classes registered in ClassDB).</summary>
        public static string? Installed(bool classExists) =>
            classExists ? null : $"the {Terrain3DNames.AddonName} addon is not installed: {Terrain3DNames.InstallHint}";
    }
}
