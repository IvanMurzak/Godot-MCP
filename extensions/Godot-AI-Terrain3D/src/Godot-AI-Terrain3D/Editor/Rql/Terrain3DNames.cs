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
    /// The Terrain3D GDExtension's class names, snake_case property / method names and region sizes, verified against
    /// <see href="https://github.com/TokisanGames/Terrain3D">TokisanGames/Terrain3D</see> v1.0.2-stable. The package takes no
    /// compile-time dependency on the addon (there is no C# type for its classes), so these strings are the contract:
    /// centralized here, pure-managed and unit-tested.
    /// </summary>
    public static class Terrain3DNames
    {
        /// <summary>Display name of the wrapped third-party addon.</summary>
        public const string AddonName = "Terrain3D";

        /// <summary>The Terrain3D node class (a Node3D; its C# wrapper is <c>Node3D</c>) registered in ClassDB.</summary>
        public const string TerrainClass = "Terrain3D";

        /// <summary>The Terrain3DMaterial resource class (its C# wrapper is <c>Resource</c>).</summary>
        public const string MaterialClass = "Terrain3DMaterial";

        public const string DataDirectoryProperty = "data_directory";
        /// <summary><c>region_size</c>: editor-only (not stored in the scene); its setter is <c>change_region_size</c>.</summary>
        public const string RegionSizeProperty = "region_size";
        public const string MaterialProperty = "material";
        public const string DataProperty = "data";
        public const string VersionProperty = "version";
        public const string MeshLodsProperty = "mesh_lods";
        public const string MeshSizeProperty = "mesh_size";
        public const string VertexSpacingProperty = "vertex_spacing";

        /// <summary><c>change_region_size(size)</c>: re-slices the existing region data into regions of the new size.</summary>
        public const string ChangeRegionSizeMethod = "change_region_size";

        /// <summary><c>get_region_count()</c> on the node's Terrain3DData.</summary>
        public const string GetRegionCountMethod = "get_region_count";

        /// <summary>The region sizes Terrain3D accepts (its RegionSize enum: the value is the edge length), ascending.</summary>
        public static readonly int[] RegionSizes = { 64, 128, 256, 512, 1024, 2048 };

        /// <summary>Terrain3D's default region size.</summary>
        public const int DefaultRegionSize = 256;

        /// <summary>What to do when the addon is missing.</summary>
        public const string InstallHint =
            "Install the 'Terrain3D' GDExtension addon (TokisanGames/Terrain3D, tested against v1.0.2-stable) into addons/terrain_3d/, " +
            "restart Godot, and enable it under Project Settings -> Plugins. Terrain3D ships precompiled binaries, so no C++ build is " +
            "needed. Requires Godot 4.4 or newer.";
    }
}
