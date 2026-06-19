/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.ReflectorNet;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Builds a <see cref="Reflector"/> pre-populated with the Godot-specific type converters.
    /// The Godot analog of Unity-MCP's <c>UnityMcpPlugin.CreateDefaultReflector()</c>. Tool families
    /// serialize their args/results through the reflector returned here so core Godot value types and
    /// refs cross the MCP boundary cleanly.
    ///
    /// This is engine-runtime code (no editor API), so it lives outside <c>#if TOOLS</c> and is unit-
    /// testable in a plain xUnit project that references GodotSharp.
    /// </summary>
    public static class GodotReflectorFactory
    {
        /// <summary>
        /// Create a <see cref="Reflector"/> with the core Godot value-type and ref converters registered.
        /// </summary>
        public static Reflector CreateDefaultReflector()
        {
            var reflector = new Reflector();
            RegisterGodotConverters(reflector);
            return reflector;
        }

        /// <summary>
        /// Register the Godot converters onto an existing <see cref="Reflector"/>. Separated from
        /// <see cref="CreateDefaultReflector"/> so a caller that already owns a configured reflector
        /// (e.g. one built by the McpPlugin client) can add Godot support without discarding it.
        /// </summary>
        public static void RegisterGodotConverters(Reflector reflector)
        {
            // Reflection converters — core Godot value types (round-trip via public instance fields).
            reflector.Converters.Add(new Godot_Vector2_ReflectionConverter());
            reflector.Converters.Add(new Godot_Vector3_ReflectionConverter());
            reflector.Converters.Add(new Godot_Color_ReflectionConverter());

            // Resource reference converter — matches Godot.Resource and EVERY Resource-derived type
            // (Mesh/Material/Texture2D/…) by inheritance distance, so a Resource-typed member is assigned
            // by reference (res:// path / instance id) instead of falling back to instantiate-and-populate
            // (which fails: "Instance creation failed for Godot.BoxMesh"). The live ResourceLoader.Load
            // resolution is injected under #if TOOLS; see Godot_Resource_ReflectionConverter.ResourceResolver.
            reflector.Converters.Add(new Godot_Resource_ReflectionConverter());

            // JSON converters — types best described as a single scalar (NodePath as its string form).
            reflector.JsonSerializer.AddConverter(new GodotNodePathJsonConverter());
        }
    }
}
