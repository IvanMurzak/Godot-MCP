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
using com.IvanMurzak.Godot.MCP.Editor.Rql.Settings;
using Godot;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>Layer masks ⇄ layer names (LayerNames): kinds by hint, names / numbers both ways, errors, kept high bits.</summary>
    public class RqlLayerNamesTests
    {
        private static readonly Dictionary<int, string> Named = new Dictionary<int, string> { [1] = "world", [2] = "enemies", [5] = "Water" };

        private static string? NameOf(int layer) => Named.TryGetValue(layer, out var n) ? n : null;

        private static LayerNames.Kind Physics3D => Kind(PropertyHint.Layers3DPhysics);

        private static LayerNames.Kind Kind(PropertyHint hint)
        {
            Assert.True(LayerNames.TryKindOf(hint, out var kind));
            return kind;
        }

        [Theory]
        [InlineData(PropertyHint.Layers2DRender, "2d_render", 20)]
        [InlineData(PropertyHint.Layers2DPhysics, "2d_physics", 32)]
        [InlineData(PropertyHint.Layers2DNavigation, "2d_navigation", 32)]
        [InlineData(PropertyHint.Layers3DRender, "3d_render", 20)]
        [InlineData(PropertyHint.Layers3DPhysics, "3d_physics", 32)]
        [InlineData(PropertyHint.Layers3DNavigation, "3d_navigation", 32)]
        [InlineData(PropertyHint.LayersAvoidance, "avoidance", 32)]
        public void EveryLayerHintHasItsKind(PropertyHint hint, string name, int count)
        {
            var kind = Kind(hint);
            Assert.Equal(name, kind.Name);
            Assert.Equal(count, kind.Count);
        }

        [Fact]
        public void OtherHintsAreNoLayers()
        {
            Assert.False(LayerNames.TryKindOf(PropertyHint.None, out _));
            Assert.False(LayerNames.TryKindOf(PropertyHint.Flags, out _));
        }

        [Fact]
        public void SettingNamesAndBitsAreOneBased()
        {
            Assert.Equal("layer_names/3d_physics/layer_2", LayerNames.SettingName("3d_physics", 2));
            Assert.Equal(1L, LayerNames.Bit(1));
            Assert.Equal(1L << 31, LayerNames.Bit(32));
        }

        [Fact]
        public void MasksReadAsNamesOrNumbers()
        {
            Assert.Equal(new[] { "world", "enemies", "3", "Water" }, LayerNames.Names(0b10111, Physics3D, NameOf));
            Assert.Empty(LayerNames.Names(0, Physics3D, NameOf));
            // Render layers stop at 20: bit 21 is not listed.
            Assert.Equal(new[] { "world" }, LayerNames.Names(1 | (1L << 20), Kind(PropertyHint.Layers3DRender), NameOf));
        }

        [Fact]
        public void NamesAndNumbersWriteTheirBits()
        {
            Assert.Equal(0b10011L, LayerNames.Mask(new List<object?> { "world", "enemies", 5.0 }, 0, Physics3D, NameOf));
            Assert.Equal(0b100L, LayerNames.Mask(new List<object?> { "3" }, 0, Physics3D, NameOf));
            Assert.Equal(0b10L, LayerNames.Mask("ENEMIES", 0, Physics3D, NameOf));
            Assert.Equal(1L << 31, LayerNames.Mask(new List<object?> { 32L }, 0, Physics3D, NameOf));
            Assert.Equal(0L, LayerNames.Mask(new List<object?>(), 0b111, Physics3D, NameOf));
            Assert.Equal(0L, LayerNames.Mask(null, 0b111, Physics3D, NameOf));
        }

        [Fact]
        public void AnExactNameWinsOverACaseInsensitiveOneAndOverANumber()
        {
            var names = new Dictionary<int, string> { [1] = "Wall", [2] = "wall", [3] = "7" };
            string? Of(int l) => names.TryGetValue(l, out var n) ? n : null;
            Assert.Equal(0b10L, LayerNames.Mask("wall", 0, Physics3D, Of));
            Assert.Equal(0b1L, LayerNames.Mask("WALL", 0, Physics3D, Of));
            Assert.Equal(0b100L, LayerNames.Mask(new List<object?> { "7" }, 0, Physics3D, Of));
            Assert.Equal(1L << 6, LayerNames.Mask(new List<object?> { 7.0 }, 0, Physics3D, Of));
        }

        [Fact]
        public void BitsAboveTheKindAreKept()
        {
            var render = Kind(PropertyHint.Layers3DRender);
            var high = 1L << 25;
            Assert.Equal(high | 0b1, LayerNames.Mask(new List<object?> { "world" }, high | 0b110, render, NameOf));
        }

        [Theory]
        [InlineData("nobody", "no 3d_physics layer named 'nobody'")]
        [InlineData("0", "numbered 1-32")]
        [InlineData("33", "numbered 1-32")]
        public void UnknownLayersAreRejectedWithTheNamedOnes(string entry, string expected)
        {
            var ex = Assert.Throws<ArgumentException>(() => LayerNames.Mask(new List<object?> { entry }, 0, Physics3D, NameOf));
            Assert.Contains(expected, ex.Message);
            if (entry == "nobody") Assert.Contains("1 world, 2 enemies, 5 Water", ex.Message);
        }

        [Fact]
        public void FractionsBareNumbersAndOtherValuesAreRejected()
        {
            Assert.Throws<ArgumentException>(() => LayerNames.Mask(new List<object?> { 1.5 }, 0, Physics3D, NameOf));
            Assert.Throws<ArgumentException>(() => LayerNames.Mask(new List<object?> { true }, 0, Physics3D, NameOf));
            var bare = Assert.Throws<ArgumentException>(() => LayerNames.Mask(3.0, 0, Physics3D, NameOf));
            Assert.Contains("write [3]", bare.Message);
            var none = Assert.Throws<ArgumentException>(() => LayerNames.Mask("x", 0, Physics3D, _ => null));
            Assert.Contains("no 3d_physics layer is named yet", none.Message);
        }
    }
}
