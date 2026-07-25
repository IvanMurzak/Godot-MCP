/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#nullable enable
using System;
using com.IvanMurzak.McpPlugin;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Skill (tool) authoring family (<c>godot-skill-*</c>) — the Godot port of Unity-MCP's
    /// <c>Editor/Scripts/API/SystemTool/Skills.*.cs</c>. Two tools, both <see cref="McpToolType.System"/>:
    /// <list type="bullet">
    ///   <item><c>godot-skill-create</c> — write a NEW C# tool file into the project so the user/AI can
    ///   extend the tool set (each partial-class file in this folder is itself an example of the shape).</item>
    ///   <item><c>godot-skill-generate</c> — regenerate every <c>SKILL.md</c> from the currently-registered
    ///   MCP tools into the selected agent's skills folder.</item>
    /// </list>
    ///
    /// <para>
    /// SYSTEM, not Standard: <c>McpPluginBuilder</c> partitions tools by <c>ToolType</c> into two DISJOINT
    /// registries, and these are editor-infrastructure operations that belong on the HTTP
    /// <c>/api/system-tools/</c> surface the desktop app + CLI drive, NOT in the <c>tools/list</c> payload
    /// AI agents see. Engine-prefixed (<c>godot-</c>) to match <c>unity-skill-create</c> /
    /// <c>unity-skill-generate</c> — the owner ruling of 2026-07-25 is that the same three system tools
    /// (<c>ping</c> + the two skill tools) exist on every engine under an engine-specific prefix.
    /// </para>
    ///
    /// <para>
    /// This family lives OUTSIDE <c>#if TOOLS</c> — like <c>Tool_Ping</c> / <c>Tool_Console</c> — so the
    /// declarations (and their <c>ToolType</c>) are visible to the McpPlugin assembly scanner and to the
    /// CI xUnit host. The editor-coupled execution is delegated to <see cref="ISkillsToolHost"/>
    /// (implemented under <c>Editor/Tools/</c>, <c>#if TOOLS</c>); the argument guards live in the
    /// pure-managed <see cref="SkillsToolPaths"/>. Both halves therefore stay testable at the level they
    /// can be: guards + dispatch here, live editor behaviour via the headless Godot smoke
    /// (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    [AiToolType]
    public partial class Tool_Skills
    {
        /// <summary>
        /// The live editor-side host, or a clear, actionable error when there is none — an exported game
        /// build (no editor to author skills in) or a call that arrived before the plugin finished booting.
        /// Never returns null, so the tool bodies stay free of null-plumbing.
        /// </summary>
        static ISkillsToolHost RequireHost()
            => SkillsToolHost.Current
               ?? throw new InvalidOperationException(
                   "The Godot-MCP skills host is not available. These tools require a running Godot EDITOR " +
                   "with the 'godot_mcp' addon enabled and booted (they are unavailable in an exported game " +
                   "build). Enable the addon in Project → Project Settings → Plugins, wait for the " +
                   "'[Godot-MCP] plugin loaded' line, then retry.");
    }
}
