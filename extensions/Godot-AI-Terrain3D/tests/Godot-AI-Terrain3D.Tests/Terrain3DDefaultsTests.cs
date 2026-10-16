/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.Godot.MCP.Terrain3D;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Terrain3D.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="Terrain3DDefaults"/> (the recommended starter config) and
    /// <see cref="RegionSizes"/> (the region-size validation/snapping the editor tools reuse). No Godot binary
    /// required — these are the CLASS-B "constants ARE the contract" pieces, verified by fast xUnit.
    /// </summary>
    public class Terrain3DDefaultsTests
    {
        [Fact]
        public void For_Default_IsMediumPreset()
        {
            var config = Terrain3DDefaults.For();
            Assert.Equal("medium", config.Preset);
            Assert.Equal(Terrain3DNames.RegionSize1024, config.RegionSize);
            Assert.Equal(Terrain3DDefaults.DefaultMeshLods, config.MeshLods);
            Assert.Equal(Terrain3DDefaults.RecommendedDataDirectory, config.DataDirectory);
        }

        [Theory]
        [InlineData("small", 256)]
        [InlineData("MEDIUM", 1024)]
        [InlineData(" large ", 2048)]
        [InlineData(null, 1024)]
        [InlineData("", 1024)]
        public void For_KnownPresets_MapToRegionSize(string? preset, int expectedRegionSize)
        {
            Assert.Equal(expectedRegionSize, Terrain3DDefaults.For(preset).RegionSize);
        }

        [Fact]
        public void For_UnknownPreset_Throws()
        {
            Assert.Throws<ArgumentException>(() => Terrain3DDefaults.For("gigantic"));
        }

        [Theory]
        [InlineData(64, true)]
        [InlineData(256, true)]
        [InlineData(2048, true)]
        [InlineData(500, false)]
        [InlineData(0, false)]
        [InlineData(4096, false)]
        public void RegionSizes_IsValid_MatchesEnum(int size, bool expected)
        {
            Assert.Equal(expected, RegionSizes.IsValid(size));
        }

        [Theory]
        [InlineData(10, 64)]      // below the floor clamps up to 64
        [InlineData(300, 256)]    // nearer 256 than 512
        [InlineData(400, 512)]    // nearer 512 than 256
        [InlineData(1000, 1024)]  // nearer 1024
        [InlineData(5000, 2048)]  // above the ceiling clamps to 2048
        [InlineData(512, 512)]    // already valid → unchanged
        public void RegionSizes_Snap_PicksNearestValid(int requested, int expected)
        {
            Assert.Equal(expected, RegionSizes.Snap(requested));
            Assert.True(RegionSizes.IsValid(RegionSizes.Snap(requested)));
        }

        [Fact]
        public void Terrain3DNames_Constants_AreStable()
        {
            // The CLASS-B string contract: class names, snake_case members, and the RegionSize enum-int values.
            Assert.Equal("Terrain3D", Terrain3DNames.TerrainClass);
            Assert.Equal("Terrain3DMaterial", Terrain3DNames.MaterialClass);
            Assert.Equal("Terrain3DData", Terrain3DNames.DataClass);
            Assert.Equal("data_directory", Terrain3DNames.DataDirectoryProperty);
            Assert.Equal("region_size", Terrain3DNames.RegionSizeProperty);
            Assert.Equal("material", Terrain3DNames.MaterialProperty);
            Assert.Equal("change_region_size", Terrain3DNames.ChangeRegionSizeMethod);
            Assert.Equal("get_region_count", Terrain3DNames.GetRegionCountMethod);
            Assert.Equal(new[] { 64, 128, 256, 512, 1024, 2048 }, Terrain3DNames.ValidRegionSizes);
        }
    }
}
