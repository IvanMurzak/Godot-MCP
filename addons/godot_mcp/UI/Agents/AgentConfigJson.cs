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
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace com.IvanMurzak.Godot.MCP.UI.Agents
{
    /// <summary>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) JSON read-merge-write helpers shared by the
    /// AI-agent configurators. Godot-MCP is a CLIENT of the shared/cloud MCP server, so a configurator only
    /// ever emits the HTTP form of an MCP-client entry — <c>{"type":"http","url":&lt;mcpUrl&gt;[,"headers":{...}]}</c>
    /// — under a <c>&lt;bodyPath&gt;.&lt;serverKey&gt;</c> nav inside the target client's config file.
    ///
    /// <para>
    /// All file operations are READ-MERGE-WRITE: an existing config is loaded (if present, parseable) and only
    /// the addon's own entry is set / removed; sibling servers and unrelated top-level keys are preserved. The
    /// methods are path-injectable and robust to a missing / empty / invalid file, so they are unit-tested on
    /// temp files in the plain-xUnit host. The editor wiring (resolving the per-OS absolute path, the dock
    /// buttons) lives behind <c>#if TOOLS</c> in <c>AgentConfiguratorsPanel.cs</c>.
    /// </para>
    /// </summary>
    /// <summary>
    /// The three states the addon's entry in an agent's on-disk config can be in, relative to the CURRENT MCP
    /// client URL. Drives the dock's AI-agent alert panel: <see cref="Missing"/> → "Setup Required",
    /// <see cref="Stale"/> → "Reconfiguration Required", <see cref="UpToDate"/> → no alert. Pure-managed so the
    /// alert decision is unit-testable without a Godot Control.
    /// </summary>
    public enum AgentConfigState
    {
        /// <summary>No addon entry exists in the config file (missing / empty / invalid file, or entry absent).</summary>
        Missing,

        /// <summary>An addon entry exists but its <c>url</c> does NOT match the current MCP client URL (stale config).</summary>
        Stale,

        /// <summary>An addon entry exists and its <c>url</c> matches the current MCP client URL (nothing to do).</summary>
        UpToDate
    }

    public static class AgentConfigJson
    {
        /// <summary>The masked stand-in shown on-screen in place of a real bearer token (never copied/written).</summary>
        public const string MaskedToken = "****";

        static readonly JsonSerializerOptions PrettyOptions = new() { WriteIndented = true };

        /// <summary>
        /// Build the JSON object for a single MCP-client server entry: always <c>type</c>=<c>http</c> and the
        /// <c>url</c>; an <c>Authorization: Bearer &lt;token&gt;</c> header is added under <c>headers</c> ONLY when
        /// <paramref name="token"/> is non-empty. When <paramref name="maskToken"/> is true the header value uses
        /// <see cref="MaskedToken"/> instead of the real token (for the on-screen snippet) — the real token is
        /// never placed into a masked entry.
        /// </summary>
        public static JsonObject BuildServerEntry(string mcpUrl, string? token, bool maskToken = false)
        {
            var entry = new JsonObject
            {
                ["type"] = "http",
                ["url"] = mcpUrl
            };

            if (!string.IsNullOrEmpty(token))
            {
                var shown = maskToken ? MaskedToken : token;
                entry["headers"] = new JsonObject
                {
                    ["Authorization"] = $"Bearer {shown}"
                };
            }

            return entry;
        }

        /// <summary>
        /// Build the pretty-printed JSON SNIPPET a user copies into their MCP client — the addon's entry nested
        /// under <paramref name="bodyPath"/> → <paramref name="serverKey"/>. When <paramref name="maskToken"/> is
        /// true the bearer is masked (for on-screen display); the copy/Configure path passes
        /// <paramref name="maskToken"/>=false to emit the real token. The snippet shows ONLY the addon's own entry
        /// (not the user's whole merged file), which is the convention every MCP client documents.
        /// </summary>
        public static string BuildSnippet(string bodyPath, string serverKey, string mcpUrl, string? token, bool maskToken)
        {
            var root = new JsonObject
            {
                [bodyPath] = new JsonObject
                {
                    [serverKey] = BuildServerEntry(mcpUrl, token, maskToken)
                }
            };

            return root.ToJsonString(PrettyOptions);
        }

        /// <summary>
        /// True when the file at <paramref name="configPath"/> already contains the addon's entry under
        /// <paramref name="bodyPath"/> → <paramref name="serverKey"/> AND its <c>url</c> matches
        /// <paramref name="mcpUrl"/> (ordinal-ignore-case, so a host casing difference is tolerated). Returns false
        /// for a missing / empty / invalid file, or when the entry is absent or points at a different url.
        /// </summary>
        public static bool IsConfigured(string configPath, string bodyPath, string serverKey, string mcpUrl)
        {
            var root = TryReadObject(configPath);
            if (root == null)
                return false;

            if (root[bodyPath] is not JsonObject body)
                return false;

            if (body[serverKey] is not JsonObject entry)
                return false;

            var url = entry["url"]?.GetValue<string>();
            return !string.IsNullOrEmpty(url)
                && string.Equals(url, mcpUrl, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Classify the addon's entry in the file at <paramref name="configPath"/> relative to the current
        /// <paramref name="mcpUrl"/>: <see cref="AgentConfigState.Missing"/> when no entry exists (missing / empty /
        /// invalid file, or the <paramref name="bodyPath"/> → <paramref name="serverKey"/> entry is absent),
        /// <see cref="AgentConfigState.Stale"/> when an entry exists but its <c>url</c> differs (ordinal-ignore-case)
        /// from <paramref name="mcpUrl"/>, and <see cref="AgentConfigState.UpToDate"/> when an entry exists with a
        /// matching url. This is the pure-managed source of truth for the dock's AI-agent alert panel; it shares the
        /// same parse + nav rules as <see cref="IsConfigured"/> (which is exactly <c>state == UpToDate</c>) so the
        /// two never disagree.
        /// </summary>
        public static AgentConfigState ConfigState(string configPath, string bodyPath, string serverKey, string mcpUrl)
        {
            var root = TryReadObject(configPath);
            if (root == null)
                return AgentConfigState.Missing;

            if (root[bodyPath] is not JsonObject body)
                return AgentConfigState.Missing;

            if (body[serverKey] is not JsonObject entry)
                return AgentConfigState.Missing;

            var url = entry["url"]?.GetValue<string>();
            if (string.IsNullOrEmpty(url))
                return AgentConfigState.Missing;

            return string.Equals(url, mcpUrl, StringComparison.OrdinalIgnoreCase)
                ? AgentConfigState.UpToDate
                : AgentConfigState.Stale;
        }

        /// <summary>
        /// READ-MERGE-WRITE the addon's entry into the config file at <paramref name="configPath"/>: load the
        /// existing JSON (or start a fresh object when the file is missing / empty / invalid), set
        /// <paramref name="bodyPath"/> → <paramref name="serverKey"/> to the freshly-built entry (with the REAL
        /// token — writing the user's own client config is the point), and write the file back pretty-printed,
        /// creating any missing parent directories. Sibling servers and unrelated top-level keys are preserved.
        /// </summary>
        public static void Configure(string configPath, string bodyPath, string serverKey, string mcpUrl, string? token)
        {
            if (string.IsNullOrWhiteSpace(configPath))
                throw new ArgumentException("Config path must be non-empty.", nameof(configPath));

            var root = TryReadObject(configPath) ?? new JsonObject();

            if (root[bodyPath] is not JsonObject body)
            {
                body = new JsonObject();
                root[bodyPath] = body;
            }

            body[serverKey] = BuildServerEntry(mcpUrl, token, maskToken: false);

            WriteObject(configPath, root);
        }

        /// <summary>
        /// Remove the addon's entry (<paramref name="bodyPath"/> → <paramref name="serverKey"/>) from the config
        /// file at <paramref name="configPath"/>, preserving every sibling server and unrelated key, then write the
        /// file back. No-op (no write) when the file is missing / empty / invalid or the entry is already absent.
        /// Returns true when an entry was actually removed.
        /// </summary>
        public static bool Remove(string configPath, string bodyPath, string serverKey)
        {
            var root = TryReadObject(configPath);
            if (root == null)
                return false;

            if (root[bodyPath] is not JsonObject body)
                return false;

            if (!body.ContainsKey(serverKey))
                return false;

            body.Remove(serverKey);
            WriteObject(configPath, root);
            return true;
        }

        // --- internals ---------------------------------------------------------------------------------------

        /// <summary>
        /// Read + parse the file at <paramref name="path"/> as a JSON object. Returns null for a missing / empty /
        /// unreadable / non-object / malformed file — every "no usable existing config" case the merge treats as
        /// "start fresh", so this never throws on a bad file (only genuine programmer misuse throws elsewhere).
        /// </summary>
        static JsonObject? TryReadObject(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string text;
            try
            {
                if (!File.Exists(path))
                    return null;
                text = File.ReadAllText(path);
            }
            catch (Exception)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
                return null;

            try
            {
                return JsonNode.Parse(text) as JsonObject;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static void WriteObject(string path, JsonObject root)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, root.ToJsonString(PrettyOptions));
        }
    }
}
