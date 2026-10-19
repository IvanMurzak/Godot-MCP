/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Editor-only (RQL needs the editor-only com.IvanMurzak.Rql package); also compiled by the xUnit suite.
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Settings
{
    /// <summary>The kinds of input event an input action can hold, as written in RQL text.</summary>
    public enum InputEventKind { Key, PhysicalKey, Mouse, JoypadButton, JoypadMotion }

    /// <summary>
    /// One input event of an input action as text, parsed: <c>Key:Ctrl+S</c> (the key text stays for the engine to resolve),
    /// <c>Mouse:Shift+Left</c>, <c>JoypadButton:A</c>, <c>JoypadMotion:LeftX-</c>. <see cref="Index"/> is the mouse button,
    /// joypad button or axis number Godot uses; <see cref="AxisSign"/> is -1 or +1 for an axis.
    /// </summary>
    public sealed class InputEventSpec
    {
        public InputEventSpec(InputEventKind kind, string keys, IReadOnlyList<string> modifiers, int index, int axisSign)
        {
            Kind = kind;
            Keys = keys;
            Modifiers = modifiers;
            Index = index;
            AxisSign = axisSign;
        }

        public InputEventKind Kind { get; }

        /// <summary>For Key / PhysicalKey: the key with its modifiers as Godot writes it ("Ctrl+S"); empty otherwise.</summary>
        public string Keys { get; }

        /// <summary>For Mouse: the modifier names in canonical order (Shift, Ctrl, Alt, Meta); empty otherwise.</summary>
        public IReadOnlyList<string> Modifiers { get; }

        public int Index { get; }

        public int AxisSign { get; }
    }

    /// <summary>
    /// The text format of input events in RQL (<c>inputActions</c> / <c>AddInputEvent</c>): <c>&lt;Kind&gt;:&lt;value&gt;</c>.
    /// Kinds are case-insensitive. Key names are resolved by the engine (<c>OS.FindKeycodeFromString</c>), so this class only
    /// splits the text; mouse buttons, joypad buttons and axes are named here, so the same names read back.
    /// </summary>
    public static class InputEventText
    {
        public const string Help =
            "an input event is \"Key:W\", \"Key:Ctrl+S\", \"PhysicalKey:Space\", \"Mouse:Left\", \"Mouse:Shift+WheelUp\", " +
            "\"JoypadButton:A\" (or a button number), \"JoypadMotion:LeftX-\" / \"JoypadMotion:TriggerRight+\" (axis and direction)";

        /// <summary>Godot's MouseButton numbers (1-based; 0 is None).</summary>
        public static readonly IReadOnlyList<string> MouseButtons = new[]
        {
            "None", "Left", "Right", "Middle", "WheelUp", "WheelDown", "WheelLeft", "WheelRight", "Xbutton1", "Xbutton2",
        };

        /// <summary>Godot's JoyButton numbers (SDL layout).</summary>
        public static readonly IReadOnlyList<string> JoypadButtons = new[]
        {
            "A", "B", "X", "Y", "Back", "Guide", "Start", "LeftStick", "RightStick", "LeftShoulder", "RightShoulder",
            "DpadUp", "DpadDown", "DpadLeft", "DpadRight", "Misc1", "Paddle1", "Paddle2", "Paddle3", "Paddle4", "Touchpad",
        };

        /// <summary>Godot's JoyAxis numbers.</summary>
        public static readonly IReadOnlyList<string> JoypadAxes = new[] { "LeftX", "LeftY", "RightX", "RightY", "TriggerLeft", "TriggerRight" };

        /// <summary>Modifier names in the order Godot writes them.</summary>
        public static readonly IReadOnlyList<string> ModifierNames = new[] { "Shift", "Ctrl", "Alt", "Meta" };

        private static readonly Dictionary<string, string> ModifierAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["shift"] = "Shift", ["ctrl"] = "Ctrl", ["control"] = "Ctrl", ["alt"] = "Alt", ["option"] = "Alt",
            ["meta"] = "Meta", ["cmd"] = "Meta", ["command"] = "Meta", ["win"] = "Meta", ["super"] = "Meta",
        };

        /// <summary>Parse <paramref name="text"/>; false with the reason when it is not an input event.</summary>
        public static bool TryParse(string? text, out InputEventSpec? spec, out string? error)
        {
            spec = null;
            error = null;
            var t = (text ?? "").Trim();
            var colon = t.IndexOf(':');
            if (colon <= 0 || colon == t.Length - 1)
            {
                error = $"'{text}' is not an input event: {Help}";
                return false;
            }
            var kindText = t.Substring(0, colon).Trim();
            var value = t.Substring(colon + 1).Trim();
            if (!TryKind(kindText, out var kind))
            {
                error = $"unknown input event kind '{kindText}': {Help}";
                return false;
            }
            switch (kind)
            {
                case InputEventKind.Key:
                case InputEventKind.PhysicalKey:
                    spec = new InputEventSpec(kind, value, Array.Empty<string>(), 0, 0);
                    return true;
                case InputEventKind.Mouse:
                {
                    var parts = value.Split('+').Select(p => p.Trim()).ToList();
                    var mods = new HashSet<string>(StringComparer.Ordinal);
                    for (int i = 0; i < parts.Count - 1; i++)
                    {
                        if (!ModifierAliases.TryGetValue(parts[i], out var mod))
                        {
                            error = $"unknown modifier '{parts[i]}' in '{text}' (use {string.Join(", ", ModifierNames)})";
                            return false;
                        }
                        mods.Add(mod);
                    }
                    var button = IndexOf(MouseButtons, parts[parts.Count - 1], first: 1);
                    if (button < 1)
                    {
                        error = $"unknown mouse button '{parts[parts.Count - 1]}' (use {string.Join(", ", MouseButtons.Skip(1))})";
                        return false;
                    }
                    spec = new InputEventSpec(kind, "", ModifierNames.Where(mods.Contains).ToList(), button, 0);
                    return true;
                }
                case InputEventKind.JoypadButton:
                {
                    var button = IndexOf(JoypadButtons, value, first: 0, max: 127);
                    if (button < 0)
                    {
                        error = $"unknown joypad button '{value}' (use {string.Join(", ", JoypadButtons)} or a number)";
                        return false;
                    }
                    spec = new InputEventSpec(kind, "", Array.Empty<string>(), button, 0);
                    return true;
                }
                default:
                {
                    int sign = 0;
                    var axisText = value;
                    if (axisText.EndsWith("+", StringComparison.Ordinal)) { sign = 1; axisText = axisText.Substring(0, axisText.Length - 1); }
                    else if (axisText.EndsWith("-", StringComparison.Ordinal)) { sign = -1; axisText = axisText.Substring(0, axisText.Length - 1); }
                    var axis = IndexOf(JoypadAxes, axisText.Trim(), first: 0, max: JoypadAxes.Count - 1);
                    if (axis < 0)
                    {
                        error = $"unknown joypad axis '{axisText}' (use {string.Join(", ", JoypadAxes)} followed by + or -)";
                        return false;
                    }
                    if (sign == 0)
                    {
                        // A trigger only moves one way; a stick needs the direction.
                        if (axis >= 4) sign = 1;
                        else
                        {
                            error = $"'{text}' needs a direction: \"JoypadMotion:{JoypadAxes[axis]}-\" or \"JoypadMotion:{JoypadAxes[axis]}+\"";
                            return false;
                        }
                    }
                    spec = new InputEventSpec(kind, "", Array.Empty<string>(), axis, sign);
                    return true;
                }
            }
        }

        /// <summary>The canonical text of a mouse button event.</summary>
        public static string Mouse(int button, IEnumerable<string> modifiers)
        {
            var name = button >= 1 && button < MouseButtons.Count ? MouseButtons[button] : button.ToString(CultureInfo.InvariantCulture);
            var mods = ModifierNames.Where(modifiers.Contains).ToList();
            return "Mouse:" + (mods.Count > 0 ? string.Join("+", mods) + "+" : "") + name;
        }

        /// <summary>The canonical text of a joypad button event.</summary>
        public static string JoypadButton(int button) =>
            "JoypadButton:" + (button >= 0 && button < JoypadButtons.Count ? JoypadButtons[button] : button.ToString(CultureInfo.InvariantCulture));

        /// <summary>The canonical text of a joypad axis event (the direction from the sign of <paramref name="value"/>).</summary>
        public static string JoypadMotion(int axis, float value) =>
            "JoypadMotion:" + (axis >= 0 && axis < JoypadAxes.Count ? JoypadAxes[axis] : axis.ToString(CultureInfo.InvariantCulture)) + (value < 0 ? "-" : "+");

        /// <summary>The canonical text of a key event (<paramref name="keys"/> is the engine's text, e.g. "Ctrl+S").</summary>
        public static string Key(string keys, bool physical) => (physical ? "PhysicalKey:" : "Key:") + keys;

        /// <summary>Whether two event texts name the same event (case-insensitive, canonical names).</summary>
        public static bool SameText(string a, string b) => string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);

        private static bool TryKind(string text, out InputEventKind kind)
        {
            switch (text.Replace("_", "").Replace(" ", "").ToLowerInvariant())
            {
                case "key": kind = InputEventKind.Key; return true;
                case "physicalkey": case "physical": kind = InputEventKind.PhysicalKey; return true;
                case "mouse": case "mousebutton": kind = InputEventKind.Mouse; return true;
                case "joypadbutton": case "joybutton": case "gamepadbutton": kind = InputEventKind.JoypadButton; return true;
                case "joypadmotion": case "joyaxis": case "joypadaxis": case "gamepadaxis": kind = InputEventKind.JoypadMotion; return true;
                default: kind = default; return false;
            }
        }

        // The index of a name (case-insensitive, '_' and spaces ignored) or a number in [first, max]; -1 when neither.
        private static int IndexOf(IReadOnlyList<string> names, string text, int first, int max = -1)
        {
            var key = text.Replace("_", "").Replace(" ", "");
            for (int i = first; i < names.Count; i++)
                if (string.Equals(names[i], key, StringComparison.OrdinalIgnoreCase)) return i;
            if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n >= first && n <= (max < 0 ? names.Count - 1 : max))
                return n;
            return -1;
        }
    }
}
#endif
