/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Navigation
{
    /// <summary>
    /// The validated members the navigation adapters share. The 2D and 3D node of each family have no common base class
    /// with these properties, so each adapter passes its own typed getter / setter; the member replaces the C# property of
    /// the same name and type and adds the <see cref="NavigationRules"/> check.
    /// </summary>
    internal static class NavigationMembers
    {
        public static MemberTable<T> NonNegative<T>(this MemberTable<T> m, string name, Func<T, float> get, Action<T, float> set, string description) =>
            m.Add<float>(name, get, set, validate: (o, v) => NavigationRules.NonNegative(name, v), description: description + " (0 or more)");

        /// <summary>EnterCost / TravelCost of a region or link.</summary>
        public static MemberTable<T> Costs<T>(this MemberTable<T> m, Func<T, float> enter, Action<T, float> setEnter,
            Func<T, float> travel, Action<T, float> setTravel) =>
            m.NonNegative("EnterCost", enter, setEnter, "cost added when a path enters this from another region / link")
             .NonNegative("TravelCost", travel, setTravel, "multiplier of the travelled distance inside it");
    }
}
#endif
