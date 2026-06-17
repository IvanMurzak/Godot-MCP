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
using System.Text.Json.Serialization;

namespace com.IvanMurzak.Godot.MCP.Connection
{
    /// <summary>
    /// System.Text.Json SOURCE-GENERATED metadata for the persisted-config graph
    /// (<see cref="GodotMcpConfig"/> → <see cref="GodotMcpFeatureMap"/> → <c>List&lt;GodotMcpFeatureState&gt;</c>).
    ///
    /// <para>
    /// <b>Why source-gen and not reflection (godotengine/godot#78513).</b> The default reflection-based
    /// <see cref="System.Text.Json.JsonSerializer"/> path compiles per-type member accessors with
    /// reflection-emit and caches them in a <b>process-wide static</b>
    /// (<c>ReflectionEmitCachingMemberAccessor</c>). When that cache holds accessors over a type defined in
    /// Godot's <b>collectible plugin AssemblyLoadContext</b> — which is where this addon (and
    /// <see cref="GodotMcpConfig"/>) lives — it roots that type, pinning the context so it can never unload
    /// on a "Build Project" hot-reload (".NET: Failed to unload assemblies"; this is a known .NET 8 issue,
    /// fixed in .NET 9, but the Godot 4.5 mono runtime is .NET 8). Source generation emits the
    /// <c>JsonTypeInfo</c> + accessors as ordinary compiled code <b>in this collectible assembly</b>, so
    /// nothing is added to that non-collectible static cache and the context unloads cleanly.
    /// </para>
    ///
    /// <para><see cref="GodotMcpConfigStore"/> serializes/deserializes EXCLUSIVELY through this context.</para>
    /// </summary>
    [JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true)]
    [JsonSerializable(typeof(GodotMcpConfig))]
    internal partial class GodotMcpConfigJsonContext : JsonSerializerContext
    {
    }
}
