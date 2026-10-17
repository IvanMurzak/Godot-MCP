/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// The value rules the module enforces before a TileMapLayer cell is painted (the 0.x tools' rules): a TileSet source id
    /// of at least 0 (-1 means "no tile": erase with EraseCell) and atlas / alternative coordinates of at least 0. Each
    /// returns the rejection reason, or null when the value is valid. Pure-managed (no Godot API), so it is unit-tested.
    /// </summary>
    public static class TilemapRules
    {
        /// <summary>Godot's "no tile" source id (<c>TileSet.INVALID_SOURCE</c>).</summary>
        public const int NoSourceId = -1;

        public static string? SourceId(int sourceId) =>
            sourceId >= 0
                ? null
                : $"sourceId must be a TileSet source id of at least 0 (got {sourceId}); erase a cell with EraseCell(x, y)";

        public static string? AtlasCoords(int atlasX, int atlasY) =>
            atlasX >= 0 && atlasY >= 0 ? null : $"atlas coordinates must be at least 0 (got ({atlasX}, {atlasY}))";

        public static string? Alternative(int alternative) =>
            alternative >= 0 ? null : $"alternative must be an alternative tile id of at least 0 (got {alternative})";
    }
}
