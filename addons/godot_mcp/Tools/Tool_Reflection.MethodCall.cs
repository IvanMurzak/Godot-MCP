/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;
using Microsoft.Extensions.Logging.Abstractions;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Reflection
    {
        public const string ReflectionMethodCallToolId = "reflection-method-call";

        [AiTool
        (
            ReflectionMethodCallToolId,
            Title = "Method C# / Call"
        )]
        [Description("Call a C# method by reflection — including private methods. The Godot analog of " +
            "Unity's 'reflection-method-call'. Requires a method schema obtained via " +
            "'reflection-method-find'. Supports static methods, instance methods (with optional target " +
            "deserialization), and main-thread / off-thread execution.\n" +
            "Inputs:\n" +
            "  - 'targetObject' (optional) — for instance methods; { type, value } where value is " +
            "deserialized to type. Null for static methods (or to construct a fresh instance).\n" +
            "  - 'inputParameters' (optional) — list of { type, name, value }; names/types are enhanced " +
            "against the resolved signature when omitted.\n" +
            "  - 'executeInMainThread' (default true) — keep true for Godot-API-touching methods; set false " +
            "only for thread-safe pure logic.")]
        public SerializedMember MethodCall
        (
            [Description("Method filter: Namespace / TypeName / MethodName / InputParameters identifying the method.")]
            MethodRef filter,
            [Description("Set true if 'filter.Namespace' is a known full namespace name; otherwise false.")]
            bool knownNamespace = false,
            [Description("Minimal match level for 'filter.TypeName' (0 ignore, 1 contains-ic [default], " +
                "2 contains-cs, 3 starts-ic, 4 starts-cs, 5 equals-ic, 6 equals-cs).")]
            int typeNameMatchLevel = 1,
            [Description("Minimal match level for 'filter.MethodName' (0 ignore, 1 contains-ic [default], " +
                "2 contains-cs, 3 starts-ic, 4 starts-cs, 5 equals-ic, 6 equals-cs).")]
            int methodNameMatchLevel = 1,
            [Description("Minimal match level for 'filter.InputParameters' (0 ignore, 1 count matches, " +
                "2 equals [default]).")]
            int parametersMatchLevel = 2,
            [Description("Target object for an instance method ({ type, value }). Null for a static method, " +
                "or to construct a fresh instance of the declaring type.")]
            SerializedMember? targetObject = null,
            [Description("Method input parameters, each { type, name, value }.")]
            SerializedMemberList? inputParameters = null,
            [Description("Run the call on the editor main thread. Keep true for Godot-API methods; false " +
                "for thread-safe pure logic.")]
            bool executeInMainThread = true
        )
        {
            // Enhance filter with input parameters if the filter carries none.
            if ((filter.InputParameters?.Count ?? 0) == 0 && (inputParameters?.Count ?? 0) > 0)
                filter.EnhanceInputParameters(inputParameters);

            var methods = FindMethods(
                filter: filter,
                knownNamespace: knownNamespace,
                typeNameMatchLevel: typeNameMatchLevel,
                methodNameMatchLevel: methodNameMatchLevel,
                parametersMatchLevel: parametersMatchLevel).ToList();

            if (methods.Count == 0)
                throw new Exception($"Method not found.\n{filter}");

            MethodInfo method;

            if (methods.Count > 1)
            {
                var isValidParameterTypeName = inputParameters.IsValidTypeNames(
                    fieldName: nameof(inputParameters),
                    out _);

                var filtered = isValidParameterTypeName
                    ? methods.FilterByParameters(inputParameters)
                    : null;

                if (filtered == null)
                    throw new Exception(Error.MoreThanOneMethodFound(GodotMcpReflector.GetOrCreate(), methods));

                method = filtered;
            }
            else
            {
                method = methods.First();
            }

            inputParameters?.EnhanceNames(method);
            inputParameters?.EnhanceTypes(method);

            Func<SerializedMember> action = () =>
            {
                var reflector = GodotMcpReflector.GetOrCreate();
                var logger = NullLogger.Instance;

                var dictInputParameters = inputParameters?.ToDictionary(
                    keySelector: p => p.name ?? throw new InvalidOperationException(
                        "Input parameter name is null. Please specify 'name' for each input parameter."),
                    elementSelector: p => reflector.Deserialize(p, logger: logger));

                MethodWrapper methodWrapper;

                if (string.IsNullOrEmpty(targetObject?.typeName))
                {
                    // No instance needed — static method (or a fresh instance constructed by the wrapper).
                    methodWrapper = new MethodWrapper(reflector, logger, method);
                }
                else
                {
                    var obj = reflector.Deserialize(targetObject!, logger: logger);
                    if (obj == null)
                        throw new Exception($"'{nameof(targetObject)}' deserialized instance is null. " +
                            $"Please specify '{nameof(targetObject)}' properly.");

                    methodWrapper = new MethodWrapper(reflector, logger, targetInstance: obj, methodInfo: method);
                }

                if (!methodWrapper.VerifyParameters(dictInputParameters, out var error))
                    throw new Exception(error);

                var task = dictInputParameters != null
                    ? methodWrapper.InvokeDict(dictInputParameters, CancellationToken.None)
                    : methodWrapper.Invoke(Array.Empty<object>());

                var result = task.Result;
                if (result is SerializedMember serializedResult)
                    return serializedResult;

                return reflector.Serialize(
                    obj: result,
                    fallbackType: method.ReturnType,
                    logger: logger);
            };

            if (executeInMainThread)
                return MainThread.Instance.Run(action);

            return action();
        }
    }
}
