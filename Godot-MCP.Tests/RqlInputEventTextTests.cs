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
using com.IvanMurzak.Godot.MCP.Editor.Rql.Settings;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The text format of input events (inputActions / AddInputEvent): every kind parses, names and numbers resolve to
    /// Godot's indices, the canonical texts read back the same, and bad texts explain the format.
    /// </summary>
    public class RqlInputEventTextTests
    {
        private static InputEventSpec Parse(string text)
        {
            Assert.True(InputEventText.TryParse(text, out var spec, out var error), error);
            return spec!;
        }

        private static string Error(string? text)
        {
            Assert.False(InputEventText.TryParse(text, out var spec, out var error));
            Assert.Null(spec);
            return error!;
        }

        [Theory]
        [InlineData("Key:W", InputEventKind.Key, "W")]
        [InlineData("key: Ctrl+S ", InputEventKind.Key, "Ctrl+S")]
        [InlineData("PhysicalKey:Space", InputEventKind.PhysicalKey, "Space")]
        [InlineData("physical_key:F1", InputEventKind.PhysicalKey, "F1")]
        public void KeysKeepTheirTextForTheEngine(string text, InputEventKind kind, string keys)
        {
            var spec = Parse(text);
            Assert.Equal(kind, spec.Kind);
            Assert.Equal(keys, spec.Keys);
        }

        [Theory]
        [InlineData("Mouse:Left", 1)]
        [InlineData("Mouse:right", 2)]
        [InlineData("MouseButton:WheelUp", 4)]
        [InlineData("Mouse:wheel_down", 5)]
        [InlineData("Mouse:Xbutton2", 9)]
        [InlineData("Mouse:3", 3)]
        public void MouseButtonsByNameOrNumber(string text, int button)
        {
            var spec = Parse(text);
            Assert.Equal(InputEventKind.Mouse, spec.Kind);
            Assert.Equal(button, spec.Index);
            Assert.Empty(spec.Modifiers);
        }

        [Fact]
        public void MouseModifiersAreCanonicalAndDeduplicated()
        {
            var spec = Parse("Mouse:ctrl+Shift+Control+Left");
            Assert.Equal(new[] { "Shift", "Ctrl" }, spec.Modifiers.ToArray());
            Assert.Equal("Mouse:Shift+Ctrl+Left", InputEventText.Mouse(spec.Index, spec.Modifiers));
            Assert.Equal(new[] { "Alt", "Meta" }, Parse("Mouse:Cmd+Option+Right").Modifiers.ToArray());
        }

        [Theory]
        [InlineData("JoypadButton:A", 0)]
        [InlineData("JoypadButton:start", 6)]
        [InlineData("joy_button:Right Shoulder", 10)]
        [InlineData("JoypadButton:DpadDown", 12)]
        [InlineData("JoypadButton:Touchpad", 20)]
        [InlineData("JoypadButton:42", 42)]
        public void JoypadButtonsByNameOrNumber(string text, int button) => Assert.Equal(button, Parse(text).Index);

        [Theory]
        [InlineData("JoypadMotion:LeftX-", 0, -1)]
        [InlineData("JoypadMotion:LeftY+", 1, 1)]
        [InlineData("joy_axis:right_x -", 2, -1)]
        [InlineData("JoypadMotion:TriggerLeft", 4, 1)]
        [InlineData("JoypadMotion:TriggerRight+", 5, 1)]
        [InlineData("JoypadMotion:3-", 3, -1)]
        public void JoypadAxesWithTheirDirection(string text, int axis, int sign)
        {
            var spec = Parse(text);
            Assert.Equal(InputEventKind.JoypadMotion, spec.Kind);
            Assert.Equal(axis, spec.Index);
            Assert.Equal(sign, spec.AxisSign);
        }

        [Fact]
        public void CanonicalTextsReadBackAsTheSameEvent()
        {
            foreach (var text in new[] { "Mouse:Left", "Mouse:Shift+WheelUp", "JoypadButton:A", "JoypadButton:DpadDown", "JoypadMotion:LeftX-", "JoypadMotion:TriggerRight+" })
            {
                var spec = Parse(text);
                var back = spec.Kind switch
                {
                    InputEventKind.Mouse => InputEventText.Mouse(spec.Index, spec.Modifiers),
                    InputEventKind.JoypadButton => InputEventText.JoypadButton(spec.Index),
                    _ => InputEventText.JoypadMotion(spec.Index, spec.AxisSign),
                };
                Assert.Equal(text, back);
            }
            Assert.Equal("JoypadButton:99", InputEventText.JoypadButton(99));
            Assert.Equal("Key:Ctrl+S", InputEventText.Key("Ctrl+S", physical: false));
            Assert.Equal("PhysicalKey:W", InputEventText.Key("W", physical: true));
            Assert.True(InputEventText.SameText(" key:space", "Key:Space "));
        }

        [Theory]
        [InlineData(null, "not an input event")]
        [InlineData("", "not an input event")]
        [InlineData("Space", "not an input event")]
        [InlineData("Key:", "not an input event")]
        [InlineData("Bogus:1", "unknown input event kind")]
        [InlineData("Mouse:Thumb", "unknown mouse button")]
        [InlineData("Mouse:None", "unknown mouse button")]
        [InlineData("Mouse:0", "unknown mouse button")]
        [InlineData("Mouse:Hyper+Left", "unknown modifier")]
        [InlineData("JoypadButton:Z", "unknown joypad button")]
        [InlineData("JoypadButton:-1", "unknown joypad button")]
        [InlineData("JoypadButton:128", "unknown joypad button")]
        [InlineData("JoypadMotion:Wheel+", "unknown joypad axis")]
        [InlineData("JoypadMotion:6+", "unknown joypad axis")]
        [InlineData("JoypadMotion:LeftX", "needs a direction")]
        public void BadTextsExplainTheFormat(string? text, string message) => Assert.Contains(message, Error(text));

        [Fact]
        public void TheHelpNamesEveryKind()
        {
            foreach (var kind in new[] { "Key:", "PhysicalKey:", "Mouse:", "JoypadButton:", "JoypadMotion:" })
                Assert.Contains(kind, InputEventText.Help);
        }
    }
}
