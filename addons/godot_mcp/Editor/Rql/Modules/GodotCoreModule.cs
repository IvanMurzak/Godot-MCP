/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Modules
{
    /// <summary>
    /// The Godot core: sources, functions, the adapters for Godot's core types and the type blacklist. Extensions contribute
    /// their own <see cref="IRqlModule"/> (adapters, sources, functions, <c>ctx.AddGuide</c>) from the consumer project
    /// assembly; <see cref="RqlGodot"/> discovers every module in the loaded assemblies.
    /// </summary>
    public sealed class GodotCoreModule : IRqlModule
    {
        public int Order => 0;

        public void Configure(RqlModuleContext ctx)
        {
            ctx.AddSource("nodes", RqlGodot.AllNodes, "every node of the edited scene, depth-first, root first (internal children excluded)")
               .AddSource("root", RqlGodot.Roots, "the root node of the edited scene")
               .AddSource("selection", RqlGodot.SelectedNodes, "nodes selected in the editor")
               .AddSource("scenes", RqlEditorApi.OpenScenes, "scenes open in the editor: path name active root; calls Open, Save")
               .AddSource("files", RqlEditorApi.Files, $"every file the editor filesystem knows (at most {RqlGodot.MaxFiles}): path name dir ext type uid")
               .AddSource("classes", RqlEditorApi.Classes, "Godot classes (ClassDB): name parent canInstantiate isNode isResource")
               .AddSource("console", RqlEditorApi.ConsoleEntries, $"captured editor log lines, oldest first (last {RqlEditorApi.MaxLogEntries}): Sequence LogType Message Timestamp StackTrace")
               .AddSource("editor", () => RqlEditorApi.Instance,
                   "editor state (isPlaying, playingScene, editedScene, selection, godotVersion) and operations: NewScene, OpenScene, SaveScene, " +
                   "SaveAllScenes, PlayScene, StopScene, CreateResource, MoveResource, DeleteResource, CreateFolder, Reimport, ClearConsole " +
                   "(rql-schema \"editor\" for signatures)")
               .AddFunction("godot.find", RqlGodot.FindNode,
                   "godot.find(name): the node with that unique name or path below the scene root (\"Level/Player\"), e.g. call Reparent(godot.find(\"Props\"))")
               .AddFunction("godot.resources", RqlGodot.FindResources,
                   $"godot.resources(type, folder?): resources of a Godot class (subclasses included) below an optional res:// folder, e.g. \"Texture2D\" (at most {RqlGodot.MaxResources}, loaded as needed)")
               .AddFunction("godot.resource", RqlGodot.LoadResource, "godot.resource(path): the resource at a res:// or uid:// path, or null")
               .AddFunction("godot.group", RqlGodot.FindGroup, "godot.group(name): the nodes of the edited scene in a group, e.g. godot.group(\"enemies\") | set Visible = false")
               .AddSource("project", () => Settings.RqlProject.Instance, Godot_ProjectSettings_Adapter.Summary)
               .AddSource("settings", Settings.RqlProject.Settings,
                   "every project setting: name section value type changed (write with project | set settings[\"<name>\"] = …)")
               .AddSource("inputActions", Settings.RqlProject.InputActions,
                   "the input map: name events deadzone builtIn (events as \"Key:W\", \"Mouse:Left\", \"JoypadButton:A\", \"JoypadMotion:LeftX-\"); calls AddEvent, RemoveEvent, Remove")
               .AddSource("autoloads", Settings.RqlProject.Autoloads, "autoloads (singletons) in load order: name path singleton order; call Remove")
               .AddSource("groups", Settings.RqlProject.Groups, "node groups of the edited scene and the project's global groups: name nodes count global description")
               .AddSource("layers", Settings.RqlLayers.All,
                   "the project's layers (Project Settings → Layer Names): kind index bit name setting; set name renames one (objects read and write their masks by these names in layerNames[\"…\"])")
               .AddFunction("godot.callable", Godot_Callable_Adapter.Function,
                   "godot.callable(target, method): a Callable of a method on a node (path or node), a resource (res://) or Class#id, e.g. godot.callable(\"Player\", \"_on_hit\")");

            // Never expose native handles (garbage or a crash when read) nor plumbing members by type.
            ctx.ExcludeTypes(t => t == typeof(IntPtr) || t == typeof(UIntPtr) || t.IsPointer || t == typeof(System.Threading.CancellationToken)
                                  || t == typeof(global::Godot.Signal));

            // Godot objects
            ctx.AddAdapter(new Godot_GodotObject_Adapter())
               .AddAdapter(new Godot_ArgumentList_Adapter())
               .AddAdapter(new Godot_Node_Adapter())
               .AddAdapter(new Godot_Node2D_Adapter())
               .AddAdapter(new Godot_Node3D_Adapter())
               .AddAdapter(new Godot_Control_Adapter())
               .AddAdapter(new Godot_Resource_Adapter())
               .AddAdapter(new Godot_ProjectSettings_Adapter())
               .AddAdapter(new Godot_TileSet_Adapter())
               .AddAdapter(new Godot_TileSetAtlasSource_Adapter())
               .AddAdapter(new Godot_Theme_Adapter())
               .AddAdapter(new Godot_Shader_Adapter())
               .AddAdapter(new Godot_ShaderMaterial_Adapter())
               .AddAdapter(new Godot_MeshLibrary_Adapter());

            // Values
            ctx.AddAdapter(new Godot_Variant_Adapter())
               .AddAdapter(new Godot_StringName_Adapter())
               .AddAdapter(new Godot_NodePath_Adapter())
               .AddAdapter(new Godot_Callable_Adapter())
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
        }
    }
}
#endif
