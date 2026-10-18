/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.Linq;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql.Settings
{
    /// <summary>
    /// Input events ⇄ the RQL text of <see cref="InputEventText"/>. Key names go through the engine
    /// (<see cref="OS.FindKeycodeFromString"/> / <see cref="OS.GetKeycodeString"/>), so "Key:Ctrl+S" reads back as Godot
    /// writes it. Every event is for all devices, like the events the Input Map editor creates.
    /// </summary>
    internal static class InputEvents
    {
        /// <summary>The deadzone the Input Map editor gives a new action (0.5 before Godot 4.4, 0.2 since).</summary>
        public static double DefaultDeadzone
        {
            get
            {
                var v = Engine.GetVersionInfo();
                var major = v["major"].AsInt32();
                var minor = v["minor"].AsInt32();
                return major > 4 || (major == 4 && minor >= 4) ? 0.2 : 0.5;
            }
        }

        /// <summary>A new input event from its text; throws with the format help when the text is not one.</summary>
        public static InputEvent Make(string text)
        {
            if (!InputEventText.TryParse(text, out var spec, out var error)) throw new ArgumentException(error);
            switch (spec!.Kind)
            {
                case InputEventKind.Key:
                case InputEventKind.PhysicalKey:
                {
                    var full = OS.FindKeycodeFromString(spec.Keys);
                    var code = (Key)((long)full & (long)KeyModifierMask.CodeMask);
                    if (code == Key.None) throw new ArgumentException($"unknown key '{spec.Keys}' in '{text}' (e.g. \"Key:W\", \"Key:Space\", \"Key:Ctrl+S\", \"Key:F1\")");
                    var key = new InputEventKey { Device = -1 };
                    if (spec.Kind == InputEventKind.PhysicalKey) key.PhysicalKeycode = code;
                    else key.Keycode = code;
                    var mods = (long)full & (long)KeyModifierMask.ModifierMask;
                    key.ShiftPressed = (mods & (long)KeyModifierMask.MaskShift) != 0;
                    key.AltPressed = (mods & (long)KeyModifierMask.MaskAlt) != 0;
                    key.CtrlPressed = (mods & (long)KeyModifierMask.MaskCtrl) != 0;
                    key.MetaPressed = (mods & (long)KeyModifierMask.MaskMeta) != 0;
                    return key;
                }
                case InputEventKind.Mouse:
                    return new InputEventMouseButton
                    {
                        Device = -1,
                        ButtonIndex = (MouseButton)spec.Index,
                        ShiftPressed = spec.Modifiers.Contains("Shift"),
                        CtrlPressed = spec.Modifiers.Contains("Ctrl"),
                        AltPressed = spec.Modifiers.Contains("Alt"),
                        MetaPressed = spec.Modifiers.Contains("Meta"),
                    };
                case InputEventKind.JoypadButton:
                    return new InputEventJoypadButton { Device = -1, ButtonIndex = (JoyButton)spec.Index };
                default:
                    return new InputEventJoypadMotion { Device = -1, Axis = (JoyAxis)spec.Index, AxisValue = spec.AxisSign };
            }
        }

        /// <summary>The RQL text of an input event; other kinds read as "Class: text" (they can be removed by that text).</summary>
        public static string Text(InputEvent e)
        {
            switch (e)
            {
                case InputEventKey key:
                {
                    var physical = key.PhysicalKeycode != Key.None;
                    var code = physical ? key.PhysicalKeycode : key.Keycode;
                    if (code == Key.None) code = key.KeyLabel;
                    long mods = 0;
                    if (key.ShiftPressed) mods |= (long)KeyModifierMask.MaskShift;
                    if (key.AltPressed) mods |= (long)KeyModifierMask.MaskAlt;
                    if (key.CtrlPressed) mods |= (long)KeyModifierMask.MaskCtrl;
                    if (key.MetaPressed) mods |= (long)KeyModifierMask.MaskMeta;
                    return InputEventText.Key(OS.GetKeycodeString((Key)((long)code | mods)), physical);
                }
                case InputEventMouseButton mouse:
                {
                    var mods = new[] { mouse.ShiftPressed ? "Shift" : null, mouse.CtrlPressed ? "Ctrl" : null, mouse.AltPressed ? "Alt" : null, mouse.MetaPressed ? "Meta" : null };
                    return InputEventText.Mouse((int)mouse.ButtonIndex, mods.Where(m => m != null)!);
                }
                case InputEventJoypadButton button:
                    return InputEventText.JoypadButton((int)button.ButtonIndex);
                case InputEventJoypadMotion motion:
                    return InputEventText.JoypadMotion((int)motion.Axis, motion.AxisValue);
                default:
                    return $"{e.GetClass()}: {e.AsText()}";
            }
        }
    }
}
#endif
