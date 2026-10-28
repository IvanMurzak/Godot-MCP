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
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Godot;
using Rql;

namespace com.IvanMurzak.Godot.MCP.Editor.Rql
{
    /// <summary>
    /// Resource files: the logic of the replaced <c>resource-create</c> / <c>resource-move</c> / <c>resource-delete</c>
    /// tools, unchanged — sidecars (<c>.import</c>, <c>.uid</c>) move and go with the file, the cached resource is rebound,
    /// open scenes follow a moved scene, the editor filesystem is rescanned.
    /// </summary>
    public sealed partial class RqlEditorApi
    {
        [RqlCallable("create a resource file (res://….tres / .res) of a Godot class or a script class_name (default Resource); returns the new resource")]
        public Resource CreateResource(string path, string className = "Resource")
        {
            var resPath = NewResourcePath(path);
            var resource = RqlGodot.Instantiate<Resource>(ResourceClass(className), "Resource", nameof(className));
            EditorToolGuards.EnsureDirectoryExists(ResPathNormalizer.ParentDir(resPath));
            var error = ResourceSaver.Save(resource, resPath);
            if (error != Error.Ok) throw new InvalidOperationException($"could not save the new resource to '{resPath}': {error}");
            resource.TakeOverPath(resPath);
            RqlReimport.Register(resPath);
            return resource;
        }

        // ---- call validators (RQL 0.2): run before the call, on dry runs too; nothing changes when they reject.

        public string? ValidateCreateResource(string path, string className) => RqlGodot.Rejection(() =>
        {
            NewResourcePath(path);
            RqlGodot.ScriptAwareClass(ResourceClass(className), "Resource", nameof(className), out _);
        });

        public string? ValidateMoveResource(string path, string newPath) => RqlGodot.Rejection(() => MovePaths(path, newPath));

        public string? ValidateDeleteResource(string path, bool force) => RqlGodot.Rejection(() => DeletablePath(path, force));

        private static string ResourceClass(string className) => string.IsNullOrWhiteSpace(className) ? "Resource" : className;

        private static string NewResourcePath(string path)
        {
            var resPath = ResPathNormalizer.RequireResFilePath(path, nameof(path));
            RequireUnprotected(resPath);
            if (!resPath.EndsWith(".tres", StringComparison.OrdinalIgnoreCase) && !resPath.EndsWith(".res", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"a resource path must end with .tres or .res; got '{path}'");
            if (FileAccess.FileExists(resPath) || ResourceLoader.Exists(resPath))
                throw new ArgumentException($"a resource already exists at '{resPath}' (edit it with godot.resource(\"{resPath}\") | set …)");
            return resPath;
        }

        // The normalized source and destination of a move, and whether it is a case-only rename of the same file.
        private static (string src, string dst, bool caseOnly) MovePaths(string path, string newPath)
        {
            var src = ResPathNormalizer.RequireResFilePath(path, nameof(path));
            var dst = ResPathNormalizer.RequireResFilePath(newPath, nameof(newPath));
            if (src == dst) throw new ArgumentException("the source and destination paths are the same");
            RequireUnprotected(src);
            RequireUnprotected(dst);
            if (!FileAccess.FileExists(src)) throw new ArgumentException($"no file exists at '{src}'");
            if (ResPathNormalizer.MoveProblem(src, dst) is { } moveProblem) throw new ArgumentException(moveProblem);
            // A case-only rename names the same file on Windows / macOS: the destination "exists" and its sidecars are the
            // source's own.
            // On a case-sensitive file system both names can be real files: then the destination is another file.
            var caseOnly = ResPathNormalizer.IsCaseOnlyRename(src, dst)
                && !DirAccess.GetFilesAt(ResPathNormalizer.ParentDir(dst)).Contains(System.IO.Path.GetFileName(dst));
            if (FileAccess.FileExists(dst) && !caseOnly)
                throw new ArgumentException($"a file already exists at '{dst}'");
            return (src, dst, caseOnly);
        }

        private static string DeletablePath(string path, bool force)
        {
            var resPath = ResPathNormalizer.RequireResFilePath(path, nameof(path));
            RequireUnprotected(resPath);
            if (!FileAccess.FileExists(resPath)) throw new ArgumentException($"no file exists at '{resPath}'");
            if (!force && RqlReferences.UsersOf(resPath) is { Count: > 0 } users)
                throw new InvalidOperationException($"'{resPath}' is still used by {RqlReferences.Describe(users)}; deleting it would break " +
                    $"those references. Point them elsewhere first, or delete anyway with DeleteResource(\"{resPath}\", true)");
            return resPath;
        }

        [RqlCallable("move or rename a file (its .import / .uid sidecars move with it); returns the new path")]
        public string MoveResource(string path, string newPath)
        {
            var (src, dst, caseOnly) = MovePaths(path, newPath);

            // Stale sidecars at the destination go.
            if (!caseOnly)
                foreach (var sidecar in EditorToolGuards.Sidecars)
                    if (FileAccess.FileExists(dst + sidecar)) DirAccess.RemoveAbsolute(dst + sidecar);
            EditorToolGuards.EnsureDirectoryExists(ResPathNormalizer.ParentDir(dst));

            var moveError = DirAccess.RenameAbsolute(src, dst);
            if (moveError != Error.Ok) throw new InvalidOperationException($"could not move '{src}' to '{dst}': {moveError}");
            try
            {
                EditorToolGuards.RebindCachedResource(src, dst);
                if (ResPathNormalizer.IsScenePath(src)) EditorToolGuards.RetargetOpenScenes(src, dst);
                foreach (var sidecar in EditorToolGuards.Sidecars)
                {
                    string from = src + sidecar, to = dst + sidecar;
                    if (!FileAccess.FileExists(from)) continue;
                    var sidecarError = DirAccess.RenameAbsolute(from, to);
                    if (sidecarError != Error.Ok)
                        throw new InvalidOperationException($"the file moved to '{dst}' but its '{sidecar}' sidecar did not ('{from}' -> '{to}': {sidecarError}); move it by hand or reimport the file");
                }
            }
            finally { Ei.GetResourceFilesystem()?.Scan(); }
            return dst;
        }

        [RqlCallable("delete a file and its .import / .uid sidecars; returns the deleted path. Refused while a scene or resource " +
            "(the edited scene's unsaved changes included) still uses the file, unless force is true")]
        public string DeleteResource(string path, bool force = false)
        {
            var resPath = DeletablePath(path, force);
            var error = DirAccess.RemoveAbsolute(resPath);
            if (error != Error.Ok) throw new InvalidOperationException($"could not delete '{resPath}': {error}");
            try
            {
                EditorToolGuards.RebindCachedResource(resPath, newPath: null);
                if (ResPathNormalizer.IsScenePath(resPath)) EditorToolGuards.CloseDeletedScene(resPath);
                foreach (var sidecar in EditorToolGuards.Sidecars)
                {
                    var sidecarPath = resPath + sidecar;
                    if (!FileAccess.FileExists(sidecarPath)) continue;
                    var sidecarError = DirAccess.RemoveAbsolute(sidecarPath);
                    if (sidecarError != Error.Ok)
                        throw new InvalidOperationException($"'{resPath}' was deleted but its '{sidecar}' sidecar could not be removed ({sidecarError}); remove it by hand");
                }
            }
            finally { Ei.GetResourceFilesystem()?.Scan(); }
            return resPath;
        }

        private static void RequireUnprotected(string resPath)
        {
            if (ResPathNormalizer.ProtectedFileProblem(resPath) is { } problem) throw new ArgumentException(problem + "; the file tools do not create, move or delete files there");
        }

        [RqlCallable("create a folder (and missing parents), e.g. \"res://art/materials\"; returns its path")]
        public string CreateFolder(string path)
        {
            var dir = ResPathNormalizer.NormalizeDir(path);
            EditorToolGuards.EnsureDirectoryExists(dir);
            RqlReimport.RegisterFolder(dir);
            return dir;
        }

        [RqlCallable("delete an EMPTY folder, e.g. \"res://old/textures\" (no files, no subfolders; delete those first with " +
            "DeleteResource / DeleteFolder); returns its path")]
        public string DeleteFolder(string path)
        {
            var dir = DeletableFolder(path);
            var error = DirAccess.RemoveAbsolute(dir);
            if (error != Error.Ok) throw new InvalidOperationException($"could not delete the folder '{dir}': {error}");
            RqlReimport.Run(null); // a settled scan: the folders source no longer lists it
            return dir;
        }

        public string? ValidateDeleteFolder(string path) => RqlGodot.Rejection(() => DeletableFolder(path));

        // The folder DeleteFolder removes, after every check: inside res://, not the root, not a protected folder, existing, empty.
        private static string DeletableFolder(string path)
        {
            var dir = ResPathNormalizer.NormalizeDir(path).TrimEnd('/');
            if (dir.Length <= "res://".Length) throw new ArgumentException("the project root cannot be deleted");
            RequireUnprotected(dir + "/");
            if (!DirAccess.DirExistsAbsolute(dir)) throw new ArgumentException($"no folder exists at '{dir}'");
            using var d = DirAccess.Open(dir) ?? throw new ArgumentException($"the folder '{dir}' cannot be opened");
            d.IncludeHidden = true;
            var files = d.GetFiles();
            var dirs = d.GetDirectories();
            if (files.Length > 0 || dirs.Length > 0)
                throw new ArgumentException($"the folder '{dir}' is not empty ({files.Length} file(s), {dirs.Length} folder(s)" +
                    ", e.g. " + files.Concat(dirs.Select(x => x + "/")).First() + "); only empty folders are deleted");
            return dir;
        }

        [RqlCallable("re-import files (res:// paths) after they changed on disk, verifying each import; no argument: a full filesystem scan. Returns a status (files NOT imported are listed first)")]
        public string Reimport(string[]? files = null) => RqlReimport.Run(files);
    }
}
#endif
