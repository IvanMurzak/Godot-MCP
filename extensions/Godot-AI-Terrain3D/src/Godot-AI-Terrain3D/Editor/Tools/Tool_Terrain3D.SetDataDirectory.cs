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
        /// Editor-only tool — sets a Terrain3D node's <c>data_directory</c> (the res:// folder where Terrain3D
        /// persists/loads region data on disk; a WRITABLE directory is required before sculpting). PRESENCE-GATED.
        /// Main-thread-marshalled; the terrain is driven dynamically by string name.
        /// </summary>
        [AiTool
        (
            SetDataDirectoryToolId,
            Title = "Terrain3D / Set Data Directory",
            IdempotentHint = true
        )]
        [Description("Set the data directory of an existing Terrain3D node, addressed by 'nodePath' (relative " +
            "to the edited scene root). 'dataDirectory' is a res://-relative path where Terrain3D persists and " +
            "loads region data on disk — it must be writable. Requires the Terrain3D addon (returns " +
            "'Installed: false' with an install hint when absent). Returns the terrain's updated structured config.")]
        public Terrain3DNodeInfo SetDataDirectory
        (
            [Description("Node path (relative to the edited scene root) of the Terrain3D node to update.")]
            string nodePath,
            [Description("The res:// data directory Terrain3D should persist/load region data from (must be writable).")]
            string dataDirectory
        )
        {
            return MainThread.Instance.Run(() =>
            {
                if (!AddonInterop.NativeClassExists(AddonGate.PresenceClass))
                    return Terrain3DNodeInfo.FromGate(AddonGate.NotInstalled());

                if (string.IsNullOrWhiteSpace(dataDirectory))
                    throw new ArgumentException("A data directory is required.", nameof(dataDirectory));

                var node = ResolveTerrainNodeOrThrow(nodePath);
                node.Set(Terrain3DNames.DataDirectoryProperty, dataDirectory);

                EditorInterface.Singleton.MarkSceneAsUnsaved();
                return ReadInfo(node);
            });
        }
    }
}
#endif
