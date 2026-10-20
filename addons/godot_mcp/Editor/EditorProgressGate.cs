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
    /// On 4.4+ it stays in the tree (a hidden <c>CenterContainer</c>); on 4.3 it is a <c>PopupPanel</c> that leaves the tree
    /// while hidden, so it is also caught by <see cref="SceneTree.NodeAdded"/> the first time it shows.
    /// </summary>
    internal sealed class EditorProgressGate
    {
        const string DialogClass = "ProgressDialog";

        readonly SceneTree _tree;
        ulong _dialogId;

        EditorProgressGate(SceneTree tree) => _tree = tree;

        public static EditorProgressGate Install(SceneTree tree)
        {
            var gate = new EditorProgressGate(tree);
            var found = tree.Root.FindChildren("*", DialogClass, recursive: true, owned: false);
            if (found.Count > 0)
                gate._dialogId = found[0].GetInstanceId();
            else
                tree.NodeAdded += gate.OnNodeAdded;
            return gate;
        }

        public bool IsShowing() =>
            _dialogId != 0 && GodotObject.InstanceFromId(_dialogId) is Node dialog && dialog.Get(CanvasItem.PropertyName.Visible).AsBool();

        /// <summary>Drop the NodeAdded subscription (it would root the collectible addon assembly across a hot reload).</summary>
        public void Uninstall()
        {
            if (_dialogId == 0 && GodotObject.IsInstanceValid(_tree))
                _tree.NodeAdded -= OnNodeAdded;
        }

        void OnNodeAdded(Node node)
        {
            if (!node.IsClass(DialogClass))
                return;
            _dialogId = node.GetInstanceId();
            _tree.NodeAdded -= OnNodeAdded;
        }
    }
}
#endif
