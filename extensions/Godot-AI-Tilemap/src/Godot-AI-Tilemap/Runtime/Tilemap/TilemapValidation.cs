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

namespace com.IvanMurzak.Godot.MCP.Tilemap
{
    /// <summary>
    /// Pure-managed (no Godot native types, CI-unit-testable) source of truth for the argument-validation rules
    /// the editor-driving tilemap tools apply BEFORE they touch a live <c>TileMapLayer</c> — so an LLM can never
    /// push a layer into an invalid state (a negative TileSet source id, negative atlas coordinates, or a
    /// non-<c>res://</c> resource path). Keeping the rules pure-managed means they are verified by fast xUnit
    /// tests with no Godot binary, and the editor tools simply reuse them.
    /// </summary>
    public static class TilemapValidation
    {
        /// <summary>The Godot sentinel for "no tile" in a cell (an erased/empty cell has <c>source_id == -1</c>).</summary>
        public const int NoSourceId = -1;

        /// <summary>
        /// Validate a TileSet source id for a tile PLACEMENT (<c>tilemap-set-cell</c>): it must be a real source,
        /// i.e. <c>&gt;= 0</c>. <c>-1</c> means "no tile" and is only produced by erase, never accepted here.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when <paramref name="sourceId"/> is negative.</exception>
        public static void ValidateSourceId(int sourceId)
        {
            if (sourceId < 0)
                throw new ArgumentException(
                    $"sourceId must be >= 0 to place a tile (got {sourceId}); -1 means 'no tile' and is only " +
                    "used by tilemap-erase-cell.", nameof(sourceId));
        }

        /// <summary>
        /// Validate atlas coordinates within a TileSet source: both components must be non-negative.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when either coordinate is negative.</exception>
        public static void ValidateAtlasCoords(int atlasX, int atlasY)
        {
            if (atlasX < 0 || atlasY < 0)
                throw new ArgumentException(
                    $"atlasCoords must be non-negative (got {atlasX},{atlasY}).", nameof(atlasX));
        }

        /// <summary>
        /// Validate (and normalize) a TileSet resource path: it must be non-empty and a Godot resource path
        /// (<c>res://…</c>). Returns the trimmed path on success.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the path is null/blank or not a <c>res://</c> path.</exception>
        public static string ValidateResourcePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A TileSet resource path is required.", nameof(path));

            var trimmed = path!.Trim();
            if (!trimmed.StartsWith("res://", StringComparison.Ordinal))
                throw new ArgumentException(
                    $"TileSet path must be a Godot resource path starting with 'res://' (got '{path}').",
                    nameof(path));

            return trimmed;
        }
    }
}
