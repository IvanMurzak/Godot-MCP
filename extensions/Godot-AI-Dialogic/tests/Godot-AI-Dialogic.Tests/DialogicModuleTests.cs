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
using Rql.Execution;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Dialogic.Tests
{
    /// <summary>
    /// The module configures cleanly (without the addon and without Godot) and shapes its view types: the <c>dialogic</c>
    /// source with its creation calls, its guide, and read-only rows. Metadata only: no member that touches Godot is read.
    /// </summary>
    public class DialogicModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(DialogicModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(DialogicModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsSourceAndGuide()
        {
            var engine = Engine();
            Assert.True(engine.Sources.ContainsKey("dialogic"));
            var guide = Assert.Single(engine.Guides, g => g.Key == DialogicGuide.Topic);
            Assert.Equal(DialogicGuide.Markdown, guide.Value);
        }

        [Fact]
        public void TheSourceHasItsListsAndCreationCalls()
        {
            var info = Engine().Types.Info(typeof(DialogicApi));
            foreach (var name in new[] { "installed", "hint", "timelines", "characters" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
            var timeline = Assert.Single(info.Callable, c => c.Name == "CreateTimeline");
            Assert.Equal(new[] { false, true, true }, timeline.Parameters.Select(p => p.HasDefault));
            var character = Assert.Single(info.Callable, c => c.Name == "CreateCharacter");
            Assert.Equal(new[] { false, false, true, true }, character.Parameters.Select(p => p.HasDefault));
        }

        [Fact]
        public void TimelineRowsAreReadOnlyWithAddText()
        {
            var info = Engine().Types.Info(typeof(DialogicTimelineRow));
            foreach (var name in new[] { "path", "name", "text", "eventCount", "resource" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
            var addText = Assert.Single(info.Callable, c => c.Name == "AddText");
            Assert.Equal(new[] { false, true }, addText.Parameters.Select(p => p.HasDefault));
        }

        [Fact]
        public void CharacterRowsAreReadOnlyAndHoldTheResource()
        {
            var info = Engine().Types.Info(typeof(DialogicCharacterRow));
            foreach (var name in new[] { "path", "name", "displayName", "color", "description", "resource" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
            Assert.Empty(info.Callable);
        }
    }
}
