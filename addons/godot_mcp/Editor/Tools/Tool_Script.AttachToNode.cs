/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptAttachToNodeToolId = "script-attach-to-node";

        [AiTool
        (
            ScriptAttachToNodeToolId,
            Title = "Script / Attach To Node",
            IdempotentHint = true
        )]
        [Description("Attach a script (C# '.cs' or GDScript '.gd') to a Node in the currently edited Godot " +
            "scene — the Godot analog of adding a MonoBehaviour to a GameObject. Identify the target with " +
            "'nodeRef' (instanceId preferred, else scene-tree path) and the script with 'scriptPath' (a " +
            "res:// script file that must exist). Pass an empty/whitespace/null 'scriptPath' to DETACH (clear) " +
            "the node's script. The script resource is loaded and set via Node.SetScript; the scene is marked " +
            "unsaved (persist with '" + Tool_Scene.SceneSaveToolId + "'). Returns the script's structured " +
            "ScriptInfo on attach, or an identity record with a 'detached' status on clear.")]
        public ScriptInfo AttachToNode
        (
            [Description("Reference to the target Node (instanceId preferred, else scene-tree path).")]
            NodeRef nodeRef,
            [Description("res:// path of the script to attach, ending in '.cs' or '.gd'. Pass " +
                "empty/whitespace/null to DETACH the node's current script.")]
            string? scriptPath = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                // ResolveNode validates the ref (null / invalid / not-found) and returns a single consistent
                // structured failure, matching the other Tool_Node handlers — no redundant top-level pre-check.
                var node = Tool_Node.ResolveNode(nodeRef, out var error)
                    ?? throw new Exception(error ?? $"Node by {nodeRef} not found.");

                // Detach mode: clear the script. SetScript(default) assigns a nil Variant, removing the script.
                if (string.IsNullOrWhiteSpace(scriptPath))
                {
                    node.SetScript(default);
                    EditorInterface.Singleton.MarkSceneAsUnsaved();
                    return new ScriptInfo
                    {
                        ResourcePath = string.Empty,
                        Language = string.Empty,
                        Status = $"Script detached from node '{node.Name}'.",
                    };
                }

                var resPath = ScriptLang_.RequireScriptResPath(scriptPath, nameof(scriptPath), out var lang);

                if (!ResourceLoader.Exists(resPath))
                    throw new ArgumentException(
                        $"No script resource exists at '{resPath}'. Create it with '{ScriptCreateToolId}' first " +
                        "(and, for a new C# type, rebuild the project so the script compiles).", nameof(scriptPath));

                var script = ResourceLoader.Load<Script>(resPath)
                    ?? throw new ArgumentException(
                        $"Resource at '{resPath}' is not a Godot Script.", nameof(scriptPath));

                node.SetScript(script);

                // Mark unsaved so the script assignment persists when the scene is saved (the assignment is
                // serialized into the .tscn). We do NOT save here — saving is an explicit, separate step
                // (Tool_Scene.Save) so the caller controls when the scene is written.
                EditorInterface.Singleton.MarkSceneAsUnsaved();

                return ToScriptInfo(resPath, lang, $"Script attached to node '{node.Name}'.");
            });
        }
    }
}
#endif
