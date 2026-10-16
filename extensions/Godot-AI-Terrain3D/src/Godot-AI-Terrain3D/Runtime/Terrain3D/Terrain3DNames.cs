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
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the Terrain3D GDExtension's
    /// class names, GDScript-style <b>snake_case</b> member names, and <c>RegionSize</c> enum-int values. Because
    /// a CLASS-B extension takes NO compile-time dependency on the addon, there are NO compile-time enum types
    /// or typed members to lean on — <b>these constants ARE the contract</b>, so they are centralized here and
    /// pinned by unit tests. The editor tools (<c>#if TOOLS</c>) drive Terrain3D dynamically with
    /// <c>GodotObject.Set/Get/Call</c> using exactly these names. Verified against
    /// <see href="https://github.com/TokisanGames/Terrain3D">TokisanGames/Terrain3D</see> @ <c>v1.0.2-stable</c>.
    /// </summary>
    public static class Terrain3DNames
    {
        // ---- ClassDB class names (string-only contract; no compile-time addon types) ------------------

        /// <summary>The Terrain3D node class (a <c>Node3D</c>) registered by the GDExtension in <c>ClassDB</c>.</summary>
        public const string TerrainClass = "Terrain3D";

        /// <summary>The Terrain3DMaterial resource class.</summary>
        public const string MaterialClass = "Terrain3DMaterial";

        /// <summary>The Terrain3DData object class (holds regions; exposed via the node's <c>data</c> property).</summary>
        public const string DataClass = "Terrain3DData";

        /// <summary>The Terrain3DAssets resource class (texture/mesh asset list).</summary>
        public const string AssetsClass = "Terrain3DAssets";

        // ---- snake_case property names (driven via GodotObject.Set/Get) -------------------------------

        /// <summary><c>data_directory</c> (String) — where Terrain3D persists/loads region data on disk.</summary>
        public const string DataDirectoryProperty = "data_directory";

        /// <summary><c>region_size</c> (RegionSize enum int) — terrain region edge length in cells.</summary>
        public const string RegionSizeProperty = "region_size";

        /// <summary><c>material</c> (Terrain3DMaterial) — the terrain's material resource.</summary>
        public const string MaterialProperty = "material";

        /// <summary><c>assets</c> (Terrain3DAssets) — the terrain's asset list resource.</summary>
        public const string AssetsProperty = "assets";

        /// <summary><c>data</c> (Terrain3DData, read-only) — the terrain's data/region container.</summary>
        public const string DataProperty = "data";

        /// <summary><c>version</c> (String, read-only) — the Terrain3D addon version string.</summary>
        public const string VersionProperty = "version";

        /// <summary><c>mesh_lods</c> (int) — number of clipmap mesh LOD levels.</summary>
        public const string MeshLodsProperty = "mesh_lods";

        /// <summary><c>mesh_size</c> (int) — clipmap mesh size.</summary>
        public const string MeshSizeProperty = "mesh_size";

        /// <summary><c>vertex_spacing</c> (float) — world-space distance between terrain vertices.</summary>
        public const string VertexSpacingProperty = "vertex_spacing";

        // ---- method names (driven via GodotObject.Call) -----------------------------------------------

        /// <summary>
        /// <c>change_region_size(value)</c> — the dedicated setter for the region size. Terrain3D exposes this
        /// as an explicit method (re-slicing is not a plain property write); call it rather than relying on a
        /// bare <c>region_size</c> property set.
        /// </summary>
        public const string ChangeRegionSizeMethod = "change_region_size";

        /// <summary><c>get_region_count()</c> on the <c>Terrain3DData</c> object — number of active regions.</summary>
        public const string GetRegionCountMethod = "get_region_count";

        // ---- RegionSize enum-int values (the int value IS the region edge length in cells) ------------

        /// <summary>Terrain3D <c>RegionSize.SIZE_64</c>.</summary>
        public const int RegionSize64 = 64;

        /// <summary>Terrain3D <c>RegionSize.SIZE_128</c>.</summary>
        public const int RegionSize128 = 128;

        /// <summary>Terrain3D <c>RegionSize.SIZE_256</c> (the addon default).</summary>
        public const int RegionSize256 = 256;

        /// <summary>Terrain3D <c>RegionSize.SIZE_512</c>.</summary>
        public const int RegionSize512 = 512;

        /// <summary>Terrain3D <c>RegionSize.SIZE_1024</c>.</summary>
        public const int RegionSize1024 = 1024;

        /// <summary>Terrain3D <c>RegionSize.SIZE_2048</c>.</summary>
        public const int RegionSize2048 = 2048;

        /// <summary>All valid Terrain3D region sizes, ascending.</summary>
        public static readonly int[] ValidRegionSizes =
            { RegionSize64, RegionSize128, RegionSize256, RegionSize512, RegionSize1024, RegionSize2048 };
    }

    /// <summary>
    /// Pure-managed helpers for validating + snapping a requested region size onto a valid Terrain3D
    /// <c>RegionSize</c> value, so an LLM-supplied number can never push the terrain into an invalid state.
    /// </summary>
    public static class RegionSizes
    {
        /// <summary>True when <paramref name="size"/> is exactly one of the valid Terrain3D region sizes.</summary>
        public static bool IsValid(int size) => Array.IndexOf(Terrain3DNames.ValidRegionSizes, size) >= 0;

        /// <summary>
        /// Snap an arbitrary requested size to the NEAREST valid Terrain3D region size (ties resolve to the
        /// smaller size). Values below the minimum clamp to 64; values above the maximum clamp to 2048.
        /// </summary>
        public static int Snap(int size)
        {
            var valid = Terrain3DNames.ValidRegionSizes;
            int best = valid[0];
            int bestDelta = Math.Abs(size - best);
            for (int i = 1; i < valid.Length; i++)
            {
                int delta = Math.Abs(size - valid[i]);
                if (delta < bestDelta)
                {
                    best = valid[i];
                    bestDelta = delta;
                }
            }
            return best;
        }
    }
}
