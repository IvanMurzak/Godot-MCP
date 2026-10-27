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
using System.Globalization;
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Godot's C# <c>Rotation</c> / <c>GlobalRotation</c> (Node3D, Node2D, Control) are radians; agents often write degrees
    /// there (<c>set Rotation = "(-45, 30, 0)"</c> turns a light by -45 rad). A written angle beyond one full turn (2π) is
    /// almost always meant as degrees: the mutation still applies (it is a valid value), and its receipt carries a warning
    /// naming the degree members. Pure (unit-tested).
    /// </summary>
    public static class RotationUnits
    {
        private const double FullTurn = 2 * Math.PI + 1e-3;

        /// <summary>Whether <paramref name="member"/> (a receipt member path such as "Rotation", "Rotation.X" or "GlobalRotation") is a radians rotation.</summary>
        public static bool IsRadiansMember(string? member)
        {
            var head = (member ?? "").Split('.')[0];
            return string.Equals(head, "Rotation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(head, "GlobalRotation", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The warning for a radians member written with <paramref name="components"/>, or null when no angle exceeds a full turn.</summary>
        public static string? Warning(string member, double[] components)
        {
            if (!IsRadiansMember(member) || !components.Any(c => Math.Abs(c) > FullTurn)) return null;
            var head = member.Split('.')[0];
            var degrees = char.ToUpperInvariant(head[0]) + head.Substring(1) + "Degrees" + member.Substring(head.Length);
            string Format(Func<double, double> f)
            {
                var list = string.Join(", ", components.Select(c => f(c).ToString("0.##", CultureInfo.InvariantCulture)));
                return components.Length == 1 ? list : "(" + list + ")";
            }
            var written = Format(c => c);
            var asDegrees = Format(c => c * 180 / Math.PI) + "°";
            return $"{member} is in radians: {written} turns by {asDegrees}. For degrees set {degrees} = {written} instead";
        }
    }
}
#endif
