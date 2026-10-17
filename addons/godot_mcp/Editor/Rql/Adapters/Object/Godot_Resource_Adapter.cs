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
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// Resource: <c>path</c> (its res:// file, empty for a built-in resource) and <c>uid</c>; <c>Save()</c> writes it to its
    /// file. rql-mutate saves a changed file resource by itself; a <c>res://</c> path works wherever a resource is expected
    /// (the GodotObject adapter's coercion).
    /// </summary>
    public sealed class Godot_Resource_Adapter : RqlTypeAdapter<Resource>
    {
        public override string Description =>
            "Resource: path (res://…), uid, props[\"…\"]; changes to a resource file are saved by rql-mutate; write as \"res://…\" or ref(\"…#id\")";

        protected override void Members(MemberTable<Resource> m)
        {
            m.Add<string>("path", r => r.ResourcePath, description: "res:// path of its file (\"…tscn::…\" or empty for a built-in resource)")
             .Add<string?>("uid", r => Uid(r), description: "uid:// of its file, or null");
            m.ReadOnly("ResourcePath");
            m.Display("path");
            m.Untyped.AddMethod("Save", Array.Empty<CallableParameter>(), typeof(string), (o, a) => Save((Resource)o),
                "write this resource to its res:// file; returns the path");
        }

        private static string? Uid(Resource r) => RqlGodot.IsFileResource(r) ? RqlGodot.UidOf(r.ResourcePath) : null;

        private static string Save(Resource r)
        {
            if (!RqlGodot.IsFileResource(r)) throw new InvalidOperationException("this resource has no file of its own (it is built into a scene or another resource)");
            if (RqlGodot.SaveFile(r) is { } problem) throw new InvalidOperationException(problem);
            return r.ResourcePath;
        }
    }
}
#endif
