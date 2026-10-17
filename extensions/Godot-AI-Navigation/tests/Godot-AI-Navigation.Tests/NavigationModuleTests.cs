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
using System.Linq;
using Godot;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Navigation.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes the navigation types: an RQL engine discovers it, registers its guide, and the
    /// member tables of the agents, regions, links and NavigationPolygon carry the validated members (replacing the C#
    /// properties of the same name and type), the virtual read members and the calls. Metadata only: no Godot object is created.
    /// </summary>
    public class NavigationModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(NavigationModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(NavigationModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == NavigationGuide.Topic);
            Assert.Equal(NavigationGuide.Markdown, guide.Value);
        }

        private static readonly string[] AgentMembers =
            { "Radius", "PathDesiredDistance", "TargetDesiredDistance", "MaxSpeed", "TimeHorizonAgents", "TimeHorizonObstacles", "AvoidancePriority" };

        [Theory]
        [InlineData(typeof(NavigationAgent3D), true)]
        [InlineData(typeof(NavigationAgent2D), false)]
        public void AgentsHaveValidatedMembers(Type type, bool hasHeight)
        {
            var info = Engine().Types.Info(type);
            foreach (var name in hasHeight ? AgentMembers.Append("Height") : AgentMembers)
                AssertValidated(info, name);
            Assert.Contains(info.Members, m => m.Name == "AvoidanceEnabled" && m.CanWrite);
        }

        [Theory]
        [InlineData(typeof(NavigationRegion3D))]
        [InlineData(typeof(NavigationRegion2D))]
        public void RegionsHaveCostsPolygonCountAndBake(Type type)
        {
            var info = Engine().Types.Info(type);
            AssertValidated(info, "EnterCost");
            AssertValidated(info, "TravelCost");
            var count = Assert.Single(info.Members, m => m.Name == "polygonCount");
            Assert.Equal(typeof(int), count.Type);
            Assert.False(count.CanWrite);
            var bake = Assert.Single(info.Callable, c => c.Name == "Bake");
            Assert.Empty(bake.Parameters);
        }

        [Theory]
        [InlineData(typeof(NavigationLink3D))]
        [InlineData(typeof(NavigationLink2D))]
        public void LinksHaveValidatedCostsAndPlainEnds(Type type)
        {
            var info = Engine().Types.Info(type);
            AssertValidated(info, "EnterCost");
            AssertValidated(info, "TravelCost");
            Assert.Contains(info.Members, m => m.Name == "StartPosition" && m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "EndPosition" && m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "Bidirectional" && m.CanWrite);
        }

        [Fact]
        public void NavigationPolygonHasOutlineCalls()
        {
            var info = Engine().Types.Info(typeof(NavigationPolygon));
            Assert.Contains(info.Callable, c => c.Name == "AddOutline" && c.Parameters.Count == 1 && c.Parameters[0].Type == typeof(Vector2[]));
            Assert.Contains(info.Callable, c => c.Name == "ClearOutlines");
            Assert.Contains(info.Members, m => m.Name == "outlineCount" && !m.CanWrite);
        }

        [Fact]
        public void EveryAdapterNamesTheExtension()
        {
            var adapters = new RqlTypeAdapter[]
            {
                new Godot_NavigationRegion3D_Adapter(), new Godot_NavigationRegion2D_Adapter(), new Godot_NavigationAgent3D_Adapter(),
                new Godot_NavigationAgent2D_Adapter(), new Godot_NavigationLink3D_Adapter(), new Godot_NavigationLink2D_Adapter(),
                new Godot_NavigationPolygon_Adapter(),
            };
            Assert.All(adapters, a => Assert.Contains("(Navigation extension)", a.Description));
        }

        private static void AssertValidated(TypeInfo info, string name)
        {
            var member = Assert.Single(info.Members, m => m.Name == name);
            Assert.Null(member.Member); // the adapter's validated member replaced the C# property
            Assert.Equal(typeof(float), member.Type);
            Assert.True(member.CanWrite);
            Assert.False(string.IsNullOrEmpty(member.Description));
        }
    }
}
