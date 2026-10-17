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
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>One PhantomCameraHost of the edited scene (a row of <c>phantomcamera.hosts</c>); read-only, writes go through <c>node</c>.</summary>
    [RqlDescription("PhantomCameraHost (read-only row): name path scriptClass camera node")]
    public sealed class PhantomCameraHostRow
    {
        internal PhantomCameraHostRow(Node node, string path, string scriptClass)
        {
            this.node = node;
            this.path = path;
            this.scriptClass = scriptClass;
        }

        [RqlDisplay, RqlDescription("node name")]
        public string name => node.Name.ToString();

        [RqlDescription("path below the edited scene root")]
        public string path { get; }

        [RqlDescription("PhantomCameraHost, or your class_name extending it")]
        public string scriptClass { get; }

        [RqlDescription("path of the Camera3D / Camera2D the host drives (its parent), or null when its parent is not a camera")]
        public string? camera
        {
            get
            {
                var parent = node.GetParent();
                return parent is Camera3D || parent is Camera2D ? PhantomCameraApi.PathInScene(parent) : null;
            }
        }

        [RqlReference, RqlDescription("the PhantomCameraHost node")]
        public Node node { get; }
    }
}
#endif
