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
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Godot's source generator gives a C# script class a <c>[ScriptPath]</c> only when the file is named after the
    /// class. A class without one gets a pathless script bridge, and on an assembly reload its old script collides
    /// with the one a re-created instance has already registered:
    /// <c>ArgumentException: An item with the same key has already been added</c> from
    /// <c>ScriptManagerBridge.TryReloadRegisteredScriptWithClass</c>. So every script class has its own file.
    /// </summary>
    public class GodotScriptFileNameTests
    {
        static readonly Regex ScriptClass = new(
            @"\[(?:Tool|GlobalClass)\]\s*(?:\[[^\]]*\]\s*)*(?:public|internal)\s+(?:sealed\s+|abstract\s+)?partial\s+class\s+(\w+)",
            RegexOptions.Compiled);

        [Fact]
        public void EveryScriptClass_LivesInAFileNamedAfterIt()
        {
            var addonRoot = FindAddonRoot();
            var classes = Directory
                .EnumerateFiles(addonRoot, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => ScriptClass.Matches(File.ReadAllText(file))
                    .Select(match => (File: file, Class: match.Groups[1].Value)))
                .ToList();

            // The scan must really see the addon's script classes, or the check below passes on nothing.
            Assert.Contains(classes, c => c.Class == "GodotMcpDock");
            Assert.Contains(classes, c => c.Class == "DockActionButton");

            var misplaced = classes
                .Where(c => !string.Equals(Path.GetFileNameWithoutExtension(c.File), c.Class, StringComparison.Ordinal))
                .Select(c => $"{c.Class} in {Path.GetRelativePath(addonRoot, c.File)}")
                .ToList();
            Assert.True(misplaced.Count == 0, "Give each script class its own file: " + string.Join(", ", misplaced));
        }

        static string FindAddonRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "addons", "godot_mcp");
                if (Directory.Exists(candidate))
                    return candidate;
            }
            throw new DirectoryNotFoundException("addons/godot_mcp not found above " + AppContext.BaseDirectory);
        }
    }
}
