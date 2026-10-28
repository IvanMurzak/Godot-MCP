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
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    public sealed partial class RqlEditorApi
    {
        [RqlCallable("add a new node of a Godot class (or a script class_name) to the edited scene and return it; options: name, parent (a " +
            "node, path or ref; the scene root by default) and any Godot property (position, rotation_degrees, scale, mesh …). Once per " +
            "item: range(0, 100) | call editor.CreateNode(\"MeshInstance3D\", {name: \"Cube\" + $, position: [$ * 2, 0, 0]}) is one undo step")]
        public Node CreateNode(string className, NodeOptions? options = null) => RqlGodot.CreateNode(className, options);

        public string? ValidateCreateNode(string className, NodeOptions? options) => RqlGodot.ValidateCreateNode(className, options);

        [RqlCallable("add a new instance of the scene at a res:// path to the edited scene and return it; options: name, parent (the scene " +
            "root by default) and Godot properties of the instance's root (position …, saved as overrides). Once per item, one undo step")]
        public Node InstantiateScene(string path, NodeOptions? options = null) => RqlGodot.InstantiateSceneWith(path, options);

        public string? ValidateInstantiateScene(string path, NodeOptions? options) => RqlGodot.ValidateInstantiateSceneWith(path, options);
    }
}
#endif
