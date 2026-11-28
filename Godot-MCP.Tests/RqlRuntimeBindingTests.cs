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
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet;
using Godot;
using Microsoft.Extensions.Logging.Abstractions;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>A developer's own value type for an in-game tool: Godot structs, a node reference, a live node.</summary>
    public class RuntimeSpawnSpec
    {
        public string? Name;
        public Vector3 Position;
        public Color Tint = Colors.White;
        public NodeRef? Target;
        public Node? Anchor;
        public List<Vector2>? Path;
    }

    public static class RuntimeBindingTestTools
    {
        public static RuntimeSpawnSpec Spawn(RuntimeSpawnSpec spec, int count = 1)
        {
            spec.Position *= count;
            return spec;
        }

        public static string Describe(RuntimeSpawnSpec spec) => string.Format(CultureInfo.InvariantCulture,
            "{0} at {1} tint {2} target {3} path {4}", spec.Name, spec.Position, spec.Tint.ToHtml(false),
            spec.Target == null ? "none" : spec.Target.InstanceId != 0 ? "id:" + spec.Target.InstanceId : "path:" + spec.Target.Path,
            spec.Path == null ? "none" : string.Join(";", spec.Path));
    }

    /// <summary>
    /// The in-game data layer (GodotRqlRuntime): a game's own tool with a custom class of Godot values (Vector3, Color,
    /// NodeRef, a Node member, a list of Vector2) gets an RQL schema, takes an RQL literal and returns RQL text — with
    /// compiled accessors (JIT) and with reflection ones (an AOT export). Engine-free: no node is created (a live Node
    /// needs the Godot runtime; ref("Node3D#12") resolution is covered by the runtime harness).
    /// </summary>
    public sealed class RqlRuntimeBindingTests
    {
        private const string Spawn = "game-spawn";
        private const string Describe = "game-describe";

        private static (McpToolManager tools, RqlToolBinding binding) Tools(AccessorMode accessors)
        {
            var reflector = new Reflector();
            var runners = new ToolRunnerCollection(reflector, null)
            {
                [Spawn] = RunTool.CreateFromStaticMethod(reflector, null, Spawn, typeof(RuntimeBindingTestTools).GetMethod(nameof(RuntimeBindingTestTools.Spawn))!),
                [Describe] = RunTool.CreateFromStaticMethod(reflector, null, Describe, typeof(RuntimeBindingTestTools).GetMethod(nameof(RuntimeBindingTestTools.Describe))!),
            };
            var tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, runners);
            var engine = GodotRqlRuntime.CreateEngine(accessors);
            // Inline main thread: the test thread plays it (the game passes its dispatcher).
            var binding = new RqlToolBinding(() => engine, mainThread: null);
            binding.Install(tools, ToolTextEscaping.Minimal);
            return (tools, binding);
        }

        private static (ResponseCallTool result, string text) Call(McpToolManager tools, string name, string json)
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var r = tools.RunCallTool(new RequestCallTool(name, args)).Result.Value!;
            return (r, string.Join("\n", r.Content.Select(c => c.Text)));
        }

        [Theory]
        [InlineData(AccessorMode.Compiled)]
        [InlineData(AccessorMode.Reflection)]
        public void TheBindingIsInstalledOnTheGameTools(AccessorMode accessors)
        {
            var (tools, binding) = Tools(accessors);
            Assert.Same(binding, tools.ToolBinding);
            var listed = tools.RunListTool(new RequestListTool()).Result.Value!.Single(t => t.Name == Spawn);
            var schema = listed.InputSchema.GetRawText();
            Assert.DoesNotContain("$defs", schema);
            Assert.Contains("RQL: RuntimeSpawnSpec", schema);
            // The custom class is described once, from RQL's type model, with its Godot member types.
            Assert.True(listed.Description!.Contains("RuntimeSpawnSpec {"), listed.Description);
            Assert.Contains("Position Vector3", listed.Description);
            Assert.Contains("Target NodeRef", listed.Description);
            Assert.Contains("Anchor Node", listed.Description);
            Assert.Contains("Node — Godot object: id, class; give one as ref(", listed.Description);
        }

        [Theory]
        [InlineData(AccessorMode.Compiled)]
        [InlineData(AccessorMode.Reflection)]
        public void ACustomClassArgumentIsAnRqlLiteral(AccessorMode accessors)
        {
            var (tools, _) = Tools(accessors);
            var (r, text) = Call(tools, Describe,
                "{\"spec\": \"{Name: \\\"Orc\\\", Position: [1, 2, 3], Tint: \\\"#ff0000\\\", Target: \\\"Level/Player\\\", Path: [[0, 0], \\\"(4, 5)\\\"]}\"}");
            Assert.True(r.Status == ResponseStatus.Success, text);
            Assert.Equal("Orc at (1, 2, 3) tint ff0000 target path:Level/Player path (0, 0);(4, 5)", text);
            // An instance id names the node of a NodeRef member too.
            (r, text) = Call(tools, Describe, "{\"spec\": \"{Name: \\\"Orc\\\", Target: 1234}\"}");
            Assert.True(r.Status == ResponseStatus.Success, text);
            Assert.Contains("target id:1234", text);
        }

        [Theory]
        [InlineData(AccessorMode.Compiled)]
        [InlineData(AccessorMode.Reflection)]
        public void ACustomClassResultIsRqlText(AccessorMode accessors)
        {
            var (tools, _) = Tools(accessors);
            var (r, text) = Call(tools, Spawn, "{\"spec\": \"{Name: \\\"Orc\\\", Position: {x: 1, y: 2, z: 3}}\", \"count\": 2}");
            Assert.True(r.Status == ResponseStatus.Success, text);
            Assert.StartsWith("{Name:Orc,", text);
            Assert.Contains("Position:\"(2, 4, 6)\"", text);
            Assert.DoesNotContain("\"x\"", text); // not ReflectorNet's JSON
        }

        [Theory]
        [InlineData("Position: [2, 0, 5]", "(2, 0, 5)")]
        [InlineData("Position: \\\"(2, 0, 5)\\\"", "(2, 0, 5)")]
        [InlineData("Position: {x: 2, y: 0, z: 5}", "(2, 0, 5)")]
        [InlineData("Tint: [1, 0, 0]", "tint ff0000")]
        [InlineData("Tint: \\\"#00ff00\\\"", "tint 00ff00")]
        [InlineData("Tint: \\\"(0, 0, 1, 1)\\\"", "tint 0000ff")]
        public void EveryFormTheTypeTextTeachesParsesInALiteral(string member, string expected)
        {
            var (tools, _) = Tools(AccessorMode.Reflection);
            var listed = tools.RunListTool(new RequestListTool()).Result.Value!.Single(t => t.Name == Describe);
            // The schema teaches the list form first and the tuple only as quoted text (a bare (2, 0, 5) is no RQL value).
            Assert.Contains("Vector3: write as [x, y, z] or \"(x, y, z)\" in quotes", listed.Description);
            var (r, text) = Call(tools, Describe, "{\"spec\": \"{Name: \\\"Orc\\\", " + member + "}\"}");
            Assert.True(r.Status == ResponseStatus.Success, text);
            Assert.Contains(expected, text);
        }

        [Fact]
        public void AWrongValueIsRefusedWithTheExpectedType()
        {
            var (tools, _) = Tools(AccessorMode.Reflection);
            var (r, text) = Call(tools, Describe, "{\"spec\": \"{Position: \\\"left\\\"}\"}");
            Assert.Equal(ResponseStatus.Error, r.Status);
            Assert.Contains("RuntimeSpawnSpec", text);
        }

        // The harness's live 'variant number' case: a whole RQL number becomes a Variant Int, as a GDScript literal 42 would.
        [Theory]
        [InlineData(42.0, 42L)]
        [InlineData(-3.0, -3L)]
        [InlineData(42.5, 42.5)]
        public void AWholeNumberForAVariantIsAnInt(double value, object expected) =>
            Assert.Equal(expected, com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters.Godot_Variant_Adapter.Input(value));

        [Fact]
        public void TextAndOtherValuesForAVariantAreUnchanged()
        {
            Assert.Equal("42", com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters.Godot_Variant_Adapter.Input("42"));
            Assert.Equal(true, com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters.Godot_Variant_Adapter.Input(true));
            Assert.Equal(1e20, com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters.Godot_Variant_Adapter.Input(1e20)); // past a long: stays a float
        }

        // The harness's live 'node result' case: an object with an identity (a Godot object in a game) returned by a tool
        // reads as its members, never as an empty "Success".
        [Theory]
        [InlineData(AccessorMode.Compiled)]
        [InlineData(AccessorMode.Reflection)]
        public void AnObjectResultWithAnIdentityIsPresented(AccessorMode accessors)
        {
            var engine = GodotRqlRuntime.CreateEngine(accessors);
            engine.AddAdapter(new IdentifiedAdapter());
            var binding = new RqlToolBinding(() => engine, mainThread: null);
            var method = typeof(RuntimeBindingTestTools).GetMethod(nameof(RuntimeBindingTestTools.Spawn))!;
            var text = string.Join("\n", binding.Result(method, new Identified())!.Content.Select(c => c.Text));
            Assert.Contains("class:Node3D", text);
        }

        public sealed class Identified { public string Label = "x"; }

        private sealed class IdentifiedAdapter : RqlTypeAdapter<Identified>
        {
            protected override string? Id(Identified o) => "12";
            protected override void Members(MemberTable<Identified> m) => m.Add<string>("class", o => "Node3D");
        }

        [Fact]
        public void TheAccessorModeFollowsDynamicCodeSupport() =>
            Assert.Equal(System.Runtime.CompilerServices.RuntimeFeature.IsDynamicCodeSupported ? AccessorMode.Compiled : AccessorMode.Reflection,
                GodotRqlRuntime.Accessors);
    }
}
