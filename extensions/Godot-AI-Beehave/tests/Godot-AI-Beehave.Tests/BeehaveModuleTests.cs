/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System.Linq;
using Godot;
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Beehave.Tests
{
    /// <summary>
    /// The module configures cleanly (without the addon and without Godot) and shapes its view types: the <c>beehave</c>
    /// source, its guide, and read-only rows that hold the node. Metadata only: no member that touches Godot is read.
    /// </summary>
    public class BeehaveModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(BeehaveModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(BeehaveModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsSourceAndGuide()
        {
            var engine = Engine();
            Assert.True(engine.Sources.ContainsKey("beehave"));
            var guide = Assert.Single(engine.Guides, g => g.Key == BeehaveGuide.Topic);
            Assert.Equal(BeehaveGuide.Markdown, guide.Value);
        }

        [Fact]
        public void TheSourceHasInstalledHintAndTheRowLists()
        {
            var info = Engine().Types.Info(typeof(BeehaveApi));
            foreach (var name in new[] { "installed", "hint", "trees", "nodes" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
        }

        [Fact]
        public void TreeRowsAreReadOnlyAndHoldTheNode()
        {
            var info = Engine().Types.Info(typeof(BeehaveTreeRow));
            foreach (var name in new[] { "name", "path", "scriptClass", "enabled", "tickRate", "processThread", "actor", "childCount", "nodeCount", "node" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
            Assert.Equal(typeof(Node), info.Members.Single(m => m.Name == "node").Type);
        }

        [Fact]
        public void NodeRowsAreReadOnlyAndHoldTheNode()
        {
            var info = Engine().Types.Info(typeof(BeehaveNodeRow));
            foreach (var name in new[] { "name", "path", "scriptClass", "kind", "tree", "depth", "parent", "childCount", "node" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
        }
    }
}
