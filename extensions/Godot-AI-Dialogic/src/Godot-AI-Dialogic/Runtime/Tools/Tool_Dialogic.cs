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

namespace com.IvanMurzak.Godot.MCP.Dialogic
{
    /// <summary>
    /// MCP tool family for the <b>Dialogic Tools</b> extension (tool ids prefixed <c>dialogic-*</c>) — a
    /// <b>CLASS-B (addon-dependent)</b> extension that authors the community
    /// <a href="https://github.com/dialogic-godot/dialogic">Dialogic</a> addon's <b>timeline</b> (<c>.dtl</c>)
    /// and <b>character</b> (<c>.dch</c>) resources. Dialogic's types are NOT in GodotSharp, so the package
    /// references them ONLY by string name (resolved + driven at runtime through the editor-only
    /// <see cref="AddonInterop"/>) and never takes a compile-time dependency on the addon. Every editor tool
    /// is <b>presence-gated</b>: when Dialogic is absent the tool returns a clean structured
    /// <c>Installed: false</c> result with an install hint instead of crashing.
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Tools are split by the API they touch, exactly like the core addon
    /// and the Class-A <c>Godot-AI-Particles</c> example:
    /// <list type="bullet">
    ///   <item>
    ///     Tools with NO Godot native API (<c>dialogic-defaults</c>, in <c>Runtime/Tools/</c>) plus the
    ///     value-logic cores (<c>Runtime/Dialogic/</c>: the timeline text format, color normalization, the
    ///     addon name/member CONSTANTS, the <see cref="AddonGateResult"/> shape) stay OUTSIDE <c>#if TOOLS</c>
    ///     so they compile in any consumer build AND are CI-unit-testable with no Godot binary.
    ///   </item>
    ///   <item>
    ///     Tools that drive the editor (<c>dialogic-timeline-create</c>, <c>-character-create</c>,
    ///     <c>-timeline-add-text</c>, <c>-get</c>, in <c>Editor/Tools/</c>) and the dynamic addon resolver
    ///     (<c>Runtime/Interop/AddonInterop.cs</c>) live behind <c>#if TOOLS</c> and marshal every Godot call
    ///     onto the editor main thread via <c>MainThread.Instance.Run(...)</c> — verified by the headless-Godot
    ///     E2E (the addon is referenced by string name only, so the constants are the contract).
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Dialogic
    {
    }
}
