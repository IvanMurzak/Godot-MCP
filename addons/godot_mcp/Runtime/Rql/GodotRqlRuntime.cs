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
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Modules;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.McpPlugin;
using Godot;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// RQL inside a running game (<c>GodotMcpRuntime</c>): the engine and the tool binding that make every in-game tool —
    /// the addon's and the developer's own <c>[AiTool]</c> methods — take RQL values as arguments and return RQL text, as
    /// the editor's tools do. The engine has Godot's value types (<see cref="GodotValuesModule"/>), object identity
    /// (<c>ref("Node3D#12")</c>), node paths and <c>res://</c> paths where an object is expected, and <c>godot.find</c> over
    /// the running scene tree; it has no editor sources and no writes (rql-query / rql-mutate are editor tools).
    /// </summary>
    public static class GodotRqlRuntime
    {
        /// <summary>
        /// Compiled member accessors (System.Linq.Expressions) where the runtime can JIT, plain reflection where it cannot
        /// (an ahead-of-time export, e.g. iOS with NativeAOT): one build of the addon serves every Godot export target.
        /// </summary>
        public static AccessorMode Accessors => RuntimeFeature.IsDynamicCodeSupported ? AccessorMode.Compiled : AccessorMode.Reflection;

        /// <summary>A new in-game engine. Every call must run on the main thread (the binding marshals its own calls).</summary>
        public static RqlEngine CreateEngine(AccessorMode? accessors = null)
        {
            var engine = new RqlEngine(accessors ?? Accessors)
            {
                RuntimeName = "godot",
                FindHint = "godot.find(\"…\")",
                TypeDisplayName = RqlHostNames.GodotClassName,
            };
            engine.ResolveStableRef = ResolveStableRef;
            foreach (var failed in engine.AddModules(new[] { typeof(GodotValuesModule) }).Where(r => !r.Ok))
                GodotMcpLog.Warning($"[Godot-MCP] RQL module skipped: {failed}");
            engine.AddAdapter(new Godot_RuntimeObject_Adapter());
            engine.AddFunction("godot.find", FindNode,
                "godot.find(name): the node with that name or path below the current scene (\"Level/Player\"), or an absolute path (\"/root/Main\")");
            return engine;
        }

        /// <summary>
        /// A tool binding over <paramref name="engine"/> whose calls run on the game's main thread (ReflectorNet's
        /// <c>MainThread</c>, which <c>GodotMcpRuntime</c> routes through its dispatcher).
        /// </summary>
        public static RqlToolBinding CreateBinding(RqlEngine engine) =>
            new RqlToolBinding(() => engine, f => com.IvanMurzak.ReflectorNet.Utils.MainThread.Instance.Run(f));

        /// <summary>Install a fresh binding as the data layer of every tool on <paramref name="tools"/>; returns it.</summary>
        public static RqlToolBinding Install(IToolManager? tools, ToolTextEscaping escaping)
        {
            var binding = CreateBinding(CreateEngine());
            binding.Install(tools, escaping);
            return binding;
        }

        /// <summary><c>Type#id</c> where id is the Godot instance id; the engine checks the type.</summary>
        internal static object? ResolveStableRef(string type, string id)
        {
            if (!ulong.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var instanceId)) return null;
            if (!GodotObject.IsInstanceIdValid(instanceId)) return null;
            return GodotObject.InstanceFromId(instanceId);
        }

        private static object? FindNode(object?[] args)
        {
            if (args.Length != 1 || !(args[0] is string key) || key.Length == 0)
                return new RqlError("type-mismatch", "godot.find(name) expects a node name or a path such as \"Level/Player\"");
            return TryFindNode(key, out var node, out var reason) ? node : new RqlError("not-found", reason!);
        }

        /// <summary>
        /// The node <paramref name="key"/> names: an absolute path (<c>/root/Main/Player</c>), a path below the current scene
        /// (<c>Level/Player</c>), or the name of a node anywhere below it (the first one, depth-first).
        /// </summary>
        internal static bool TryFindNode(string key, out Node? node, out string? reason)
        {
            node = null;
            reason = null;
            if (!(global::Godot.Engine.GetMainLoop() is SceneTree tree))
            {
                reason = "there is no scene tree";
                return false;
            }
            Node scene = tree.CurrentScene ?? tree.Root;
            node = key.StartsWith("/", StringComparison.Ordinal) ? tree.Root.GetNodeOrNull(key) : scene.GetNodeOrNull(key);
            if (node == null && key.IndexOf('/') < 0)
                node = scene.FindChild(key, recursive: true, owned: false);
            if (node == null) reason = $"no node '{key}' in the running scene ({scene.Name})";
            return node != null;
        }

        /// <summary>The resource at a res:// or uid:// path (loaded with <see cref="ResourceLoader"/>), or the reason.</summary>
        internal static bool TryLoadResource(string path, out Resource? resource, out string? reason)
        {
            reason = null;
            resource = ResourceLoader.Exists(path) ? ResourceLoader.Load(path) : null;
            if (resource == null) reason = $"no resource at '{path}'";
            return resource != null;
        }
    }
}
