/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
│  Copyright (c) 2026 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/
using System.Collections.Generic;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// script-validate's warning probe: which warning settings it raises to Error, and how a raised warning row is read back
    /// (the live probe runs in the editor smoke).
    /// </summary>
    public class ScriptWarningProbeTests
    {
        static KeyValuePair<string, object?> S(string key, object? value) => new(key, value);

        [Fact]
        public void WarnLevelSettings_AreTheWarningsAtLevelOne()
        {
            var raised = ScriptDiagnosticsFilter.WarnLevelSettings(new[]
            {
                S("debug/gdscript/warnings/enable", true),
                S("debug/gdscript/warnings/unused_variable", 1L),
                S("debug/gdscript/warnings/untyped_declaration", 0L),
                S("debug/gdscript/warnings/integer_division", 2L),
                S("debug/gdscript/warnings/shadowed_variable", 1),
                S("debug/gdscript/warnings/directory_rules", null),
                S("debug/settings/fps/force_fps", 1L),
            });

            Assert.Equal(new[] { "debug/gdscript/warnings/unused_variable", "debug/gdscript/warnings/shadowed_variable" }, raised);
        }

        [Fact]
        public void WarnLevelSettings_NoneWhenWarningsAreDisabled()
        {
            Assert.Empty(ScriptDiagnosticsFilter.WarnLevelSettings(new[]
            {
                S("debug/gdscript/warnings/enable", false),
                S("debug/gdscript/warnings/unused_variable", 1L),
            }));
        }

        [Fact]
        public void RaisedWarning_IsTheWarningText()
        {
            Assert.Equal("The local variable \"unused_counter\" is declared but never used in the block.",
                ScriptDiagnosticsFilter.RaisedWarning(
                    "Parse Error: The local variable \"unused_counter\" is declared but never used in the block. (Warning treated as error.)"));
        }

        [Fact]
        public void RaisedWarning_IsNullForAGenuineError()
        {
            Assert.Null(ScriptDiagnosticsFilter.RaisedWarning("Parse Error: Expected expression for variable initial value after \"=\"."));
            Assert.Null(ScriptDiagnosticsFilter.RaisedWarning(null));
        }
    }
}
