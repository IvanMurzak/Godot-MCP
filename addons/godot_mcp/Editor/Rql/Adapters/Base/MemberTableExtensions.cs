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
using System.Linq;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Adapters
{
    /// <summary>
    /// For members that exist only in some Godot versions (the addon supports Godot 4.3+ and compiles against the 4.3 C#
    /// API, while the editor running it may be newer). Members present in every supported version use the strict
    /// MemberTable calls, which reject unknown names so typos fail loudly.
    /// </summary>
    public static class MemberTableExtensions
    {
        public static MemberTable HideExisting(this MemberTable table, params string[] names) =>
            table.Hide(names.Where(table.Contains).ToArray());

        public static MemberTable ReferenceExisting(this MemberTable table, params string[] names) =>
            table.Reference(names.Where(table.Contains).ToArray());
    }
}
