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
    public partial class Tool_Terrain3D
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>terrain3d-defaults</c>).</summary>
        public const string DefaultsToolId = "terrain3d-defaults";

        /// <summary>Editor tool id — create a Terrain3D node (<c>terrain3d-create</c>).</summary>
        public const string CreateToolId = "terrain3d-create";

        /// <summary>Editor tool id — set the terrain data directory (<c>terrain3d-set-data-directory</c>).</summary>
        public const string SetDataDirectoryToolId = "terrain3d-set-data-directory";

        /// <summary>Editor tool id — set the terrain region size (<c>terrain3d-set-region-size</c>).</summary>
        public const string SetRegionSizeToolId = "terrain3d-set-region-size";

        /// <summary>Editor tool id — assign/create a Terrain3DMaterial (<c>terrain3d-set-material</c>).</summary>
        public const string SetMaterialToolId = "terrain3d-set-material";

        /// <summary>Editor tool id — read a Terrain3D node's scalar config (<c>terrain3d-get</c>).</summary>
        public const string GetToolId = "terrain3d-get";
    }
}
