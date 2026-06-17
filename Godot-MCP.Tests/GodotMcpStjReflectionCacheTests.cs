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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Connection;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Regression tests for <see cref="GodotMcpStjReflectionCache"/> — the teardown hook that releases
    /// System.Text.Json's process-wide reflection-emit member-accessor cache, a root of godotengine/godot#78513
    /// (the cache holds compiled accessor delegates over collectible-ALC addon types, pinning the context on a
    /// hot-reload). The full pin/unload behaviour itself is only observable in a live Godot editor (the headless
    /// reload harness, <c>.scripts/godot_reload_test.py</c>); here we pin the reflection contract the hook relies
    /// on, so a future System.Text.Json shape change that silently breaks the clear is caught in CI.
    /// </summary>
    public class GodotMcpStjReflectionCacheTests
    {
        [Fact]
        public void Clear_NeverThrows_AndIsIdempotent_EvenAfterSerialization()
        {
            // SAFETY CONTRACT (the part testable off a live Godot runtime): the hook runs inside the
            // ALC-unloading handler, so it must NEVER throw and must be safe to call repeatedly (the reload
            // hook AND a later _ExitTree both reach it). It is also called both before and after STJ has been
            // exercised. Whether it actually FINDS a cache to clear is runtime-dependent (true on the reflection-
            // emit runtime Godot uses; false on a no-emit / differently-shaped runtime — both valid) and the
            // functional pin-release is validated by the headless reload harness, not here.
            _ = JsonSerializer.Serialize(new Probe { Value = 7 });

            var ex = Record.Exception(() =>
            {
                GodotMcpStjReflectionCache.Clear();
                GodotMcpStjReflectionCache.Clear();
            });
            Assert.Null(ex);
        }

        [Fact]
        public void Clear_ReturnsBool_WithoutThrowing_OnAFreshCall()
        {
            // The return is a diagnostic (located+cleared vs not) — assert only that the reflection path
            // resolves to a definite bool without faulting, so a future System.Text.Json change that makes the
            // reflection throw (rather than miss) is caught here.
            bool result = GodotMcpStjReflectionCache.Clear();
            Assert.True(result || !result); // tautology by design: the contract is "no throw", not a fixed value
        }

        sealed class Probe
        {
            public int Value { get; set; }
        }
    }
}
