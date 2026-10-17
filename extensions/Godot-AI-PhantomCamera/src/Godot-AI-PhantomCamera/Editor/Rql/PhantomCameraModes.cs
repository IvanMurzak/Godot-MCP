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
using System.Globalization;

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
        /// <summary>The label of a mode int ("Simple", "ThirdPerson"; an unknown int as its number).</summary>
        public static string Label<T>(int value) where T : struct, Enum =>
            Enum.IsDefined(typeof(T), value) ? Enum.GetName(typeof(T), value)! : value.ToString(CultureInfo.InvariantCulture);

        /// <summary>
        /// A mode by name, case-insensitive, ignoring '_', '-' and spaces ("third_person", "Third Person" match ThirdPerson);
        /// numbers are not names and are rejected.
        /// </summary>
        public static bool TryParse<T>(string? value, out T mode) where T : struct, Enum
        {
            var name = (value ?? string.Empty).Trim().Replace("_", "").Replace("-", "").Replace(" ", "");
            mode = default;
            if (name.Length == 0 || char.IsDigit(name[0]) || name[0] == '+') return false;
            return Enum.TryParse(name, ignoreCase: true, out mode) && Enum.IsDefined(typeof(T), mode);
        }
    }
}
