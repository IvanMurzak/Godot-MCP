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
using System;
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    public partial class Tool_Terrain3D
    {
        /// <summary>
        /// Editor-only tool — ensures a Terrain3D node has a <c>Terrain3DMaterial</c> assigned: creates a fresh
        /// one (resolved by string name through <c>ClassDB</c>) and assigns it when the terrain has none (or when
        /// <c>replace</c> is true). PRESENCE-GATED; main-thread-marshalled.
        /// </summary>
        [AiTool
        (
            SetMaterialToolId,
            Title = "Terrain3D / Set Material",
            IdempotentHint = true
        )]
        [Description("Assign a Terrain3DMaterial to an existing Terrain3D node, addressed by 'nodePath' " +
            "(relative to the edited scene root). When the terrain has no material a fresh Terrain3DMaterial is " +
            "created and assigned; pass 'replace: true' to always create+assign a fresh one. Requires the " +
            "Terrain3D addon (returns 'Installed: false' with an install hint when absent). Returns the " +
            "terrain's updated structured config (HasMaterial is true on success).")]
        public Terrain3DNodeInfo SetMaterial
        (
            [Description("Node path (relative to the edited scene root) of the Terrain3D node to update.")]
            string nodePath,
            [Description("When true, always create and assign a fresh Terrain3DMaterial even if one is already " +
                "assigned. When omitted/false, an existing material is left in place.")]
            bool? replace = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.NativeClassExists(AddonGate.PresenceClass))
                    return Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());

                var node = ResolveTerrainNodeOrThrow(nodePath);

                var existing = node.Get(Terrain3DNames.MaterialProperty).As<GodotObject>();
                if (existing == null || replace == true)
                {
                    var material = AddonInterop.InstantiateNative(Terrain3DNames.MaterialClass)
                        ?? throw new InvalidOperationException(
                            $"Failed to instantiate '{Terrain3DNames.MaterialClass}' via ClassDB even though the class exists.");
                    node.Set(Terrain3DNames.MaterialProperty, material);
                }

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
