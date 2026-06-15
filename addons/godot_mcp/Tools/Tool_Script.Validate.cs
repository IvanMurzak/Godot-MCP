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
using System.Collections.Generic;
using System.ComponentModel;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    public partial class Tool_Script
    {
        public const string ScriptValidateToolId = "script-validate";

        // Cap a full-project scan so a huge project can't validate thousands of files in one call. A targeted
        // single-path validation ignores this. The agent can target a path when the project is large.
        const int FullScanFileCap = 500;

        [AiTool
        (
            ScriptValidateToolId,
            Title = "Script / Validate",
            ReadOnlyHint = true,
            IdempotentHint = true,
            OpenWorldHint = false
        )]
        [Description("Validate GDScript ('.gd') files and return STRUCTURED parse/compile diagnostics — the " +
            "on-demand 'is the project error-free?' query. Closes the gap where the agent gets NO feedback on " +
            "GDScript parse errors and assumes the game runs fine.\n" +
            "Inputs:\n" +
            "  - 'scriptPath' (optional): a single res:// '.gd' path to validate. Omit to scan every '.gd' " +
            "under res:// (capped at " + nameof(FullScanFileCap) + ").\n" +
            "Result (ScriptDiagnosticsResult): 'ok' (true when no errors), 'diagnostics' [{ path, line, " +
            "message, severity }], counts, a 'truncated' flag (true when a full scan hit the file cap, so " +
            "'ok' is NOT a whole-project all-clear), and a 'fidelity' note:\n" +
            "  - Godot 4.5+: 'Precise' — exact line + message captured via the engine Logger hook.\n" +
            "  - Godot < 4.5: 'Coarse' — per-file pass/fail with the engine error code (no line/message text).\n" +
            "C# ('.cs') files are NOT validated here (Godot has no in-editor C# compiler to reach cheaply); " +
            "their compile errors surface through the project build. Pair with 'console-get-logs', which on " +
            "Godot 4.5+ now also captures engine GDScript parse errors passively.")]
        public ScriptDiagnosticsResult Validate
        (
            [Description("Optional single res:// '.gd' script path to validate, e.g. 'res://scripts/player.gd'. " +
                "Omit (null/empty) to scan every '.gd' file under res://.")]
            string? scriptPath = null
        )
        {
            return MainThread.Instance.Run(() =>
            {
                var targets = ResolveTargets(scriptPath, out var truncated);

                var result = new ScriptDiagnosticsResult
                {
                    ScannedPaths = targets,
                    ScannedCount = targets.Count,
                    Truncated = truncated,
                    // Precise ONLY when a live capture session actually exists (the 4.5 Logger hook was
                    // installed). Keying off a compile-time "is 4.5 compiled in?" flag would over-claim Precise
                    // on a 4.5 build where TryInstall returned null / Current is null and the coarse per-file
                    // Reload()-code path is the one actually taken.
                    Fidelity = ScriptErrorCapture.Current != null
                        ? ScriptDiagnosticsFidelity.Precise
                        : ScriptDiagnosticsFidelity.Coarse,
                };

                var diagnostics = new List<ScriptDiagnostic>();
                foreach (var path in targets)
                    diagnostics.AddRange(ValidateOne(path));

                // Errors first, then warnings, each in scan order.
                diagnostics.Sort((a, b) => a.Severity.CompareTo(b.Severity));
                result.Diagnostics = diagnostics;
                result.ErrorCount = diagnostics.FindAll(d => d.Severity == ScriptDiagnosticSeverity.Error).Count;
                result.WarningCount = diagnostics.FindAll(d => d.Severity == ScriptDiagnosticSeverity.Warning).Count;
                result.Ok = result.ErrorCount == 0;
                result.Note = BuildNote(result);
                return result;
            });
        }

        /// <summary>
        /// Resolve the set of '.gd' paths to validate: a single explicit path (validated for res:// + '.gd'),
        /// or a full res:// scan capped at <see cref="FullScanFileCap"/>. Main-thread only (scan touches the
        /// editor filesystem). C# paths are rejected for the single-path case with an actionable message.
        /// </summary>
        static List<string> ResolveTargets(string? scriptPath, out bool truncated)
        {
            truncated = false;
            var raw = (scriptPath ?? string.Empty).Trim();
            if (raw.Length > 0)
            {
                var resPath = ScriptLang_.RequireScriptResPath(raw, nameof(scriptPath), out var lang);
                if (lang != ScriptLang.GDScript)
                    throw new ArgumentException(
                        $"'{ScriptValidateToolId}' validates GDScript ('.gd') only; '{resPath}' is C#. " +
                        $"C# compile errors surface through the project build, not this tool.", nameof(scriptPath));
                if (!FileAccess.FileExists(resPath))
                    throw new System.IO.FileNotFoundException($"No script file exists at '{resPath}'.", resPath);
                return new List<string> { resPath };
            }

            var found = new List<string>();
            // Walk res:// with DirAccess rather than the editor filesystem INDEX: the index is empty/partial
            // mid-scan (e.g. right after editor boot, IsScanning()==true) so an index walk silently returns 0
            // scripts, whereas DirAccess reads the real directory tree on disk regardless of scan state — the
            // same robust path the single-file FileAccess.FileExists check above relies on. Main-thread only.
            CollectGdScripts(ResPathNormalizer.ResScheme, found);
            if (found.Count > FullScanFileCap)
            {
                truncated = true; // surface the cap so an ok:true result isn't read as a whole-project all-clear.
                found = found.GetRange(0, FullScanFileCap);
            }
            return found;
        }

        /// <summary>
        /// Depth-first walk of the on-disk <c>res://</c> tree (via <see cref="DirAccess"/>) collecting every
        /// '.gd' file path. Skips hidden dot-directories (e.g. <c>.godot/</c>) so generated caches aren't
        /// validated. Main-thread only (touches Godot <see cref="DirAccess"/>).
        /// </summary>
        static void CollectGdScripts(string dirResPath, List<string> into)
        {
            using var dir = DirAccess.Open(dirResPath);
            if (dir == null)
                return;

            dir.IncludeNavigational = false; // skip "." and ".."
            dir.IncludeHidden = false;       // skip .godot/, .import, etc.

            // Files first.
            foreach (var fileName in dir.GetFiles())
            {
                if (ScriptLang_.TryGetLang(fileName, out var lang) && lang == ScriptLang.GDScript)
                    into.Add(dirResPath + fileName);
            }

            // Then recurse into sub-directories. Defensively skip any dot-directory the flags missed.
            foreach (var subName in dir.GetDirectories())
            {
                if (subName.StartsWith('.'))
                    continue;
                CollectGdScripts(dirResPath + subName + "/", into);
            }
        }

        /// <summary>
        /// Validate a single '.gd' file and return its diagnostics. On Godot 4.5+ a capture session harvests
        /// the engine's precise <c>ERROR_TYPE_SCRIPT</c> callbacks emitted during a deliberate
        /// <see cref="GDScript.Reload"/>; on older versions only the <c>Reload()</c> <see cref="Error"/> code
        /// is available, yielding a single coarse diagnostic with line -1. Main-thread only (touches GDScript).
        /// </summary>
        static List<ScriptDiagnostic> ValidateOne(string resPath)
        {
            var diagnostics = new List<ScriptDiagnostic>();

            // Load the on-disk source through a throwaway GDScript instance (same probe technique as the
            // pre-write ValidateSyntax helper, but reading the file rather than a candidate string).
            string content;
            using (var file = FileAccess.Open(resPath, FileAccess.ModeFlags.Read))
            {
                if (file == null)
                {
                    diagnostics.Add(new ScriptDiagnostic(resPath, -1,
                        $"Failed to open for validation: {FileAccess.GetOpenError()}."));
                    return diagnostics;
                }
                content = file.GetAsText();
            }

            // Probe with a throwaway GDScript. Do NOT set ResourcePath to the REAL res:// path: Reload()
            // would then register/replace this throwaway in Godot's global class registry and resource cache,
            // perturbing the live cached script. Leaving ResourcePath unset keeps the probe out of the cache;
            // the engine still reports the source path via the Logger hook (and we stamp resPath on any rows
            // that lack one below). Dispose it after use, mirroring the using blocks for FileAccess/DirAccess.
            // Current is non-null only on a 4.5+ build where the engine Logger hook installed (the #else stub
            // never sets it), so it alone decides precise-vs-coarse — no separate compile-time const needed.
            var capture = ScriptErrorCapture.Current;
            capture?.BeginSession(resPath);
            Error err;
            using (var probe = new GDScript { SourceCode = content })
            {
                try
                {
                    err = probe.Reload(keepState: false);
                }
                finally
                {
                    if (capture != null)
                    {
                        var captured = capture.EndSession();
                        diagnostics.AddRange(captured);
                    }
                }
            }

            // 4.5+ precise path: the session captured the engine's script errors for THIS file (the session's
            // path-match filter restricts rows to resPath, plus any pathless rows the engine emitted, which are
            // stamped with resPath below). When present, those structured rows ARE the diagnostics.
            if (capture != null && diagnostics.Count > 0)
            {
                // Stamp the path on any rows the engine reported without a file (defensive).
                foreach (var d in diagnostics)
                    if (string.IsNullOrEmpty(d.Path))
                        d.Path = resPath;
                return diagnostics;
            }

            // Coarse path (Godot < 4.5, OR 4.5 reported no structured row but Reload still failed): emit a
            // single per-file diagnostic from the Error code. ParseError is a genuine syntax error; other
            // non-Ok results MAY be a resolution failure the standalone probe can't see (extends/class_name/
            // preload of project state) — mirror ValidateSyntax and treat only ParseError as a hard error.
            if (err == Error.ParseError)
            {
                diagnostics.Add(new ScriptDiagnostic(resPath, -1,
                    capture != null
                        ? $"{err} (engine reported no structured location)."
                        : $"{err} (Godot < 4.5: no line/message detail available; open the script in the editor)."));
            }

            return diagnostics;
        }

        /// <summary>
        /// Compose the human-readable summary note, including a fidelity caveat (Godot &lt; 4.5) and a
        /// truncation hint (full-scan cap hit) where relevant.
        /// </summary>
        static string BuildNote(ScriptDiagnosticsResult result)
        {
            string note;
            if (result.Ok)
            {
                note = $"No GDScript errors found ({result.ScannedCount} scanned).";
                if (result.Fidelity == ScriptDiagnosticsFidelity.Coarse)
                    note += " NOTE: Godot < 4.5 — only a per-file pass/fail was checked (no semantic detail).";
            }
            else
            {
                var noun = result.ErrorCount == 1 ? "error" : "errors";
                note = $"{result.ErrorCount} {noun} across {result.ScannedCount} scanned script(s).";
                if (result.WarningCount > 0)
                    note += $" {result.WarningCount} warning(s).";
                if (result.Fidelity == ScriptDiagnosticsFidelity.Coarse)
                    note += " NOTE: Godot < 4.5 — line/message detail is unavailable; open the failing script in the editor.";
            }

            if (result.Truncated)
                note += $" NOTE: full-scan cap of {FullScanFileCap} files hit — only the first {result.ScannedCount} " +
                        "'.gd' files were validated, so this is NOT a whole-project all-clear; validate a specific path/sub-tree to cover the rest.";

            return note;
        }
    }
}
#endif
