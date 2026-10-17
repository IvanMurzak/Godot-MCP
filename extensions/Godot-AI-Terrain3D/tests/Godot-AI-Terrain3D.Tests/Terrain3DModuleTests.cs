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

namespace com.IvanMurzak.Godot.MCP.Terrain3D.Tests
{
    /// <summary>
    /// The module configures cleanly: an RQL engine discovers it and registers its sources, its function and its guide, and the
    /// view type carries the read-only members, the node reference and ChangeRegionSize. Metadata only: no source getter,
    /// function or Godot object is run (they need the editor and the addon).
    /// </summary>
    public class Terrain3DModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(Terrain3DModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(Terrain3DModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == Terrain3DGuide.Topic);
            Assert.Equal(Terrain3DGuide.Markdown, guide.Value);
        }

        [Fact]
        public void ModuleRegistersItsSourcesAndFunction()
        {
            var engine = Engine();
            Assert.Contains("(Terrain3D extension)", engine.Sources["terrain3d"].Description);
            Assert.True(engine.Sources.ContainsKey("terrain3d.addon"));
            Assert.True(engine.Functions.ContainsKey("terrain3d.newMaterial"));
            Assert.All(engine.Sources.Keys.Concat(engine.Functions.Keys), name => Assert.StartsWith("terrain3d", name));
        }

        [Fact]
        public void ViewsAreReadOnlyRowsWithTheNodeAndChangeRegionSize()
        {
            var info = Engine().Types.Info(typeof(Terrain3DView));
            foreach (var name in new[] { "id", "node", "name", "path", "regionSize", "dataDirectory", "material", "regionCount", "meshLods", "meshSize", "vertexSpacing", "version" })
            {
                var member = Assert.Single(info.Members, m => m.Name == name);
                Assert.False(member.CanWrite, name);
                Assert.False(string.IsNullOrEmpty(member.Description), name);
            }
            Assert.Equal(typeof(Node3D), Assert.Single(info.Members, m => m.Name == "node").Type);
            var change = Assert.Single(info.Callable, c => c.Name == "ChangeRegionSize");
            Assert.Equal(typeof(int), Assert.Single(change.Parameters).Type);
        }

        [Fact]
        public void AddonRowSaysWhetherItIsInstalled()
        {
            var info = Engine().Types.Info(typeof(Terrain3DAddon));
            Assert.Equal(new[] { "installed", "addon", "hint" }.OrderBy(n => n), info.Members.Select(m => m.Name).OrderBy(n => n));
        }
    }
}
