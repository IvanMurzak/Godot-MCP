/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.PhantomCamera
{
    /// <summary>
    /// The extension's RQL module, discovered by the core addon in the consumer project assembly once this package is
    /// installed: the <c>phantomcamera</c> source (installed, hint, cameras, hosts as read-only rows; the validated
    /// <c>SetFollow</c> / <c>SetLookAt</c> calls) and the agent guide rendered into the <c>godot-rql</c> skill. Phantom
    /// Camera classes are GDScript class_names (no C# type), so the module adds no adapters: cameras and hosts are created
    /// with the core <c>AddChild</c> and changed through the row's <c>node</c>. Configuring never touches Godot, so an
    /// absent addon cannot fail it.
    /// </summary>
    public sealed class PhantomCameraModule : IRqlModule
    {
        public int Order => 100;

        public void Configure(RqlModuleContext ctx) =>
            ctx.AddSource("phantomcamera", () => new PhantomCameraApi(),
                    "Phantom Camera virtual cameras of the edited scene: installed, hint, cameras (name path scriptClass priority " +
                    "followMode followTarget lookAtMode lookAtTarget node; calls SetFollow, SetLookAt), hosts (name path camera node); " +
                    "write through node. Needs the Phantom Camera addon (ramokz/phantom-camera, res://addons/phantom_camera) enabled; " +
                    "without it the lists are empty")
               .AddGuide(PhantomCameraGuide.Topic, PhantomCameraGuide.Markdown);
    }
}
#endif
