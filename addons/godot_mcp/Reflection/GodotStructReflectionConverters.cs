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

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    // Core Godot value types. Each round-trips via the inherited reflection serializer over the
    // type's public instance fields (Vector2.X/Y, Vector3.X/Y/Z, Color.R/G/B/A). Mirror of
    // Unity-MCP's UnityEngineDataStructReflectionConverters.cs registration shape.

    public class Godot_Vector2_ReflectionConverter : GodotStructReflectionConverter<global::Godot.Vector2> { }
    public class Godot_Vector3_ReflectionConverter : GodotStructReflectionConverter<global::Godot.Vector3> { }
    public class Godot_Color_ReflectionConverter : GodotStructReflectionConverter<global::Godot.Color> { }
}
