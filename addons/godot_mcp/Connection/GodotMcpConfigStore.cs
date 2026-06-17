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

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Pure-managed (no Godot native types) persistence helper for <see cref="GodotMcpConfig"/>. Loads
    /// and saves the serialized config layer to a path supplied by the caller, so it is unit-testable in
    /// the plain-xUnit <c>Godot-MCP.Tests</c> host. The editor wiring that resolves the concrete on-disk
    /// path (<c>ProjectSettings.GlobalizePath("user://godot-mcp-config.json")</c>) lives behind
    /// <c>#if TOOLS</c> in <see cref="GodotMcpConnection"/>; the load/save core stays here, path-injectable.
    ///
    /// <para>
    /// <b>Precedence (critical).</b> The persisted file is the <em>serialized config</em> layer. The full
    /// precedence — highest wins — remains:
    /// <list type="number">
    ///   <item>process environment variable (<c>GODOT_MCP_*</c>);</item>
    ///   <item><c>.env</c> file value (<see cref="GodotMcpEnvFile"/>);</item>
    ///   <item>persisted config (this store);</item>
    ///   <item>built-in default.</item>
    /// </list>
    /// To honour that, the boot path (<see cref="GodotMcpConnection"/>) applies the PERSISTED values
    /// FIRST — into the config's serialized backing fields — and only THEN layers the <c>.env</c> file
    /// and (live) process-env on top. Because <see cref="GodotMcpConfig"/>'s <c>Host</c>/<c>Token</c>/
    /// <c>ActiveMode</c> read the process env live on every access, and <see cref="GodotMcpEnvFile.Apply"/>
    /// overwrites the same backing fields this store wrote, an explicit env/.env value always shadows the
    /// persisted one. This store therefore NEVER reads env — it only round-trips the serialized fields.
    /// </para>
    /// </summary>
    public static class GodotMcpConfigStore
    {
        /// <summary>
        /// Load the persisted <see cref="GodotMcpConfig"/> from <paramref name="path"/>. Returns the
        /// deserialized config on success, or <c>null</c> when the file is missing, empty, unreadable, or
        /// corrupt — a missing/corrupt config file is the common first-run case, NOT an error, so this
        /// never throws. The caller treats <c>null</c> as "no persisted layer; use the default config".
        /// </summary>
        public static GodotMcpConfig? Load(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;

            string json;
            try
            {
                if (!File.Exists(path))
                    return null;
                json = File.ReadAllText(path);
            }
            catch (Exception)
            {
                // IO / permission / encoding failure — treat as "no persisted config".
                return null;
            }

            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                return JsonSerializer.Deserialize(json, GodotMcpConfigJsonContext.Default.GodotMcpConfig);
            }
            catch (JsonException)
            {
                // Corrupt / partially-written file — treat as "no persisted config" rather than breaking boot.
                return null;
            }
        }

        /// <summary>
        /// Serialize <paramref name="config"/> to <paramref name="path"/> using the config's
        /// <c>[JsonPropertyName]</c> serialization (host/token/cloudToken/connectionMode). Creates any
        /// missing parent directory first (the <c>user://</c> dir normally exists, but a caller may pass an
        /// arbitrary path). Throws nothing for the missing-parent case; genuine IO errors propagate so a
        /// save the user explicitly triggered surfaces a failure rather than silently dropping it.
        /// </summary>
        public static void Save(string path, GodotMcpConfig config)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Config path must be non-empty.", nameof(path));
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(config, GodotMcpConfigJsonContext.Default.GodotMcpConfig);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Apply the SERIALIZED-LAYER values from <paramref name="persisted"/> onto <paramref name="target"/>,
        /// writing only the serialized backing fields (<c>CustomHost</c>/<c>CustomToken</c>/
        /// <c>CloudToken</c>/<c>ConnectionMode</c>/<c>AuthOption</c>/<c>LogLevel</c>/<c>Features</c>). This is how the boot path
        /// seeds the config with the persisted baseline BEFORE the <c>.env</c>/process-env layers are applied on
        /// top — keeping the documented precedence intact. No-op when <paramref name="persisted"/> is
        /// <c>null</c>. Returns <paramref name="target"/> for fluent use.
        /// </summary>
        public static GodotMcpConfig ApplyPersisted(GodotMcpConfig target, GodotMcpConfig? persisted)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            if (persisted == null)
                return target;

            target.CustomHost = persisted.CustomHost;
            target.CustomToken = persisted.CustomToken;
            target.CloudToken = persisted.CloudToken;
            target.ConnectionMode = persisted.ConnectionMode;
            target.AuthOption = persisted.AuthOption;
            target.LogLevel = persisted.LogLevel;
            // The auto-generate-skills toggle (a serialized bool on the base ConnectionConfig). Honoured here so a
            // user's persisted choice survives a restart and overrides the constructor's ON default — without this
            // copy the boot path would always re-seed ON and silently drop a user's OFF. The skills card writes this
            // (+ Save) when toggled; SkillsPath/ProjectRootPath are runtime-only (swap-and-restore) and not persisted.
            target.GenerateSkillFiles = persisted.GenerateSkillFiles;
            // The persisted feature enable-map (tools/prompts/resources). A missing `features` key in an older
            // config file deserializes to a non-null empty map (the property initializer), so this is always safe.
            target.Features = persisted.Features ?? new GodotMcpFeatureMap();
            // The dock's selected AI-agent id (pure presentation state). A missing key in an older config file
            // deserializes to the property default ("claude-code"); guard the null case defensively all the same.
            target.SelectedAgentId = string.IsNullOrEmpty(persisted.SelectedAgentId)
                ? target.SelectedAgentId
                : persisted.SelectedAgentId;

            return target;
        }
    }
}
