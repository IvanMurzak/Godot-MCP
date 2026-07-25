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

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Pure-string argument validation for the <c>godot-skill-*</c> SYSTEM tools (<see cref="Tool_Skills"/>) —
    /// the Godot analog of the inline path guards Unity-MCP's <c>Tool_Skills.Create</c>/<c>GenerateAll</c>
    /// perform. Extracted here (no Godot API, no <c>#if TOOLS</c>) so the guards — the part an AI agent is
    /// most likely to probe with a hostile path — are unit-tested in the plain xUnit host.
    ///
    /// <para>
    /// Two different path contracts are enforced, because the two tools address two different roots:
    /// <list type="bullet">
    ///   <item><c>godot-skill-create</c> writes a C# tool file into the Godot project, so it takes a
    ///   <c>res://</c> path (the project's own virtual root) — validated by
    ///   <see cref="RequireSkillFileResPath"/> on top of the shared
    ///   <see cref="ResPathNormalizer.RequireResFilePath"/> guards.</item>
    ///   <item><c>godot-skill-generate</c> takes an OPTIONAL project-RELATIVE output folder (e.g.
    ///   <c>.claude/skills</c>), matching Unity's <c>path</c> argument, validated by
    ///   <see cref="RequireRelativeSkillsFolder"/>. Null/empty means "use the selected agent's configured
    ///   folder", which is the normal case.</item>
    /// </list>
    /// </para>
    /// </summary>
    public static class SkillsToolPaths
    {
        /// <summary>
        /// Validate the <c>godot-skill-create</c> target path: a <c>res://</c> FILE path, with no <c>..</c>
        /// segment, ending in <c>.cs</c>. A skill/tool is C# ONLY — a GDScript file cannot carry the
        /// <c>[AiToolType]</c>/<c>[AiTool]</c> attributes the McpPlugin assembly scanner discovers — so
        /// (unlike <c>script-create</c>, which accepts both languages) <c>.gd</c> is rejected here with an
        /// explicit pointer at <c>script-create</c>. Returns the trimmed path on success.
        /// </summary>
        public static string RequireSkillFileResPath(string? path, string paramName)
        {
            var resPath = ResPathNormalizer.RequireResFilePath(path, paramName);

            if (!resPath.EndsWith(ScriptLang_.CSharpExtension, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"A skill must be a C# file: path must end with '{ScriptLang_.CSharpExtension}'; got '{path}'. " +
                    $"Only C# can carry the [AiToolType]/[AiTool] attributes the MCP tool scanner discovers — " +
                    $"use the '{ScriptCreateToolIdRef}' tool for a GDScript ('{ScriptLang_.GDScriptExtension}') file.",
                    paramName);

            return resPath;
        }

        /// <summary>
        /// The <c>script-create</c> tool id, quoted by <see cref="RequireSkillFileResPath"/>'s rejection
        /// message. Held as a local literal rather than a reference to <c>Tool_Script.ScriptCreateToolId</c>
        /// because that family is editor-only (<c>#if TOOLS</c>) while this file must stay compilable — and
        /// unit-testable — outside the editor. <c>internal</c> (not <c>public</c>) so the CLI's
        /// <c>discoverAddonToolIds</c> scan, which keys on <c>public const string …ToolId</c>, never
        /// double-counts it. Pinned against drift by <c>SystemToolsTests</c>.
        /// </summary>
        internal const string ScriptCreateToolIdRef = "script-create";

        /// <summary>
        /// Validate the OPTIONAL <c>godot-skill-generate</c> output-folder override. Returns <c>null</c> for a
        /// null/empty input (meaning: fall back to the selected agent's configured skills folder — the normal
        /// case), otherwise the normalized (forward-slash, no trailing slash) project-relative folder.
        /// Rejects an ABSOLUTE/rooted path and any <c>..</c> traversal segment, exactly like Unity's
        /// <c>GenerateAll</c> guards, reusing the OS-independent
        /// <see cref="UI.SkillsPathUtils.IsSafeRelativeSkillsPath"/> predicate so a Windows drive-letter path
        /// is rejected on a Linux host too.
        /// </summary>
        public static string? RequireRelativeSkillsFolder(string? path, string paramName)
        {
            // Null, empty, and whitespace-only all mean "use the selected agent's configured folder".
            if (string.IsNullOrWhiteSpace(path))
                return null;

            var trimmed = path!.Trim();

            if (ResPathNormalizer.IsResPath(trimmed))
                throw new ArgumentException(
                    $"Path must be a project-RELATIVE folder (e.g. '.claude/skills'), not a " +
                    $"'{ResPathNormalizer.ResScheme}' path; got '{path}'.", paramName);

            if (!UI.SkillsPathUtils.IsSafeRelativeSkillsPath(trimmed))
                throw new ArgumentException(
                    $"Path must be a relative folder inside the Godot project with no '..' traversal " +
                    $"segments (e.g. '.claude/skills'); got '{path}'.", paramName);

            return trimmed.Replace('\\', '/').TrimEnd('/');
        }
    }
}
