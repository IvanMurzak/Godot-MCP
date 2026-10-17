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

namespace com.IvanMurzak.Godot.MCP.PhantomCamera.Tests
{
    /// <summary>
    /// The module configures cleanly (without the addon and without Godot) and shapes its view types: the
    /// <c>phantomcamera</c> source, its guide, read-only rows that hold the node, and the validated mode calls. Metadata
    /// only: no member that touches Godot is read.
    /// </summary>
    public class PhantomCameraModuleTests
    {
        private static RqlEngine Engine()
        {
            var engine = new RqlEngine();
            var reports = engine.AddModulesFrom(new[] { typeof(PhantomCameraModule).Assembly });
            Assert.All(reports, r => Assert.True(r.Ok, r.ToString()));
            Assert.Contains(reports, r => r.Module == typeof(PhantomCameraModule).FullName);
            return engine;
        }

        [Fact]
        public void ModuleRegistersItsSourceAndGuide()
        {
            var engine = Engine();
            Assert.True(engine.Sources.ContainsKey("phantomcamera"));
            var guide = Assert.Single(engine.Guides, g => g.Key == PhantomCameraGuide.Topic);
            Assert.Equal(PhantomCameraGuide.Markdown, guide.Value);
        }

        [Fact]
        public void TheSourceHasInstalledHintAndTheRowLists()
        {
            var info = Engine().Types.Info(typeof(PhantomCameraApi));
            foreach (var name in new[] { "installed", "hint", "cameras", "hosts" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
        }

        [Fact]
        public void CameraRowsAreReadOnlyWithValidatedModeCalls()
        {
            var info = Engine().Types.Info(typeof(PhantomCameraRow));
            foreach (var name in new[] { "name", "path", "scriptClass", "priority", "followMode", "followTarget", "lookAtMode", "lookAtTarget", "followDamping", "lookAtDamping", "node" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
            Assert.Equal(typeof(Node), info.Members.Single(m => m.Name == "node").Type);
            foreach (var call in new[] { "SetFollow", "SetLookAt" })
            {
                var method = Assert.Single(info.Callable, c => c.Name == call);
                Assert.Equal(2, method.Parameters.Count);
                Assert.False(method.Parameters[0].HasDefault);
                Assert.True(method.Parameters[1].HasDefault); // the target is optional
            }
        }

        [Fact]
        public void HostRowsAreReadOnly()
        {
            var info = Engine().Types.Info(typeof(PhantomCameraHostRow));
            foreach (var name in new[] { "name", "path", "scriptClass", "camera", "node" })
                Assert.Contains(info.Members, m => m.Name == name && !m.CanWrite);
        }
    }
}
