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
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Reflection;
using com.IvanMurzak.Godot.MCP.Tools;
using com.IvanMurzak.ReflectorNet;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the scene-tree helpers RQL reuses: the path normalizer
    /// (<see cref="NodePathNormalizer"/>, behind <c>godot.find</c> and NodeRef resolution) and the ambient reflector
    /// accessor (<see cref="GodotMcpReflector"/>). None of these touch a live Godot SceneTree, so they run in the
    /// plain xUnit host with no Godot binary. The node editing itself (RQL's Node adapter, <c>#if TOOLS</c>) is
    /// verified by the live CI smoke (scripts/smoke_rql.py).
    /// </summary>
    public class NodePathNormalizerTests
    {
        // ---- NodePathNormalizer ------------------------------------------------------------------

        [Theory]
        // Absolute '/root/<Root>/...' forms reduce to a root-relative child path.
        [InlineData("/root/Main/Player", "Main", "Player")]
        [InlineData("/root/Main/Player/Weapon", "Main", "Player/Weapon")]
        // Leading single slash is stripped.
        [InlineData("/Main/Player", "Main", "Player")]
        // Root-prefixed (no /root/) forms strip the root segment.
        [InlineData("Main/Player", "Main", "Player")]
        // The root segment alone resolves to '.' (the root itself).
        [InlineData("/root/Main", "Main", ".")]
        [InlineData("Main", "Main", ".")]
        // A path that does not name the root is left root-relative as-is.
        [InlineData("Player/Weapon", "Main", "Player/Weapon")]
        // Whitespace is trimmed.
        [InlineData("  Main/Player  ", "Main", "Player")]
        // Bare slash and bare '/root/' (no segment after the prefix) normalize to empty — ResolveNode
        // then treats empty as the edited root (documented degenerate-input behavior).
        [InlineData("/", "Main", "")]
        [InlineData("/root/", "Main", "")]
        // A root NAME that is a PREFIX of (not equal to) the first segment must NOT be stripped: root
        // "Main" + "MainMenu/X" keeps "MainMenu/X" (the trailing-'/' guard prevents a prefix false-match).
        [InlineData("MainMenu/X", "Main", "MainMenu/X")]
        [InlineData("/root/MainMenu/X", "Main", "MainMenu/X")]
        public void NodePathNormalizer_Normalizes(string raw, string rootName, string expected)
        {
            Assert.Equal(expected, NodePathNormalizer.Normalize(raw, rootName));
        }

        [Fact]
        public void NodePathNormalizer_HandlesNullAndEmpty()
        {
            Assert.Equal(string.Empty, NodePathNormalizer.Normalize(string.Empty, "Main"));
            Assert.Equal(string.Empty, NodePathNormalizer.Normalize(null!, "Main"));
        }

        // ---- NodeIndexResolver (issue #294) -------------------------------------------------------

        [Theory]
        // In-range positions pass through untouched.
        [InlineData(0, 5, 0)]
        [InlineData(2, 5, 2)]
        [InlineData(4, 5, 4)]
        // Negative counts back from the end, matching Godot's own MoveChild convention.
        [InlineData(-1, 5, 4)]
        [InlineData(-2, 5, 3)]
        [InlineData(-5, 5, 0)]
        // Out of range clamps into range rather than letting Godot log an error and silently no-op.
        [InlineData(5, 5, 4)]
        [InlineData(99, 5, 4)]
        [InlineData(-6, 5, 0)]
        [InlineData(-99, 5, 0)]
        // A single child (the just-added node) can only be at 0, whatever was asked for.
        [InlineData(0, 1, 0)]
        [InlineData(7, 1, 0)]
        [InlineData(-3, 1, 0)]
        // A parent with no children resolves to 0 and never produces a negative index.
        [InlineData(0, 0, 0)]
        [InlineData(3, 0, 0)]
        [InlineData(-1, 0, 0)]
        [InlineData(-1, -1, 0)]
        public void NodeIndexResolver_Resolves(int requestedIndex, int childCount, int expected)
        {
            Assert.Equal(expected, NodeIndexResolver.Resolve(requestedIndex, childCount));
        }

        [Fact]
        public void NodeIndexResolver_NeverReturnsAnIndexGodotWouldReject()
        {
            // MoveChild answers an out-of-range index with an engine error and NO move — the silent-no-op
            // class this whole PR is about. Sweep the plausible input space and pin the invariant.
            for (var childCount = 0; childCount <= 8; childCount++)
            {
                for (var requested = -20; requested <= 20; requested++)
                {
                    var resolved = NodeIndexResolver.Resolve(requested, childCount);
                    Assert.True(resolved >= 0, $"resolved {resolved} for ({requested}, {childCount}) is negative");
                    Assert.True(resolved <= System.Math.Max(0, childCount - 1),
                        $"resolved {resolved} for ({requested}, {childCount}) exceeds the last valid index");
                }
            }
        }
    }

    /// <summary>
    /// The two <see cref="GodotMcpReflector.GetOrCreate"/> facts that mutate the process-wide
    /// <see cref="GodotMcpReflector.Current"/> static — extracted out of the otherwise-parallel
    /// <see cref="NodePathNormalizerTests"/> so they join the serial <see cref="GodotMcpReflectorCurrentCollection"/>
    /// and can never race another class that touches that static (issue #195: currently unraced, but the same
    /// bare-mutator antipattern as the other three statics, so it is guarded pre-emptively and enforced by the
    /// drift-guard). Snapshots/restores <c>Current</c> in the ctor/Dispose so the suite is non-destructive.
    /// </summary>
    [Collection(GodotMcpReflectorCurrentCollection.Name)]
    public class GodotMcpReflectorCurrentTests : System.IDisposable
    {
        readonly Reflector? _savedCurrent;

        public GodotMcpReflectorCurrentTests()
        {
            _savedCurrent = GodotMcpReflector.Current;
        }

        public void Dispose()
        {
            GodotMcpReflector.Current = _savedCurrent;
        }

        [Fact]
        public void GodotMcpReflector_GetOrCreate_FallsBack_WhenNoCurrent()
        {
            GodotMcpReflector.Current = null;
            var reflector = GodotMcpReflector.GetOrCreate();
            Assert.NotNull(reflector);
            // The fallback is NOT cached into Current (the connection remains the sole owner).
            Assert.Null(GodotMcpReflector.Current);
        }

        [Fact]
        public void GodotMcpReflector_GetOrCreate_ReturnsCurrent_WhenSet()
        {
            var mine = new Reflector();
            GodotMcpReflector.Current = mine;
            Assert.Same(mine, GodotMcpReflector.GetOrCreate());
        }
    }

    /// <summary>
    /// Dedicated serial collection for writers of the process-wide <see cref="GodotMcpReflector.Current"/>
    /// static (issue #195), keeping the rest of <see cref="NodePathNormalizerTests"/> parallel.
    /// </summary>
    [CollectionDefinition(Name, DisableParallelization = true)]
    public sealed class GodotMcpReflectorCurrentCollection
    {
        public const string Name = "GodotMcpReflector.Current (serial)";
    }
}
