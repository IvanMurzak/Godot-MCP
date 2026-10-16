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

namespace com.IvanMurzak.Godot.MCP.GridMap
{
    /// <summary>
    /// MCP tool family for the <b>GridMap Tools</b> extension (tool ids prefixed <c>gridmap-*</c>) —
    /// a source-only NuGet extension for Godot's built-in <c>GridMap</c> node (a 3D grid of cells, each
    /// holding a mesh from an assigned <c>MeshLibrary</c>). This is the SAME authoring model as Unity-MCP
    /// and the core Godot-MCP addon: ReflectorNet reflects the attributes, and McpPlugin's assembly scanner
    /// auto-discovers the family once the package's source compiles into the consumer's Godot project —
    /// <b>no registry edit needed</b>.
    ///
    /// <para>
    /// <b>Namespace-shadow note.</b> This extension's root namespace ends in <c>.GridMap</c>, which SHADOWS
    /// the engine type <c>Godot.GridMap</c>. Every editor file that names the engine type aliases it
    /// (<c>using GdGridMap = Godot.GridMap;</c>) so an unqualified <c>GridMap</c> can never bind to the
    /// namespace (a <c>CS0118</c> at package-build time).
    /// </para>
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Tools are split by the API they touch, exactly like the core addon:
    /// <list type="bullet">
    ///   <item>
    ///     Tools with NO Godot native API (<c>gridmap-defaults</c>, in <c>Runtime/Tools/</c>) stay OUTSIDE
    ///     <c>#if TOOLS</c> so they compile in any consumer build AND are CI-unit-testable with no Godot binary.
    ///   </item>
    ///   <item>
    ///     Tools that drive the editor / live scene (<c>gridmap-create</c>, <c>-set-mesh-library</c>,
    ///     <c>-set-cell</c>, <c>-clear-cell</c>, <c>-clear</c>, <c>-get</c>, in <c>Editor/Tools/</c>) live behind
    ///     <c>#if TOOLS</c> (excluded from an exported game) and marshal every Godot call onto the editor main
    ///     thread via <c>MainThread.Instance.Run(...)</c> — verified by the headless-Godot E2E.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_GridMap
    {
    }
}
