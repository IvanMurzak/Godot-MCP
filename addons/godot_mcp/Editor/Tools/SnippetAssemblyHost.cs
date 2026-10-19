/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
// Editor-only (script-execute); also compiled by the xUnit suite.
#if TOOLS || GODOT_MCP_UNIT_TESTS
#nullable enable
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using com.IvanMurzak.CodeExec;

namespace com.IvanMurzak.Godot.MCP.Tools
{
    /// <summary>
    /// Loads each <c>script-execute</c> snippet into its own collectible context that binds its references to the
    /// assemblies the editor ALREADY runs, before the default context is asked. Code-Exec's own collectible host asks the
    /// default context first, and in the editor that context answers misses through
    /// <see cref="Connection.GodotMcpAssemblyResolver"/>, which would load a SECOND copy of the game assembly and of
    /// GodotSharp from <c>.godot/mono/temp/bin</c>: the snippet would see fresh statics instead of the live game, and
    /// GodotSharp's uninitialized native interop crashes the editor on the first engine call. Pure BCL, unit-testable.
    /// </summary>
    public sealed class SnippetAssemblyHost : IAssemblyHost
    {
        public bool SupportsUnload => true;

        public ILoadedAssembly Load(byte[] image, byte[]? symbols, string name)
        {
            var context = new SnippetContext("CodeExec:" + name);
            using var imageStream = new MemoryStream(image);
            using var symbolStream = symbols == null ? null : new MemoryStream(symbols);
            return new Loaded(context, context.LoadFromStream(imageStream, symbolStream));
        }

        /// <summary>The newest loaded, non-snippet assembly with this simple name (a reload may briefly leave an old one).</summary>
        public static Assembly? FindLoaded(string? simpleName)
        {
            if (simpleName == null)
                return null;
            Assembly? found = null;
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.IsDynamic || assembly.FullName?.StartsWith(CodeRunner.SnippetAssemblyPrefix, StringComparison.Ordinal) == true)
                    continue;
                // The simple name from the runtime-cached FullName: no AssemblyName allocation per assembly and lookup.
                var full = assembly.FullName ?? "";
                var comma = full.IndexOf(',');
                var length = comma < 0 ? full.Length : comma;
                if (length == simpleName.Length && string.Compare(full, 0, simpleName, 0, length, StringComparison.OrdinalIgnoreCase) == 0)
                    found = assembly;
            }
            return found;
        }

        sealed class SnippetContext : AssemblyLoadContext
        {
            public SnippetContext(string name) : base(name, isCollectible: true) { }

            // Not loaded anywhere yet (null) = the usual default-context lookup.
            protected override Assembly? Load(AssemblyName assemblyName) => FindLoaded(assemblyName.Name);
        }

        sealed class Loaded : ILoadedAssembly
        {
            AssemblyLoadContext? _context;

            public Loaded(AssemblyLoadContext context, Assembly assembly)
            {
                _context = context;
                Assembly = assembly;
            }

            public Assembly Assembly { get; }

            public void Dispose()
            {
                _context?.Unload();
                _context = null;
            }
        }
    }
}
#endif
