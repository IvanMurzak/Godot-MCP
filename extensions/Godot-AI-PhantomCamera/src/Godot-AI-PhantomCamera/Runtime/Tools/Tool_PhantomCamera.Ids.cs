/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    public partial class Tool_PhantomCamera
    {
        // The tool ids the dock / godot-cli / shared catalog reference. Declared here PURE-MANAGED (outside
        // #if TOOLS) — even for the editor-only tools — so a single source of truth is pinned by the unit
        // tests and can never drift silently from the [AiTool(...)] ids the editor files use.

        /// <summary>Pure-managed defaults tool id (<c>phantomcamera-defaults</c>).</summary>
        public const string DefaultsToolId = "phantomcamera-defaults";

        /// <summary>Editor tool id — ensure a <c>PhantomCameraHost</c> on a Camera3D (<c>phantomcamera-host-create</c>).</summary>
        public const string HostCreateToolId = "phantomcamera-host-create";

        /// <summary>Editor tool id — create a <c>PhantomCamera3D</c> (<c>phantomcamera-create</c>).</summary>
        public const string CreateToolId = "phantomcamera-create";

        /// <summary>Editor tool id — set follow target + mode (<c>phantomcamera-set-follow</c>).</summary>
        public const string SetFollowToolId = "phantomcamera-set-follow";

        /// <summary>Editor tool id — set look-at target + mode (<c>phantomcamera-set-look-at</c>).</summary>
        public const string SetLookAtToolId = "phantomcamera-set-look-at";

        /// <summary>Editor tool id — set priority (<c>phantomcamera-set-priority</c>).</summary>
        public const string SetPriorityToolId = "phantomcamera-set-priority";

        /// <summary>Editor tool id — read a PhantomCamera's config (<c>phantomcamera-get</c>).</summary>
        public const string GetToolId = "phantomcamera-get";
    }
}
