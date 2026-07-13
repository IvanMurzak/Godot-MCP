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

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// The credential material a successful device-authorization run produces: the ES256 access token, the
    /// rotating refresh token, and the access token's absolute expiry. It is the ONLY channel the secrets
    /// leave <see cref="GodotDeviceAuthFlow"/> through — a transient return value, NEVER stored on the flow
    /// instance nor logged (the flow's <c>ErrorMessage</c>/<c>UserCode</c> stay non-secret). The caller
    /// (<see cref="GodotAccountAuth"/>) immediately hands it to the machine credential store and lets it go
    /// out of scope.
    /// </summary>
    public sealed class GodotDeviceAuthResult
    {
        /// <summary>The short-lived ES256 JWT access token (hub audience).</summary>
        public string AccessToken { get; }

        /// <summary>The rotating refresh token used to mint a new access token before <see cref="ExpiresAt"/>.</summary>
        public string? RefreshToken { get; }

        /// <summary>Absolute expiry of <see cref="AccessToken"/> (from the token response's <c>expires_in</c>), or null when unknown.</summary>
        public DateTimeOffset? ExpiresAt { get; }

        public GodotDeviceAuthResult(string accessToken, string? refreshToken, DateTimeOffset? expiresAt)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            ExpiresAt = expiresAt;
        }
    }
}
