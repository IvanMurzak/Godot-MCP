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

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Resolves a caller-supplied child index to a concrete, in-range sibling position for
    /// <c>Node.MoveChild</c> (issue #294 — <c>node-create</c>'s <c>index</c> and the <c>node-reorder</c>
    /// tool).
    ///
    /// <para>
    /// Extracted from the editor-only handlers — like <see cref="NodePathNormalizer"/> — because the index
    /// arithmetic (negative-from-end, clamping) is exactly the off-by-one-prone part, and it is the ONLY
    /// part of node ordering that can be unit-tested without a live scene tree.
    /// </para>
    ///
    /// <para>
    /// Clamping rather than rejecting is deliberate: an out-of-range index handed straight to Godot's
    /// <c>Node.MoveChild</c> makes the engine log an error and leave the child where it was — a silent
    /// no-op from the caller's point of view. Clamping always produces the nearest legal position, and the
    /// tools report the RESULTING index back in <c>NodeData.index</c> so the caller can verify what actually
    /// happened rather than trusting a bare "ok".
    /// </para>
    /// </summary>
    public static class NodeIndexResolver
    {
        /// <summary>
        /// Resolve <paramref name="requestedIndex"/> against a parent that has
        /// <paramref name="childCount"/> children AFTER the node is in place.
        ///
        /// A non-negative index is the 0-based position from the start; a negative index counts back from
        /// the end (<c>-1</c> = last, <c>-2</c> = second to last), matching Godot's own <c>MoveChild</c>
        /// convention. Anything outside <c>[0, childCount - 1]</c> clamps into range; a parent with no
        /// children resolves to 0.
        /// </summary>
        public static int Resolve(int requestedIndex, int childCount)
        {
            if (childCount <= 0)
                return 0;

            var resolved = requestedIndex < 0
                ? childCount + requestedIndex
                : requestedIndex;

            return System.Math.Clamp(resolved, 0, childCount - 1);
        }
    }
}
