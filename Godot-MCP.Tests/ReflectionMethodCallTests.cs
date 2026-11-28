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
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.McpPlugin.Common.Model;
using com.IvanMurzak.ReflectorNet;
using Godot;
using Microsoft.Extensions.Logging.Abstractions;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public class ReflectionCallTarget
    {
        public string Prefix = "p";

        public static Vector3 Scale(Vector3 at, int times = 2) => at * times;

        public static string Overload(int value) => "int:" + value;

        public static string Overload(string text, bool loud) => loud ? text.ToUpperInvariant() : text;

        public string Greet(string name) => Prefix + ":" + name;

        public static void Nothing() { }
    }

    /// <summary>
    /// The in-game csharp-exec and reflection-method-call value paths through the in-game RQL binding: parameters as RQL
    /// values (named, converted to the declared or the method's types), results presented by RQL; no ReflectorNet model in
    /// the signatures. Engine-free: Godot objects as targets / values need the runtime harness.
    /// </summary>
    public sealed class ReflectionMethodCallTests
    {
        public static ScriptParameters? Captured;

        public static string Capture(ScriptParameters parameters)
        {
            Captured = parameters;
            return "ok";
        }

        public static string Echo(ScriptParameters parameters) =>
            string.Join(";", parameters.Items.Select(p => $"{p.Name}:{ScriptResultText.TypeName(p.Type)}={System.Convert.ToString(p.Value, System.Globalization.CultureInfo.InvariantCulture)}"));

        private static McpToolManager Tools()
        {
            var reflector = new Reflector();
            var runners = new ToolRunnerCollection(reflector, null)
            {
                [Tool_Reflection.ReflectionMethodCallToolId] = RunTool.CreateFromClassMethod(reflector, null, Tool_Reflection.ReflectionMethodCallToolId,
                    typeof(Tool_Reflection), typeof(Tool_Reflection).GetMethod(nameof(Tool_Reflection.MethodCall))!),
                ["echo-parameters"] = RunTool.CreateFromStaticMethod(reflector, null, "echo-parameters", typeof(ReflectionMethodCallTests).GetMethod(nameof(Echo))!),
                ["capture-parameters"] = RunTool.CreateFromStaticMethod(reflector, null, "capture-parameters", typeof(ReflectionMethodCallTests).GetMethod(nameof(Capture))!),
            };
            var tools = new McpToolManager(NullLogger<McpToolManager>.Instance, reflector, runners);
            var engine = GodotRqlRuntime.CreateEngine(AccessorMode.Reflection);
            new RqlToolBinding(() => engine, mainThread: null).Install(tools, McpPlugin.ToolTextEscaping.Minimal);
            return tools;
        }

        private static (ResponseStatus status, string text) Call(McpToolManager tools, string name, string json)
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
            var r = tools.RunCallTool(new RequestCallTool(name, args)).Result.Value!;
            return (r.Status, string.Join("\n", r.Content.Select(c => c.Text)));
        }

        private const string Filter = "\"filter\": {\"namespace\": \"com.IvanMurzak.Godot.MCP.Tests\", \"typeName\": \"ReflectionCallTarget\", \"methodName\": \"{0}\"}, " +
            "\"knownNamespace\": true, \"typeNameMatchLevel\": 6, \"methodNameMatchLevel\": 6, \"executeInMainThread\": false";

        private static string Args(string method, string more = "") => "{" + Filter.Replace("{0}", method) + (more.Length > 0 ? ", " + more : "") + "}";

        [Fact]
        public void TheSchemaHasNoReflectorNetValueModel()
        {
            var listed = Tools().RunListTool(new RequestListTool()).Result.Value!.Single(t => t.Name == Tool_Reflection.ReflectionMethodCallToolId);
            Assert.DoesNotContain("SerializedMember", listed.InputSchema.GetRawText());
            Assert.Contains("RQL: RqlValues", listed.InputSchema.GetRawText());
            Assert.Contains("RQL: RqlValue", listed.InputSchema.GetRawText());
        }

        [Fact]
        public void ArgumentsAreRqlValuesConvertedToTheParameterTypes()
        {
            var (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Scale", "\"inputParameters\": \"{at: [1, 2, 3], times: 3}\""));
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("(3, 6, 9)", text);
            // A parameter left out takes its default.
            (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Scale", "\"inputParameters\": \"{at: \\\"(1, 1, 1)\\\"}\""));
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("(2, 2, 2)", text);
        }

        [Fact]
        public void TheArgumentNamesChooseTheOverload()
        {
            var tools = Tools();
            var (status, text) = Call(tools, Tool_Reflection.ReflectionMethodCallToolId, Args("Overload", "\"parametersMatchLevel\": 0, \"inputParameters\": \"{text: \\\"hi\\\", loud: true}\""));
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("HI", text);
            (status, text) = Call(tools, Tool_Reflection.ReflectionMethodCallToolId, Args("Overload", "\"parametersMatchLevel\": 0, \"inputParameters\": \"{value: 7}\""));
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("int:7", text);
        }

        [Fact]
        public void AnInstanceMethodRunsOnTheTargetLiteral()
        {
            var (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId,
                Args("Greet", "\"targetObject\": \"{Prefix: \\\"hey\\\"}\", \"inputParameters\": \"{name: \\\"Bob\\\"}\""));
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("hey:Bob", text);
        }

        [Fact]
        public void AValueThatDoesNotConvertIsRefused()
        {
            var (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Scale", "\"inputParameters\": \"{at: \\\"left\\\"}\""));
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("'at'", text);
            (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Scale", "\"inputParameters\": \"{at: [1, 2, 3], nope: 1}\""));
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("nope is not a parameter", text);
        }

        // net8 CI: another test had loaded a copy of this assembly into its own load context, so every method was found twice.
        [Fact]
        public void ACopyOfTheAssemblyInAnotherLoadContextIsOneMatch()
        {
            var context = new System.Runtime.Loader.AssemblyLoadContext("reflection-call-copy", isCollectible: true);
            try
            {
                using (var file = System.IO.File.OpenRead(typeof(ReflectionCallTarget).Assembly.Location))
                    context.LoadFromStream(file);
                com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearAssemblyTypeCache();
                var (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Scale", "\"inputParameters\": \"{at: [1, 2, 3]}\""));
                Assert.True(status == ResponseStatus.Success, text);
                Assert.Equal("(2, 4, 6)", text);
            }
            finally
            {
                context.Unload();
                com.IvanMurzak.ReflectorNet.Utils.TypeUtils.ClearAssemblyTypeCache();
            }
        }

        [Fact]
        public void AVoidMethodReportsSuccess()
        {
            var (status, text) = Call(Tools(), Tool_Reflection.ReflectionMethodCallToolId, Args("Nothing"));
            Assert.True(status == ResponseStatus.Success, text);
        }

        [Fact]
        public void CsharpExecParametersAreNamedRqlValues()
        {
            var tools = Tools();
            var (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"{n: 6, f: 1.5, s: \\\"x\\\", b: true, none: null, big: 5000000000}\"}");
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("n:int=6;f:double=1.5;s:string=x;b:bool=True;none:object=;big:long=5000000000", text);
            // A type before the name converts the value to it (Godot struct, C# alias, array).
            (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"{\\\"Vector3 at\\\": [1, 2, 3], \\\"float speed\\\": 2, \\\"int[] ids\\\": [1, 2]}\"}");
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("at:Vector3=(1, 2, 3);speed:float=2;ids:int[]=System.Int32[]", text);
            // The list form of earlier versions; a nameless entry is positional.
            (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"[{name: \\\"n\\\", typeName: \\\"System.Int32\\\", value: 6}, {typeName: \\\"System.String\\\", value: \\\"x\\\"}]\"}");
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("n:int=6;arg1:string=x", text);
        }

        // AFTER-eval: {"crate": "godot.find(\"Crate\")"} (the call written as text) passed and failed only when the method ran.
        [Theory]
        [InlineData("{\"parameters\": {\"crate\": \"godot.find(\\\"Crate\\\")\"}}")]
        [InlineData("{\"parameters\": \"{crate: \\\"ref(\\\\\\\"Node3D#12\\\\\\\")\\\"}\"}")]
        public void RqlCodeWrittenAsTextIsRefusedUpFront(string json)
        {
            var (status, text) = Call(Tools(), "echo-parameters", json);
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("RQL code written as text", text);
            Assert.Contains("without quotes", text);
        }

        [Fact]
        public void FullModeParametersAreConvertedToTheDeclaredTypesBeforeTheRun()
        {
            var tools = Tools();
            var (status, text) = Call(tools, "capture-parameters", "{\"parameters\": \"{speed: 2, at: \\\"(1, 2, 3)\\\", crate: \\\"left\\\"}\"}");
            Assert.True(status == ResponseStatus.Success, text);
            var given = Captured!;
            // A whole number for a float, text for a Vector3: converted to the method's types.
            var typed = ScriptExecutor.ToDeclaredTypes(new ScriptParameters(given.Items.Take(2).ToList()), Declared("float", "Vector3"));
            Assert.Equal(new object?[] { 2f, new Vector3(1, 2, 3) }, typed.Items.Select(p => p.Value));
            Assert.Equal(new[] { typeof(float), typeof(Vector3) }, typed.Items.Select(p => p.Type));
            // A value that does not convert is an error now, before the run (a Node needs Godot: the live smoke covers it).
            var e = Assert.Throws<System.ArgumentException>(() => ScriptExecutor.ToDeclaredTypes(
                new ScriptParameters(given.Items.Skip(2).ToList()), Declared("Vector3")));
            Assert.Contains("parameter 'crate' (Vector3 in Vector3)", e.Message);
        }

        private static IReadOnlyList<(string name, IReadOnlyList<string> namespaces)?> Declared(params string[] names) =>
            names.Select(n => ((string, IReadOnlyList<string>)?)(n, System.Array.Empty<string>())).ToList();

        [Fact]
        public void AnExplicitStringTypeKeepsTextThatLooksLikeACall()
        {
            var (status, text) = Call(Tools(), "echo-parameters", "{\"parameters\": {\"string label\": \"ref (page 4)\"}}");
            Assert.True(status == ResponseStatus.Success, text);
            Assert.Equal("label:string=ref (page 4)", text);
        }

        [Fact]
        public void CsharpExecParametersRefuseWhatTheyCannotType()
        {
            var tools = Tools();
            var (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"{at: [1, 2, 3]}\"}");
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("\"Vector3 at\": [1, 2, 3]", text);
            (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"{\\\"NoSuchType x\\\": 1}\"}");
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("type 'NoSuchType' not found", text);
            // ref() takes a Type#id from a query; a bare name is refused with the form that works (BEFORE-eval G1).
            (status, text) = Call(tools, "echo-parameters", "{\"parameters\": \"{node: ref(\\\"Crate\\\")}\"}");
            Assert.Equal(ResponseStatus.Error, status);
            Assert.Contains("godot.find(\"Crate\")", text);
            Assert.DoesNotContain("not supported", text);
        }
    }
}
