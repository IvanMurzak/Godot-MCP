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
using MicrosoftLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The user-facing verbosity threshold for the Godot-MCP plugin's logging — the Godot analog of
    /// Unity-MCP's <c>com.IvanMurzak.Unity.MCP.Runtime.Utils.LogLevel</c>. Lower ordinals are more verbose;
    /// a configured level shows that level and everything more severe. Pure-managed (no Godot native types,
    /// no <c>#if TOOLS</c>) so it serializes into <see cref="GodotMcpConfig"/> and is unit-testable in the
    /// plain-xUnit host.
    ///
    /// <para>
    /// Unity carries a distinct <c>Exception</c> level between <c>Error</c> and <c>None</c>; Godot's
    /// logging surface (<c>GD.Print</c>/<c>GD.PushWarning</c>/<c>GD.PushError</c>) has no separate
    /// exception channel, so this enum collapses critical/error into the single <see cref="Error"/> bucket
    /// — matching how <see cref="Data.GodotLogType"/> already mirrors Godot's three output levels.
    /// </para>
    /// </summary>
    public enum GodotMcpLogLevel
    {
        /// <summary>Show everything — Trace, Debug, Info, Warning, Error.</summary>
        Trace = 0,

        /// <summary>Show Debug and above (Debug, Info, Warning, Error).</summary>
        Debug = 1,

        /// <summary>Show Info and above (Info, Warning, Error). The default.</summary>
        Info = 2,

        /// <summary>Show Warning and above (Warning, Error).</summary>
        Warning = 3,

        /// <summary>Show Error (and Critical) only.</summary>
        Error = 4,

        /// <summary>Show nothing.</summary>
        None = 5
    }

    /// <summary>
    /// Pure-managed level-gate for the Godot-MCP logger: maps a framework
    /// <see cref="MicrosoftLogLevel"/> to the matching <see cref="GodotMcpLogLevel"/> bucket and decides
    /// whether it is enabled under a configured threshold. The Godot analog of Unity-MCP's
    /// <c>UnityLogger.IsEnabled</c> + <c>LogLevelEx.IsEnabled</c>. No Godot dependency, no
    /// <c>#if TOOLS</c> — unit-tested directly in the plain-xUnit host.
    /// </summary>
    public static class GodotMcpLogGate
    {
        /// <summary>
        /// Map a framework <see cref="MicrosoftLogLevel"/> to the matching <see cref="GodotMcpLogLevel"/>
        /// bucket. <see cref="MicrosoftLogLevel.Critical"/> folds into <see cref="GodotMcpLogLevel.Error"/>
        /// (Godot has no separate critical channel); <see cref="MicrosoftLogLevel.None"/> folds into
        /// <see cref="GodotMcpLogLevel.None"/> (it is never actually logged). Mirrors the switch in
        /// Unity-MCP's <c>UnityLogger.IsEnabled</c>.
        /// </summary>
        public static GodotMcpLogLevel Map(MicrosoftLogLevel msLevel) => msLevel switch
        {
            MicrosoftLogLevel.Trace => GodotMcpLogLevel.Trace,
            MicrosoftLogLevel.Debug => GodotMcpLogLevel.Debug,
            MicrosoftLogLevel.Information => GodotMcpLogLevel.Info,
            MicrosoftLogLevel.Warning => GodotMcpLogLevel.Warning,
            MicrosoftLogLevel.Error => GodotMcpLogLevel.Error,
            MicrosoftLogLevel.Critical => GodotMcpLogLevel.Error,
            _ => GodotMcpLogLevel.None
        };

        /// <summary>
        /// True when a message at <paramref name="msLevel"/> should be shown under the
        /// <paramref name="configured"/> threshold. A message is enabled when its mapped bucket is at least
        /// as severe as the configured level (i.e. <c>configured &lt;= mapped</c>, mirroring Unity's
        /// <c>LogLevelEx.IsEnabled</c>). <see cref="GodotMcpLogLevel.None"/> as the configured threshold
        /// suppresses everything; a message that maps to <see cref="GodotMcpLogLevel.None"/> (i.e.
        /// <see cref="MicrosoftLogLevel.None"/>) is never enabled.
        /// </summary>
        public static bool IsEnabled(MicrosoftLogLevel msLevel, GodotMcpLogLevel configured)
        {
            if (configured == GodotMcpLogLevel.None)
                return false;

            var mapped = Map(msLevel);
            if (mapped == GodotMcpLogLevel.None)
                return false;

            return configured <= mapped;
        }
    }
}
