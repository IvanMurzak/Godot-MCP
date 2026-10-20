/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>What an object enclosing a change is, for <see cref="RqlMutationOwner.Nearest{T}"/>.</summary>
    internal enum RqlOwnerKind
    {
        /// <summary>Not an object with undo (a plain C# view, a freed object): passed over, whatever its target.</summary>
        None,
        /// <summary>A plain Godot object (not a node or resource).</summary>
        Plain,
        /// <summary>A node, resource or the project settings: what the scene / file / project.godot stores.</summary>
        Stored,
        /// <summary>A view row standing for an object (IRqlViewOf); the target is that object.</summary>
        View,
    }

    /// <summary>Picks the object whose undo snapshot covers a change. Pure managed: no Godot types (unit-tested).</summary>
    internal static class RqlMutationOwner
    {
        /// <summary>
        /// The owner of a change from the objects enclosing it, nearest first (RQL's <c>MutationRecord.Owners</c>): the
        /// nearest node / resource / settings object, or the object a view stands for. A plain object on the way is the
        /// owner itself (a write to an Object held by a node property does not change the node's stored properties), except
        /// under a view: the view says it is stored by its object (a TileSet tile's TileData is saved by its atlas source).
        /// <paramref name="classify"/> returns a non-null target for every kind but <see cref="RqlOwnerKind.None"/>.
        /// </summary>
        public static T? Nearest<T>(IEnumerable<object> enclosing, Func<object, (T? target, RqlOwnerKind kind)> classify) where T : class
        {
            T? plain = null;
            foreach (var candidate in enclosing)
            {
                var (target, kind) = classify(candidate);
                switch (kind)
                {
                    case RqlOwnerKind.View: return target;
                    case RqlOwnerKind.Stored: return plain ?? target;
                    case RqlOwnerKind.Plain: plain ??= target; break;
                }
            }
            return plain;
        }
    }
}
#endif
