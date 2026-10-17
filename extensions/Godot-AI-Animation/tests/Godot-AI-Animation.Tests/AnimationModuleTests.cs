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
using GodotAnimation = Godot.Animation;

namespace com.IvanMurzak.Godot.MCP.Animation.Tests
{
    /// <summary>
    /// The module configures cleanly and shapes the animation types: an RQL engine discovers it, registers its guide, and the
    /// member tables of AnimationMixer (so AnimationPlayer / AnimationTree), AnimationLibrary and Animation carry the maps,
    /// rows and calls. Metadata only: no Godot object is created.
    /// </summary>
    public class AnimationModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(AnimationModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(AnimationModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsGuide()
        {
            var guide = Assert.Single(Engine().Guides, g => g.Key == AnimationGuide.Topic);
            Assert.Equal(AnimationGuide.Markdown, guide.Value);
        }

        [Theory]
        [InlineData(typeof(AnimationPlayer))]
        [InlineData(typeof(AnimationTree))]
        public void MixersHaveLibrariesAnimationsAndCalls(System.Type type)
        {
            var info = Engine().Types.Info(type);
            foreach (var name in new[] { "libraries", "animations" })
            {
                var member = Assert.Single(info.Members, m => m.Name == name);
                Assert.False(member.CanWrite);
                Assert.False(string.IsNullOrEmpty(member.Description));
            }
            var add = Assert.Single(info.Callable, c => c.Name == "AddLibrary");
            Assert.Equal(typeof(AnimationLibrary), add.ReturnType);
            var create = Assert.Single(info.Callable, c => c.Name == "CreateAnimation");
            Assert.Equal(new[] { "name", "length", "loopMode", "library" }, create.Parameters.Select(p => p.Name));
            Assert.Equal(new[] { false, true, true, true }, create.Parameters.Select(p => p.HasDefault));
            Assert.Equal(typeof(GodotAnimation), create.ReturnType);
        }

        [Fact]
        public void LibraryHasAnimationsAndCreateAnimation()
        {
            var info = Engine().Types.Info(typeof(AnimationLibrary));
            Assert.Contains(info.Members, m => m.Name == "animations" && !m.CanWrite);
            var create = Assert.Single(info.Callable, c => c.Name == "CreateAnimation");
            Assert.Equal(new[] { "name", "length", "loopMode" }, create.Parameters.Select(p => p.Name));
            Assert.Equal(1.0, create.Parameters[1].Default);
            Assert.Equal("none", create.Parameters[2].Default);
        }

        [Fact]
        public void AnimationHasValidatedLengthTracksAndKeyCalls()
        {
            var info = Engine().Types.Info(typeof(GodotAnimation));
            var length = Assert.Single(info.Members, m => m.Name == "Length");
            Assert.Null(length.Member); // the adapter's validated member replaced the C# property
            Assert.Equal(typeof(double), length.Type); // version-independent (float before Godot 4.7, double since)
            Assert.True(length.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "tracks" && !m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "trackCount" && m.Type == typeof(int) && !m.CanWrite);
            Assert.Contains(info.Members, m => m.Name == "LoopMode" && m.CanWrite);
            Assert.Contains(info.Callable, c => c.Name == "AddTrack" && c.ReturnType == typeof(int) && c.Parameters.Count == 2);
            Assert.Contains(info.Callable, c => c.Name == "InsertKey" && c.Parameters[2].Type == typeof(Variant));
            Assert.Contains(info.Callable, c => c.Name == "InsertTransformKey" && c.Parameters[2].Type == typeof(Vector3));
        }

        [Fact]
        public void TrackRowsAreReadOnlyColumns()
        {
            var info = Engine().Types.Info(typeof(AnimationTrackRow));
            Assert.Equal(new[] { "index", "kind", "path", "keyCount" }, info.Members.Select(m => m.Name));
            Assert.All(info.Members, m => Assert.False(m.CanWrite));
        }
    }
}
