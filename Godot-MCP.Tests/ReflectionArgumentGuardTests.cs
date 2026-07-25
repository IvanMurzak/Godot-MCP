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
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.ReflectorNet.Model;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Coverage for the generic half of the issue-#292 fix: ReflectorNet reports an unreadable argument ONLY
    /// through its optional <see cref="Logs"/> sink (it still returns the type's default), so
    /// <c>reflection-method-call</c> must refuse to invoke when that sink records a failure.
    /// </summary>
    public class ReflectionArgumentGuardTests
    {
        static Logs LogsWith(params (LogType Type, string Message)[] entries)
        {
            var logs = new Logs();
            foreach (var entry in entries)
                logs.AddLast(new LogEntry { Type = entry.Type, Message = entry.Message });
            return logs;
        }

        [Fact]
        public void RequireNoErrors_EmptyLogs_DoesNotThrow()
        {
            ReflectionArgumentGuard.RequireNoErrors(new Logs(), "value", "Godot.Variant");
        }

        [Fact]
        public void RequireNoErrors_NullLogs_DoesNotThrow()
        {
            ReflectionArgumentGuard.RequireNoErrors(null, "value", "Godot.Variant");
        }

        [Theory]
        [InlineData(LogType.Trace)]
        [InlineData(LogType.Debug)]
        [InlineData(LogType.Info)]
        [InlineData(LogType.Success)]
        [InlineData(LogType.Warning)]
        public void RequireNoErrors_NonFailureSeverities_DoNotThrow(LogType type)
        {
            // Warnings in particular are informational ("no resolver installed outside the editor") and must
            // never break a working call.
            ReflectionArgumentGuard.RequireNoErrors(LogsWith((type, "informational")), "value", "Godot.Variant");
        }

        [Theory]
        [InlineData(LogType.Error)]
        [InlineData(LogType.Critical)]
        public void RequireNoErrors_FailureSeverities_Throw(LogType type)
        {
            var ex = Assert.Throws<ArgumentException>(
                () => ReflectionArgumentGuard.RequireNoErrors(LogsWith((type, "could not resolve res://missing.tres")), "value", "Godot.Variant"));

            Assert.Contains("Could not deserialize 'value' to 'Godot.Variant'", ex.Message);
            Assert.Contains("the call was NOT made", ex.Message);
            Assert.Contains("could not resolve res://missing.tres", ex.Message);
        }

        [Fact]
        public void RequireNoErrors_ReportsEveryFailure()
        {
            var ex = Assert.Throws<ArgumentException>(() => ReflectionArgumentGuard.RequireNoErrors(
                LogsWith(
                    (LogType.Warning, "just a warning"),
                    (LogType.Error, "first failure"),
                    (LogType.Critical, "second failure")),
                "targetObject",
                declaredTypeName: null));

            Assert.Contains("first failure", ex.Message);
            Assert.Contains("second failure", ex.Message);
            Assert.DoesNotContain("just a warning", ex.Message);
            Assert.Contains("the declared type", ex.Message);
        }

        [Fact]
        public void Failures_ReturnsOnlyFailureMessages_InOrder()
        {
            var failures = ReflectionArgumentGuard.Failures(LogsWith(
                (LogType.Info, "a"),
                (LogType.Error, "b"),
                (LogType.Warning, "c"),
                (LogType.Critical, "d")));

            Assert.Equal(new[] { "b", "d" }, failures);
        }
    }
}
