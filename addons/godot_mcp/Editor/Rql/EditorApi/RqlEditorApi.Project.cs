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
using System.Collections.Generic;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;
using Rql.Execution;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>The project sources of the <c>editor</c> API: <c>files</c> (the editor filesystem) and <c>classes</c> (ClassDB).</summary>
    public sealed partial class RqlEditorApi
    {
        /// <summary>
        /// The <c>files</c> source: every file the editor filesystem knows, depth-first, at most
        /// <see cref="RqlGodot.MaxFiles"/>; lazy, so <c>files | first 5</c> walks only that far.
        /// </summary>
        public static IEnumerable<RqlFile> Files()
        {
            var references = new FileReferences(); // one per query: deps read once, the usedBy index built on first use
            return RqlGodot.WalkFiles(EditorToolGuards.GetResourceFileSystemOrThrow().GetFilesystem())
                .Select(f => new RqlFile(f.path, f.type, references))
                .Take(RqlGodot.MaxFiles);
        }

        /// <summary>
        /// The <c>folders</c> source: every folder the editor filesystem knows (res:// first, depth-first) with its direct file and
        /// subfolder counts, so empty folders are found with <c>folders | where empty</c> (the <c>files</c> source lists files only).
        /// </summary>
        public static IEnumerable<RqlFolder> Folders()
        {
            var stack = new Stack<EditorFileSystemDirectory>();
            stack.Push(EditorToolGuards.GetResourceFileSystemOrThrow().GetFilesystem());
            while (stack.Count > 0)
            {
                var dir = stack.Pop();
                yield return new RqlFolder(dir.GetPath(), dir.GetName(), dir.GetFileCount(), dir.GetSubdirCount());
                for (var i = dir.GetSubdirCount() - 1; i >= 0; i--) stack.Push(dir.GetSubdir(i));
            }
        }

        /// <summary>The <c>classes</c> source: every Godot class (ClassDB), sorted by name (the list is read once per session).</summary>
        public static List<RqlClass> Classes() => GodotTypes.GodotClasses().Select(c => new RqlClass(c)).ToList();
    }

    /// <summary>One folder of the project (the <c>folders</c> source).</summary>
    public sealed class RqlFolder
    {
        internal RqlFolder(string path, string name, int fileCount, int folderCount)
        {
            this.path = path;
            this.name = name;
            this.fileCount = fileCount;
            this.folderCount = folderCount;
        }

        [RqlDescription("res:// path of the folder (with a trailing /)")]
        public string path { get; }

        [RqlDescription("folder name")]
        public string name { get; }

        [RqlDescription("files directly in it (the editor's view: imported sidecars are not counted)")]
        public int fileCount { get; }

        [RqlDescription("subfolders directly in it")]
        public int folderCount { get; }

        [RqlDescription("no files and no subfolders in the editor's view (delete it with editor | call DeleteFolder(path), which " +
            "also refuses files the editor does not list)")]
        public bool empty => fileCount == 0 && folderCount == 0;
    }

    /// <summary>One file of the project (the <c>files</c> source).</summary>
    public sealed class RqlFile
    {
        private readonly FileReferences _references;

        internal RqlFile(string path, string type, FileReferences references)
        {
            this.path = path;
            this.type = type;
            _references = references;
        }

        [RqlDescription("res:// path")]
        public string path { get; }

        [RqlDescription("file name with extension")]
        public string name => path.Substring(path.LastIndexOf('/') + 1);

        [RqlDescription("res:// folder (with a trailing slash)")]
        public string dir => ResPathNormalizer.ParentDir(path);

        [RqlDescription("extension without the dot, lower case (\"tscn\", \"png\")")]
        public string ext => ReimportClassifier.ExtensionOf(path);

        [RqlDescription("resource type the importer assigned (\"PackedScene\", \"Texture2D\" …), empty when unknown")]
        public string type { get; }

        [RqlDescription("uid:// of the file, or null")]
        public string? uid => RqlGodot.UidOf(path);

        [RqlDescription("bytes of the file on disk (largest files: files | sort size desc | first 5); on request")]
        public long size => FileBytes(path);

        [RqlDescription("bytes of everything the import produced (the .godot/imported files its .import lists, every platform variant; " +
            "an export packs its platform's instead of the source), null for a file with no import output; on request")]
        public long? importedSize => ImportSettings.DestFiles(path) is { Length: > 0 } files ? files.Sum(FileBytes) : (long?)null;

        private static long FileBytes(string resPath)
        {
            var file = new System.IO.FileInfo(ProjectSettings.GlobalizePath(resPath));
            return file.Exists ? file.Length : 0;
        }

        [RqlDescription("res:// paths this file loads (a material's textures, a scene's sub-scenes, scripts and resources …), as saved on disk")]
        public List<string> deps => _references.DepsOf(path);

        [RqlDescription("dependencies whose file no longer exists: broken references")]
        public List<string> missingDeps => _references.MissingDepsOf(path);

        [RqlDescription("the scenes and resources whose deps include this one, as saved (scripts' load / preload, project settings such as " +
            "the main scene, icon or autoloads, and unsaved edits are not seen); the first usedBy of a query scans the project")]
        public List<string> usedBy => _references.UsedBy(path);
    }

    /// <summary>A file's references are read on request only (<c>select deps</c>, <c>where usedBy.count() == 0</c>): reading them loads files.</summary>
    public sealed class RqlFile_Adapter : RqlTypeAdapter<RqlFile>
    {
        protected override void Members(MemberTable<RqlFile> m)
        {
            m.Untyped.AddMixedMap("importSettings",
                o => ImportSettings.Keys(((RqlFile)o).path),
                (o, key) => ImportSettings.TypeOf(((RqlFile)o).path, key),
                (o, key) => ImportSettings.Get(((RqlFile)o).path, key),
                (o, key, value) => ImportSettings.Set(((RqlFile)o).path, key, value),
                (o, key, value) => ImportSettings.Problem(((RqlFile)o).path, key),
                "an imported file's import options (the [params] of its .import: \"edit/loop_mode\" of a .wav, \"compress/mode\" of a " +
                "texture …); set one to change it and re-import the file (like the Import dock: not undoable)",
                // Every key reaches validate, which names the file's options.
                has: (o, key) => true);
            m.Untyped.ExplicitOnly("size", "deps", "missingDeps", "usedBy", "importSettings", "importedSize");
        }
    }

    /// <summary>The import options of an imported file: the <c>[params]</c> section of its <c>.import</c> sidecar (read once per query).</summary>
    internal static class ImportSettings
    {
        private const string Section = "params";

        private sealed class Sidecar
        {
            public ConfigFile? Config;
        }

        private static ConfigFile? Load(string path) => GodotVariants.Scoped(("import", path), () =>
        {
            var config = new ConfigFile();
            var sidecar = ReimportClassifier.ImportSidecarPath(path);
            return new Sidecar { Config = FileAccess.FileExists(sidecar) && config.Load(sidecar) == Error.Ok ? config : null };
        }).Config;

        private static string[] KeysOf(ConfigFile c) => c.HasSection(Section) ? c.GetSectionKeys(Section) : System.Array.Empty<string>();

        /// <summary>The imported artifacts the sidecar lists (<c>[deps] dest_files</c>), or null for a file that is not imported.</summary>
        public static string[]? DestFiles(string path) => Load(path) is { } c ? c.GetValue("deps", "dest_files", new string[0]).AsStringArray() : null;

        public static IEnumerable<string> Keys(string path) => Load(path) is { } c ? KeysOf(c) : System.Array.Empty<string>();

        public static System.Type TypeOf(string path, string key) =>
            Load(path) is { } c && c.HasSectionKey(Section, key) ? GodotVariants.ClrType(c.GetValue(Section, key).VariantType) : typeof(object);

        public static object? Get(string path, string key) =>
            Load(path) is { } c && c.HasSectionKey(Section, key) ? GodotVariants.ToClr(c.GetValue(Section, key)) : null;

        public static string? Problem(string path, string key) => Load(path) switch
        {
            null => $"'{path}' is not an imported file (it has no .import sidecar)",
            var c when c.HasSectionKey(Section, key) => null,
            var c => KeysOf(c).Length == 0
                ? $"'{path}' has no import options"
                : $"'{path}' has no import option '{key}'; its options: {string.Join(", ", KeysOf(c))}",
        };

        public static void Set(string path, string key, object? value)
        {
            var config = Load(path) ?? throw new System.InvalidOperationException(Problem(path, key));
            config.SetValue(Section, key, GodotVariants.ToVariant(value));
            var error = config.Save(ReimportClassifier.ImportSidecarPath(path));
            if (error != Error.Ok) throw new System.InvalidOperationException($"could not write '{path}.import': {error}");
            var status = RqlReimport.Run(new[] { path });
            if (status.Contains("NOT imported", System.StringComparison.Ordinal)) throw new System.InvalidOperationException(status);
        }
    }

    /// <summary>
    /// Which file loads which, from <see cref="ResourceLoader.GetDependencies"/> (what the saved file references); one instance per
    /// <c>files</c> query, so a file's dependencies are read once and the reverse index (<see cref="UsedBy"/>) is built on first use.
    /// </summary>
    internal sealed class FileReferences
    {
        private readonly Dictionary<string, List<string>> _deps = new();
        private Dictionary<string, List<string>>? _usedBy;

        public List<string> DepsOf(string path)
        {
            if (!_deps.TryGetValue(path, out var deps))
                _deps[path] = deps = ResourceLoader.GetDependencies(path)
                    .Select(entry => Resolve(ResPathNormalizer.DependencyParts(entry)))
                    .Where(p => p.Length > 0)
                    .Distinct()
                    .ToList();
            return deps;
        }

        /// <summary>The dependencies of <paramref name="path"/> whose file no longer exists (broken references).</summary>
        public List<string> MissingDepsOf(string path) => DepsOf(path).Where(d => !Exists(d)).ToList();

        public List<string> UsedBy(string path)
        {
            if (_usedBy == null)
            {
                _usedBy = new Dictionary<string, List<string>>();
                // Only scenes and resources reference other files (as in DeleteResource's users check).
                foreach (var (file, _) in RqlGodot.WalkFiles(EditorToolGuards.GetResourceFileSystemOrThrow().GetFilesystem()).Take(RqlGodot.MaxFiles))
                {
                    if (!RqlReferences.CanDepend(file)) continue;
                    foreach (var dep in DepsOf(file))
                    {
                        if (!_usedBy.TryGetValue(dep, out var users)) _usedBy[dep] = users = new List<string>();
                        users.Add(file);
                    }
                }
            }
            return _usedBy.TryGetValue(path, out var found) ? found : new List<string>();
        }

        /// <summary>A file or an imported resource exists at <paramref name="path"/>.</summary>
        public static bool Exists(string path) => FileAccess.FileExists(path) || ResourceLoader.Exists(path);

        // Godot loads a dependency by its uid first: a uid that still names a file gives its current path (the file may have moved);
        // else the entry's fallback path, else the unresolved uid as written.
        private static string Resolve((string? uid, string? path) dep)
        {
            if (dep.uid != null)
            {
                var id = ResourceUid.TextToId(dep.uid);
                if (id != ResourceUid.InvalidId && ResourceUid.HasId(id)) return ResourceUid.GetIdPath(id);
            }
            return dep.path ?? dep.uid ?? "";
        }
    }

    /// <summary>One Godot class (the <c>classes</c> source).</summary>
    public sealed class RqlClass
    {
        public RqlClass(string name) { this.name = name; }

        [RqlDescription("class name, e.g. \"OmniLight3D\"")]
        public string name { get; }

        [RqlDescription("parent class name (empty for Object)")]
        public string parent => ClassDB.GetParentClass(name).ToString();

        [RqlDescription("can be created (AddChild / CreateResource)")]
        public bool canInstantiate => ClassDB.CanInstantiate(name);

        [RqlDescription("a Node class")]
        public bool isNode => ClassDB.IsParentClass(name, "Node");

        [RqlDescription("a Resource class")]
        public bool isResource => ClassDB.IsParentClass(name, "Resource");
    }
}
#endif
