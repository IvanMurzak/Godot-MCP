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

namespace com.IvanMurzak.Godot.MCP.YOUR_FEATURE.Tests
{
    /// <summary>
    /// The module configures cleanly in a plain RQL engine: it is discovered, registers its source and guide, and the source's
    /// view type exposes its members and callable methods. Metadata only: no member that touches the Godot editor is read.
    /// </summary>
    public class YOUR_FEATUREModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(YOUR_FEATUREModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(YOUR_FEATUREModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsSourceAndGuide()
        {
            var engine = Engine();
            Assert.True(engine.Sources.ContainsKey("YOUR_SOURCE"));
            var guide = Assert.Single(engine.Guides, g => g.Key == YOUR_FEATUREGuide.Topic);
            Assert.Equal(YOUR_FEATUREGuide.Markdown, guide.Value);
        }

        [Fact]
        public void SourceTypeHasItsMembersAndEcho()
        {
            var info = Engine().Types.Info(typeof(YOUR_FEATUREApi));
            Assert.Contains(info.Members, m => m.Name == "godotVersion" && !m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "editorScale");
            Assert.Contains(info.Callable, c => c.Name == "Echo");
        }

        [Theory]
        [InlineData(null, "YOUR_SOURCE-ready")]
        [InlineData("", "YOUR_SOURCE-ready")]
        [InlineData("ci-roundtrip", "ci-roundtrip")]
        public void EchoReturnsTheMessageOrTheReadyText(string? message, string expected) =>
            Assert.Equal(expected, YOUR_FEATUREApi.EchoText(message));
    }
}
