/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    public partial class Tool_Terrain3D
    {
        /// <summary>
        /// Editor-only, read-only tool — reads the scalar config (data directory, region size, version, mesh
        /// LODs/size, vertex spacing, material presence, region count) of an existing Terrain3D node, driven
        /// dynamically by string name. PRESENCE-GATED; main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            GetToolId,
            Title = "Terrain3D / Get",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Read the scalar config (data directory, region size, version, mesh LODs, mesh size, " +
            "vertex spacing, whether a material is assigned, region count) of an existing Terrain3D node, " +
            "addressed by 'nodePath' (relative to the edited scene root). Read-only: does not modify the scene. " +
            "Requires the Terrain3D addon (returns 'Installed: false' with an install hint when absent).")]
        public Terrain3DNodeInfo Get
        (
            [Description("Node path (relative to the edited scene root) of the Terrain3D node to read.")]
            string nodePath
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.NativeClassExists(AddonGate.PresenceClass))
                    return Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());

                return ReadInfo(ResolveTerrainNodeOrThrow(nodePath));
            });
        }
    }
}
#endif
