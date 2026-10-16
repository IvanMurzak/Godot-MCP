/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Beehave;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// Pins the Beehave addon's <c>class_name</c> identifiers, <c>snake_case</c> member names, and enum-int
    /// values — the Class-B CONTRACT (there are no compile-time types for these). Verified against
    /// <c>bitbrain/beehave</c> tag <c>v2.9.2</c>; if upstream renames a class/member/enum these tests fail
    /// loudly instead of the live e2e drifting silently RED.
    /// </summary>
    public class BeehaveEnumsTests
    {
        [Fact]
        public void ClassNames_AreStable()
        {
            Assert.Equal("BeehaveTree", BeehaveEnums.PresenceClass);
            Assert.Equal("BeehaveTree", BeehaveEnums.TreeClass);
            Assert.Equal("Blackboard", BeehaveEnums.BlackboardClass);
            Assert.Equal("SelectorComposite", BeehaveEnums.SelectorClass);
            Assert.Equal("SequenceComposite", BeehaveEnums.SequenceClass);
            Assert.Equal("InverterDecorator", BeehaveEnums.InverterClass);
            Assert.Equal("LimiterDecorator", BeehaveEnums.LimiterClass);
            Assert.Equal("ActionLeaf", BeehaveEnums.ActionLeafClass);
            Assert.Equal("ConditionLeaf", BeehaveEnums.ConditionLeafClass);
        }

        [Fact]
        public void MemberNames_AreSnakeCase()
        {
            Assert.Equal("enabled", BeehaveEnums.EnabledMember);
            Assert.Equal("tick_rate", BeehaveEnums.TickRateMember);
            Assert.Equal("actor", BeehaveEnums.ActorMember);
            Assert.Equal("actor_node_path", BeehaveEnums.ActorNodePathMember);
            Assert.Equal("process_thread", BeehaveEnums.ProcessThreadMember);
        }

        [Fact]
        public void ProcessThread_EnumInts_MatchAddon()
        {
            // enum ProcessThread { IDLE, PHYSICS, MANUAL } — beehave_tree.gd @ v2.9.2.
            Assert.Equal(0, BeehaveEnums.ProcessThreadIdle);
            Assert.Equal(1, BeehaveEnums.ProcessThreadPhysics);
            Assert.Equal(2, BeehaveEnums.ProcessThreadManual);
        }
    }
}
