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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Editor.Rql.Modules;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public enum RqlBindingTestMode { Fast, Slow }

    public class RqlBindingTestItem
    {
        public string? name;
        public int count;
    }

    public class RqlBindingTestSettings
    {
        public string? title;
        public RqlBindingTestMode mode;
        public List<RqlBindingTestItem>? items;
    }

    public static class RqlBindingTestTools
    {
        public static RqlBindingTestSettings Echo(RqlBindingTestSettings settings, int times = 1, bool loud = false)
        {
            if (loud) settings.title = settings.title?.ToUpperInvariant();
            if (settings.items != null) foreach (var item in settings.items) item.count *= times;
            return settings;
        }

        public static List<RqlBindingTestItem> Items(string name) => new List<RqlBindingTestItem>
        {
            new RqlBindingTestItem { name = name, count = 1 },
            new RqlBindingTestItem { name = "b", count = 2 },
        };

        public static string Target(NodeRef nodeRef) => nodeRef.InstanceId != 0 ? "id:" + nodeRef.InstanceId : "path:" + nodeRef.Path;

        public static string Configure(ClashAlpha.Settings a, ClashBeta.Settings b) => "ok";
    }

    /// <summary>
    /// RqlToolBinding over a plain RqlEngine (the editor's engine is native-backed): RQL as the tools' data layer —
    /// schemas from RQL's type model, arguments as RQL values, results as compact text — and the config switch to
    /// ReflectorNet's JSON. Serial: the binding is a process-wide singleton.
    /// </summary>
    [Collection(RqlToolBindingCollection.Name)]
    public sealed class RqlToolBindingTests : IDisposable
    {
        private const string Echo = "binding-echo";
        private const string Items = "binding-items";
        private const string Target = "binding-target";

        private readonly McpToolManager _tools;
        private readonly ToolTextEscaping _savedEscaping;

        public RqlToolBindingTests()
        {
            _savedEscaping = RqlToolBinding.Instance.Escaping;
            var engine = new RqlEngine();
            engine.AddModules(new[] { typeof(ToolTypesModule) });
            RqlToolBinding.EngineProvider = () => engine;
            RqlToolBinding.MainThreadRunner = null;
            RqlToolBinding.Instance.Reset();
            var reflector = GodotReflectorFactory.CreateDefaultReflector();
            var tools = new ToolRunnerCollection(reflector, null)
            {
                [Echo] = RunTool.CreateFromStaticMethod(reflector, null, Echo, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Echo))!),
                [Items] = RunTool.CreateFromStaticMethod(reflector, null, Items, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!),
                [Target] = RunTool.CreateFromStaticMethod(reflector, null, Target, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Target))!),
            };
            _tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, tools) { ToolBinding = RqlToolBinding.Instance };
        }

        public void Dispose()
        {
            RqlToolBinding.Instance.Escaping = _savedEscaping;
            RqlToolBinding.Instance.Reset();
            RqlToolBinding.EngineProvider = null;
        }

        private ResponseListTool Listed(string name) => _tools.RunListTool(new RequestListTool()).Result.Value!.Single(t => t.Name == name);

        private (ResponseCallTool result, string text) Call(string name, string json)
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var r = _tools.RunCallTool(new RequestCallTool(name, args)).Result.Value!;
            return (r, string.Join("\n", r.Content.Select(c => c.Text)));
        }

        [Fact]
        public void RecordParametersAreRqlTextDescribedOnce()
        {
            var listed = Listed(Echo);
            var schema = listed.InputSchema.GetRawText();
            Assert.DoesNotContain("$defs", schema);
            var settings = listed.InputSchema.GetProperty("properties").GetProperty("settings");
            Assert.Equal("string", settings.GetProperty("type").GetString());
            Assert.Contains("RQL: RqlBindingTestSettings", settings.GetProperty("description").GetString());
            Assert.Equal("integer", listed.InputSchema.GetProperty("properties").GetProperty("times").GetProperty("type").GetString());
            Assert.Contains("RqlBindingTestSettings {title string, mode Fast|Slow, items [RqlBindingTestItem]", listed.Description);
        }

        [Fact]
        public void ArgumentsAreRqlLiteralsAndResultsAreRqlOutput()
        {
            var (r, text) = Call(Echo, "{\"settings\": \"{title: \\\"Hi\\\", mode: Slow, items: [{name: a, count: 2}]}\", \"times\": 3}");
            Assert.Equal(ResponseStatus.Success, r.Status);
            Assert.Contains("Hi", text);
            Assert.Contains("Slow", text);
            Assert.Contains("6", text);
            Assert.DoesNotContain("\"title\"", text);
        }

        [Fact]
        public void ABadArgumentNamesTheParameterAndTheExpectedType()
        {
            var (r, text) = Call(Echo, "{\"settings\": \"{title: }\"}");
            Assert.Equal(ResponseStatus.Error, r.Status);
            Assert.Contains("'settings'", text);
            Assert.Contains("RqlBindingTestSettings", text);
        }

        [Theory]
        [InlineData("\"Level/Player\"", "path:Level/Player")]
        [InlineData("\"\\\"Player\\\"\"", "path:Player")]
        [InlineData("\"1234\"", "id:1234")]
        [InlineData("1234", "id:1234")]
        public void NodeRefArgumentsTakePathsNamesAndInstanceIds(string value, string expected)
        {
            var listed = Listed(Target);
            Assert.Contains("RQL: NodeRef", listed.InputSchema.GetRawText());
            Assert.Contains("NodeRef — a node: ref(\"Node3D#12\")", listed.Description);
            var (r, text) = Call(Target, "{\"nodeRef\": " + value + "}");
            Assert.Equal(ResponseStatus.Success, r.Status);
            Assert.Contains(expected, text);
        }

        [Fact]
        public void ConfigSelectsTheBinding()
        {
            RqlToolBinding.Apply(_tools, ToolPresentationFormat.Json, ToolTextEscaping.Ascii);
            Assert.Null(_tools.ToolBinding);
            Assert.Equal(ToolTextEscaping.Ascii, RqlToolBinding.Instance.Escaping);
            Assert.Contains("$defs", Listed(Echo).InputSchema.GetRawText());
            RqlToolBinding.Apply(_tools, ToolPresentationFormat.Rql, ToolTextEscaping.Minimal);
            Assert.Same(RqlToolBinding.Instance, _tools.ToolBinding);
            Assert.DoesNotContain("$defs", Listed(Echo).InputSchema.GetRawText());
        }

        [Theory]
        [InlineData(ToolTextEscaping.Minimal, "\"é+<|x\"")]
        [InlineData(ToolTextEscaping.Ascii, "\"\\u00e9+<|x\"")]
        [InlineData(ToolTextEscaping.Json, "\"\\u00E9\\u002B\\u003C|x\"")]
        public void QuotedResultTextFollowsTheConfiguredEscaping(ToolTextEscaping escaping, string expected)
        {
            RqlToolBinding.Instance.Escaping = escaping;
            var (r, text) = Call(Items, "{\"name\": \"é+<|x\"}");
            Assert.Equal(ResponseStatus.Success, r.Status);
            Assert.StartsWith("2 results\nname|count", text);
            Assert.Contains(expected + "|1", text);
        }

        [Fact]
        public void ClashingShortNamesGetDistinctNamesInTheTypeBlock()
        {
            var method = typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Configure))!;
            var signature = RqlToolBinding.Instance.Describe("clash", method, null)!;
            Assert.Contains("ClashAlpha.Settings {speed float}", signature.Description);
            Assert.Contains("ClashBeta.Settings {color string}", signature.Description);
            var properties = signature.InputSchema["properties"]!;
            Assert.Contains("RQL: ClashAlpha.Settings", properties["a"]!["description"]!.GetValue<string>());
            Assert.Contains("RQL: ClashBeta.Settings", properties["b"]!["description"]!.GetValue<string>());
        }

        [Fact]
        public void StructuredJsonResultsBecomeCompactText()
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse("{\"result\": [{\"name\": \"a\", \"count\": 1}, {\"name\": \"b\", \"count\": 2}]}")!;
            var response = ResponseCallTool.Success("ignored");
            response.StructuredContent = json;
            var shown = RqlToolBinding.Instance.Result(typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!, response)!;
            var text = string.Join("\n", shown.Content.Select(c => c.Text));
            Assert.Equal("name | count\na | 1\nb | 2", text);
            Assert.Null(shown.StructuredContent);
        }
    }

    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class RqlToolBindingCollection
    {
        public const string Name = "RqlToolBinding (process-wide singleton)";
    }
}

namespace com.IvanMurzak.Godot.MCP.Tests.ClashAlpha
{
    public class Settings { public float speed; }
}

namespace com.IvanMurzak.Godot.MCP.Tests.ClashBeta
{
    public class Settings { public string? color; }
}
