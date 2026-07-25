/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
#if TOOLS
#nullable enable
using System;
using System.IO;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.UI;
using com.IvanMurzak.Godot.MCP.UI.Agents;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// The editor-only half of the <c>godot-skill-*</c> SYSTEM tools: the live-editor implementation of
    /// <see cref="ISkillsToolHost"/> that <see cref="Tool_Skills"/> delegates to. Registered into
    /// <see cref="SkillsToolHost.Current"/> by <c>GodotMcpPlugin.BootMcp</c> and cleared on teardown.
    ///
    /// <para>
    /// Everything here needs the live editor — writing a <c>.cs</c> into <c>res://</c> and reimporting it
    /// through <c>EditorFileSystem</c>, and driving the BUILT <c>IMcpPlugin</c>'s skill generator — which is
    /// exactly why it is split out behind <c>#if TOOLS</c> while the attribute-bearing tool declarations stay
    /// unguarded and CI-testable. Verified by the headless Godot smoke (<c>test.md</c> Suite 3).
    /// </para>
    /// </summary>
    public sealed class GodotSkillsToolHost : ISkillsToolHost
    {
        readonly GodotMcpConnection _connection;

        public GodotSkillsToolHost(GodotMcpConnection connection)
        {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        /// <inheritdoc/>
        public ScriptInfo CreateSkillFile(string resPath, string code)
        {
            // The path was already validated by Tool_Skills.Create (SkillsToolPaths); this half only writes.
            // Marshal onto the editor main thread — FileAccess/EditorFileSystem are not thread-safe.
            return MainThread.Instance.Run(() =>
            {
                // `global::` qualified for TWO reasons: `using System.IO` (needed for Path/Directory below)
                // makes a bare `FileAccess` ambiguous with System.IO.FileAccess, and a bare `Godot.` prefix
                // binds to the ENCLOSING `com.IvanMurzak.Godot` namespace, not the engine's.
                var verb = global::Godot.FileAccess.FileExists(resPath) ? "updated" : "created";

                // Reuse the script family's write + reimport + bounded-settle path verbatim so a skill file
                // and a `script-create` file land through exactly ONE code path (a skill IS a C# script;
                // duplicating the settle loop here is how the two would silently diverge).
                return Tool_Script.WriteScript(resPath, ScriptLang.CSharp, code, verb);
            });
        }

        /// <inheritdoc/>
        public SkillsGenerateInfo GenerateSkills(string? relativeFolder)
        {
            return MainThread.Instance.Run(() =>
            {
                var projectRoot = ProjectRoot();

                string skillsDir;
                string? agentId;

                if (relativeFolder != null)
                {
                    // Explicit override (already validated relative + traversal-free by SkillsToolPaths).
                    // Normalize separators: projectRoot is forward-slashed (GlobalizePath) and relativeFolder
                    // is too, so a raw Path.Combine yields "C:/proj\.claude/skills" on Windows — which then
                    // lands in the live config AND is echoed back to the client as SkillsFolder, comparing
                    // unequal to the dock's own resolution of the same directory.
                    skillsDir = Path.Combine(projectRoot, relativeFolder).Replace('\\', '/');
                    agentId = null;
                }
                else
                {
                    // Normal case: the folder configured for the editor's SELECTED AI agent — the SAME
                    // resolution the dock's Skills card and the boot-time auto-generate use, so all three
                    // paths can never write to different places.
                    var agent = GodotAgentConfigurators.GetByAgentId(_connection.Config.SelectedAgentId);
                    var plan = SkillsPlan.Resolve(agent, projectRoot);
                    if (!plan.Supported || string.IsNullOrEmpty(plan.SkillsDir))
                        throw new InvalidOperationException(
                            "The selected AI agent does not support skills, so there is no configured skills " +
                            "folder to generate into. Select a skills-capable agent in the Godot-MCP dock, or " +
                            "pass an explicit project-relative 'path' (e.g. '.claude/skills').");

                    skillsDir = plan.SkillsDir!;
                    agentId = agent?.AgentId;
                }

                if (!IsInsideProject(skillsDir, projectRoot))
                    throw new ArgumentException(
                        $"Refusing to generate: the resolved skills folder '{skillsDir}' escapes the Godot " +
                        $"project root '{projectRoot}'.");

                EnsureDirectory(skillsDir);

                var generated = GenerateWithSwapRestore(_connection, skillsDir, projectRoot);

                return new SkillsGenerateInfo
                {
                    SkillsFolder = skillsDir,
                    AgentId = agentId,
                    SkillCount = CountSkillFiles(skillsDir),
                    Status = generated
                        ? "Skills generated."
                        : "Skill generation reported no changes (or failed) — check the Godot Output panel.",
                };
            });
        }

        // --- shared editor-side generation primitive ---------------------------------------------------

        /// <summary>
        /// Drive the live plugin's skill generator into <paramref name="skillsDir"/> using the
        /// swap-and-restore pattern: point the live config's <c>SkillsPath</c> + <c>ProjectRootPath</c> at
        /// the destination, generate, and restore both in a <c>finally</c> so the persisted configuration is
        /// unchanged after the call. The single home of that pattern — the dock's Skills card
        /// (<see cref="SkillsPanel.OnGeneratePressed"/>) and the <c>godot-skill-generate</c> tool both call
        /// it, so the two can never drift. Throws <see cref="InvalidOperationException"/> when the plugin has
        /// not been built yet.
        /// </summary>
        internal static bool GenerateWithSwapRestore(GodotMcpConnection connection, string skillsDir, string projectRoot)
        {
            var plugin = connection.Plugin
                ?? throw new InvalidOperationException(
                    "Cannot generate skills: the MCP plugin is not initialized yet. Wait for the " +
                    "'[Godot-MCP] plugin loaded' line and retry.");

            var config = connection.Config;
            var originalSkillsPath = config.SkillsPath;
            var originalProjectRoot = config.ProjectRootPath;
            try
            {
                config.SkillsPath = skillsDir;
                config.ProjectRootPath = projectRoot;
                return plugin.GenerateSkillFiles(skillsDir);
            }
            finally
            {
                config.SkillsPath = originalSkillsPath;
                config.ProjectRootPath = originalProjectRoot;
            }
        }

        /// <summary>The absolute Godot project root (<c>res://</c> globalized, trailing slash stripped).</summary>
        internal static string ProjectRoot() => ProjectSettings.GlobalizePath("res://").TrimEnd('/');

        /// <summary>
        /// True when <paramref name="dir"/> resolves INSIDE <paramref name="projectRoot"/>. Belt-and-braces
        /// behind the pure-string guards in <see cref="SkillsToolPaths"/> — this one resolves the real full
        /// paths, so a symlink-free escape that survived the string checks is still refused before any write.
        /// </summary>
        internal static bool IsInsideProject(string dir, string projectRoot)
        {
            try
            {
                var rootFull = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
                var dirFull = Path.GetFullPath(dir).Replace('\\', '/').TrimEnd('/');
                return dirFull == rootFull || dirFull.StartsWith(rootFull + "/", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Create <paramref name="dir"/> (recursively) when missing; throws with the Godot error code.</summary>
        static void EnsureDirectory(string dir)
        {
            if (DirAccess.DirExistsAbsolute(dir))
                return;

            var error = DirAccess.MakeDirRecursiveAbsolute(dir);
            if (error != Error.Ok)
                throw new IOException($"Could not create the skills folder '{dir}' ({error}).");
        }

        /// <summary>
        /// Count the <c>SKILL.md</c> files under <paramref name="dir"/> (the generator emits one per
        /// registered tool, each in its own sub-folder). Best-effort: a read failure reports 0 rather than
        /// failing a generation that already succeeded.
        /// </summary>
        static int CountSkillFiles(string dir)
        {
            try
            {
                return Directory.Exists(dir)
                    ? Directory.GetFiles(dir, "SKILL.md", SearchOption.AllDirectories).Length
                    : 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
#endif
