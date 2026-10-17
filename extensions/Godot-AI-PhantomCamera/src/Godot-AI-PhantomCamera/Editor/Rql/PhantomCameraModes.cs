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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>PhantomCamera3D / PhantomCamera2D <c>FollowMode</c> (the 2D camera has no ThirdPerson).</summary>
    public enum PhantomFollowMode
    {
        None = 0,
        Glued = 1,
        Simple = 2,
        Group = 3,
        Path = 4,
        Framed = 5,
        ThirdPerson = 6
    }

    /// <summary>PhantomCamera3D <c>LookAtMode</c> (the 2D camera has none).</summary>
    public enum PhantomLookAtMode
    {
        None = 0,
        Mimic = 1,
        Simple = 2,
        Group = 3
    }

    /// <summary>Mode names to the addon's enum ints and back. Pure-managed, unit-tested.</summary>
    public static class PhantomCameraModes
    {
        /// <summary>The label of a follow mode int ("Simple", "ThirdPerson"; an unknown int as its number).</summary>
        public static string FollowLabel(int value) =>
            Enum.IsDefined(typeof(PhantomFollowMode), value) ? ((PhantomFollowMode)value).ToString() : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>The label of a look-at mode int ("Mimic"; an unknown int as its number).</summary>
        public static string LookAtLabel(int value) =>
            Enum.IsDefined(typeof(PhantomLookAtMode), value) ? ((PhantomLookAtMode)value).ToString() : value.ToString(System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>A follow mode by name, case-insensitive; "third_person" / "third-person" / "Third Person" also match.</summary>
        public static bool TryParseFollow(string? value, out PhantomFollowMode mode)
        {
            mode = PhantomFollowMode.None;
            switch (Normalize(value))
            {
                case "none": mode = PhantomFollowMode.None; return true;
                case "glued": mode = PhantomFollowMode.Glued; return true;
                case "simple": mode = PhantomFollowMode.Simple; return true;
                case "group": mode = PhantomFollowMode.Group; return true;
                case "path": mode = PhantomFollowMode.Path; return true;
                case "framed": mode = PhantomFollowMode.Framed; return true;
                case "thirdperson": mode = PhantomFollowMode.ThirdPerson; return true;
                default: return false;
            }
        }

        /// <summary>A look-at mode by name, case-insensitive.</summary>
        public static bool TryParseLookAt(string? value, out PhantomLookAtMode mode)
        {
            mode = PhantomLookAtMode.None;
            switch (Normalize(value))
            {
                case "none": mode = PhantomLookAtMode.None; return true;
                case "mimic": mode = PhantomLookAtMode.Mimic; return true;
                case "simple": mode = PhantomLookAtMode.Simple; return true;
                case "group": mode = PhantomLookAtMode.Group; return true;
                default: return false;
            }
        }

        private static string Normalize(string? value) =>
            (value ?? string.Empty).Trim().ToLowerInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
    }
}
