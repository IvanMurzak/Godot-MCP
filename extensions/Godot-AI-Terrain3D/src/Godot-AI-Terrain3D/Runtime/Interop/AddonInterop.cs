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

namespace com.IvanMurzak.Godot.MCP.Terrain3D
{
    /// <summary>
    /// CLASS-B dynamic wrapper for the <b>Terrain3D GDExtension</b>. Terrain3D's classes are NOT in GodotSharp,
    /// so the package resolves + instantiates them BY STRING NAME through Godot's <c>ClassDB</c> (the
    /// GDExtension path — GDExtension classes register in <c>ClassDB</c>, unlike GDScript <c>class_name</c>
    /// addons which use the global class list).
    ///
    /// <para>
    /// This helper lives ENTIRELY behind <c>#if TOOLS</c> and is <b>E2E-verified, NOT unit-tested</b>: every
    /// method touches a Godot static facade (<c>ClassDB</c>) and/or constructs a <c>Node</c>/<c>Resource</c>
    /// (which P/Invokes and would throw <c>AccessViolationException</c> in a no-Godot xUnit host). Only the
    /// pure-managed name/enum constants (<see cref="Terrain3DNames"/>) and the <see cref="AddonGateResult"/>
    /// shape are unit-tested — they ARE the contract this editor-only plumbing consumes.
    /// </para>
    /// </summary>
    public static class AddonInterop
    {
        /// <summary>True when the named GDExtension class is registered in <c>ClassDB</c> (the presence check).</summary>
        public static bool NativeClassExists(string className) => ClassDB.ClassExists(className);

        /// <summary>
        /// Instantiate a GDExtension <c>Node</c> class by name (e.g. <c>"Terrain3D"</c>), or null when the class
        /// is absent / not instantiable. The caller has already run the presence gate.
        /// </summary>
        public static Node? InstantiateNativeNode(string className) =>
            ClassDB.ClassExists(className) && ClassDB.CanInstantiate(className)
                ? ClassDB.Instantiate(className).As<Node>()
                : null;

        /// <summary>
        /// Instantiate a GDExtension object class by name (e.g. a <c>"Terrain3DMaterial"</c> resource), or null
        /// when the class is absent / not instantiable.
        /// </summary>
        public static GodotObject? InstantiateNative(string className) =>
            ClassDB.ClassExists(className) && ClassDB.CanInstantiate(className)
                ? ClassDB.Instantiate(className).As<GodotObject>()
                : null;
    }
}
#endif
