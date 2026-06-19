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
using System.Threading;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Script tool family (<c>script-*</c>) — the Godot analog of Unity-MCP's <c>Tool_Script</c>, covering
    /// BOTH of Godot's first-class scripting languages: C# (<c>.cs</c>) and GDScript (<c>.gd</c>). Each tool
    /// method lives in its own partial-class file (Read / Create / Update / Delete / AttachToNode) and
    /// drives the Godot editor's script + filesystem pipeline through the main-thread dispatcher.
    ///
    /// <para>
    /// Godot ↔ Unity mapping: a Godot <see cref="Script"/> file on disk ↔ a Unity <c>MonoScript</c>
    /// <c>.cs</c>; <c>script-read</c>/<c>script-update-or-create</c>/<c>script-delete</c> map 1:1; attaching a
    /// script to a node (<c>node.SetScript</c>) ↔ Unity's "add a MonoBehaviour component". The key
    /// difference from Unity: Godot has no Roslyn <c>script-execute</c> (Unity compiles + runs arbitrary C#
    /// at runtime), so dynamic code-execution is intentionally out of scope here — this family is file CRUD
    /// + attach only.
    /// </para>
    ///
    /// <para>
    /// Compile/reload discipline: a freshly-written or deleted <c>.cs</c> only takes effect after the project
    /// assembly is rebuilt — so the C# write/delete handlers reimport through the editor filesystem and
    /// BOUNDED-settle (mirroring <see cref="Tool_FileSystem"/>'s reimport settle loop) before returning, so a
    /// follow-up tool sees a consistent state. A <c>.gd</c> file is interpreted and re-parsed on load, so it
    /// needs only a filesystem update, not a build.
    /// </para>
    ///
    /// Editor-only (<c>#if TOOLS</c>): the handlers touch <see cref="EditorInterface"/> and live
    /// <see cref="Script"/>/<see cref="Node"/> objects. The pure-managed pieces
    /// (<see cref="Data.ScriptInfo"/>, <see cref="ScriptLang_"/>, <see cref="ResPathNormalizer"/>) live
    /// outside this guard and are unit-tested.
    /// </summary>
    [AiToolType]
    public partial class Tool_Script
    {
        // Shared bounded-settle tunables for a C# rebuild, mirroring Tool_FileSystem.Reimport. The
        // MainThread dispatcher already runs us on the editor main thread, so a short Thread.Sleep yields
        // without re-entrancy issues.
        const int SettleSleepMs = 25;
        const int SettleMaxWaits = 200;   // 200 * 25ms = 5s settle ceiling

        /// <summary>
        /// Make a freshly-written/removed script visible to the editor filesystem and, for C#, drain any
        /// resulting import/build scan (bounded). A <c>.gd</c> file is interpreted, so it only needs the
        /// filesystem to learn about it; a <c>.cs</c> file changes the compiled assembly, so we reimport it
        /// and wait for the editor's filesystem scan to settle. Returns a short status note. Main-thread only.
        /// </summary>
        static string RefreshAndSettle(string resPath, ScriptLang lang, bool removed)
        {
            var efs = EditorInterface.Singleton.GetResourceFilesystem();
            if (efs == null)
                return "Editor resource filesystem unavailable; change written but not reimported.";

            // GDScript: interpreted, no build. A full filesystem scan picks up an add/remove; a single-file
            // update just needs UpdateFile so the editor re-reads it.
            if (lang == ScriptLang.GDScript)
            {
                if (removed)
                    efs.Scan();
                else
                    efs.UpdateFile(resPath);
                return removed ? "GDScript removed; filesystem rescanned." : "GDScript written; filesystem updated.";
            }

            // C#: the assembly must pick up the change. Reimport the specific file (add/update) or rescan
            // (remove), then bound-wait for the editor filesystem scan to settle so a follow-up tool sees a
            // consistent state. We do NOT block on the actual MSBuild here — Godot rebuilds the C# assembly
            // out-of-band (on focus / explicit Build) and a headless editor may never run it; settling the
            // filesystem scan is the bounded, reliable signal we can observe.
            if (removed)
            {
                efs.Scan();
            }
            else
            {
                efs.ReimportFiles(new[] { resPath });
            }

            var waits = 0;
            while (efs.IsScanning() && waits < SettleMaxWaits)
            {
                Thread.Sleep(SettleSleepMs);
                waits++;
            }

            var settled = !efs.IsScanning();
            var verb = removed ? "removed" : "written";
            return settled
                ? $"C# script {verb}; filesystem settled (rebuild the project to load the new assembly)."
                : $"C# script {verb}; filesystem still scanning after {SettleMaxWaits * SettleSleepMs}ms " +
                  $"(progress={efs.GetScanningProgress():0.00}).";
        }

        /// <summary>
        /// Build a <see cref="ScriptInfo"/> identity record (no content). Main-thread-agnostic — operates on
        /// already-captured strings.
        /// </summary>
        static ScriptInfo ToScriptInfo(string resPath, ScriptLang lang, string? status = null)
            => new ScriptInfo
            {
                ResourcePath = resPath,
                Language = lang.ToString(),
                Status = status,
            };

        /// <summary>
        /// Best-effort pre-write syntax validation. For GDScript we can actually compile-check the source via
        /// <see cref="GDScript.Reload"/> on a parser-fed instance, so an obviously-broken <c>.gd</c> is
        /// rejected before it touches disk. For C# there is no in-editor compiler we can reach cheaply (Godot
        /// builds via out-of-band MSBuild), so it is accepted as-is and the post-write build settle is what
        /// surfaces real compile errors. Returns true when the content is acceptable; on a GDScript parse
        /// failure returns false and sets <paramref name="error"/>. Main-thread only (touches GDScript).
        ///
        /// <para>
        /// LIMITATION (parse-only): the throwaway <see cref="GDScript.Reload"/> probe can return a non-Ok
        /// Error for reasons that are NOT a syntax error — e.g. the source <c>extends</c> a project type,
        /// declares a <c>class_name</c>, or <c>preload(...)</c>s a resource that the standalone probe cannot
        /// resolve. Hard-rejecting on those would falsely block a syntactically-valid <c>.gd</c> that merely
        /// references project state. So we ONLY hard-reject an actual parse/syntax error
        /// (<see cref="Error.ParseError"/>); any other non-Ok Reload result is treated as ACCEPTED
        /// best-effort (parallel to the C# path) and the real load is left to the editor's full re-parse.
        /// </para>
        /// </summary>
        static bool ValidateSyntax(string content, ScriptLang lang, out string? error)
        {
            error = null;
            if (lang != ScriptLang.GDScript)
                return true; // C#: no cheap in-editor check; the build settle is the real gate.

            // Feed the source into a throwaway GDScript instance and reload it. Reload() parses + compiles the
            // source; a genuine syntax error surfaces as Error.ParseError. Other non-Ok results (a resolution
            // failure against project state the standalone probe can't see — extends/class_name/preload) are
            // NOT syntax errors, so we accept them best-effort rather than misclassify them as a parse failure.
            var probe = new GDScript { SourceCode = content };
            var err = probe.Reload(keepState: false);
            if (err == Error.ParseError)
            {
                error = $"GDScript failed to parse ({err}). Fix the syntax and retry.";
                return false;
            }
            return true;
        }

        /// <summary>
        /// Write <paramref name="content"/> to <paramref name="resPath"/> (creating parent directories as
        /// needed — Godot's FileAccess does NOT auto-create them), then refresh + bounded-settle. Main-thread
        /// only. Caller is responsible for the create-vs-update existence precondition.
        /// </summary>
        static ScriptInfo WriteScript(string resPath, ScriptLang lang, string content, string verb)
        {
            // FileAccess.Open(...Write) does not create missing parent directories — make them first so a
            // nested target (e.g. 'res://scripts/ai/Brain.cs') works, mirroring Tool_Resource.Create.
            var slash = resPath.LastIndexOf('/');
            var targetDir = slash >= 0 ? resPath.Substring(0, slash + 1) : ResPathNormalizer.ResScheme;
            if (!DirAccess.DirExistsAbsolute(targetDir))
            {
                var mkErr = DirAccess.MakeDirRecursiveAbsolute(targetDir);
                if (mkErr != Error.Ok)
                    throw new Exception($"Failed to create target directory '{targetDir}': {mkErr}.");
            }

            using (var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Write))
            {
                if (file == null)
                    throw new Exception($"Failed to open '{resPath}' for writing: {FileAccess.GetOpenError()}.");
                file.StoreString(content);
            }

            var status = RefreshAndSettle(resPath, lang, removed: false);
            return ToScriptInfo(resPath, lang, $"Script {verb}; {status}");
        }
    }
}
#endif
