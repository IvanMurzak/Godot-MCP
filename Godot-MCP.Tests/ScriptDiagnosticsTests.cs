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
using System.Linq;
using System.Text.Json;
using com.IvanMurzak.Godot.MCP.Data;
using com.IvanMurzak.Godot.MCP.Tools;
using Xunit;

namespace com.IvanMurzak.Godot.MCP.Tests
{
    /// <summary>
    /// Pure-managed coverage for the <c>script-validate</c> feature: the diagnostic result models
    /// (<see cref="ScriptDiagnostic"/> / <see cref="ScriptDiagnosticsResult"/>) and the
    /// <see cref="ScriptErrorCapture"/> router (engine-error classification, passive log routing, and the
    /// validation capture-session lifecycle). None touch a live Godot scripting runtime, so they run in the
    /// plain xUnit host with no Godot binary. The editor-driving handler (<c>Tool_Script.Validate.cs</c>,
    /// <c>#if TOOLS</c>) and the 4.5 engine Logger subclass (<c>#if GODOT4_5_OR_GREATER</c>) are verified by
    /// the headless Godot smoke (test.md Suite 3).
    /// </summary>
    public class ScriptDiagnosticsTests
    {
        // ---- ScriptDiagnostic / ScriptDiagnosticsResult serialization ----------------------------

        [Fact]
        public void ScriptDiagnostic_Serializes_WithExpectedJsonNames()
        {
            var d = new ScriptDiagnostic("res://scripts/player.gd", 12, "Unexpected token",
                ScriptDiagnosticSeverity.Error);

            var json = JsonSerializer.Serialize(d);
            Assert.Contains("\"path\"", json);
            Assert.Contains("\"line\"", json);
            Assert.Contains("\"message\"", json);
            Assert.Contains("\"severity\"", json);

            var restored = JsonSerializer.Deserialize<ScriptDiagnostic>(json);
            Assert.NotNull(restored);
            Assert.Equal("res://scripts/player.gd", restored!.Path);
            Assert.Equal(12, restored.Line);
            Assert.Equal("Unexpected token", restored.Message);
            Assert.Equal(ScriptDiagnosticSeverity.Error, restored.Severity);
        }

        [Fact]
        public void ScriptDiagnostic_UnknownLine_DefaultsToMinusOne()
        {
            var d = new ScriptDiagnostic("res://x.gd", -1, "ParseError");
            Assert.Equal(-1, d.Line);
            Assert.Contains("res://x.gd", d.ToString());
        }

        [Fact]
        public void ScriptDiagnosticsResult_Serializes_WithExpectedJsonNames()
        {
            var r = new ScriptDiagnosticsResult
            {
                Ok = false,
                ScannedCount = 3,
                ErrorCount = 1,
                WarningCount = 0,
                ScannedPaths = { "res://a.gd", "res://b.gd", "res://c.gd" },
                Diagnostics = { new ScriptDiagnostic("res://a.gd", 4, "boom") },
                Fidelity = ScriptDiagnosticsFidelity.Precise,
                Note = "1 error across 3 scanned script(s).",
            };

            var json = JsonSerializer.Serialize(r);
            foreach (var key in new[] { "ok", "scannedCount", "errorCount", "warningCount",
                "scannedPaths", "diagnostics", "fidelity", "note" })
                Assert.Contains($"\"{key}\"", json);

            var restored = JsonSerializer.Deserialize<ScriptDiagnosticsResult>(json);
            Assert.NotNull(restored);
            Assert.False(restored!.Ok);
            Assert.Equal(3, restored.ScannedCount);
            Assert.Equal(1, restored.ErrorCount);
            Assert.Single(restored.Diagnostics);
            Assert.Equal(ScriptDiagnosticsFidelity.Precise, restored.Fidelity);
        }

        // ---- ScriptErrorCapture: passive log routing ---------------------------------------------

        [Fact]
        public void Route_AlwaysAppendsToLogSink_RegardlessOfSession()
        {
            var captured = new System.Collections.Generic.List<(GodotLogType, string)>();
            var capture = new ScriptErrorCapture { LogSink = (t, m) => captured.Add((t, m)) };

            capture.Route(EngineErrorKind.Script, "res://x.gd", 7, "cond", "Parse error");
            capture.Route(EngineErrorKind.Warning, "res://y.gd", 3, "cond2", "Deprecated call");

            Assert.Equal(2, captured.Count);
            Assert.Equal(GodotLogType.Error, captured[0].Item1);   // Script -> Error severity in the log
            Assert.Contains("res://x.gd:7", captured[0].Item2);
            Assert.Contains("Parse error", captured[0].Item2);
            Assert.Equal(GodotLogType.Warning, captured[1].Item1);
        }

        [Fact]
        public void Route_PrefersRationaleOverCode_FallsBackToCode()
        {
            var captured = new System.Collections.Generic.List<string>();
            var capture = new ScriptErrorCapture { LogSink = (_, m) => captured.Add(m) };

            // rationale present -> used.
            capture.Route(EngineErrorKind.Error, "res://a.gd", 1, "the C++ cond", "human rationale");
            Assert.Contains("human rationale", captured[^1]);

            // rationale empty -> fall back to code.
            capture.Route(EngineErrorKind.Error, "res://b.gd", 2, "the C++ cond", "");
            Assert.Contains("the C++ cond", captured[^1]);
        }

        // ---- ScriptErrorCapture: validation capture session --------------------------------------

        [Fact]
        public void Session_CollectsOnlyScriptErrors()
        {
            var capture = new ScriptErrorCapture();
            Assert.False(capture.SessionActive);

            capture.BeginSession();
            Assert.True(capture.SessionActive);

            capture.Route(EngineErrorKind.Script, "res://broken.gd", 9, "cond", "unexpected indent");
            capture.Route(EngineErrorKind.Error, "res://other.gd", 1, "cond", "generic error"); // not Script
            capture.Route(EngineErrorKind.Warning, "res://w.gd", 1, "cond", "warn");             // not Script

            var diags = capture.EndSession();
            Assert.False(capture.SessionActive);

            var only = Assert.Single(diags);
            Assert.Equal("res://broken.gd", only.Path);
            Assert.Equal(9, only.Line);
            Assert.Equal("unexpected indent", only.Message);
            Assert.Equal(ScriptDiagnosticSeverity.Error, only.Severity);
        }

        [Fact]
        public void Route_OutsideSession_RecordsNoDiagnostics()
        {
            var capture = new ScriptErrorCapture();
            capture.Route(EngineErrorKind.Script, "res://broken.gd", 9, "cond", "boom");
            // No session was open; EndSession on no session yields empty.
            Assert.Empty(capture.EndSession());
        }

        [Fact]
        public void BeginSession_DiscardsPriorBuffer()
        {
            var capture = new ScriptErrorCapture();
            capture.BeginSession();
            capture.Route(EngineErrorKind.Script, "res://1.gd", 1, "c", "first");
            capture.BeginSession(); // re-open discards the prior row
            capture.Route(EngineErrorKind.Script, "res://2.gd", 2, "c", "second");

            var diags = capture.EndSession();
            var one = Assert.Single(diags);
            Assert.Equal("res://2.gd", one.Path);
        }

        [Fact]
        public void EndSession_WithoutBegin_ReturnsEmpty_AndIsSafe()
        {
            var capture = new ScriptErrorCapture();
            Assert.Empty(capture.EndSession());
        }

        // ---- FormatLogLine -----------------------------------------------------------------------

        [Theory]
        [InlineData(EngineErrorKind.Script, "res://x.gd", 5, "msg", "[Script] res://x.gd:5 — msg")]
        [InlineData(EngineErrorKind.Error, "res://x.gd", -1, "msg", "[Error] res://x.gd — msg")]
        [InlineData(EngineErrorKind.Warning, "", 5, "msg", "[Warning] — msg")]
        public void FormatLogLine_ShapesLocationPrefix(EngineErrorKind kind, string file, int line,
            string text, string expected)
        {
            Assert.Equal(expected, ScriptErrorCapture.FormatLogLine(kind, file, line, text));
        }

        [Fact]
        public void Route_StaticCurrent_IsSettableAndClearable()
        {
            var prior = ScriptErrorCapture.Current;
            try
            {
                var c = new ScriptErrorCapture();
                ScriptErrorCapture.Current = c;
                Assert.Same(c, ScriptErrorCapture.Current);
                ScriptErrorCapture.Current = null;
                Assert.Null(ScriptErrorCapture.Current);
            }
            finally
            {
                ScriptErrorCapture.Current = prior;
            }
        }

        [Fact]
        public void Diagnostics_SortBySeverity_ErrorsBeforeWarnings()
        {
            // Mirrors the ordering Tool_Script.Validate applies before returning.
            var list = new System.Collections.Generic.List<ScriptDiagnostic>
            {
                new("res://a.gd", 1, "w", ScriptDiagnosticSeverity.Warning),
                new("res://b.gd", 2, "e", ScriptDiagnosticSeverity.Error),
            };
            list.Sort((a, b) => a.Severity.CompareTo(b.Severity));
            Assert.Equal(ScriptDiagnosticSeverity.Error, list.First().Severity);
        }
    }
}
