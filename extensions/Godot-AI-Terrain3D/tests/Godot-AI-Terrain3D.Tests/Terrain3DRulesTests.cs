/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Terrain3D.Tests
{
    public class Terrain3DRulesTests
    {
        [Theory]
        [InlineData(64)]
        [InlineData(128)]
        [InlineData(256)]
        [InlineData(512)]
        [InlineData(1024)]
        [InlineData(2048)]
        public void TerrainRegionSizesAreValid(int size) => Assert.Null(Terrain3DRules.RegionSize(size));

        [Theory]
        [InlineData(500)]
        [InlineData(0)]
        [InlineData(-256)]
        [InlineData(32)]
        [InlineData(4096)]
        public void OtherRegionSizesAreRejected(int size) =>
            Assert.Equal($"RegionSize must be one of 64, 128, 256, 512, 1024, 2048 (got {size})", Terrain3DRules.RegionSize(size));

        [Fact]
        public void MissingAddonCarriesTheInstallHint()
        {
            Assert.Null(Terrain3DRules.Installed(true));
            var reason = Terrain3DRules.Installed(false);
            Assert.StartsWith("the Terrain3D addon is not installed", reason);
            Assert.Contains("TokisanGames/Terrain3D", reason);
            Assert.Contains("addons/terrain_3d/", reason);
        }

        [Fact]
        public void NamesAreTheAddonsSnakeCaseContract()
        {
            Assert.Equal("Terrain3D", Terrain3DNames.TerrainClass);
            Assert.Equal("Terrain3DMaterial", Terrain3DNames.MaterialClass);
            Assert.Equal("region_size", Terrain3DNames.RegionSizeProperty);
            Assert.Equal("data_directory", Terrain3DNames.DataDirectoryProperty);
            Assert.Equal("change_region_size", Terrain3DNames.ChangeRegionSizeMethod);
            Assert.Equal("get_region_count", Terrain3DNames.GetRegionCountMethod);
        }
    }
}
