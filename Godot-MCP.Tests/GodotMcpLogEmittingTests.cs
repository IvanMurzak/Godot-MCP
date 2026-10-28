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
using System.IO;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Connection;
using com.IvanMurzak.Godot.MCP.Data;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The plugin's own-diagnostics guard (<see cref="GodotMcpLog.IsEmitting"/>) without a <c>[ThreadStatic]</c>: thread
    /// statics of an assembly in Godot's reloadable context can corrupt a live static of the reloaded plugin on .NET 10
    /// (dotnet/runtime#135441), so the addon keeps none.
    /// </summary>
    public class GodotMcpLogEmittingTests
    {
        [Fact]
        public void IsEmitting_IsTrueOnlyInsideTheWriter_OnItsOwnThread()
        {
            bool inside = false, nested = false, afterNested = false, otherThread = true;

            GodotMcpLog.Emit(GodotLogType.Log, "outer", () =>
            {
                inside = GodotMcpLog.IsEmitting;
                GodotMcpLog.Emit(GodotLogType.Log, "inner", () => nested = GodotMcpLog.IsEmitting, collect: false);
                afterNested = GodotMcpLog.IsEmitting;
                // A dedicated thread: a blocking wait on Task.Run may inline the task onto this very thread.
                var other = new System.Threading.Thread(() => otherThread = GodotMcpLog.IsEmitting);
                other.Start();
                other.Join();
            }, collect: false);

            Assert.True(inside);
            Assert.True(nested);
            Assert.True(afterNested); // leaving the inner writer keeps the outer one marked
            Assert.False(otherThread);
            Assert.False(GodotMcpLog.IsEmitting);
        }

        [Fact]
        public void IsEmitting_IsClearedWhenTheWriterThrows()
        {
            GodotMcpLog.Emit(GodotLogType.Error, "boom", () => throw new System.InvalidOperationException("GD unavailable"), collect: false);

            Assert.False(GodotMcpLog.IsEmitting);
        }

        [Fact]
        public void Addon_DeclaresNoThreadStaticFields()
        {
            var dir = new DirectoryInfo(System.AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Godot-MCP.csproj"))) dir = dir.Parent;
            Assert.NotNull(dir);

            var offenders = Directory.EnumerateFiles(Path.Combine(dir!.FullName, "addons", "godot_mcp"), "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains("[ThreadStatic]"))
                .Select(path => Path.GetRelativePath(dir.FullName, path))
                .ToArray();

            Assert.True(offenders.Length == 0, "[ThreadStatic] in the addon (dotnet/runtime#135441): " + string.Join(", ", offenders));
        }
    }
}
