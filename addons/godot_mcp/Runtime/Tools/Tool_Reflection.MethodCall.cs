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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using com.IvanMurzak.Godot.MCP.Editor.Rql.ToolFormat;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Reflection
    {
        public const string ReflectionMethodCallToolId = "reflection-method-call";

        [AiTool
        (
            ReflectionMethodCallToolId,
            Title = "Method C# / Call",
            ReadOnlyHint = false,
            DestructiveHint = true,
            IdempotentHint = false,
            OpenWorldHint = true
        )]
        [Description("Call a C# method by reflection — including private methods. The Godot analog of " +
            "Unity's 'reflection-method-call'. Identify the method as with 'reflection-method-find'. Supports static methods, " +
            "instance methods and main-thread / off-thread execution. Values are RQL values, converted to the types the method " +
            "declares: 'targetObject' is the instance (ref(\"Node3D#12\"), godot.find(\"Player\"), a node path, \"res://…\", or an " +
            "object literal of the declaring type; empty for a static method or a fresh instance), 'inputParameters' the " +
            "arguments by name ({amount: 5, at: [1, 2, 3], target: ref(\"Node3D#12\")}; a parameter left out takes its default). " +
            "Returns the method's result as RQL text. A value that does not convert is an error: the method is never invoked " +
            "with a defaulted argument.")]
        public object? MethodCall
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
            [Description("The instance an instance method runs on, converted to its declaring type. Empty for a static method, " +
                "or to construct a fresh instance of the declaring type.")]
            RqlValue? targetObject = null,
            [Description("The method's arguments by parameter name, each converted to the parameter's type.")]
            RqlValues? inputParameters = null,
            [Description("Run the call on the main thread. Keep true for Godot-API methods; false " +
                "for thread-safe pure logic.")]
            bool executeInMainThread = true
        )
        {
            var given = inputParameters?.Items ?? Array.Empty<KeyValuePair<string, RqlValue>>();
            var methods = FindMethods(
                filter: filter,
                knownNamespace: knownNamespace,
                typeNameMatchLevel: typeNameMatchLevel,
                methodNameMatchLevel: methodNameMatchLevel,
                // Without parameters in the filter, the argument names choose among overloads (below).
                parametersMatchLevel: (filter.InputParameters?.Count ?? 0) == 0 && given.Count > 0 ? 0 : parametersMatchLevel).ToList();

            if (methods.Count == 0)
                throw new Exception($"Method not found.\n{filter}");

            if (methods.Count > 1 && (filter.InputParameters?.Count ?? 0) == 0)
            {
                var fits = methods.Where(m => Accepts(m, given.Select(kv => kv.Key))).ToList();
                if (fits.Count > 0) methods = fits;
            }

            if (methods.Count > 1)
                throw new Exception(Error.MoreThanOneMethodFound(Reflector, methods));

            var method = methods[0];
            Func<object?> action = () => Invoke(method, targetObject, given);
            return executeInMainThread ? MainThread.Instance.Run(action) : action();
        }

        /// <summary>Every given name is a parameter of <paramref name="method"/>, and every parameter without a default is given.</summary>
        static bool Accepts(MethodInfo method, IEnumerable<string> names)
        {
            var parameters = method.GetParameters();
            var set = new HashSet<string>(names, StringComparer.Ordinal);
            return set.All(n => parameters.Any(p => p.Name == n)) && parameters.All(p => set.Contains(p.Name!) || p.HasDefaultValue || p.IsOptional);
        }

        static object? Invoke(MethodInfo method, RqlValue? targetObject, IReadOnlyList<KeyValuePair<string, RqlValue>> given)
        {
            object? target = null;
            if (!method.IsStatic)
            {
                var declaring = method.DeclaringType!;
                if (targetObject != null)
                {
                    if (!targetObject.TryTo(declaring, out target, out var why))
                        throw new ArgumentException($"'targetObject': {why}");
                    if (target == null)
                        throw new ArgumentException($"'targetObject' is null; give the {declaring.Name} the method runs on.");
                }
                else
                {
                    try { target = Activator.CreateInstance(declaring, nonPublic: true); }
                    catch (Exception ex) { throw new ArgumentException($"'targetObject' is empty and a fresh {declaring.Name} cannot be built: {ex.Message}"); }
                }
            }

            var parameters = method.GetParameters();
            var unknown = given.Select(kv => kv.Key).Where(n => parameters.All(p => p.Name != n)).ToList();
            if (unknown.Count > 0)
                throw new ArgumentException($"'inputParameters': {string.Join(", ", unknown)} {(unknown.Count == 1 ? "is not a parameter" : "are not parameters")} of " +
                    $"{method.Name}({string.Join(", ", parameters.Select(p => p.ParameterType.Name + " " + p.Name))})");
            var args = new object?[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                var value = given.FirstOrDefault(kv => kv.Key == p.Name).Value;
                if (value != null)
                {
                    if (!value.TryTo(p.ParameterType, out args[i], out var why))
                        throw new ArgumentException($"'{p.Name}': {why}");
                }
                else if (p.HasDefaultValue) args[i] = p.DefaultValue;
                else if (p.IsOptional) args[i] = Type.Missing;
                else throw new ArgumentException($"'inputParameters' has no '{p.Name}' ({p.ParameterType.Name}), and it has no default.");
            }

            object? result;
            try { result = method.Invoke(target, args); }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
                // A Task<T> method's value; a plain Task (whose runtime type may still be generic) has none.
                return method.ReturnType.IsGenericType ? task.GetType().GetProperty(nameof(Task<object>.Result))?.GetValue(task) : null;
            }
            return result;
        }
    }
}
