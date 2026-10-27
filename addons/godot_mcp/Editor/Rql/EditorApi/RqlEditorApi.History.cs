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
using System.Collections.Generic;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Undo / redo for agents: <c>editor | call Undo()</c> is the editor's own Undo (the Scene menu item behind Ctrl+Z), so the
    /// editor picks the history (the edited scene's or the global one) and keeps its bookkeeping, exactly as when a user
    /// presses Ctrl+Z. Godot's C# API has no editor-level undo: stepping a history's <see cref="UndoRedo"/> directly (what an
    /// agent otherwise does through script-execute) leaves the editor's own undo stack out of step.
    /// </summary>
    public sealed partial class RqlEditorApi
    {
        [RqlCallable("undo the last editor action, like Ctrl+Z (an rql-mutate, an edit in the Scene dock or the inspector …); " +
            "returns the undone action, e.g. \"RQL: nodes | … | set name = …\". Fails when there is nothing to undo")]
        public string Undo() => EditorHistory.Step(undo: true);

        [RqlCallable("redo the last undone action, like Ctrl+Shift+Z; returns the redone action. Fails when there is nothing to redo")]
        public string Redo() => EditorHistory.Step(undo: false);
    }

    internal static class EditorHistory
    {
        // EditorUndoRedoManager.SpecialHistory.Global.
        private const int GlobalHistory = 0;

        // The Scene menu (a PopupMenu of the editor's MenuBar) found last; looked up again when it is gone.
        private static ulong _menuId;

        public static string Step(bool undo)
        {
            var manager = RqlGodot.UndoRedo?.Invoke();
            if (manager == null || !GodotObject.IsInstanceValid(manager))
                throw new InvalidOperationException("the editor's undo history is not available (the plugin is not installed)");
            var command = undo ? "Undo" : "Redo";
            var (menu, id) = FindMenuItem(command)
                ?? throw new InvalidOperationException($"the editor's {command} menu item was not found");

            var histories = new List<UndoRedo>();
            if (manager.GetHistoryUndoRedo(GlobalHistory) is { } global) histories.Add(global);
            if (RqlGodot.EditedRoot() is { } root && manager.GetHistoryUndoRedo(manager.GetObjectHistoryId(root)) is { } scene)
                histories.Add(scene);
            var before = new List<HistoryState>();
            foreach (var h in histories) before.Add(new HistoryState(h.GetVersion(), h.GetCurrentActionName()));

            menu.EmitSignal(PopupMenu.SignalName.IdPressed, id);
            RqlGodot.InvalidateGroups(); // the step may change groups behind RQL

            var after = new List<HistoryState>();
            foreach (var h in histories) after.Add(new HistoryState(h.GetVersion(), h.GetCurrentActionName()));
            return HistoryStepText.Describe(undo, before, after);
        }

        private static (PopupMenu menu, int id)? FindMenuItem(string shortcutName)
        {
            if (GodotObject.InstanceFromId(_menuId) is PopupMenu cached && Item(cached, shortcutName) is { } cachedId)
                return (cached, cachedId);
            var found = Find(EditorInterface.Singleton.GetBaseControl(), shortcutName);
            if (found == null) return null;
            _menuId = found.Value.menu.GetInstanceId();
            return found;
        }

        // Depth-first; only a PopupMenu directly under a MenuBar (the editor's main menu) is considered.
        private static (PopupMenu menu, int id)? Find(Node node, string shortcutName)
        {
            if (node is PopupMenu popup && popup.GetParent() is MenuBar && Item(popup, shortcutName) is { } id) return (popup, id);
            var count = node.GetChildCount(includeInternal: true);
            for (int i = 0; i < count; i++)
                if (Find(node.GetChild(i, includeInternal: true), shortcutName) is { } hit) return hit;
            return null;
        }

        // The item whose shortcut is the editor's "Undo" / "Redo" (shortcut names are not translated); the item text as a fallback.
        private static int? Item(PopupMenu popup, string shortcutName)
        {
            for (int i = 0; i < popup.ItemCount; i++)
                if (popup.GetItemShortcut(i)?.ResourceName == shortcutName) return popup.GetItemId(i);
            for (int i = 0; i < popup.ItemCount; i++)
                if (popup.GetItemText(i) == shortcutName) return popup.GetItemId(i);
            return null;
        }
    }
}
#endif
