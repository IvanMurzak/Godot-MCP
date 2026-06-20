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
using com.IvanMurzak.Godot.MCP.Connection.DevControl;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pins the load-bearing env gate for the DEV-ONLY inject/control bridge (<see cref="DevControlGate"/>).
    /// The bridge (<c>DevControlServer</c>, <c>#if TOOLS</c> + 127.0.0.1) is UNAUTHENTICATED; its only security
    /// boundary is that it is constructed/started ONLY when <c>GODOT_MCP_DEV_CONTROL</c> is exactly <c>"1"</c>.
    /// These tests guarantee that contract is provably load-bearing: the predicate fails closed for every
    /// non-<c>"1"</c> value, and the construction-site guard throws when the gate is off — so a regression that
    /// wires DevControl on without the env var fails fast (here in CI, and at runtime construction).
    /// Pure-managed (no Godot native types, no <c>#if TOOLS</c>), so it runs in the plain-xUnit CI host.
    /// </summary>
    public class DevControlGateTests
    {
        // --- IsEnabled: only exactly "1" (optionally whitespace-padded) is ON ---

        [Fact]
        public void IsEnabled_ExactlyOne_IsTrue()
        {
            Assert.True(DevControlGate.IsEnabled("1"));
            Assert.Equal("1", DevControlGate.EnabledValue);
        }

        [Theory]
        [InlineData(" 1")]
        [InlineData("1 ")]
        [InlineData("\t1\n")]
        public void IsEnabled_OneWithSurroundingWhitespace_IsTrue(string value)
        {
            // The boot site forwards the env/.env value; surrounding whitespace must not flip a real "1" to OFF.
            Assert.True(DevControlGate.IsEnabled(value));
        }

        [Theory]
        [InlineData(null)]       // unset env var → OFF (the default, shipped-addon posture)
        [InlineData("")]         // empty → OFF
        [InlineData("0")]        // explicit disable → OFF
        [InlineData("2")]        // any other number → OFF
        [InlineData("true")]     // truthy-looking but NOT the contract → OFF (fail closed)
        [InlineData("TRUE")]
        [InlineData("yes")]
        [InlineData("on")]
        [InlineData("11")]       // must not substring/prefix-match "1"
        [InlineData("1x")]
        [InlineData(" 1 0 ")]
        [InlineData("enabled")]
        public void IsEnabled_AnythingButOne_IsFalse(string? value)
        {
            // Fail closed: every value other than exactly "1" leaves the unauthenticated bridge OFF. This is the
            // core anti-fail-open guarantee — a stray/garbage env value can never silently enable the surface.
            Assert.False(DevControlGate.IsEnabled(value));
        }

        // --- AssertEnabledOrThrow: the construction-site guard ---

        [Fact]
        public void AssertEnabledOrThrow_WhenEnabled_DoesNotThrow()
        {
            // The normal-operation path: gate is on → construction proceeds (no throw).
            DevControlGate.AssertEnabledOrThrow("1");
            DevControlGate.AssertEnabledOrThrow(" 1 ");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("true")]
        [InlineData("yes")]
        public void AssertEnabledOrThrow_WhenDisabled_ThrowsFailFast(string? value)
        {
            // The load-bearing assertion: reaching the DevControlServer construction path with the gate OFF is a
            // regression and must fail fast rather than open an unauthenticated loopback control surface. This is
            // what would catch a future refactor that drops/reorders the boot-site early-return.
            Assert.Throws<InvalidOperationException>(() => DevControlGate.AssertEnabledOrThrow(value));
        }

        [Fact]
        public void AssertEnabledOrThrow_IsConsistentWithIsEnabled()
        {
            // The guard's throw/no-throw decision is exactly the predicate — one source of truth, no drift.
            foreach (var value in new string?[] { null, "", "0", "1", " 1 ", "2", "true", "yes", "on", "11" })
            {
                if (DevControlGate.IsEnabled(value))
                    DevControlGate.AssertEnabledOrThrow(value); // must not throw
                else
                    Assert.Throws<InvalidOperationException>(() => DevControlGate.AssertEnabledOrThrow(value));
            }
        }
    }
}
