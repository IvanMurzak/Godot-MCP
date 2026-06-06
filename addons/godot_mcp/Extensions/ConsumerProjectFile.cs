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
using System.Linq;
using Godot;

namespace com.IvanMurzak.Godot.MCP.Extensions
{
    /// <summary>
    /// The editor-only (<c>#if TOOLS</c>) <see cref="IConsumerProjectFile"/> over the consumer's game
    /// <c>.csproj</c>: it globalizes <c>res://</c> to the absolute project root and locates the single project
    /// <c>.csproj</c> (the file Godot compiles every <c>.cs</c> into). Read/write go through <see cref="System.IO"/>
    /// (the editor runs on the desktop, so this is reliable), keeping all the install LOGIC in the pure-managed
    /// <see cref="ExtensionInstaller"/> / <see cref="ExtensionInstallPlanner"/> (CI-unit-tested); this class is the
    /// thin filesystem shell, verified via the headless Godot smoke (<c>test.md</c> Suite 3).
    ///
    /// <para>
    /// Locating the <c>.csproj</c>: Godot names the project assembly after the project's <c>.csproj</c>, which sits
    /// at the project root. We pick the <c>.csproj</c> at the root whose name matches the project (when resolvable),
    /// else the single root-level <c>.csproj</c>. If zero (a pure-GDScript project) or an ambiguous set is found,
    /// <see cref="Exists"/> is false and the dock shows a "no project .csproj" notice rather than guessing.
    /// </para>
    /// </summary>
    public sealed class ConsumerProjectFile : IConsumerProjectFile
    {
        readonly string? _path;

        public ConsumerProjectFile()
        {
            _path = LocateProjectCsproj();
        }

        /// <summary>True when a single unambiguous consumer <c>.csproj</c> was located at the project root.</summary>
        public bool Exists => _path != null;

        /// <summary>The absolute path of the located consumer <c>.csproj</c>, or null when none.</summary>
        public string? Path => _path;

        /// <summary>Read the located <c>.csproj</c> text; null when none located or the read fails.</summary>
        public string? Read()
        {
            if (_path == null)
                return null;

            try
            {
                return File.ReadAllText(_path);
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] could not read consumer .csproj '{_path}': {ex.Message}");
                return null;
            }
        }

        /// <summary>Write the updated <c>.csproj</c> text; false when none located or the write fails.</summary>
        public bool Write(string csprojText)
        {
            if (_path == null)
                return false;

            try
            {
                File.WriteAllText(_path, csprojText);
                return true;
            }
            catch (Exception ex)
            {
                GD.PushError($"[Godot-MCP] could not write consumer .csproj '{_path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Find the consumer's project <c>.csproj</c> at the globalized project root. Prefers the one whose file name
        /// matches the project's configured C# assembly name (Godot's <c>dotnet/project/assembly_name</c> /
        /// application name); falls back to the SINGLE root-level <c>.csproj</c>. Returns null when zero are present
        /// (pure-GDScript project) or when several are present with no name match (ambiguous — never guess).
        /// </summary>
        static string? LocateProjectCsproj()
        {
            string projectRoot;
            try
            {
                projectRoot = ProjectSettings.GlobalizePath("res://").TrimEnd('/');
            }
            catch (Exception)
            {
                return null;
            }

            if (string.IsNullOrEmpty(projectRoot) || !Directory.Exists(projectRoot))
                return null;

            string[] candidates;
            try
            {
                candidates = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly);
            }
            catch (Exception)
            {
                return null;
            }

            if (candidates.Length == 0)
                return null;

            if (candidates.Length == 1)
                return candidates[0];

            // Multiple root-level .csproj — disambiguate by the project's assembly / application name when available.
            var assemblyName = ResolveAssemblyName();
            if (!string.IsNullOrEmpty(assemblyName))
            {
                var match = candidates.FirstOrDefault(
                    c => string.Equals(
                        System.IO.Path.GetFileNameWithoutExtension(c),
                        assemblyName,
                        StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            // Ambiguous (several .csproj, no name match) → do not guess.
            return null;
        }

        /// <summary>Read the project's C# assembly name from project settings (Godot 4 stores it under <c>dotnet/project/assembly_name</c>); empty when unset.</summary>
        static string ResolveAssemblyName()
        {
            try
            {
                if (ProjectSettings.HasSetting("dotnet/project/assembly_name"))
                {
                    var name = ProjectSettings.GetSetting("dotnet/project/assembly_name").AsString();
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            catch (Exception)
            {
                // fall through to empty
            }

            return string.Empty;
        }
    }
}
#endif
