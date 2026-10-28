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
using com.IvanMurzak.Godot.MCP.Data;
using Microsoft.Extensions.Logging;
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// An <see cref="ILogger"/> that routes the reused <c>com.IvanMurzak.McpPlugin</c> framework's
    /// <c>Microsoft.Extensions.Logging</c> output to the Godot editor Output — the Godot analog of
    /// Unity-MCP's <c>UnityLogger</c>. The framework's connection / version-handshake / hub-state logs
    /// (currently invisible because the plugin passes no logger provider to <c>McpPluginBuilder</c>) become
    /// visible here, gated by a configurable <see cref="GodotMcpLogLevel"/>.
    ///
    /// <para>
    /// <b>Pure-managed by design.</b> The only Godot dependency — writing to <c>GD.Print</c>/
    /// <c>GD.PushWarning</c>/<c>GD.PushError</c> — is injected as a <see cref="GodotLogSink"/> delegate, and
    /// the LIVE configured level is read through a <see cref="Func{TResult}"/> on every call (so the dock's
    /// Log Level dropdown takes effect without a rebuild). This keeps the logger (and its
    /// <see cref="GodotMcpLoggerProvider"/>) outside <c>#if TOOLS</c> and unit-testable in the plain-xUnit
    /// host with a fake sink; the editor boot wires a real <c>GD.*</c> + <see cref="Tools.GodotLogCollector"/>
    /// sink. The level decision itself lives in the pure <see cref="GodotMcpLogGate"/>.
    /// </para>
    ///
    /// <para>
    /// <b>Security.</b> This logger only passes through messages the framework already emits; it never
    /// constructs or logs a token. The framework avoids logging secrets, and nothing here re-introduces one.
    /// </para>
    /// </summary>
    public sealed class GodotMcpLogger : ILogger
    {
        /// <summary>Prefix prepended to every routed line so framework logs are attributable in the Output.</summary>
        public const string Prefix = "[McpPlugin] ";

        /// <summary>
        /// Whether a line is the framework's echo of a tool call: McpToolManager's "Run tool '…'" and "… Response to AI"
        /// without an exception. Those go to the echo sink (printed, not collected for the console source: the agent
        /// already has both). A line with an exception, a tool failure logged by ToolRunnerCollection (its stack trace is
        /// recorded nowhere else) and every other McpToolManager line (e.g. a duplicate tool name) stay collected.
        /// </summary>
        public static bool IsToolTraffic(string category, string text, Exception? exception) =>
            exception == null && category == "McpToolManager" && text != null
            && (text.StartsWith("Run tool '", StringComparison.Ordinal)
                || text.StartsWith("Success Response to AI", StringComparison.Ordinal)
                || text.StartsWith("Error Response to AI", StringComparison.Ordinal)
                || text.StartsWith("Processing Response to AI", StringComparison.Ordinal));

        /// <summary>
        /// Optional: true for an error line that is expected and needs no backtrace, written at info severity instead. The
        /// editor sets it for the reload gate's refusals: an agent hammering calls during a reload produced hundreds of
        /// <c>GD.PushError</c> backtraces a second, slowing the editor's main thread enough to delay the reload itself.
        /// </summary>
        public static Func<string, bool>? IsExpectedError { get; set; }

        readonly string _categoryName;
        readonly Func<GodotMcpLogLevel> _levelProvider;
        readonly GodotLogSink _sink;
        readonly GodotLogSink _echoSink;

        /// <summary>
        /// Construct a logger for <paramref name="categoryName"/>.
        /// </summary>
        /// <param name="categoryName">The DI category (typically a framework type name); the short name is shown.</param>
        /// <param name="levelProvider">Reads the LIVE configured threshold on every call (dropdown takes effect without rebuild).</param>
        /// <param name="sink">Routes a formatted line to the Godot Output + log collector. Injected so the core stays pure-managed.</param>
        /// <param name="echoSink">Routes the tool-call echo (<see cref="IsToolTraffic"/>); null = <paramref name="sink"/>.</param>
        public GodotMcpLogger(string categoryName, Func<GodotMcpLogLevel> levelProvider, GodotLogSink sink, GodotLogSink? echoSink = null)
        {
            // Short category (drop the namespace) for a compact Output line, mirroring UnityLogger.
            _categoryName = categoryName != null && categoryName.Contains('.')
                ? categoryName.Substring(categoryName.LastIndexOf('.') + 1)
                : categoryName ?? string.Empty;
            _levelProvider = levelProvider ?? throw new ArgumentNullException(nameof(levelProvider));
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
            _echoSink = echoSink ?? sink;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        /// <summary>
        /// True when a message at <paramref name="logLevel"/> passes the LIVE configured threshold. Read
        /// live (not cached) so toggling the dock's Log Level dropdown changes verbosity without rebuilding
        /// the connection.
        /// </summary>
        public bool IsEnabled(MicrosoftLogLevel logLevel) =>
            GodotMcpLogGate.IsEnabled(logLevel, _levelProvider());

        public void Log<TState>(
            MicrosoftLogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;
            if (formatter == null)
                throw new ArgumentNullException(nameof(formatter));

            var text = formatter(state, exception);
            var message = string.IsNullOrEmpty(_categoryName)
                ? $"{Prefix}{text}"
                : $"{Prefix}{_categoryName}: {text}";

            // Append the exception detail (message + stack) so a failing handshake/connect is diagnosable.
            if (exception != null)
                message = $"{message}\n{exception}";

            var godotType = logLevel switch
            {
                MicrosoftLogLevel.Critical => GodotLogType.Error,
                MicrosoftLogLevel.Error => GodotLogType.Error,
                MicrosoftLogLevel.Warning => GodotLogType.Warning,
                _ => GodotLogType.Log
            };
            if (godotType == GodotLogType.Error && exception == null && IsExpectedError?.Invoke(text) == true)
                godotType = GodotLogType.Log;

            (IsToolTraffic(_categoryName, text, exception) ? _echoSink : _sink)(godotType, message);
        }
    }

    /// <summary>
    /// Routes a single formatted log line at a given <see cref="GodotLogType"/> severity to its
    /// destination(s). The editor boot injects a sink that writes to <c>GD.Print</c>/<c>GD.PushWarning</c>/
    /// <c>GD.PushError</c> AND appends to <see cref="Tools.GodotLogCollector"/>; the unit-test host injects a
    /// capturing sink. Keeping this a delegate is what lets <see cref="GodotMcpLogger"/> stay pure-managed.
    /// </summary>
    public delegate void GodotLogSink(GodotLogType logType, string message);
}
