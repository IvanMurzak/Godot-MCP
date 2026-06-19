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
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// One tool family the dock's "Extensions" section offers to install into the CONSUMER's Godot project. The
    /// Godot analog of Unity-MCP's <c>ExtensionData</c> (in <c>MainWindowEditor.Extensions.cs</c>), except the
    /// distribution mechanism is a NuGet <c>&lt;PackageReference&gt;</c> in the consumer's game <c>.csproj</c>
    /// (Godot compiles every <c>.cs</c> under the project into one assembly, so adding the package makes the
    /// extension's <c>[AiToolType]</c> tool family compile into the consumer's project) rather than Unity's UPM
    /// <c>manifest.json</c> entry.
    ///
    /// <para>
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>) so the descriptor + the install/detect logic that
    /// consumes it are CI-unit-tested. The editor wiring (the dock panel, the consumer-<c>.csproj</c> read/write)
    /// lives behind <c>#if TOOLS</c>.
    /// </para>
    /// </summary>
    /// <param name="Name">Human-readable display name shown as the row title (e.g. "ProBuilder Tools").</param>
    /// <param name="Description">One-line description shown muted under the title.</param>
    /// <param name="PackageId">
    /// The NuGet package id added as a <c>&lt;PackageReference Include="..." /&gt;</c> to the consumer's
    /// <c>.csproj</c> (e.g. <c>com.IvanMurzak.Godot.MCP.ProBuilder</c>). This is the install IDENTITY — installed
    /// state is detected by matching this id against the consumer's existing package references.
    /// </param>
    /// <param name="Version">
    /// The package version to pin in the <c>&lt;PackageReference&gt;</c> (e.g. <c>1.0.0</c>). Optional: when null/empty
    /// the reference is added WITHOUT a <c>Version</c> attribute (floating / centrally-managed). When set, it also
    /// drives the up-to-date / update-available decision against the consumer's currently-referenced version.
    /// </param>
    /// <param name="GitUrl">Optional repository / docs URL surfaced as a link in the row (mirrors Unity's <c>gitUrl</c>).</param>
    /// <param name="Tools">
    /// The tool entries this extension contributes, each a <c>(Name, Description)</c> pair — shown as the
    /// extension's tool list (mirrors Unity's per-extension tool enumeration). May be empty.
    /// </param>
    public sealed record GodotExtensionDescriptor(
        string Name,
        string Description,
        string PackageId,
        string? Version = null,
        string? GitUrl = null,
        IReadOnlyList<(string Name, string Description)>? Tools = null)
    {
        /// <summary>The tool entries this extension contributes, never null (an absent list reads as empty).</summary>
        public IReadOnlyList<(string Name, string Description)> Tools { get; init; }
            = Tools ?? System.Array.Empty<(string Name, string Description)>();

        /// <summary>True when a concrete <see cref="Version"/> pin is present (drives the up-to-date / update decision).</summary>
        public bool HasVersion => !string.IsNullOrWhiteSpace(Version);
    }
}
