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

        public static string Optionals(string name, string? note, NodeRef? nodeRef, int? count, int times = 1) =>
            name + "|" + (note ?? "null") + "|" + (nodeRef == null ? "null" : nodeRef.Path) + "|" + (count?.ToString() ?? "null") + "|" + times;
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
        private const string Optionals = "binding-optionals";

        private readonly McpToolManager _tools;
        private readonly ToolTextEscaping _savedEscaping;

        public RqlToolBindingTests()
        {
            _savedEscaping = RqlToolBinding.Instance.Escaping;
            var engine = new RqlEngine();
            engine.AddModules(new[] { typeof(GodotValuesModule) });
            RqlToolBinding.Instance.EngineProvider = () => engine;
            RqlToolBinding.Instance.MainThreadRunner = null;
            RqlToolBinding.Instance.Reset();
            var reflector = GodotReflectorFactory.CreateDefaultReflector();
            var tools = new ToolRunnerCollection(reflector, null)
            {
                [Echo] = RunTool.CreateFromStaticMethod(reflector, null, Echo, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Echo))!),
                [Items] = RunTool.CreateFromStaticMethod(reflector, null, Items, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!),
                [Target] = RunTool.CreateFromStaticMethod(reflector, null, Target, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Target))!),
                [Optionals] = RunTool.CreateFromStaticMethod(reflector, null, Optionals, typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Optionals))!),
            };
            _tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, tools) { ToolBinding = RqlToolBinding.Instance };
        }

        public void Dispose()
        {
            RqlToolBinding.Instance.Escaping = _savedEscaping;
            RqlToolBinding.Instance.Reset();
            RqlToolBinding.Instance.EngineProvider = null;
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
        public void OnlyNonNullableParametersWithoutDefaultsAreRequired()
        {
            // string name: required; string? note / NodeRef? nodeRef (NRT) and int? count (Nullable<T>): not required;
            // int times = 1: not required.
            var schema = Listed(Optionals).InputSchema;
            var required = schema.GetProperty("required").EnumerateArray().Select(e => e.GetString()).ToArray();
            Assert.Equal(new[] { "name" }, required);
            // No "null" in the schema types: nullability only drops the parameter from "required".
            var properties = schema.GetProperty("properties");
            Assert.Equal("string", properties.GetProperty("note").GetProperty("type").GetString());
            Assert.Equal("string", properties.GetProperty("nodeRef").GetProperty("type").GetString());
            Assert.Equal("integer", properties.GetProperty("count").GetProperty("type").GetString());
            Assert.DoesNotContain("\"null\"", schema.GetRawText());
        }

        [Theory]
        [InlineData("{\"name\": \"a\"}", "a|null|null|null|1")]
        [InlineData("{\"name\": \"a\", \"note\": null, \"nodeRef\": null, \"count\": null}", "a|null|null|null|1")]
        [InlineData("{\"name\": \"a\", \"note\": \"n\", \"nodeRef\": \"Level/Player\", \"count\": 2, \"times\": 3}", "a|n|Level/Player|2|3")]
        public void OmittedOrNullNullableArgumentsReachTheMethodAsNull(string json, string expected)
        {
            var (r, text) = Call(Optionals, json);
            Assert.Equal(ResponseStatus.Success, r.Status);
            Assert.Contains(expected, text);
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
        public void ApplyAlwaysInstallsTheBinding()
        {
            _tools.ToolBinding = null;
            Assert.Contains("$defs", Listed(Echo).InputSchema.GetRawText());
            RqlToolBinding.Apply(_tools, ToolTextEscaping.Ascii);
            Assert.Same(RqlToolBinding.Instance, _tools.ToolBinding);
            Assert.Equal(ToolTextEscaping.Ascii, RqlToolBinding.Instance.Escaping);
            Assert.DoesNotContain("$defs", Listed(Echo).InputSchema.GetRawText());
            RqlToolBinding.Instance.Escaping = ToolTextEscaping.Minimal;
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

        [Fact]
        public void StructuredResultsKeepTheirNonTextBlocks()
        {
            var method = typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!;
            var response = ResponseCallTool.Image(new byte[] { 1, 2, 3 }, McpPlugin.Common.Consts.MimeType.ImagePng, "ignored");
            response.StructuredContent = System.Text.Json.Nodes.JsonNode.Parse("{\"result\": {\"width\": 4}}")!;
            var shown = RqlToolBinding.Instance.Result(method, response)!;

            Assert.Equal(McpPlugin.Common.Consts.ContentType.Text, shown.Content[0].Type);
            Assert.Contains("width", shown.Content[0].Text);
            Assert.DoesNotContain(shown.Content, c => c.Text == "ignored");
            var image = Assert.Single(shown.Content, c => c.Type != McpPlugin.Common.Consts.ContentType.Text);
            Assert.Equal(McpPlugin.Common.Consts.MimeType.ImagePng, image.MimeType);
            Assert.Null(shown.StructuredContent);
        }

        [Fact]
        public void ResultsWithoutStructuredContentPassThroughUnchanged()
        {
            var method = typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!;
            var response = ResponseCallTool.Image(new byte[] { 1, 2, 3 }, McpPlugin.Common.Consts.MimeType.ImagePng, "caption");
            var before = response.Content.ToList();

            Assert.Null(RqlToolBinding.Instance.Result(method, response));
            Assert.Equal(before, response.Content);
            Assert.Contains(response.Content, c => c.MimeType == McpPlugin.Common.Consts.MimeType.ImagePng);
        }

        [Fact]
        public void ScriptExecuteResultShowsTheValueThenTheOutput()
        {
            var method = typeof(RqlBindingTestTools).GetMethod(nameof(RqlBindingTestTools.Items))!;
            string Show(com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult r) =>
                string.Join("\n", RqlToolBinding.Instance.Result(method, r)!.Content.Select(c => c.Text));

            Assert.Equal("System.Int32: 3", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = 3, TypeName = "System.Int32" }));
            Assert.Equal("System.Int32: 3\nlogs:\nhello\nwarning: careful",
                Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = 3, TypeName = "System.Int32", Logs = new List<string> { "hello", "warning: careful" } }));
            Assert.Equal("Success", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult()));
            Assert.Equal("System.String: null", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { TypeName = "System.String" }));
            Assert.Equal("System.String: abc", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = "abc", TypeName = "System.String" }));
            Assert.Equal("System.Boolean: true", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = true, TypeName = "System.Boolean" }));
            Assert.Equal("System.Double: 1.5", Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = 1.5, TypeName = "System.Double" }));
            // Anything else is presented by RQL, cut at the result's limit.
            var cut = Show(new com.IvanMurzak.Godot.MCP.Data.ScriptExecuteResult { Result = Enumerable.Range(0, 5000).ToList(), TypeName = "List<int>", MaxValueChars = 100 });
            Assert.StartsWith("List<int>: ", cut);
            Assert.Contains("[truncated: 100 of ", cut);
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
