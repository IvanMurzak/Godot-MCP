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
using System.Text.RegularExpressions;

namespace com.IvanMurzak.Godot.MCP.Particles
{
    /// <summary>
    /// The agent guide of the Particles RQL module, rendered into the <c>godot-rql</c> skill (<see cref="ParticlesModule"/>).
    /// Pure strings (no Godot API, not behind <c>#if TOOLS</c>) so the xUnit suite parses every example; the live e2e
    /// manifest (<c>tests/e2e/e2e-tools.json</c>) runs each one, in this order, against a scene whose root is a Node3D.
    /// </summary>
    public static class ParticlesGuide
    {
        public const string Topic = "Particles";

        /// <summary>One example per line: the query, three or more spaces, a note.</summary>
        public const string Examples =
            "root | call AddChild(\"GPUParticles3D\", \"FX\")   a 3D emitter (GPUParticles2D under a 2D scene)\n" +
            "nodes | where name == \"FX\" | set Amount = 32, Lifetime = 2, Explosiveness = 0.5, Randomness = 0.25, OneShot = false   emission settings (validated)\n" +
            "nodes | where name == \"FX\" | set ProcessMaterial = {\"$type\": \"ParticleProcessMaterial\"}   a new process material\n" +
            "nodes | where name == \"FX\" | set ProcessMaterial.props[\"initial_velocity_min\"] = 2, ProcessMaterial.props[\"initial_velocity_max\"] = 4   tune it by Godot property name\n" +
            "nodes | where name == \"FX\" | call Restart()   restart the emission from the beginning\n" +
            "nodes | where name == \"FX\" | set Emitting = false   stop emitting (true starts)\n" +
            "nodes | where $ is GPUParticles3D | select name Amount Lifetime Emitting Explosiveness ProcessMaterial   read the 3D emitters";

        public const string Markdown =
            "GPU particle emitters are nodes: GPUParticles3D and GPUParticles2D (the same members; `$ is GPUParticles2D` finds the 2D " +
            "ones). Add one with `AddChild`, then set its members (undoable; the scene is marked unsaved):\n\n```\n" + Examples + "\n```\n\n" +
            "`Amount` (at least 1), `Lifetime` (seconds, above 0), `Explosiveness` and `Randomness` (0 to 1) are validated: an " +
            "out-of-range value fails the whole mutation and changes nothing. Every other Godot property is reachable as a C# member " +
            "(`SpeedScale`, `Preprocess`, `LocalCoords`, `DrawPass1` …) or by its snake_case name in `props[\"…\"]`; " +
            "`rql-schema \"GPUParticles3D\"` lists them. `ProcessMaterial.props[\"…\"]` reaches the ParticleProcessMaterial's " +
            "properties (direction, spread, gravity, color …).";

        /// <summary>The queries of <see cref="Examples"/>, in order.</summary>
        public static string[] Queries =>
            Examples.Split('\n').Select(l => Regex.Split(l.Trim(), @"\s{3,}")[0]).Where(l => l.Length > 0).ToArray();
    }
}
