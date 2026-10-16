/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// MCP tool family for the <b>PhantomCamera Tools</b> extension (tool ids prefixed <c>phantomcamera-*</c>) —
    /// AI tools for the community <a href="https://github.com/ramokz/phantom-camera">Phantom Camera</a> addon
    /// (Cinemachine-style virtual cameras). The McpPlugin assembly scanner auto-discovers this
    /// <c>[AiToolType]</c> family once the package's source compiles into the consumer's Godot project — no
    /// registry edit needed.
    ///
    /// <para>
    /// <b>Class B (addon-dependent).</b> Phantom Camera's classes are NOT in GodotSharp and the package must
    /// NOT depend on the addon (that would break the "no addon/GodotSharp dependency" nuspec invariant and
    /// force every consumer to vendor the exact addon version). So this family references Phantom Camera's
    /// classes <b>only by string name</b>, resolved + driven at runtime via <c>AddonInterop</c>
    /// (<c>GodotObject.Set/Get/Call</c> with GDScript <c>snake_case</c> member names), and <b>presence-gates</b>
    /// every editor tool: a missing addon returns a structured <c>installed:false</c> result
    /// (<see cref="PhantomCameraInfo.NotInstalled"/>) rather than crashing. The package compiles with the
    /// addon ABSENT — it never names a Phantom Camera type.
    /// </para>
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Tools split by the API they touch:
    /// <list type="bullet">
    ///   <item>
    ///     No Godot native API (<c>phantomcamera-defaults</c>, in <c>Runtime/Tools/</c>) — outside
    ///     <c>#if TOOLS</c>, CI-unit-testable with no Godot binary.
    ///   </item>
    ///   <item>
    ///     Editor/scene-driving (<c>phantomcamera-host-create</c>, <c>-create</c>, <c>-set-follow</c>,
    ///     <c>-set-look-at</c>, <c>-set-priority</c>, <c>-get</c>, in <c>Editor/Tools/</c>) — behind
    ///     <c>#if TOOLS</c>, every Godot call marshalled via <c>MainThread.Instance.Run(...)</c>, the presence
    ///     gate as the FIRST line, E2E-verified.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_PhantomCamera
    {
    }
}
