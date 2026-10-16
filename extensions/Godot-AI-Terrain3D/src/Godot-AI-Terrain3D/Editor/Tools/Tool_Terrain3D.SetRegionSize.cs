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
using Godot;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    public partial class Tool_Terrain3D
    {
        /// <summary>
        /// Editor-only tool — sets a Terrain3D node's region size via its dedicated <c>change_region_size</c>
        /// method (Terrain3D re-slices existing data, so the size is not a plain property write). The requested
        /// size is snapped to a valid <c>RegionSize</c> (64/128/256/512/1024/2048) first. PRESENCE-GATED;
        /// main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetRegionSizeToolId,
            Title = "Terrain3D / Set Region Size",
            IdempotentHint = true
        )]
        [Description("Set the region size of an existing Terrain3D node, addressed by 'nodePath' (relative to " +
            "the edited scene root). 'regionSize' is snapped to the nearest valid Terrain3D RegionSize " +
            "(64/128/256/512/1024/2048). Calls the addon's change_region_size (which re-slices existing region " +
            "data). Requires the Terrain3D addon (returns 'Installed: false' with an install hint when absent). " +
            "Returns the terrain's updated structured config (RegionSize reflects the snapped value).")]
        public Terrain3DNodeInfo SetRegionSize
        (
            [Description("Node path (relative to the edited scene root) of the Terrain3D node to update.")]
            string nodePath,
            [Description("Requested region size; snapped to the nearest valid Terrain3D RegionSize " +
                "(64/128/256/512/1024/2048).")]
            int regionSize
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.NativeClassExists(AddonGate.PresenceClass))
                    return Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());

                var node = ResolveTerrainNodeOrThrow(nodePath);
                node.Call(Terrain3DNames.ChangeRegionSizeMethod, RegionSizes.Snap(regionSize));

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
