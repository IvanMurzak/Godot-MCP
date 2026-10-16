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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// MCP tool family for the <b>Terrain3D Tools</b> extension (tool ids prefixed <c>terrain3d-*</c>) —
    /// AI tools for the community <a href="https://github.com/TokisanGames/Terrain3D">Terrain3D</a> addon
    /// (large-scale, editable clipmap terrain). This is a <b>CLASS-B</b> extension: Terrain3D is a precompiled
    /// C++ <b>GDExtension</b>, so its classes (<c>Terrain3D</c>, <c>Terrain3DData</c>, <c>Terrain3DMaterial</c>)
    /// are NOT in the consumer's GodotSharp. The package therefore takes NO compile-time dependency on the
    /// addon — it resolves + drives those classes BY STRING NAME at runtime through Godot's <c>ClassDB</c>
    /// (see <c>Interop/AddonInterop.cs</c>) and <b>presence-gates</b> every editor tool (a missing addon
    /// returns a structured <c>Installed: false</c> result instead of crashing).
    ///
    /// <para>
    /// <b>Pure-managed vs editor-only.</b> Tools are split by the API they touch, exactly like the core addon:
    /// <list type="bullet">
    ///   <item>
    ///     Tools with NO Godot native API (<c>terrain3d-defaults</c>, in <c>Runtime/Tools/</c>) stay OUTSIDE
    ///     <c>#if TOOLS</c> so they compile in any consumer build AND are CI-unit-testable with no Godot binary.
    ///     The addon name/member/enum-int CONSTANTS (<c>Runtime/Terrain3D/Terrain3DNames.cs</c>) and the
    ///     <c>AddonGateResult</c> shape (<c>Runtime/Interop/AddonGate.cs</c>) are pure-managed + unit-tested —
    ///     they ARE the contract, since there are no compile-time addon types to lean on.
    ///   </item>
    ///   <item>
    ///     Tools that drive the editor / live scene (<c>terrain3d-create</c>, <c>-set-data-directory</c>,
    ///     <c>-set-region-size</c>, <c>-set-material</c>, <c>-get</c>, in <c>Editor/Tools/</c>) live behind
    ///     <c>#if TOOLS</c> and marshal every Godot call onto the editor main thread via
    ///     <c>MainThread.Instance.Run(...)</c> — verified by the headless-Godot E2E (with the real addon
    ///     installed), never unit-tested.
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Terrain3D
    {
    }
}
