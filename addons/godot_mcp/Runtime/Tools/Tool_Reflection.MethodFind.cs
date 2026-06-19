/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.ComponentModel;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Reflection
    {
        public const string ReflectionMethodFindToolId = "reflection-method-find";

        [AiTool
        (
            ReflectionMethodFindToolId,
            Title = "Method C# / Find",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [Description("Find C# methods across every loaded assembly by name / declaring-type / parameters — " +
            "including private methods. The Godot analog of Unity's 'reflection-method-find'. Returns " +
            "serialized MethodData entries usable as schemas for 'reflection-method-call'.\n" +
            "Match levels (apply to typeName / MethodName / Parameters):\n" +
            "  - typeNameMatchLevel / methodNameMatchLevel (default 1 = contains-ignoring-case): " +
            "0 ignore filter, 1 contains-ic, 2 contains-cs, 3 starts-with-ic, 4 starts-with-cs, 5 equals-ic, " +
            "6 equals-cs.\n" +
            "  - parametersMatchLevel (default 0 = ignore filter): 0 ignore, 1 count matches, 2 equals.")]
        public string MethodFind
        (
            [Description("Method filter: Namespace / TypeName / MethodName / InputParameters to match against.")]
            MethodRef filter,
            [Description("Set true if 'filter.Namespace' is a known full namespace name; otherwise false.")]
            bool knownNamespace = false,
            [Description("Minimal match level for 'filter.TypeName' (0 ignore, 1 contains-ic [default], " +
                "2 contains-cs, 3 starts-ic, 4 starts-cs, 5 equals-ic, 6 equals-cs).")]
            int typeNameMatchLevel = 1,
            [Description("Minimal match level for 'filter.MethodName' (0 ignore, 1 contains-ic [default], " +
                "2 contains-cs, 3 starts-ic, 4 starts-cs, 5 equals-ic, 6 equals-cs).")]
            int methodNameMatchLevel = 1,
            [Description("Minimal match level for 'filter.InputParameters' (0 ignore [default], " +
                "1 count matches, 2 equals).")]
            int parametersMatchLevel = 0
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var methods = FindMethods(
                    filter: filter,
                    knownNamespace: knownNamespace,
                    typeNameMatchLevel: typeNameMatchLevel,
                    methodNameMatchLevel: methodNameMatchLevel,
                    parametersMatchLevel: parametersMatchLevel).ToList();

                if (methods.Count == 0)
                    return $"[Success] Method not found. With request:\n{filter}";

                var reflector = GodotMcpReflector.GetOrCreate();

                var methodRefs = methods
                    .Select(method => new MethodData(reflector, method, justRef: false))
                    .ToList();

                return $@"[Success] Found {methods.Count} method(s):
```json
{methodRefs.ToJson(reflector)}
```";
            });
        }
    }
}
