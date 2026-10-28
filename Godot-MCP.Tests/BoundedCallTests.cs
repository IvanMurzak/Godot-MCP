/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
using System;
using System.Diagnostics;
using System.Threading;
using com.IvanMurzak.Godot.MCP.Reload;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>The reload teardown's bounded steps: a step that never returns cannot hold the caller past the timeout.</summary>
    public class BoundedCallTests
    {
        [Fact]
        public void FinishedStep_ReturnsTrue()
        {
            var ran = false;
            Assert.True(BoundedCall.Run(() => ran = true, TimeSpan.FromSeconds(5), out var error));
            Assert.True(ran);
            Assert.Null(error);
        }

        [Fact]
        public void BlockedStep_ReturnsAfterTheTimeout()
        {
            using var never = new ManualResetEventSlim(false);
            var clock = Stopwatch.StartNew();

            Assert.False(BoundedCall.Run(() => never.Wait(), TimeSpan.FromMilliseconds(200), out var error));

            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(5));
            Assert.Null(error);
            never.Set();
        }

        [Fact]
        public void ThrowingStep_ReturnsItsException()
        {
            Assert.True(BoundedCall.Run(() => throw new InvalidOperationException("boom"), TimeSpan.FromSeconds(5), out var error));
            Assert.IsType<InvalidOperationException>(error);
        }

        [Fact]
        public void StepRunsOnAnotherThread()
        {
            var caller = Environment.CurrentManagedThreadId;
            var stepThread = caller;
            BoundedCall.Run(() => stepThread = Environment.CurrentManagedThreadId, TimeSpan.FromSeconds(5), out _);
            Assert.NotEqual(caller, stepThread);
        }
    }
}
