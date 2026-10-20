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
using Godot;

namespace com.IvanMurzak.Godot.MCP
{
    /// <summary>
    /// "The editor's modal progress dialog is showing": the <see cref="MainThreadDispatch.MainThreadDispatcher.HoldWhile"/>
    /// predicate. Godot does not expose the dialog to scripts; it is the one <c>ProgressDialog</c> node the editor creates.
    /// On 4.4+ it stays in the tree (a hidden <c>CenterContainer</c>); on 4.3 it is a <c>PopupPanel</c> that is in the tree
    /// only while it shows. So it is looked up until found; the dispatcher asks only when calls are queued.
    /// </summary>
    internal sealed class EditorProgressGate
    {
        readonly SceneTree _tree;
        ulong _dialogId;

        public EditorProgressGate(SceneTree tree) => _tree = tree;

        public bool IsShowing()
        {
            if (_dialogId == 0)
            {
                var found = _tree.Root.FindChildren("*", "ProgressDialog", recursive: true, owned: false);
                if (found.Count == 0)
                    return false;
                _dialogId = found[0].GetInstanceId();
            }
            return GodotObject.InstanceFromId(_dialogId) is Node dialog && dialog.Get(CanvasItem.PropertyName.Visible).AsBool();
        }
    }
}
#endif
