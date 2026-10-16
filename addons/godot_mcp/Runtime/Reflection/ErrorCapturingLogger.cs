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
using Microsoft.Extensions.Logging;

namespace com.IvanMurzak.Godot.MCP.Reflection
{
    /// <summary>
    /// An <see cref="ILogger"/> that only records error-level messages. ReflectorNet 5.4's
    /// <c>TryModifyAt</c> writes a failed member setter (e.g. a <c>double</c> into a <c>float</c> property)
    /// to its <c>ILogger</c> alone — not to <c>Logs</c> — and still returns <c>true</c>. Passing this logger
    /// makes that swallowed failure visible to the caller.
    /// </summary>
    public sealed class ErrorCapturingLogger : ILogger
    {
        readonly List<string> errors = new List<string>();

        public IReadOnlyList<string> Errors => errors;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Error;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(logLevel))
                errors.Add(formatter(state, exception).Trim());
        }
    }
}
