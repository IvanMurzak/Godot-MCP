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
using System.Collections.Generic;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Splits a <c>filesystem-reimport</c> request into the files Godot can actually IMPORT and the NATIVE
    /// project files it cannot — the fix for issue #310.
    ///
    /// <para>
    /// <b>The defect.</b> <c>EditorFileSystem.ReimportFiles</c> only accepts files owned by a
    /// <c>ResourceFormatImporter</c> (textures, meshes, audio, fonts …). Godot's NATIVE formats —
    /// <c>.tscn</c>, <c>.tres</c>, <c>.gd</c>, <c>.cs</c>, <c>.gdshader</c> … — are loaded directly and have
    /// no importer, so queueing one makes the editor log
    /// <c>ERROR: BUG: File queued for import, but can't be imported, importer for type '' not found.</c>
    /// while <c>ReimportFiles</c> itself returns nothing. The tool then reported success and the agent
    /// believed the scene had been refreshed.
    /// </para>
    ///
    /// <para>
    /// <b>The signal.</b> Godot writes a <c>&lt;file&gt;.import</c> sidecar next to EVERY file that goes
    /// through an importer, and never for a native one. That is authoritative and dynamic (it follows
    /// whatever importers — including plugin-registered ones — the project actually has), unlike an
    /// extension allow-list which would rot. A genuinely importable file that has not been scanned yet also
    /// has no sidecar; it is classified native and refreshed via <c>EditorFileSystem.UpdateFile</c>, which is
    /// exactly the call that makes Godot notice it and generate the import — so the degradation is benign.
    /// </para>
    ///
    /// Pure-managed (the sidecar probe is injected as a delegate), so the partition logic is unit-tested in
    /// the plain xUnit host with no Godot filesystem — mirroring <see cref="ResPathNormalizer"/>.
    /// </summary>
    public static class ReimportClassifier
    {
        /// <summary>Suffix of the sidecar Godot writes beside every imported (non-native) project file.</summary>
        public const string ImportSidecarSuffix = ".import";

        /// <summary>The <c>&lt;file&gt;.import</c> sidecar path for a project file.</summary>
        public static string ImportSidecarPath(string resPath) => resPath + ImportSidecarSuffix;

        /// <summary>
        /// Partition <paramref name="paths"/> (already normalized <c>res://</c> file paths) into the
        /// importable ones and the native ones, preserving first-occurrence order and dropping duplicates
        /// (a repeated path must not be imported twice).
        /// </summary>
        /// <param name="paths">Normalized <c>res://</c> file paths.</param>
        /// <param name="importSidecarExists">
        /// Probe answering whether a given sidecar path exists — <c>Godot.FileAccess.FileExists</c> in the
        /// editor, a fake in tests.
        /// </param>
        public static ReimportPlan Plan(IEnumerable<string> paths, Func<string, bool> importSidecarExists)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            if (importSidecarExists == null)
                throw new ArgumentNullException(nameof(importSidecarExists));

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var importable = new List<string>();
            var native = new List<string>();

            foreach (var path in paths)
            {
                if (!seen.Add(path))
                    continue;

                if (importSidecarExists(ImportSidecarPath(path)))
                    importable.Add(path);
                else
                    native.Add(path);
            }

            return new ReimportPlan(importable, native);
        }
    }

    /// <summary>The outcome of <see cref="ReimportClassifier.Plan"/>: which files to import, which to refresh.</summary>
    public sealed class ReimportPlan
    {
        public ReimportPlan(IReadOnlyList<string> importable, IReadOnlyList<string> native)
        {
            Importable = importable;
            Native = native;
        }

        /// <summary>Files owned by an importer — safe to hand to <c>EditorFileSystem.ReimportFiles</c>.</summary>
        public IReadOnlyList<string> Importable { get; }

        /// <summary>
        /// Native project files (<c>.tscn</c>/<c>.tres</c>/<c>.gd</c>/…) — these must NOT be queued for
        /// import; they are refreshed with <c>EditorFileSystem.UpdateFile</c> instead.
        /// </summary>
        public IReadOnlyList<string> Native { get; }

        /// <summary>Human-readable summary of what was done, for the tool's result string.</summary>
        public string Describe()
        {
            if (Importable.Count == 0 && Native.Count == 0)
                return "No files to refresh";

            if (Importable.Count > 0 && Native.Count > 0)
                return $"Reimported {Importable.Count} file(s); refreshed {Native.Count} native file(s) " +
                       "(.tscn/.tres/.gd and friends have no importer, so they were refreshed via " +
                       "EditorFileSystem.UpdateFile rather than queued for import)";

            if (Importable.Count > 0)
                return $"Reimported {Importable.Count} file(s)";

            return $"Refreshed {Native.Count} native file(s) (no importer — none of them are imported assets)";
        }
    }
}
