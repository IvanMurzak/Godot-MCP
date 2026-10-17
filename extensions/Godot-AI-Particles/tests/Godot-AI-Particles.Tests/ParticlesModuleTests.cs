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

namespace com.IvanMurzak.Godot.MCP.Particles.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes the emitter types: an RQL engine discovers it, registers its guide, and the
    /// member tables of GPUParticles3D / GPUParticles2D carry the validated members and Restart(). Metadata only: no Godot
    /// object is created.
    /// </summary>
    public class ParticlesModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(ParticlesModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(ParticlesModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == ParticlesGuide.Topic);
            Assert.Equal(ParticlesGuide.Markdown, guide.Value);
        }

        [Theory]
        [InlineData(typeof(GpuParticles3D))]
        [InlineData(typeof(GpuParticles2D))]
        public void EmittersHaveValidatedMembersAndRestart(System.Type type)
        {
            var info = Engine().Types.Info(type);
            foreach (var (name, memberType) in new[] { ("Amount", typeof(int)), ("Lifetime", typeof(double)), ("Explosiveness", typeof(float)), ("Randomness", typeof(float)) })
            {
                var member = Assert.Single(info.Members, m => m.Name == name);
                Assert.Null(member.Member); // the adapter's validated member replaced the C# property
                Assert.Equal(memberType, member.Type);
                Assert.True(member.CanWrite);
                Assert.False(string.IsNullOrEmpty(member.Description));
            }
            Assert.Contains(info.Callable, c => c.Name == "Restart" && c.Parameters.All(p => p.HasDefault));
            Assert.Contains(info.Members, m => m.Name == "Emitting" && m.CanWrite);
        }
    }
}
