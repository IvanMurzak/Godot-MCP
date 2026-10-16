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

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE
{
    /// <summary>
    /// Sample MCP tool family for the YOUR_DISPLAY_NAME extension (tool ids prefixed
    /// <c>YOUR_TOOL_PREFIX-*</c>). A tool family is one <c>[AiToolType]</c> <c>partial class</c>;
    /// each tool method (<c>[AiTool("&lt;name&gt;")]</c> + <c>[Description]</c>) lives in its own
    /// partial-class file. This is the SAME authoring model as Unity-MCP and the core Godot-MCP addon —
    /// ReflectorNet reflects the attributes, McpPlugin's assembly scanner auto-discovers the family
    /// once the package's source compiles into the consumer's Godot project (no registry edit needed).
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Split tools by what API they touch, exactly like the core addon:
    /// <list type="bullet">
    ///   <item>
    ///     Tools with NO Godot native API surface (this file's <c>Echo</c>) stay OUTSIDE <c>#if TOOLS</c>
    ///     so they compile in any consumer build AND are CI-unit-testable (no Godot binary required —
    ///     a plain xUnit host can construct the class and call the method).
    ///   </item>
    ///   <item>
    ///     Tools that touch the Godot editor (<c>EditorInterface</c>, live <c>Node</c>/<c>Resource</c>)
    ///     live behind <c>#if TOOLS</c> (see <c>../../Editor/Tools/Tool_YOUR_FEATURE.EditorInfo.cs</c>),
    ///     so they are excluded from an exported game build, and they marshal onto the editor main thread
    ///     via <c>MainThread.Instance.Run(...)</c> — NEVER touch Godot objects off-thread.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_YOUR_FEATURE
    {
    }
}
