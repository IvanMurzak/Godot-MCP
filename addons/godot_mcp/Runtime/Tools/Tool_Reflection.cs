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
using System.Linq;
using System.Reflection;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Model;
using com.IvanMurzak.ReflectorNet.Utils;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Reflection tool family (<c>reflection-method-*</c>) — the Godot analog of Unity-MCP's
    /// <c>Tool_Reflection</c>. This family is engine-agnostic: it reuses ReflectorNet's reflection engine
    /// (<see cref="TypeUtils.AllTypes"/>, <see cref="MethodData"/>, <see cref="MethodWrapper"/>) to find
    /// and call C# methods (static + instance, public + private) across every loaded assembly. The only
    /// Godot-specific wiring is the reflector source: it reads <see cref="Reflection.GodotMcpReflector"/>
    /// (the connection-built reflector with Godot type converters) instead of Unity's
    /// <c>UnityMcpPluginEditor.Instance.Reflector</c>.
    ///
    /// <para>
    /// No Godot editor-API surface, so it intentionally lives OUTSIDE <c>#if TOOLS</c>. The find/call
    /// logic is pure-managed and discovered by the McpPlugin assembly scanner. Main-thread marshalling is
    /// applied per-call (Godot-touching reflected methods need it; thread-safe pure logic can opt out via
    /// <c>executeInMainThread = false</c>).
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Reflection
    {
        static IEnumerable<Type> AllTypes => TypeUtils.AllTypes;

        static int Compare(string original, string value)
        {
            if (string.IsNullOrEmpty(original) || string.IsNullOrEmpty(value))
                return 0;

            if (original.Equals(value, StringComparison.OrdinalIgnoreCase))
                return original.Equals(value, StringComparison.Ordinal) ? 6 : 5;

            if (original.StartsWith(value, StringComparison.OrdinalIgnoreCase))
                return original.StartsWith(value, StringComparison.Ordinal) ? 4 : 3;

            if (original.Contains(value, StringComparison.OrdinalIgnoreCase))
                return original.Contains(value, StringComparison.Ordinal) ? 2 : 1;

            return 0;
        }

        static int Compare(ParameterInfo[] original, List<MethodRef.Parameter>? value)
        {
            if (original == null && value == null)
                return 2;

            if (original == null || value == null)
                return 0;

            if (original.Length != value.Count)
                return 0;

            for (int i = 0; i < original.Length; i++)
            {
                var parameter = original[i];
                var methodRefParameter = value[i];

                if (parameter.Name != methodRefParameter.Name)
                    return 1;

                if (parameter.ParameterType.IsMatch(methodRefParameter.TypeName) == false)
                    return 1;
            }

            return 2;
        }

        /// <summary>
        /// Find methods across every loaded assembly matching the <paramref name="filter"/>, with tunable
        /// match levels for the declaring-type name, the method name, and the parameters. A faithful port
        /// of Unity-MCP's <c>Tool_Reflection.FindMethods</c> (the logic is engine-agnostic — only the
        /// reflector source differs, and that is read at the call sites in MethodFind / MethodCall).
        /// </summary>
        static IEnumerable<MethodInfo> FindMethods(
            MethodRef filter,
            bool knownNamespace = false,
            int typeNameMatchLevel = 1,
            int methodNameMatchLevel = 1,
            int parametersMatchLevel = 2,
            BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        {
            filter.Namespace = filter.Namespace?.Trim()?.Replace("null", string.Empty);
            if (string.IsNullOrEmpty(filter.TypeName))
                filter.TypeName = null!;

            var typesEnumerable = AllTypes
                .Where(type => type.IsVisible)
                .Where(type => !type.IsInterface)
                .Where(type => !type.IsAbstract || type.IsSealed)
                .Where(type => !type.IsGenericTypeDefinition);

            if (knownNamespace)
                typesEnumerable = typesEnumerable.Where(type => type.Namespace == filter.Namespace);

            if (typeNameMatchLevel > 0 && !string.IsNullOrEmpty(filter.TypeName))
                typesEnumerable = typesEnumerable
                    .Select(type => new { Type = type, MatchLevel = Compare(type.Name, filter.TypeName) })
                    .Where(entry => entry.MatchLevel >= typeNameMatchLevel)
                    .OrderByDescending(entry => entry.MatchLevel)
                    .Select(entry => entry.Type);

            var types = typesEnumerable.ToList();

            var methodEnumerable = types
                .SelectMany(type => type.GetMethods(bindingFlags)
                    .Where(method => method.DeclaringType == type))
                .Where(method => method.DeclaringType != null)
                .Where(method => !method.DeclaringType!.IsAbstract || method.DeclaringType.IsSealed)
                .Where(method => !method.IsGenericMethodDefinition);

            if (methodNameMatchLevel > 0 && !string.IsNullOrEmpty(filter.MethodName))
                methodEnumerable = methodEnumerable
                    .Select(method => new { Method = method, MatchLevel = Compare(method.Name, filter.MethodName) })
                    .Where(entry => entry.MatchLevel >= methodNameMatchLevel)
                    .OrderByDescending(entry => entry.MatchLevel)
                    .Select(entry => entry.Method);

            if (parametersMatchLevel > 0)
                methodEnumerable = methodEnumerable
                    .Select(method => new { Method = method, MatchLevel = Compare(method.GetParameters(), filter.InputParameters) })
                    .Where(entry => entry.MatchLevel >= parametersMatchLevel)
                    .OrderByDescending(entry => entry.MatchLevel)
                    .Select(entry => entry.Method);

            return methodEnumerable;
        }

        public static class Error
        {
            public static string MoreThanOneMethodFound(Reflector reflector, List<MethodInfo> methods)
            {
                var methodsString = methods
                    .Select(method => new MethodData(reflector, method, justRef: false))
                    .ToJson(reflector);

                return @$"Found more than one method. Only single method should be targeted. Please specify the method name more precisely.
Found {methods.Count} method(s):
```json
{methodsString}
```";
            }
        }
    }
}
