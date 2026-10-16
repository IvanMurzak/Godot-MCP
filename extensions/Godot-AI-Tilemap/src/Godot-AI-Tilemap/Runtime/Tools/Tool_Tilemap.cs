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

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// MCP tool family for the <b>Tilemap Tools</b> extension (tool ids prefixed <c>tilemap-*</c>) — a
    /// source-only NuGet package that wraps Godot's built-in <c>TileMapLayer</c> node (Godot 4.3+, the modern
    /// replacement for the deprecated <c>TileMap</c>). Like the core Godot-MCP addon and Unity-MCP, ReflectorNet
    /// reflects the attributes and McpPlugin's assembly scanner auto-discovers the family once the package's
    /// source compiles into the consumer's Godot project — <b>no registry edit needed</b>.
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only (load-bearing).</b> Tools and support types are split by the API they touch:
    /// <list type="bullet">
    ///   <item>
    ///     Pure-managed support types (<c>TileCell</c>, <c>TileMapLayerInfo</c>, <c>TilemapValidation</c> in
    ///     <c>Runtime/Tilemap/</c>) and the tool-id consts (<c>Tool_Tilemap.Ids.cs</c>) touch NO Godot native
    ///     type, so they stay OUTSIDE <c>#if TOOLS</c> and are CI-unit-testable with no Godot binary.
    ///   </item>
    ///   <item>
    ///     Every tool in this family drives the live editor / scene (<c>tilemap-create</c>, <c>-set-tileset</c>,
    ///     <c>-set-cell</c>, <c>-erase-cell</c>, <c>-get-used-cells</c>, <c>-clear</c>, in <c>Editor/Tools/</c>),
    ///     so it lives behind <c>#if TOOLS</c> (excluded from an exported game) and marshals every Godot call onto
    ///     the editor main thread via <c>MainThread.Instance.Run(...)</c> — verified by the headless-Godot E2E.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Tilemap
    {
    }
}
