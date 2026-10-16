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
using com.IvanMurzak.Godot.MCP.Beehave;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// Pure-managed specs for <see cref="BeehaveNodeKinds"/> — the kind→class mapping the editor tools use to
    /// turn an LLM-supplied "selector"/"inverter"/"action" string into the Beehave <c>class_name</c> to
    /// instantiate. No Godot binary required.
    /// </summary>
    public class BeehaveNodeKindsTests
    {
        [Theory]
        [InlineData("selector", "SelectorComposite")]
        [InlineData("Selector", "SelectorComposite")]
        [InlineData(" sel ", "SelectorComposite")]
        [InlineData("sequence", "SequenceComposite")]
        [InlineData("SEQ", "SequenceComposite")]
        public void ResolveCompositeClass_KnownKinds(string kind, string expected)
        {
            Assert.Equal(expected, BeehaveNodeKinds.ResolveCompositeClass(kind));
        }

        [Theory]
        [InlineData("inverter", "InverterDecorator")]
        [InlineData("invert", "InverterDecorator")]
        [InlineData("limiter", "LimiterDecorator")]
        [InlineData(" LIMIT ", "LimiterDecorator")]
        public void ResolveDecoratorClass_KnownKinds(string kind, string expected)
        {
            Assert.Equal(expected, BeehaveNodeKinds.ResolveDecoratorClass(kind));
        }

        [Theory]
        [InlineData("action", "ActionLeaf")]
        [InlineData("Action", "ActionLeaf")]
        [InlineData("condition", "ConditionLeaf")]
        [InlineData("cond", "ConditionLeaf")]
        public void ResolveLeafClass_KnownKinds(string kind, string expected)
        {
            Assert.Equal(expected, BeehaveNodeKinds.ResolveLeafClass(kind));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("parallel")]
        public void ResolveCompositeClass_Unknown_Throws(string? kind)
        {
            Assert.Throws<ArgumentException>(() => BeehaveNodeKinds.ResolveCompositeClass(kind));
        }

        [Theory]
        [InlineData("cooldown")]
        [InlineData("repeater")]
        public void ResolveDecoratorClass_Unknown_Throws(string kind)
        {
            Assert.Throws<ArgumentException>(() => BeehaveNodeKinds.ResolveDecoratorClass(kind));
        }

        [Theory]
        [InlineData("blackboard")]
        [InlineData("leaf")]
        public void ResolveLeafClass_Unknown_Throws(string kind)
        {
            Assert.Throws<ArgumentException>(() => BeehaveNodeKinds.ResolveLeafClass(kind));
        }
    }
}
