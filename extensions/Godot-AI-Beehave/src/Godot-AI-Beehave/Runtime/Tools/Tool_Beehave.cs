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

namespace com.IvanMurzak.Godot.MCP.Beehave
{
    /// <summary>
    /// MCP tool family for authoring <b>Beehave</b> behaviour trees in the Godot editor (tool ids prefixed
    /// <c>beehave-*</c>). One <c>[AiToolType]</c> <c>partial class</c>; each tool method lives in its own
    /// partial-class file. ReflectorNet reflects the attributes and McpPlugin's assembly scanner auto-discovers
    /// the family once the source-only package compiles into the consumer's Godot project — no registry edit.
    ///
    /// <para>
    /// <b>This is a CLASS-B (addon-dependent) extension.</b> Beehave is a THIRD-PARTY GDScript addon
    /// (<c>bitbrain/beehave</c>) whose classes are NOT in GodotSharp, so the package must not take a
    /// compile-time dependency on it. The editor tools therefore reference Beehave's classes ONLY by string
    /// name — resolved + driven at runtime through <see cref="AddonInterop"/> (global-class list + <c>GD.Load</c>
    /// + <c>GodotObject.Set/Get/Call</c> with GDScript <c>snake_case</c> members) — and each editor tool's first
    /// line is a presence gate (<see cref="AddonGate"/>) returning a structured <c>Installed = false</c> result
    /// when the addon is absent, never a crash.
    /// </para>
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only split (load-bearing).</b>
    /// <list type="bullet">
    ///   <item>
    ///     Pure-managed cores — the tool-id consts (<c>Tool_Beehave.Ids.cs</c>), the addon class/member/enum-int
    ///     CONSTANTS (<c>Runtime/Beehave/BeehaveEnums.cs</c>), the kind parsing, the result shapes, the gate
    ///     shape, and the pure-managed <c>beehave-defaults</c> tool — live OUTSIDE <c>#if TOOLS</c> and are
    ///     CI-unit-tested (no Godot binary).
    ///   </item>
    ///   <item>
    ///     Editor tools (<c>Editor/Tools/*</c>) and the dynamic <see cref="AddonInterop"/>
    ///     (<c>Runtime/Interop/AddonInterop.cs</c>) live behind <c>#if TOOLS</c>, marshal every Godot call
    ///     through <c>MainThread.Instance.Run(...)</c>, and are E2E-verified — never unit-tested (constructing a
    ///     <c>Node</c> in a no-Godot xUnit host faults).
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Beehave
    {
    }
}
