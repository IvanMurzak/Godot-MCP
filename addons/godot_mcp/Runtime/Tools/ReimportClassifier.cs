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
using System.Linq;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Splits a <c>filesystem-reimport</c> request into the files Godot already IMPORTS, the brand-new files
    /// it COULD import but never has, and the NATIVE project files it cannot import at all.
    ///
    /// <para>
    /// <b>Native files (issue #310).</b> <c>EditorFileSystem.ReimportFiles</c> only accepts files owned by a
    /// <c>ResourceFormatImporter</c> (textures, meshes, audio, fonts …). Godot's NATIVE formats —
    /// <c>.tscn</c>, <c>.tres</c>, <c>.gd</c>, <c>.cs</c>, <c>.gdshader</c> … — are loaded directly and have
    /// no importer, so queueing one makes the editor log
    /// <c>ERROR: BUG: File queued for import, but can't be imported, importer for type '' not found.</c>
    /// while <c>ReimportFiles</c> itself returns nothing. They are refreshed via
    /// <c>EditorFileSystem.UpdateFile</c> instead.
    /// </para>
    ///
    /// <para>
    /// <b>Already-imported files.</b> Godot writes a <c>&lt;file&gt;.import</c> sidecar next to EVERY file
    /// that went through an importer, and never for a native one, so a sidecar is authoritative: the file
    /// goes to <c>ReimportFiles</c>.
    /// </para>
    ///
    /// <para>
    /// <b>Brand-new importable files (D32).</b> A file that has never been imported has no sidecar either,
    /// so the sidecar alone cannot tell a new <c>.png</c> from a <c>.tscn</c>. Routing it to
    /// <c>UpdateFile</c> (the earlier behaviour) does NOT import it — <c>UpdateFile</c> only registers the
    /// file with the editor filesystem, it never runs an importer — while the tool still reported success.
    /// So a sidecar-less file is classified by EXTENSION against the importer extensions the editor actually
    /// has (injected, so plugin-registered importers count), with a native-loader guard on top: Godot's
    /// "every recognised extension" list also contains the native ones, and a file a native loader can load
    /// on its own is never an import candidate. Such a file lands in <see cref="ReimportPlan.NewImportable"/>
    /// and the editor handler must first make it known to the filesystem, then import it, then verify.
    /// </para>
    ///
    /// Pure-managed (every probe is injected as a delegate), so the partition logic is unit-tested in the
    /// plain xUnit host with no Godot filesystem — mirroring <see cref="ResPathNormalizer"/>.
    /// </summary>
    public static class ReimportClassifier
    {
        /// <summary>Suffix of the sidecar Godot writes beside every imported (non-native) project file.</summary>
        public const string ImportSidecarSuffix = ".import";

        /// <summary>The <c>&lt;file&gt;.import</c> sidecar path for a project file.</summary>
        public static string ImportSidecarPath(string resPath) => resPath + ImportSidecarSuffix;

        /// <summary>
        /// The extension of a <c>res://</c> file path, lower-cased and without the dot (<c>"png"</c> for
        /// <c>res://art/Hero.PNG</c>), or an empty string when the file name has none. A dot in a DIRECTORY
        /// name (<c>res://v1.2/readme</c>) is not an extension. Mirrors Godot's <c>String.get_extension()</c>.
        /// </summary>
        public static string ExtensionOf(string resPath)
        {
            if (resPath == null)
                throw new ArgumentNullException(nameof(resPath));

            var slash = resPath.LastIndexOf('/');
            var dot = resPath.LastIndexOf('.');
            return dot > slash ? resPath.Substring(dot + 1).ToLowerInvariant() : string.Empty;
        }

        /// <summary>
        /// Partition <paramref name="paths"/> (already normalized <c>res://</c> file paths) into the
        /// already-imported ones, the brand-new importable ones and the native ones, preserving
        /// first-occurrence order and dropping duplicates (a repeated path must not be imported twice).
        /// </summary>
        /// <param name="paths">Normalized <c>res://</c> file paths.</param>
        /// <param name="importSidecarExists">
        /// Probe answering whether a given sidecar path exists — <c>Godot.FileAccess.FileExists</c> in the
        /// editor, a fake in tests.
        /// </param>
        /// <param name="recognizedExtensions">
        /// Extensions the editor's loaders recognise (with or without a leading dot; compared
        /// case-insensitively). In the editor this is <c>ResourceLoader.GetRecognizedExtensionsForType("")</c>,
        /// which lists the importer's extensions AND the native loaders' — <paramref name="loadsNatively"/>
        /// filters the latter, so it is required: without it a sidecar-less <c>.tscn</c> would be queued for
        /// import again (#310).
        /// </param>
        /// <param name="loadsNatively">
        /// Probe answering whether a non-importer (native) loader can load the file as it is —
        /// <c>ResourceLoader.Exists</c> in the editor, which is true for a sidecar-less <c>.tscn</c>/<c>.gd</c>
        /// and false for a sidecar-less <c>.png</c> (only the importer loader handles that, and only once a
        /// sidecar exists). When it answers true the file is native whatever its extension.
        /// </param>
        public static ReimportPlan Plan(
            IEnumerable<string> paths,
            Func<string, bool> importSidecarExists,
            IEnumerable<string> recognizedExtensions,
            Func<string, bool> loadsNatively)
        {
            if (paths == null)
                throw new ArgumentNullException(nameof(paths));
            if (importSidecarExists == null)
                throw new ArgumentNullException(nameof(importSidecarExists));
            if (recognizedExtensions == null)
                throw new ArgumentNullException(nameof(recognizedExtensions));
            if (loadsNatively == null)
                throw new ArgumentNullException(nameof(loadsNatively));

            var importExts = new HashSet<string>(
                recognizedExtensions
                    .Where(e => !string.IsNullOrWhiteSpace(e))
                    .Select(e => e.Trim().TrimStart('.').ToLowerInvariant()),
                StringComparer.Ordinal);

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var importable = new List<string>();
            var newImportable = new List<string>();
            var native = new List<string>();

            foreach (var path in paths)
            {
                if (!seen.Add(path))
                    continue;

                if (importSidecarExists(ImportSidecarPath(path)))
                    importable.Add(path);
                else if (importExts.Contains(ExtensionOf(path)) && !loadsNatively(path))
                    newImportable.Add(path);
                else
                    native.Add(path);
            }

            return new ReimportPlan(importable, newImportable, native);
        }

        /// <summary>
        /// Whether the text of a <c>&lt;file&gt;.import</c> sidecar records a SUCCESSFUL import. Godot writes
        /// the sidecar even when the importer fails (e.g. a corrupt PNG) and marks that case with
        /// <c>valid=false</c> in the <c>[remap]</c> section, so "the sidecar exists" alone is not proof of an
        /// import. Any other content (no <c>valid</c> key, or <c>valid=true</c>) counts as imported.
        /// </summary>
        public static bool SidecarRecordsValidImport(string? sidecarText)
        {
            if (string.IsNullOrWhiteSpace(sidecarText))
                return false;

            var inRemap = false;
            foreach (var rawLine in sidecarText!.Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.StartsWith("[", StringComparison.Ordinal))
                {
                    inRemap = string.Equals(line, "[remap]", StringComparison.Ordinal);
                    continue;
                }

                if (!inRemap)
                    continue;

                var eq = line.IndexOf('=');
                if (eq <= 0)
                    continue;

                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                if (string.Equals(key, "valid", StringComparison.Ordinal) &&
                    string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        /// <summary>Reason: no sidecar exists after the import.</summary>
        public const string NoSidecarReason =
            "Godot wrote no '.import' sidecar, so the file was not imported — see the editor log";

        /// <summary>Reason: the sidecar predates the request, so this request's import never ran.</summary>
        public const string StaleSidecarReason =
            "Godot did not rewrite the '.import' sidecar during this request, so the import did not run — " +
            "the editor may have been busy importing or scanning, so retry or run filesystem-reimport without 'files'";

        /// <summary>Reason: the sidecar exists but could not be read.</summary>
        public const string UnreadableSidecarReason =
            "the '.import' sidecar could not be read, so the import could not be verified";

        /// <summary>Reason: Godot's importer recorded a failure.</summary>
        public const string ImporterFailedReason =
            "Godot's importer failed (the '.import' sidecar is marked valid=false) — the file may be corrupt " +
            "or in an unsupported format, see the editor log";

        /// <summary>
        /// Decide whether ONE file was imported by the current request. Godot rewrites the sidecar on every
        /// import attempt, so a sidecar last written BEFORE the request started proves only that an earlier
        /// import happened — <c>ReimportFiles</c> returns nothing and silently skips a file it cannot find, so
        /// an untouched sidecar must not count as this request's success. The comparison is in whole seconds
        /// (Godot's <c>FileAccess.GetModifiedTime</c> resolution), so a sidecar written in the same second the
        /// request started is accepted.
        /// </summary>
        /// <param name="sidecarText">The sidecar's text, or null when no sidecar exists.</param>
        /// <param name="sidecarModifiedUnix">The sidecar's modification time, Unix seconds.</param>
        /// <param name="requestStartedUnix">When the request started, Unix seconds (rounded down).</param>
        public static ImportOutcome JudgeImport(string? sidecarText, ulong sidecarModifiedUnix, ulong requestStartedUnix)
        {
            if (sidecarText == null)
                return ImportOutcome.Failure(NoSidecarReason);
            if (sidecarModifiedUnix < requestStartedUnix)
                return ImportOutcome.Failure(StaleSidecarReason);
            if (string.IsNullOrWhiteSpace(sidecarText))
                return ImportOutcome.Failure(UnreadableSidecarReason);
            return SidecarRecordsValidImport(sidecarText)
                ? ImportOutcome.Success()
                : ImportOutcome.Failure(ImporterFailedReason);
        }
    }

    /// <summary>
    /// The outcome of <see cref="ReimportClassifier.Plan"/>: which files to reimport, which to import for the
    /// first time, and which to refresh.
    /// </summary>
    public sealed class ReimportPlan
    {
        public ReimportPlan(IReadOnlyList<string> importable, IReadOnlyList<string> newImportable, IReadOnlyList<string> native)
        {
            Importable = importable;
            NewImportable = newImportable;
            Native = native;
        }

        /// <summary>Files owned by an importer (they have a sidecar) — handed to <c>EditorFileSystem.ReimportFiles</c>.</summary>
        public IReadOnlyList<string> Importable { get; }

        /// <summary>
        /// Brand-new files with an importer extension and no sidecar yet. The editor must first make each one
        /// known to <c>EditorFileSystem</c>, then <c>ReimportFiles</c> it, then verify the import happened.
        /// </summary>
        public IReadOnlyList<string> NewImportable { get; }

        /// <summary>
        /// Native project files (<c>.tscn</c>/<c>.tres</c>/<c>.gd</c>/…) and files of an unknown type — these
        /// must NOT be queued for import; they are refreshed with <c>EditorFileSystem.UpdateFile</c> instead.
        /// </summary>
        public IReadOnlyList<string> Native { get; }

        /// <summary>
        /// Human-readable summary of what happened, for the tool's result string. <paramref name="outcomes"/>
        /// carries the VERIFIED result of every file routed to the importer (keyed by path); a routed file
        /// with no outcome is reported as not imported — the text never claims an import that was not
        /// observed. Files that failed are listed first (in request order), each with its reason.
        /// </summary>
        public string Describe(IReadOnlyDictionary<string, ImportOutcome> outcomes)
        {
            if (outcomes == null)
                throw new ArgumentNullException(nameof(outcomes));

            if (Importable.Count == 0 && NewImportable.Count == 0 && Native.Count == 0)
                return "No files to refresh";

            bool Imported(string path) => outcomes.GetValueOrDefault(path)?.Imported == true;

            var failed = Importable.Concat(NewImportable)
                .Where(path => !Imported(path))
                .Select(path => $"{path} ({outcomes.GetValueOrDefault(path)?.Reason ?? ImportOutcome.NoOutcomeReason})")
                .ToList();
            var reimported = Importable.Count(Imported);
            var imported = NewImportable.Where(Imported).ToList();

            var parts = new List<string>();
            if (failed.Count > 0)
                parts.Add($"NOT imported {failed.Count} file(s): {string.Join("; ", failed)}");
            if (reimported > 0)
                parts.Add($"Reimported {reimported} file(s)");
            if (imported.Count > 0)
                parts.Add($"Imported {imported.Count} new file(s): {string.Join(", ", imported)}");
            if (Native.Count > 0)
                parts.Add(parts.Count == 0
                    ? $"Refreshed {Native.Count} native file(s) (no importer handles them — Godot's native formats " +
                      "or unknown types — so they were refreshed via EditorFileSystem.UpdateFile, not imported)"
                    : $"refreshed {Native.Count} native file(s) (.tscn/.tres/.gd and friends have no importer, " +
                      "so they were refreshed via EditorFileSystem.UpdateFile rather than queued for import)");

            return string.Join("; ", parts);
        }
    }

    /// <summary>The verified result of routing one file through Godot's importer.</summary>
    public sealed class ImportOutcome
    {
        /// <summary>Reason used when the handler recorded no outcome for a routed file.</summary>
        public const string NoOutcomeReason = "no import result was observed";

        ImportOutcome(string? reason) => Reason = reason;

        /// <summary>True only when the import was OBSERVED to succeed (a valid sidecar exists afterwards).</summary>
        public bool Imported => Reason == null;

        /// <summary>Why the file was not imported; null when <see cref="Imported"/> is true.</summary>
        public string? Reason { get; }

        public static ImportOutcome Success() => new ImportOutcome(null);

        public static ImportOutcome Failure(string reason)
            => new ImportOutcome(string.IsNullOrWhiteSpace(reason) ? NoOutcomeReason : reason);
    }
}
