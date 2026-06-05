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
using System.IO;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Pure-managed loader for a project-root <c>.env</c> file that self-configures the Godot-MCP
    /// connection. Lets a Godot project ship its server URL + token (e.g. a worktree pointing at a
    /// local dev server) WITHOUT the developer having to export process environment variables — Godot
    /// is normally launched from the GUI, which inherits no shell exports. The Godot superset of
    /// Unity-MCP's <c>EnvironmentUtils</c> env-override path (Unity reads only process env; here we add
    /// a file layer beneath it).
    ///
    /// <para>
    /// Precedence (highest wins), realised by <see cref="Apply"/>:
    /// <list type="number">
    ///   <item>process environment variable (<c>GODOT_MCP_*</c>) — still wins, applied live by the
    ///   <see cref="GodotMcpConfig"/> getters AFTER this layer writes its fields;</item>
    ///   <item><c>.env</c> file value — written here into the config's serialized backing fields;</item>
    ///   <item>serialized config field (whatever the config already carried);</item>
    ///   <item>built-in default.</item>
    /// </list>
    /// Because <see cref="GodotMcpConfig"/>'s <c>Host</c>/<c>Token</c>/<c>ActiveMode</c> read the
    /// process env live on every access, writing the <c>.env</c> value into the backing field below the
    /// env layer is exactly what makes env outrank file: an env value shadows the field we set here.
    /// </para>
    ///
    /// <para>
    /// Entirely pure-managed — no Godot native types — so it is unit-testable in the plain-xUnit
    /// <c>Godot-MCP.Tests</c> host. The editor boot path (<see cref="GodotMcpConnection"/>, behind
    /// <c>#if TOOLS</c>) resolves the file path via <c>ProjectSettings.GlobalizePath("res://.env")</c>
    /// and feeds it through <see cref="LoadFile"/> + <see cref="Apply"/>.
    /// </para>
    /// </summary>
    public static class GodotMcpEnvFile
    {
        /// <summary>The recognized keys. Any other key in the file is ignored.</summary>
        static readonly string[] RecognizedKeys =
        {
            GodotMcpConfig.EnvConnectionMode,
            GodotMcpConfig.EnvHost,
            GodotMcpConfig.EnvCloudUrl,
            GodotMcpConfig.EnvToken,
            GodotMcpConfig.EnvAuthOption,
            GodotMcpConfig.EnvLogLevel
        };

        /// <summary>
        /// Load and parse the <c>.env</c> file at <paramref name="path"/>, returning the recognized
        /// <c>GODOT_MCP_*</c> values. A missing or unreadable file returns an empty dictionary (never
        /// throws) — the absence of a <c>.env</c> file is the common case, not an error.
        /// </summary>
        public static IReadOnlyDictionary<string, string> LoadFile(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return Empty;

            string[] lines;
            try
            {
                if (!File.Exists(path))
                    return Empty;
                lines = File.ReadAllLines(path);
            }
            catch (Exception)
            {
                // IO/permission/encoding failure — treat as "no .env" rather than breaking plugin boot.
                return Empty;
            }

            return Parse(lines);
        }

        /// <summary>
        /// Parse <c>.env</c>-style lines into the recognized <c>GODOT_MCP_*</c> values. Rules:
        /// <list type="bullet">
        ///   <item>blank lines and lines whose first non-whitespace char is <c>#</c> are skipped;</item>
        ///   <item>each line is split on the first <c>=</c>; lines without one are skipped;</item>
        ///   <item>the key is trimmed; only the four <c>GODOT_MCP_*</c> keys are kept (case-sensitive,
        ///   matching how process env vars are read);</item>
        ///   <item>the value is sanitized via the SAME normalizer the config applies to process-env
        ///   values (<see cref="GodotMcpConfig.NormalizeEnv"/>: trim whitespace + a single pair of
        ///   wrapping quotes), so <c>GODOT_MCP_TOKEN="abc"</c> yields <c>abc</c>;</item>
        ///   <item>a blank value (after sanitize) is skipped — it carries no override;</item>
        ///   <item>on duplicate keys the LAST occurrence wins.</item>
        /// </list>
        /// Single-quote wrapping is also stripped (a convenience superset over process env, which is
        /// already shell-unquoted by the time it reaches the process).
        /// </summary>
        public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string>? lines)
        {
            if (lines == null)
                return Empty;

            var result = new Dictionary<string, string>(StringComparer.Ordinal);

            foreach (var rawLine in lines)
            {
                if (rawLine == null)
                    continue;

                var line = rawLine.Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line.Substring(0, eq).Trim();
                if (Array.IndexOf(RecognizedKeys, key) < 0)
                    continue;

                var rawValue = line.Substring(eq + 1);
                var value = Sanitize(rawValue);
                if (string.IsNullOrEmpty(value))
                    continue;

                result[key] = value!;
            }

            return result;
        }

        /// <summary>
        /// Apply the parsed <c>.env</c> <paramref name="values"/> to <paramref name="config"/>, writing
        /// each recognized key into the matching serialized backing field — BENEATH the process-env
        /// layer (see the class precedence note). Token routes to the active mode's token field
        /// (Cloud → <see cref="GodotMcpConfig.CloudToken"/>, Custom → <see cref="GodotMcpConfig.CustomToken"/>),
        /// mirroring the process-env token path.
        ///
        /// <para>
        /// Mode selection (highest wins): an explicit <c>GODOT_MCP_CONNECTION_MODE</c> from the env layer
        /// already wins via <see cref="GodotMcpConfig.ActiveMode"/> (it is not set here — env is live).
        /// Otherwise an explicit mode in the <c>.env</c> file wins; otherwise, if the effective host
        /// (env host &gt; file host &gt; configured host) is a loopback address, auto-select
        /// <see cref="GodotMcpConnectionMode.Custom"/> (parity with Unity-MCP's loopback inference).
        /// </para>
        ///
        /// <para>
        /// Idempotent / no-op when <paramref name="values"/> is empty. Pure-managed; safe to call from
        /// the unit-test host. Returns <paramref name="config"/> for fluent use.
        /// </para>
        /// </summary>
        public static GodotMcpConfig Apply(GodotMcpConfig config, IReadOnlyDictionary<string, string>? values)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));
            if (values == null || values.Count == 0)
                return config;

            // 1) Host fields. The file CLOUD_URL writes through to the cloud-base used by Cloud mode;
            //    HOST writes the custom-mode host. (The config's live getters still let env override.)
            if (values.TryGetValue(GodotMcpConfig.EnvHost, out var fileHost))
                config.CustomHost = fileHost;

            // GODOT_MCP_CLOUD_URL has no serialized backing field on the config (the cloud base is
            // resolved purely from env/default by ResolveCloudBaseUrl). There is nothing to write for it
            // at the file layer beyond informing the loopback decision below; a file CLOUD_URL only takes
            // effect when also exported to the process env, matching the env-only cloud-base contract.

            // 2) Mode: explicit file mode wins next; else loopback host → Custom. (Env mode already wins
            //    live via ActiveMode, so we never need to special-case it here.)
            if (values.TryGetValue(GodotMcpConfig.EnvConnectionMode, out var fileMode) &&
                TryParseMode(fileMode, out var parsedMode))
            {
                config.ConnectionMode = parsedMode;
            }
            else if (GodotMcpConfig.IsLoopbackUrl(EffectiveHostForLoopback(values)))
            {
                config.ConnectionMode = GodotMcpConnectionMode.Custom;
            }

            // 2b) Auth option (Custom-mode only): an explicit file value writes the serialized field
            //     beneath the env layer (env GODOT_MCP_AUTH_OPTION still wins live via ActiveAuthOption).
            if (values.TryGetValue(GodotMcpConfig.EnvAuthOption, out var fileAuth) &&
                TryParseAuthOption(fileAuth, out var parsedAuth))
            {
                config.AuthOption = parsedAuth;
            }

            // 2c) Log level (cross-cutting): an explicit file value writes the serialized field beneath the
            //     env layer (env GODOT_MCP_LOG_LEVEL still wins live via ActiveLogLevel).
            if (values.TryGetValue(GodotMcpConfig.EnvLogLevel, out var fileLogLevel) &&
                TryParseLogLevel(fileLogLevel, out var parsedLogLevel))
            {
                config.LogLevel = parsedLogLevel;
            }

            // 3) Token: route to the active mode's token field (after mode is settled above so the route
            //    matches what the connection will actually use). Env token still wins live.
            if (values.TryGetValue(GodotMcpConfig.EnvToken, out var fileToken))
            {
                if (config.ActiveMode == GodotMcpConnectionMode.Cloud)
                    config.CloudToken = fileToken;
                else
                    config.CustomToken = fileToken;
            }

            return config;
        }

        /// <summary>
        /// The host string used for the loopback auto-mode decision: process-env host (highest), then
        /// the <c>.env</c> file host. CLOUD_URL is excluded because a cloud base never implies Custom
        /// mode, and the already-configured <c>CustomHost</c> is excluded too: loopback inference fires
        /// only for an EXPLICITLY-supplied host (env or file), never the disk-baseline default — mirroring
        /// Unity-MCP, where only an env/flag-supplied URL (not the persisted <c>LocalHost</c>) flips the
        /// mode. Without this, the default <c>CustomHost</c> (<c>http://localhost:8080</c>, itself a
        /// loopback) would silently flip every Cloud config to Custom.
        /// </summary>
        static string? EffectiveHostForLoopback(IReadOnlyDictionary<string, string> values)
        {
            var envHost = GodotMcpConfig.NormalizeUrl(Environment.GetEnvironmentVariable(GodotMcpConfig.EnvHost));
            if (!string.IsNullOrEmpty(envHost))
                return envHost;

            if (values.TryGetValue(GodotMcpConfig.EnvHost, out var fileHost) && !string.IsNullOrWhiteSpace(fileHost))
                return GodotMcpConfig.NormalizeUrl(fileHost);

            return null;
        }

        /// <summary>
        /// Parse a connection-mode string to <see cref="GodotMcpConnectionMode"/>, accepting only the
        /// named values (<c>Cloud</c>/<c>Custom</c>, case-insensitive) and rejecting numeric strings —
        /// identical discipline to <see cref="GodotMcpConfig.ResolveActiveMode"/>.
        /// </summary>
        static bool TryParseMode(string? raw, out GodotMcpConnectionMode mode)
        {
            mode = default;
            var normalized = Sanitize(raw);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (int.TryParse(normalized, out _))
                return false;
            return Enum.TryParse(normalized, ignoreCase: true, out mode);
        }

        /// <summary>
        /// Parse a Custom-mode auth-option string to <see cref="GodotMcpAuthOption"/>, accepting only the
        /// named values (<c>None</c>/<c>Required</c>, case-insensitive) and rejecting numeric strings —
        /// identical discipline to <see cref="TryParseMode"/> / <see cref="GodotMcpConfig.ResolveActiveAuthOption"/>.
        /// </summary>
        static bool TryParseAuthOption(string? raw, out GodotMcpAuthOption authOption)
        {
            authOption = default;
            var normalized = Sanitize(raw);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (int.TryParse(normalized, out _))
                return false;
            return Enum.TryParse(normalized, ignoreCase: true, out authOption);
        }

        /// <summary>
        /// Parse a log-level string to <see cref="GodotMcpLogLevel"/>, accepting only the named values
        /// (case-insensitive) and rejecting numeric strings — identical discipline to
        /// <see cref="TryParseMode"/> / <see cref="GodotMcpConfig.ResolveActiveLogLevel"/>.
        /// </summary>
        static bool TryParseLogLevel(string? raw, out GodotMcpLogLevel logLevel)
        {
            logLevel = default;
            var normalized = Sanitize(raw);
            if (string.IsNullOrEmpty(normalized))
                return false;
            if (int.TryParse(normalized, out _))
                return false;
            return Enum.TryParse(normalized, ignoreCase: true, out logLevel);
        }

        /// <summary>Sanitize a file value identically to a process-env value (see <see cref="Parse"/>).</summary>
        static string? Sanitize(string? raw)
        {
            // Strip a single pair of wrapping single quotes first (env normalizer only handles double
            // quotes), then defer to the shared env normalizer for whitespace + double-quote trimming.
            if (raw != null)
            {
                var trimmed = raw.Trim();
                if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
                    raw = trimmed.Substring(1, trimmed.Length - 2);
            }

            return GodotMcpConfig.NormalizeEnv(raw);
        }

        static readonly IReadOnlyDictionary<string, string> Empty =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
