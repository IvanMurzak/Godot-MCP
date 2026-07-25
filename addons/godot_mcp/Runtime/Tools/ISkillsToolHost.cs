/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using com.IvanMurzak.Godot.MCP.Data;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Editor-side execution seam for the <c>godot-skill-*</c> SYSTEM tools. The tool METHODS
    /// (<see cref="Tool_Skills"/>) live outside <c>#if TOOLS</c> — they must be visible to the McpPlugin
    /// assembly scanner and unit-testable in the plain xUnit host — but their actual work needs the live
    /// editor (write a <c>.cs</c> into <c>res://</c> + reimport; drive the built <c>IMcpPlugin</c>'s skill
    /// generator against the selected agent's folder). This interface is that boundary: the tools validate
    /// their arguments and delegate here; the editor-only implementation
    /// (<c>Editor/Tools/GodotSkillsToolHost.cs</c>, <c>#if TOOLS</c>) is registered into
    /// <see cref="SkillsToolHost.Current"/> at plugin boot.
    ///
    /// <para>
    /// Same shape as the addon's other "pure handler + ambient service" pairs
    /// (<c>Tool_Console</c> ⇄ <c>GodotLogCollector.Current</c>, <c>Tool_RuntimeErrors</c> ⇄
    /// <c>RuntimeErrorCollector</c>): the seam keeps the attribute-bearing declarations CI-testable while the
    /// engine-coupled half is verified by the headless Godot smoke (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    public interface ISkillsToolHost
    {
        /// <summary>
        /// Write <paramref name="code"/> to the already-validated <c>res://</c> C# path
        /// <paramref name="resPath"/> (creating missing parent directories), then make the editor pick it up
        /// (reimport + bounded settle). An existing file is OVERWRITTEN — mirroring Unity's
        /// <c>unity-skill-create</c>, which reports "Skill updated" rather than failing — because re-emitting
        /// a skill after a fix is the common AI-authoring loop.
        /// </summary>
        ScriptInfo CreateSkillFile(string resPath, string code);

        /// <summary>
        /// Regenerate every <c>SKILL.md</c> from the currently-registered MCP tools.
        /// <paramref name="relativeFolder"/> is the already-validated project-relative output folder, or
        /// <c>null</c> to use the selected AI agent's configured skills folder (the normal case).
        /// </summary>
        SkillsGenerateInfo GenerateSkills(string? relativeFolder);
    }

    /// <summary>
    /// Ambient holder for the editor-side <see cref="ISkillsToolHost"/> implementation. Set once at plugin
    /// boot (<c>GodotMcpPlugin.BootMcp</c>) and CLEARED on teardown so a stale host can never pin the
    /// collectible <c>AssemblyLoadContext</c> open across a C# hot-reload — the same discipline
    /// <c>GodotLogCollector.Current</c> / <c>GodotMcpReflector.Current</c> follow.
    ///
    /// <para>
    /// <c>null</c> in an exported game build (there is no editor to author skills in) and before boot
    /// completes; <see cref="Tool_Skills"/> turns that into an explicit, actionable error rather than a
    /// <c>NullReferenceException</c>.
    /// </para>
    /// </summary>
    public static class SkillsToolHost
    {
        /// <summary>The live editor-side host, or <c>null</c> outside a booted editor session.</summary>
        public static ISkillsToolHost? Current { get; set; }
    }
}
