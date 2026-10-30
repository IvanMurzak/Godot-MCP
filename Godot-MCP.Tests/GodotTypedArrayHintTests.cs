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
using com.IvanMurzak.Godot.MCP.Editor.Rql;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    public class GodotTypedArrayHintTests
    {
        [Fact]
        public void TypeStringHintNamesTheObjectElement()
        {
            // @export var items: Array[Resource] reports PROPERTY_HINT_TYPE_STRING "24/17:Resource" (wave 7: its literals failed).
            Assert.Equal(("Resource", 17), GodotTypedArrayHint.ObjectElement(23, "24/17:Resource"));
            Assert.Equal(("Node3D", 34), GodotTypedArrayHint.ObjectElement(23, "24/34:Node3D"));
            Assert.Equal(("ItemData", 17), GodotTypedArrayHint.ObjectElement(23, " 24/17:ItemData "));
            Assert.Equal(("Font", 0), GodotTypedArrayHint.ObjectElement(23, "24:Font"));
        }

        [Fact]
        public void ArrayTypeHintNamesTheClass()
        {
            Assert.Equal(("Texture2D", 0), GodotTypedArrayHint.ObjectElement(31, "Texture2D"));
            Assert.Equal(("Resource", 17), GodotTypedArrayHint.ObjectElement(31, "24/17:Resource"));
        }

        [Fact]
        public void OtherArraysHaveNoObjectElement()
        {
            Assert.Null(GodotTypedArrayHint.ObjectElement(0, null));
            Assert.Null(GodotTypedArrayHint.ObjectElement(23, "4:"));          // Array[String]
            Assert.Null(GodotTypedArrayHint.ObjectElement(23, "2/0:"));        // Array[int]
            Assert.Null(GodotTypedArrayHint.ObjectElement(23, "28:24/17:Resource")); // nested arrays
            Assert.Null(GodotTypedArrayHint.ObjectElement(23, "24/17:"));
            Assert.Null(GodotTypedArrayHint.ObjectElement(31, "4"));           // a value element
            Assert.Null(GodotTypedArrayHint.ObjectElement(0, "Resource"));     // not an array hint
        }
    }
}
