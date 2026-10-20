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
using com.IvanMurzak.Godot.MCP.Connection;
using Godot;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// Builds the <see cref="MainThreadDispatch.MainThreadDispatcher.HoldWhile"/> predicate: "the editor's modal
    /// progress dialog is showing". The dialog runs <c>Main::iteration()</c> from inside the operation it reports, so
    /// while it is visible every dispatcher tick is a nested frame in the middle of an editor operation. Godot does
    /// not expose the dialog to scripts; it is the one <c>ProgressDialog</c> node the editor creates at startup
    /// (a <c>PopupPanel</c> on 4.3, a <c>CenterContainer</c> from 4.4; both have <c>visible</c>).
    /// </summary>
    internal static class EditorProgressGate
    {
        /// <summary>The predicate, or <c>null</c> (no hold) when the dialog cannot be found.</summary>
        public static Func<bool>? Create(Node editorNode)
        {
            var root = editorNode.GetTree()?.Root;
            var found = root?.FindChildren("*", "ProgressDialog", recursive: true, owned: false);
            if (found == null || found.Count == 0)
            {
                GodotMcpLog.Warning("[Godot-MCP] editor ProgressDialog not found; MCP calls are not held while the editor shows progress");
                return null;
            }
            var dialogId = found[0].GetInstanceId();
            return () => GodotObject.InstanceFromId(dialogId) is Node dialog && dialog.Get(CanvasItem.PropertyName.Visible).AsBool();
        }
    }
}
#endif
