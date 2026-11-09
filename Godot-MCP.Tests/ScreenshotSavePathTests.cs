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
using System.Diagnostics;
using System.IO;
using System.Linq;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// The <c>savePath</c> of the screenshot tools, on a real file system with a temp folder as the project. The tools
    /// themselves need a rendering editor: <c>scripts/smoke_screenshot_save.py</c> drives them live.
    /// </summary>
    public class ScreenshotSavePathTests : IDisposable
    {
        readonly string _parent;
        readonly string _root;
        readonly List<string> _links = new();

        public ScreenshotSavePathTests()
        {
            _parent = Path.Combine(Path.GetTempPath(), "ScreenshotSavePathTests-" + Guid.NewGuid().ToString("N")).Replace('\\', '/');
            _root = _parent + "/project";
            Directory.CreateDirectory(_root);
        }

        public void Dispose()
        {
            // Junctions first: a recursive delete refuses them on Windows (on Unix it unlinks a symlink without following it).
            foreach (var link in _links) Directory.Delete(link);
            Directory.Delete(_parent, recursive: true);
        }

        // ---- no save asked for ------------------------------------------------------------------

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NoRequest_MeansNoSaveAndNoError(string? requested)
        {
            Assert.True(ScreenshotSavePath.TryResolve(requested, _root, out var full, out var error));
            Assert.Null(full);
            Assert.Null(error);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_root));
        }

        // ---- accepted ---------------------------------------------------------------------------

        [Theory]
        [InlineData("Shots/shot.png", "Shots/shot.png")]
        [InlineData("res://Shots/shot.png", "Shots/shot.png")]
        [InlineData("res://shot.png", "shot.png")]
        [InlineData("Shots\\shot.PNG", "Shots/shot.PNG")]
        [InlineData("./shot.png", "shot.png")]
        [InlineData("Shots/./shot.png", "Shots/shot.png")]
        [InlineData("Shots//shot.png", "Shots/shot.png")]
        [InlineData("Shots/Sub/../shot.png", "Shots/shot.png")]
        [InlineData("res://Shots/Sub/../shot.png", "Shots/shot.png")]
        [InlineData("Shots/shot", "Shots/shot.png")]
        [InlineData("a.b.png", "a.b.png")]
        [InlineData("  Shots/shot.png  ", "Shots/shot.png")]
        [InlineData("res/shot.png", "res/shot.png")]
        [InlineData("addons/other/shot.png", "addons/other/shot.png")]
        [InlineData("CONTROL.png", "CONTROL.png")]
        [InlineData("COMX.png", "COMX.png")]
        public void Accepts_AFileInsideTheProject(string requested, string expected)
        {
            Assert.True(ScreenshotSavePath.TryResolve(requested, _root, out var full, out var error), error);
            Assert.Equal(_root + "/" + expected, full);
            Assert.Empty(Directory.EnumerateFileSystemEntries(_root)); // resolving writes nothing
        }

        [Theory]
        [InlineData("/Deep/shot.png", "Deep/shot.png")]
        [InlineData("//Deep//shot.png", "Deep/shot.png")]
        [InlineData("/Deep/Sub/../shot.png", "Deep/shot.png")]
        public void Accepts_AnAbsolutePathUnderTheProject(string suffix, string expected)
        {
            Assert.True(ScreenshotSavePath.TryResolve(_root + suffix, _root, out var full, out var error), error);
            Assert.Equal(_root + "/" + expected, full);
        }

        [Fact]
        public void Accepts_AProjectRootWithATrailingSlashOrBackslashes()
        {
            Assert.True(ScreenshotSavePath.TryResolve("shot.png", _root + "/", out var full, out var error), error);
            Assert.Equal(_root + "/shot.png", full);
            Assert.True(ScreenshotSavePath.TryResolve(_root + "/shot.png", _root.Replace('/', Path.DirectorySeparatorChar), out full, out error), error);
            Assert.Equal(_root + "/shot.png", full);
        }

        // ---- refused ----------------------------------------------------------------------------

        [Theory]
        // Outside.
        [InlineData("../shot.png", "inside the project folder")]
        [InlineData("..\\shot.png", "inside the project folder")]
        [InlineData("Shots/../../shot.png", "inside the project folder")]
        [InlineData("res://../shot.png", "inside the project folder")]
        [InlineData("res://Shots/../../shot.png", "inside the project folder")]
        [InlineData("/shot.png", "inside the project folder")]
        [InlineData("C:/shot.png", "inside the project folder")]
        [InlineData("C:\\Windows\\shot.png", "inside the project folder")]
        [InlineData("\\\\server\\share\\shot.png", "inside the project folder")]
        [InlineData("//?/C:/shot.png", "inside the project folder")]
        // Other schemes.
        [InlineData("user://shot.png", "because user://")]
        [InlineData("USER://shot.png", "because user://")]
        [InlineData("uid://abc", "only a res:// path")]
        [InlineData("file:///C:/shot.png", "only a res:// path")]
        [InlineData("res:///shot.png", "relative to the project root")]
        // A folder, whatever a path API would collapse it to.
        [InlineData(".", "names a folder")]
        [InlineData("./", "names a folder")]
        [InlineData("./.", "names a folder")]
        [InlineData(".//.", "names a folder")]
        [InlineData(".\\", "names a folder")]
        [InlineData("..", "names a folder")]
        [InlineData("../", "names a folder")]
        [InlineData("Shots/", "names a folder")]
        [InlineData("Shots//", "names a folder")]
        [InlineData("Shots/.", "names a folder")]
        [InlineData("Shots/./", "names a folder")]
        [InlineData("Shots/..", "names a folder")]
        [InlineData("Shots/Sub/..", "names a folder")]
        [InlineData("res://", "names a folder")]
        [InlineData("res://.", "names a folder")]
        [InlineData("res://Shots/", "names a folder")]
        // No file name.
        [InlineData(".png", "no file name")]
        [InlineData("..png", "no file name")]
        [InlineData("Shots/..png", "no file name")]
        [InlineData("res://.png", "no file name")]
        [InlineData(".hidden", "no file name")]
        // A segment Windows would change, or could not open.
        [InlineData(".. /shot.png", "ends in a dot or a space")]
        [InlineData("Shots/.../shot.png", "ends in a dot or a space")]
        [InlineData("Foo./shot.png", "ends in a dot or a space")]
        [InlineData("shot.png.", "ends in a dot or a space")]
        [InlineData("Foo /shot.png", "ends in a dot or a space")]
        [InlineData("C:shot.png", "character a file name cannot use")]
        [InlineData("folder:stream/shot.png", "character a file name cannot use")]
        [InlineData("shot.png:stream", "character a file name cannot use")]
        [InlineData("res://shot.png::$DATA", "character a file name cannot use")]
        [InlineData("sh?t.png", "character a file name cannot use")]
        [InlineData("sh\u0001t.png", "character a file name cannot use")]
        [InlineData("CON.png", "reserved for a device")]
        [InlineData("com1.png", "reserved for a device")]
        [InlineData("AUX/shot.png", "reserved for a device")]
        [InlineData("res://LPT9.tar.png", "reserved for a device")]
        // Another format.
        [InlineData("shot.jpg", "must end in .png")]
        [InlineData("shot.png.txt", "must end in .png")]
        [InlineData("res://shot.tres", "must end in .png")]
        // Godot's cache and this addon.
        [InlineData(".godot/shot.png", "cache folder")]
        [InlineData("res://.godot/shot.png", "cache folder")]
        [InlineData(".GODOT/imported/shot.png", "cache folder")]
        [InlineData("Shots/../.godot/shot.png", "cache folder")]
        [InlineData("addons/godot_mcp/shot.png", "Godot-MCP addon")]
        [InlineData("res://addons//godot_mcp/shot.png", "Godot-MCP addon")]
        public void Refuses(string requested, string reason)
        {
            AssertRefused(requested, reason);
        }

        [Fact]
        public void Refuses_AnAbsolutePathOutsideOrBesideTheProject()
        {
            AssertRefused(_parent + "/shot.png", "inside the project folder");
            AssertRefused(_root + "-sibling/shot.png", "inside the project folder");
            AssertRefused(_root + "/../shot.png", "inside the project folder");
            AssertRefused(_root, "inside the project folder"); // the root itself: adding .png would name a file beside it
            AssertRefused(_root + "/", "names a folder");
            AssertRefused(_root + "/.", "names a folder");
        }

        [Fact]
        public void Refuses_AnExistingFileOrFolder_LeavingItUntouched()
        {
            var existing = _root + "/old.png";
            File.WriteAllBytes(existing, new byte[] { 1, 2, 3 });
            Directory.CreateDirectory(_root + "/dir.png");

            AssertRefused("old.png", "never overwrites");
            AssertRefused("res://old.png", "never overwrites");
            AssertRefused("old", "never overwrites"); // .png added, then the same file
            AssertRefused("dir.png", "never overwrites");
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(existing));
        }

        [Fact]
        public void Refuses_AnyLinkOnThePath()
        {
            var outside = _parent + "/outside";
            Directory.CreateDirectory(outside);
            var inside = _root + "/real";
            Directory.CreateDirectory(inside);
            MakeDirectoryLink(_root + "/out", outside);
            MakeDirectoryLink(_root + "/in", inside);
            MakeDirectoryLink(_root + "/real/nested-out", outside);

            AssertRefused("out/shot.png", "'out' is a symlink or junction");
            AssertRefused("res://out/deep/er/shot.png", "'out' is a symlink or junction");
            AssertRefused("real/nested-out/shot.png", "'real/nested-out' is a symlink or junction");
            AssertRefused("in/shot.png", "'in' is a symlink or junction"); // stays inside, but could reach res://.godot/

            Assert.True(ScreenshotSavePath.TryResolve("real/shot.png", _root, out var full, out var error), error);
            Assert.Equal(_root + "/real/shot.png", full);
        }

        [Fact]
        public void Refuses_ALinkWhoseTargetReadsAsInsideButLeadsOutThroughAnother()
        {
            var outside = _parent + "/outside";
            Directory.CreateDirectory(outside + "/deep");
            Directory.CreateDirectory(_root + "/real");
            MakeDirectoryLink(_root + "/real/nested-out", outside);
            MakeDirectoryLink(_root + "/chained", _root + "/real/nested-out/deep");

            AssertRefused("chained/shot.png", "'chained' is a symlink or junction");
            Assert.Empty(Directory.EnumerateFileSystemEntries(outside + "/deep"));
        }

        [Fact]
        public void AProjectOpenedThroughALinkStillWorks()
        {
            MakeDirectoryLink(_parent + "/linked-root", _root);

            Assert.True(ScreenshotSavePath.TryResolve("shot.png", _parent + "/linked-root", out var full, out var error), error);
            Assert.Equal(_parent + "/linked-root/shot.png", full);
        }

        [Fact]
        public void Refuses_ADanglingLink()
        {
            var gone = _parent + "/gone";
            Directory.CreateDirectory(gone);
            MakeDirectoryLink(_root + "/dangling", gone);
            Directory.Delete(gone);

            AssertRefused("dangling/shot.png", "'dangling' is a symlink or junction");
        }

        [Fact]
        public void EveryRefusalSaysWhatToDo()
        {
            foreach (var requested in new[] { "../a.png", "user://a.png", ".", ".png", "a.jpg", ".godot/a.png", "Foo./a.png", "a?.png" })
            {
                Assert.False(ScreenshotSavePath.TryResolve(requested, _root, out var full, out var error));
                Assert.Null(full);
                Assert.StartsWith($"Cannot save to '{requested}': ", error);
                // Each names a correct form or the thing to change.
                Assert.True(error!.Contains("res://Screenshots/shot.png") || error.Contains("rename") || error.Contains("must end in .png"), error);
            }
        }

        void AssertRefused(string requested, string reason)
        {
            Assert.False(ScreenshotSavePath.TryResolve(requested, _root, out var full, out var error), $"'{requested}' was accepted: {full}");
            Assert.Null(full);
            Assert.StartsWith($"Cannot save to '{requested}': ", error);
            Assert.Contains(reason, error);
        }

        // ---- the write --------------------------------------------------------------------------

        [Fact]
        public void TryWrite_CreatesFoldersAndWritesTheBytes()
        {
            Assert.True(ScreenshotSavePath.TryResolve("res://New/Folder/shot", _root, out var full, out _));
            var png = new byte[] { 0x89, 0x50, 0x4E, 0x47 };

            Assert.True(ScreenshotSavePath.TryWrite(full!, png, out var error), error);

            Assert.Equal(png, File.ReadAllBytes(_root + "/New/Folder/shot.png"));
        }

        [Fact]
        public void TryWrite_NeverOverwrites_AFileThatAppearedAfterTheCheck()
        {
            Assert.True(ScreenshotSavePath.TryResolve("late.png", _root, out var full, out _));
            File.WriteAllBytes(full!, new byte[] { 1, 2, 3 }); // appears while the capture renders

            Assert.False(ScreenshotSavePath.TryWrite(full!, new byte[] { 9 }, out var error));

            Assert.Contains("never overwrites", error);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(full!));
        }

        [Fact]
        public void TryWrite_ReportsAFolderItCannotCreate()
        {
            File.WriteAllBytes(_root + "/file", new byte[] { 1 });

            Assert.False(ScreenshotSavePath.TryWrite(_root + "/file/shot.png", new byte[] { 9 }, out var error));

            Assert.Contains("Could not write", error);
            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(_root + "/file"));
        }

        [Fact]
        public void TryWrite_NeverFollowsALinkAtTheFileItself()
        {
            if (OperatingSystem.IsWindows()) return; // a file symlink needs a privilege there; the path check covers folders

            var target = _parent + "/target.png";
            File.WriteAllBytes(target, new byte[] { 1 });
            File.CreateSymbolicLink(_root + "/link.png", target);

            Assert.False(ScreenshotSavePath.TryWrite(_root + "/link.png", new byte[] { 9 }, out _));

            Assert.Equal(new byte[] { 1 }, File.ReadAllBytes(target));
        }

        [Fact]
        public void SavedNote_StatesTheAbsolutePath_AndTheImport()
        {
            var note = ScreenshotSavePath.SavedNote("C:/Project/Screenshots/shot.png");

            Assert.StartsWith("Saved to C:/Project/Screenshots/shot.png.", note);
            Assert.Contains("'shot.png.import'", note);
        }

        // ---- the tools (source scan: they need native Godot types to compile) ---------------------

        [Theory]
        [InlineData("Viewport")]
        [InlineData("Camera")]
        [InlineData("Isolated")]
        public void EveryTool_TakesAnOptionalSavePath_AndAdvertisesIt(string family)
        {
            var source = File.ReadAllText(Path.Combine(ToolAnnotationsTests.RepoRoot(), "addons", "godot_mcp", "Editor", "Tools", $"Tool_Screenshot.{family}.cs"));

            Assert.Contains("[Description(ScreenshotSavePath.ParameterDescription)]", source);
            Assert.Contains("string? savePath = null", source);
            Assert.Contains("ScreenshotSavePath.ToolDescriptionNote", source);
            // Refused before any capture work: the check is the first thing on the main thread.
            var run = source.IndexOf("MainThread.Instance.Run", StringComparison.Ordinal);
            var check = source.IndexOf("ResolveSavePath(savePath", StringComparison.Ordinal);
            var capture = new[] { "ResolveNode(", "GetEditorViewport" }
                .Select(s => source.IndexOf(s, StringComparison.Ordinal)).Where(i => i >= 0).Min();
            Assert.True(run >= 0 && check >= 0, "the tool no longer has the expected shape");
            Assert.InRange(check, run, capture);
            // The checked path reaches the write (camera and isolated pass it down to RenderSubViewportToPng).
            Assert.Contains("saveTo)", source);
        }

        [Fact]
        public void Descriptions_NameTheFileCase()
        {
            Assert.Contains("wants the picture as a file", ScreenshotSavePath.ToolDescriptionNote);
            Assert.Contains("Saved to <absolute path>", ScreenshotSavePath.ToolDescriptionNote);
            Assert.Contains("wants the picture as a file", ScreenshotSavePath.ParameterDescription);
            Assert.Contains("never overwrites", ScreenshotSavePath.ParameterDescription);
            Assert.Contains(".import", ScreenshotSavePath.ParameterDescription);
        }

        // A junction on Windows (no privilege needed), a symlink elsewhere.
        void MakeDirectoryLink(string link, string target)
        {
            if (!OperatingSystem.IsWindows())
            {
                Directory.CreateSymbolicLink(link, target);
                return;
            }
            _links.Add(link);
            var mklink = new ProcessStartInfo("cmd.exe") { RedirectStandardOutput = true, RedirectStandardError = true };
            mklink.ArgumentList.Add("/c");
            mklink.ArgumentList.Add("mklink");
            mklink.ArgumentList.Add("/J");
            mklink.ArgumentList.Add(link.Replace('/', '\\'));
            mklink.ArgumentList.Add(target.Replace('/', '\\'));
            using var process = Process.Start(mklink)!;
            process.WaitForExit();
            Assert.Equal(0, process.ExitCode);
        }
    }
}
