/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    public class BeehaveNamesTests
    {
        [Theory]
        [InlineData("tree", "BeehaveTree")]
        [InlineData("composite", "SelectorComposite", "Composite", "BeehaveNode")]
        [InlineData("composite", "SelectorRandomComposite", "RandomizedComposite", "Composite", "BeehaveNode")]
        [InlineData("decorator", "InverterDecorator", "Decorator", "BeehaveNode")]
        [InlineData("action", "MyAttack", "ActionLeaf", "Leaf", "BeehaveNode")]
        [InlineData("condition", "BlackboardHasCondition", "ConditionLeaf", "Leaf", "BeehaveNode")]
        [InlineData("leaf", "Leaf", "BeehaveNode")]
        [InlineData("node", "MyCustom", "BeehaveNode")]
        public void KindComesFromTheNearestBeehaveBaseClass(string kind, params string[] chain) =>
            Assert.Equal(kind, BeehaveNames.KindOf(chain));

        [Theory]
        [InlineData]
        [InlineData("Blackboard")]
        [InlineData("PlayerController")]
        public void NonBeehaveNodesHaveNoKind(params string[] chain) => Assert.Null(BeehaveNames.KindOf(chain));

        [Theory]
        [InlineData(0, "Idle")]
        [InlineData(1, "Physics")]
        [InlineData(2, "Manual")]
        [InlineData(7, "7")]
        public void ProcessThreadLabels(int value, string label) => Assert.Equal(label, BeehaveNames.ProcessThreadLabel(value));

        [Fact]
        public void TheInstallHintNamesTheAddonAndThePinnedRelease()
        {
            Assert.Contains("bitbrain/beehave v2.9.2", BeehaveNames.InstallHint);
            Assert.Contains("Project Settings -> Plugins", BeehaveNames.InstallHint);
        }
    }
}
