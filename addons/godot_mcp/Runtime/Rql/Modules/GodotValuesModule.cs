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
using System;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Modules
{
    /// <summary>
    /// Godot's value types in RQL terms — the structs (Vector3, Color, Transform3D …), Variant, Godot arrays, StringName,
    /// NodePath, Rid — plus the parameter types of the addon's tools (NodeRef, csharp-exec parameters) and the type blacklist.
    /// No editor API: the editor engine (<see cref="RqlGodot"/>, which finds it like any module) and the in-game runtime
    /// engine (<see cref="GodotRqlRuntime"/>) share it, so a tool argument reads the same in the editor and in a game.
    /// </summary>
    public sealed class GodotValuesModule : IRqlModule
    {
        public int Order => 0;

        public void Configure(RqlModuleContext ctx)
        {
            // Never expose native handles (garbage or a crash when read) nor plumbing members by type.
            ctx.ExcludeTypes(t => t == typeof(IntPtr) || t == typeof(UIntPtr) || t.IsPointer || t == typeof(System.Threading.CancellationToken)
                                  || t == typeof(global::Godot.Signal));

            // Values
            ctx.AddAdapter(new Godot_Variant_Adapter())
               .AddAdapter(new Godot_Array_Adapter())
               .AddAdapter(new Godot_ObjectList_Adapter<global::Godot.Resource>())
               .AddAdapter(new Godot_ObjectList_Adapter<global::Godot.Node>())
               .AddAdapter(new Godot_StringName_Adapter())
               .AddAdapter(new Godot_NodePath_Adapter())
               .AddAdapter(new Godot_Rid_Adapter());

            // Structs
            ctx.AddAdapter(new Godot_Vector2_Adapter())
               .AddAdapter(new Godot_Vector2I_Adapter())
               .AddAdapter(new Godot_Vector3_Adapter())
               .AddAdapter(new Godot_Vector3I_Adapter())
               .AddAdapter(new Godot_Vector4_Adapter())
               .AddAdapter(new Godot_Vector4I_Adapter())
               .AddAdapter(new Godot_Color_Adapter())
               .AddAdapter(new Godot_Quaternion_Adapter())
               .AddAdapter(new Godot_Basis_Adapter())
               .AddAdapter(new Godot_Transform2D_Adapter())
               .AddAdapter(new Godot_Transform3D_Adapter())
               .AddAdapter(new Godot_Rect2_Adapter())
               .AddAdapter(new Godot_Rect2I_Adapter())
               .AddAdapter(new Godot_Aabb_Adapter())
               .AddAdapter(new Godot_Plane_Adapter())
               .AddAdapter(new Godot_Projection_Adapter());

            // Tool parameter types
            ctx.AddAdapter(new Godot_NodeRef_Adapter())
               .AddAdapter(new ReflectorNet_SerializedMemberList_Adapter());
        }
    }
}
