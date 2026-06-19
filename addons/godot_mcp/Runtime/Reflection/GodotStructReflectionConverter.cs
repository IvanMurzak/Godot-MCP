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
using com.IvanMurzak.ReflectorNet.Converter;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Base ReflectorNet converter for Godot value-type structs (Vector2/Vector3/Color, etc).
    /// The Godot analog of Unity-MCP's <c>UnityStructReflectionConverter&lt;T&gt;</c>.
    ///
    /// Godot value types expose their components as plain public instance fields (e.g.
    /// <c>Vector3.X/Y/Z</c>, <c>Color.R/G/B/A</c>), so the inherited reflection-based
    /// serialize/deserialize round-trips them member-for-member with no per-type code.
    /// <see cref="GenericReflectionConverter{T}.AllowCascadeSerialization"/> is turned off
    /// because these are flat leaf values — there is no nested object graph to descend into.
    /// </summary>
    public class GodotStructReflectionConverter<T> : GenericReflectionConverter<T>
    {
        public override bool AllowCascadeSerialization => false;
    }
}
