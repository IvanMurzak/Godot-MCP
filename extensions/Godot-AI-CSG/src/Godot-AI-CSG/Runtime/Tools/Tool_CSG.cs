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

namespace com.IvanMurzak.Godot.MCP.CSG
{
    /// <summary>
    /// MCP tool family for the <b>CSG Tools</b> extension (tool ids prefixed <c>csg-*</c>) — wraps Godot's
    /// built-in Constructive-Solid-Geometry nodes (<c>CsgBox3D</c>, <c>CsgSphere3D</c>, <c>CsgCylinder3D</c>
    /// primitives + the <c>CsgCombiner3D</c> container) so an LLM can block out 3D geometry and compose
    /// boolean (union / intersection / subtraction) shapes. A built-in, dependency-free family — no
    /// third-party engine code — so the package proves source-only distribution with no extra variable.
    /// This is the SAME authoring model as Unity-MCP and the core Godot-MCP addon: ReflectorNet reflects the
    /// attributes, and McpPlugin's assembly scanner auto-discovers the family once the package's source
    /// compiles into the consumer's Godot project — <b>no registry edit needed</b>.
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Tools are split by the API they touch, exactly like the core addon:
    /// <list type="bullet">
    ///   <item>
    ///     Tools with NO Godot native API (<c>csg-defaults</c>, in <c>Runtime/Tools/</c>) stay OUTSIDE
    ///     <c>#if TOOLS</c> so they compile in any consumer build AND are CI-unit-testable with no Godot binary.
    ///   </item>
    ///   <item>
    ///     Tools that drive the editor / live scene (<c>csg-box-create</c>, <c>-sphere-create</c>,
    ///     <c>-cylinder-create</c>, <c>-combiner-create</c>, <c>-set-operation</c>, <c>-get</c>, in
    ///     <c>Editor/Tools/</c>) live behind <c>#if TOOLS</c> (excluded from an exported game) and marshal every
    ///     Godot call onto the editor main thread via <c>MainThread.Instance.Run(...)</c> — verified by the
    ///     headless-Godot E2E.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_CSG
    {
    }
}
