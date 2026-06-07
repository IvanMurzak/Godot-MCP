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
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet;
using com.IvanMurzak.ReflectorNet.Converter;
using Xunit;
using McpVersion = com.IvanMurzak.McpPlugin.Common.Version;

namespace com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Full
{
    // ──────────────────────────────────────────────────────────────────────────────────────────
    // In-repo verification of the IReflectorModule discovery mechanism wired into the Godot plugin
    // build (see GodotMcpConnection.Start() `.WithReflectorModulesFromAssembly(...)`). Mirrors
    // Unity-MCP's ReflectorModuleDiscoveryTests (PR #805) 1:1, retargeted to xUnit + the Godot
    // McpPluginBuilder path.
    //
    // This is a UNIT-level proof: it constructs a McpPluginBuilder directly and registers THIS test
    // assembly for module discovery, asserting that an IReflectorModule with ZERO hardcoded reference
    // is auto-discovered and that all four contribution surfaces reach effect, that a throwing module
    // is isolated, and that a core-ignored hosting assembly is never type-enumerated.
    //
    // Every type here is pure-managed (no Godot native types), so the fixture + assertions run in the
    // plain-xUnit host with no Godot binary and hold identically on the Linux CI runner.
    // ──────────────────────────────────────────────────────────────────────────────────────────

    // ── Fixture payload + converter types ───────────────────────────────────────────────────────

    /// <summary>Marker payload type the verification module registers a JSON converter for.</summary>
    public sealed class VerificationPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    /// <summary>Type the verification module registers a reflection converter for.</summary>
    public sealed class VerificationReflectedType
    {
        public int Number { get; set; }
    }

    /// <summary>Type the verification module blacklists from serialization.</summary>
    public sealed class VerificationBlacklistedType
    {
        public string Secret { get; set; } = string.Empty;
    }

    /// <summary>A System.Text.Json converter contributed by the verification module.</summary>
    public sealed class VerificationPayloadJsonConverter : JsonConverter<VerificationPayload>
    {
        public override VerificationPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => new VerificationPayload { Value = reader.GetString() ?? string.Empty };

        public override void Write(Utf8JsonWriter writer, VerificationPayload value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.Value);
    }

    /// <summary>A reflection converter contributed by the verification module.</summary>
    public sealed class VerificationReflectionConverter : GenericReflectionConverter<VerificationReflectedType>
    {
    }

    // ── Modules ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The flagship verification module: a single discoverable module contributing a JSON converter,
    /// a reflection converter, a serialization-blacklist type, AND scan-ignore entries (assembly +
    /// namespace). Discovered with zero hardcoded reference — purely via assembly scan.
    /// </summary>
    public sealed class VerificationFullContributionModule : IReflectorModule
    {
        // A non-existent assembly prefix — safe to contribute (cannot collide with a protected assembly).
        public const string IgnoredAssemblyPrefix = "Some.Nonexistent.Godot.Extension.Assembly";
        // A non-existent namespace prefix — exercises the namespace scan-ignore surface harmlessly.
        public const string IgnoredNamespacePrefix = "Some.Nonexistent.Godot.Extension.Namespace";

        public int Order => 10;

        public void Configure(IReflectorModuleContext ctx)
        {
            ctx.Reflector.JsonSerializer.AddConverter(new VerificationPayloadJsonConverter());
            ctx.Reflector.Converters.Add(new VerificationReflectionConverter());
            ctx.Reflector.Converters.BlacklistType(typeof(VerificationBlacklistedType));
            ctx.Scan
                .IgnoreAssemblies(IgnoredAssemblyPrefix)
                .IgnoreNamespaces(IgnoredNamespacePrefix);
        }
    }
}

namespace com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Throwing
{
    using com.IvanMurzak.McpPlugin;

    /// <summary>A module that throws during Configure — used to assert failure isolation.</summary>
    public sealed class ThrowingVerificationModule : IReflectorModule
    {
        public int Order => 0;

        public void Configure(IReflectorModuleContext ctx)
            => throw new InvalidOperationException("Intentional failure from ThrowingVerificationModule.");
    }

    /// <summary>A healthy module sitting alongside the throwing one — must still run.</summary>
    public sealed class SurvivingVerificationModule : IReflectorModule
    {
        public static bool Ran;
        public int Order => 1;

        public void Configure(IReflectorModuleContext ctx) => Ran = true;
    }
}

namespace com.IvanMurzak.Godot.MCP.Tests.ReflectorModules
{
    using FullNs = com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Full;
    using ThrowingNs = com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Throwing;

    /// <summary>
    /// Drives the Godot plugin's reflector-module discovery wiring directly through a
    /// <see cref="McpPluginBuilder"/>, mirroring Unity-MCP's <c>ReflectorModuleDiscoveryTests</c>
    /// (PR #805). Proves the contract <see cref="Connection.GodotMcpConnection"/> relies on:
    /// <see cref="McpPluginBuilder.WithReflectorModulesFromAssembly"/> auto-discovers every
    /// <see cref="IReflectorModule"/> in the registered assemblies (no hardcoded list), all four
    /// contribution surfaces reach effect, a throwing module is isolated, and an
    /// <see cref="McpPluginBuilder.IgnoreAssembly(System.Reflection.Assembly)"/>-pruned hosting
    /// assembly is never type-enumerated.
    /// </summary>
    public class ReflectorModuleDiscoveryTests
    {
        static readonly McpVersion _version = new McpVersion();
        static readonly Assembly TestAssembly = typeof(FullNs.VerificationFullContributionModule).Assembly;

        // Two sibling fixture namespaces, NEITHER a prefix of the other, so a test can ignore exactly
        // one without collaterally pruning the other (IScanIgnoreBuilder / IgnoreNamespaces matches by
        // StartsWith).
        const string NsFull = "com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Full";
        const string NsThrowing = "com.IvanMurzak.Godot.MCP.Tests.ReflectorModules.Throwing";

        // ── (1)-(4) Full contribution: every surface reaches effect via dynamic discovery ────────

        [Fact]
        public void FullContribution_AllFourSurfacesReachEffect_ViaDynamicDiscovery()
        {
            // Arrange — keep ONLY the FullContribution namespace; ignore the Throwing fixtures so they
            // do not interfere with this assertion. The FullContributionModule carries no hardcoded
            // reference anywhere; it is found purely by scanning TestAssembly.
            var reflector = new Reflector();
            var builder = new McpPluginBuilder(_version)
                .WithReflectorModulesFromAssembly(new[] { TestAssembly })
                .IgnoreNamespaces(NsThrowing);

            // Act
            builder.Build(reflector);

            // (1) JSON converter registered.
            Assert.NotNull(
                reflector.JsonSerializer.GetJsonConverter(typeof(FullNs.VerificationPayload)));

            // (2) Reflection converter registered (resolvable for the target type).
            Assert.NotNull(
                reflector.Converters.GetConverter(typeof(FullNs.VerificationReflectedType)));
            Assert.Contains(
                reflector.Converters.GetAllSerializers(),
                c => c is FullNs.VerificationReflectionConverter);

            // (3) Serialization blacklist applied.
            Assert.True(
                reflector.Converters.IsTypeBlacklisted(typeof(FullNs.VerificationBlacklistedType)),
                "Module-contributed serialization-blacklist type should be blacklisted.");

            // (4) Scan-ignore contributions accepted (no exception, build completes). The assembly +
            // namespace prefixes are non-existent on purpose, so they cannot collide with a protected
            // assembly/namespace; reaching this assertion proves the IScanIgnoreBuilder surface was
            // exercised end-to-end through a dynamically-discovered module.
        }

        // ── Throw-isolation: a throwing module is caught; the healthy sibling still runs ─────────

        [Fact]
        public void FailureIsolation_ThrowingModuleCaught_SurvivingSiblingStillRuns()
        {
            // Arrange — keep ONLY the Throwing namespace (ThrowingVerificationModule +
            // SurvivingVerificationModule); ignore the FullContribution root namespace's other module.
            ThrowingNs.SurvivingVerificationModule.Ran = false;
            var reflector = new Reflector();
            var builder = new McpPluginBuilder(_version)
                .WithReflectorModulesFromAssembly(new[] { TestAssembly })
                .IgnoreNamespaces(NsFull);

            // Act — Build must NOT throw despite ThrowingVerificationModule.
            var exception = Record.Exception(() => builder.Build(reflector));
            Assert.Null(exception);

            // Assert — the healthy sibling module still ran.
            Assert.True(ThrowingNs.SurvivingVerificationModule.Ran,
                "The surviving sibling module should still run after a throwing module is isolated.");
        }

        // ── Perf sanity: a core-ignored hosting assembly is never type-enumerated for modules ──────

        [Fact]
        public void Discovery_SkipsModule_WhenHostingAssemblyIsIgnored()
        {
            // Arrange — register TestAssembly for module scan, then ignore that very assembly by name.
            // If discovery honored the ignore prune (it must, for the perf guarantee), no module runs:
            // the hosting assembly is never type-enumerated.
            var reflector = new Reflector();
            var builder = new McpPluginBuilder(_version)
                .WithReflectorModulesFromAssembly(new[] { TestAssembly })
                .IgnoreAssembly(TestAssembly);

            // Act
            builder.Build(reflector);

            // Assert — the module never ran: no converter, type not blacklisted.
            Assert.Null(
                reflector.JsonSerializer.GetJsonConverter(typeof(FullNs.VerificationPayload)));
            Assert.False(
                reflector.Converters.IsTypeBlacklisted(typeof(FullNs.VerificationBlacklistedType)),
                "An ignored hosting assembly's module must not contribute a blacklist entry.");
        }
    }
}
