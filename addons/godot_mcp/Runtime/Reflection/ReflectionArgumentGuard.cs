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
using com.IvanMurzak.ReflectorNet.Model;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// Turns ReflectorNet's ADVISORY deserialization diagnostics into a hard failure for
    /// <c>reflection-method-call</c> arguments (issue #292).
    ///
    /// <para>
    /// ReflectorNet's <c>Reflector.Deserialize</c> never signals failure through its return value: on a
    /// payload it cannot read it records a <see cref="LogType.Error"/> entry into the optional
    /// <see cref="Logs"/> and returns the type's DEFAULT (<c>null</c> for a reference type, a zeroed struct
    /// for a value type). <c>MethodWrapper.VerifyParameters</c> then waves that default through, because a
    /// boxed <c>default(T)</c> is a perfectly valid <c>T</c>. The call therefore runs with a bogus argument
    /// and reports success — the exact failure mode issue #292 describes for <c>Godot.Variant</c>.
    /// </para>
    ///
    /// <para>
    /// Passing a <see cref="Logs"/> sink per argument and refusing to invoke when it contains an
    /// <see cref="LogType.Error"/> / <see cref="LogType.Critical"/> entry closes that hole GENERICALLY — for
    /// every argument type, not just the ones with a bespoke converter. Warnings are left alone: they are
    /// informational (e.g. "no resolver installed outside the editor") and must not break working calls.
    /// </para>
    ///
    /// <para>
    /// <b>Scope of the behaviour change.</b> This is applied at the <c>reflection-method-call</c> argument
    /// site only. Converters that record a <see cref="LogType.Error"/> and return <c>null</c> — notably
    /// <see cref="Godot_Resource_ReflectionConverter{T}"/>'s "could not resolve this res:// path" — will now
    /// FAIL such a call instead of passing <c>null</c> into the method. Other tools that pass their own
    /// <see cref="Logs"/> (<c>node-modify</c>, <c>resource-modify</c>) are untouched and still degrade to
    /// null with a logged error, which is the right behaviour for a merge-patch that reports per-member
    /// outcomes.
    /// </para>
    ///
    /// Pure-managed (it only reads a <see cref="Logs"/> list), so it is unit-tested in the plain xUnit host.
    /// </summary>
    public static class ReflectionArgumentGuard
    {
        /// <summary>Diagnostic severities that mean "the value was NOT produced as requested".</summary>
        static bool IsFailure(LogType type) => type == LogType.Error || type == LogType.Critical;

        /// <summary>
        /// Throw when <paramref name="logs"/> records that deserializing <paramref name="argumentName"/>
        /// failed. Returns quietly for an empty/warning-only log.
        /// </summary>
        /// <param name="logs">The diagnostics sink handed to <c>Reflector.Deserialize</c>.</param>
        /// <param name="argumentName">The argument being deserialized, for the error message.</param>
        /// <param name="declaredTypeName">The argument's declared type name, for the error message.</param>
        public static void RequireNoErrors(Logs? logs, string argumentName, string? declaredTypeName)
        {
            // Scan first: "no failures" is the overwhelmingly common outcome, and it should not pay for a
            // LINQ pipeline + array on every argument of every call.
            if (logs == null)
                return;

            var clean = true;
            foreach (var entry in logs)
            {
                if (!IsFailure(entry.Type))
                    continue;
                clean = false;
                break;
            }
            if (clean)
                return;

            var failures = Failures(logs);

            var declared = string.IsNullOrEmpty(declaredTypeName) ? "the declared type" : $"'{declaredTypeName}'";
            throw new ArgumentException(
                $"Could not deserialize '{argumentName}' to {declared} — the call was NOT made. " +
                string.Join(" ", failures));
        }

        /// <summary>The failure messages recorded in <paramref name="logs"/>, in order.</summary>
        public static IReadOnlyList<string> Failures(Logs? logs)
            => logs == null
                ? Array.Empty<string>()
                : logs.Where(entry => IsFailure(entry.Type)).Select(entry => entry.Message).ToArray();
    }
}
