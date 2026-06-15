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
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.UI;

namespace com.IvanMurzak.Godot.MCP.Connection.DevControl
{
    /// <summary>
    /// PURE-MANAGED (no Godot native types, no <c>#if TOOLS</c>) routing + parsing core for the DEV-ONLY
    /// inject/control HTTP bridge (<see cref="DevControlServer"/>). It owns the table-driven
    /// <c>(method, path)</c> → command-id mapping plus the status-string parsers, so the editor-side server
    /// stays a thin transport shell and ALL the routing decisions are CI-unit-testable in the plain-xUnit
    /// host (mirrors how <see cref="ConnectionPanelView"/> / <see cref="GodotMcpServerView"/> hold the
    /// connection panel's logic). The <see cref="DevControlServer"/> (editor-only) consumes this; this file
    /// references only the pure-managed <see cref="ConnectionStatus"/> / <see cref="GodotMcpServerStatus"/>
    /// enums and BCL types.
    /// </summary>
    public static class DevControlRouter
    {
        /// <summary>
        /// The command a routed request maps to — the editor server switches on this instead of re-parsing
        /// the raw <c>(method, path)</c>. <see cref="Unknown"/> is the sentinel for an unmatched route (the
        /// server answers it with 404).
        /// </summary>
        public enum Command
        {
            Unknown = 0,
            Health,
            State,
            InjectConnectionStatus,
            InjectServerStatus,
            ControlServerUrl,
            ControlSelectAgent,
            ControlClick,
        }

        /// <summary>
        /// The declarative route table: each entry maps an exact <c>(METHOD, /path)</c> to a
        /// <see cref="Command"/>. Method is matched case-insensitively; path is matched exactly (no trailing
        /// slash, no query string — the server strips both before calling <see cref="Route"/>).
        /// </summary>
        static readonly IReadOnlyList<(string Method, string Path, Command Command)> Routes = new[]
        {
            ("GET",  "/health",                   Command.Health),
            ("GET",  "/state",                    Command.State),
            ("POST", "/inject/connection-status", Command.InjectConnectionStatus),
            ("POST", "/inject/server-status",     Command.InjectServerStatus),
            ("POST", "/control/server-url",       Command.ControlServerUrl),
            ("POST", "/control/select-agent",     Command.ControlSelectAgent),
            ("POST", "/control/click",            Command.ControlClick),
        };

        /// <summary>
        /// Resolve an HTTP <paramref name="method"/> + <paramref name="path"/> to a <see cref="Command"/>.
        /// Method matching is case-insensitive; the path must match exactly (the caller is responsible for
        /// stripping any trailing slash + query string first). Returns <see cref="Command.Unknown"/> for an
        /// unmatched route (→ 404 at the server).
        /// </summary>
        public static Command Route(string method, string path)
        {
            if (method == null || path == null)
                return Command.Unknown;

            foreach (var route in Routes)
            {
                if (string.Equals(route.Method, method, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(route.Path, path, StringComparison.Ordinal))
                    return route.Command;
            }

            return Command.Unknown;
        }

        /// <summary>
        /// Parse a connection-status string ("Connected" / "Connecting" / "Disconnected", case-insensitive)
        /// into a <see cref="ConnectionStatus"/>. Returns <c>false</c> (and a default <paramref name="status"/>)
        /// for null / empty / unrecognized input so the server can answer a 400 instead of throwing.
        /// </summary>
        public static bool TryParseConnectionStatus(string? value, out ConnectionStatus status)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "connected":
                    status = ConnectionStatus.Connected;
                    return true;
                case "connecting":
                    status = ConnectionStatus.Connecting;
                    return true;
                case "disconnected":
                    status = ConnectionStatus.Disconnected;
                    return true;
                default:
                    status = ConnectionStatus.Disconnected;
                    return false;
            }
        }

        /// <summary>
        /// Parse a server-status string ("Stopped" / "Starting" / "Running" / "Stopping" / "External",
        /// case-insensitive) into a <see cref="GodotMcpServerStatus"/>. Returns <c>false</c> (and a default
        /// <paramref name="status"/>) for null / empty / unrecognized input so the server can answer a 400.
        /// </summary>
        public static bool TryParseServerStatus(string? value, out GodotMcpServerStatus status)
        {
            switch ((value ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "stopped":
                    status = GodotMcpServerStatus.Stopped;
                    return true;
                case "starting":
                    status = GodotMcpServerStatus.Starting;
                    return true;
                case "running":
                    status = GodotMcpServerStatus.Running;
                    return true;
                case "stopping":
                    status = GodotMcpServerStatus.Stopping;
                    return true;
                case "external":
                    status = GodotMcpServerStatus.External;
                    return true;
                default:
                    status = GodotMcpServerStatus.Stopped;
                    return false;
            }
        }

        /// <summary>
        /// The set of click targets accepted by <c>POST /control/click</c>, normalized to lowercase. The
        /// editor server maps a valid target to a dock node name + emits its "pressed" signal; an
        /// unrecognized target is a 400. Kept here (pure-managed) so the accepted vocabulary is unit-tested.
        /// </summary>
        static readonly HashSet<string> ClickTargets = new(StringComparer.OrdinalIgnoreCase)
        {
            "configure", "reconfigure", "remove", "connect", "start-server", "generate",
        };

        /// <summary>
        /// Validate + normalize a click <paramref name="target"/> (case-insensitive) to its canonical
        /// lowercase form. Returns <c>false</c> for null / empty / unrecognized input. "reconfigure" is a
        /// synonym of "configure" at the dock level (the same Configure button is re-pressed), but both are
        /// accepted here and the dock collapses them.
        /// </summary>
        public static bool TryNormalizeClickTarget(string? target, out string normalized)
        {
            var trimmed = (target ?? string.Empty).Trim();
            if (ClickTargets.Contains(trimmed))
            {
                normalized = trimmed.ToLowerInvariant();
                return true;
            }

            normalized = string.Empty;
            return false;
        }
    }
}
