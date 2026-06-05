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
using System.Security.Cryptography;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// Generates a cryptographically-random, URL-safe bearer token for the Custom-mode connection.
    /// The Godot analog of Unity-MCP's <c>UnityMcpPlugin.GenerateToken()</c>: 32 random bytes,
    /// Base64-encoded, then made URL-safe (strip <c>=</c> padding, <c>+</c>→<c>-</c>, <c>/</c>→<c>_</c>).
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>): only <see cref="RandomNumberGenerator"/>
    /// and <see cref="Convert"/>, so it is unit-testable in the plain-xUnit <c>Godot-MCP.Tests</c> host.
    /// Because the output is random, the tests pin the FORMAT invariants (length / charset / url-safety),
    /// not a fixed value.
    /// </para>
    /// </summary>
    public static class GodotMcpTokenGenerator
    {
        /// <summary>Number of random bytes hashed into the token (matches the Unity reference).</summary>
        public const int TokenByteLength = 32;

        /// <summary>
        /// Produce a fresh URL-safe token. 32 random bytes → Base64 → strip <c>=</c> padding, then
        /// <c>+</c>→<c>-</c> and <c>/</c>→<c>_</c> so the result is safe in URLs / headers without escaping.
        /// </summary>
        public static string Generate()
        {
            var bytes = new byte[TokenByteLength];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes)
                .TrimEnd('=')
                .Replace('+', '-')
                .Replace('/', '_');
        }
    }
}
