"""Cases for scripts/check-runtime-boundary.py: which `#if` guards keep editor APIs and editor-only packages out of games."""
import importlib.util
from pathlib import Path
import tempfile
import unittest

_SCRIPT = Path(__file__).resolve().parents[1] / "check-runtime-boundary.py"
_spec = importlib.util.spec_from_file_location("check_runtime_boundary", _SCRIPT)
boundary = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(boundary)


def scan(source: str):
    with tempfile.TemporaryDirectory() as tmp:
        path = Path(tmp) / "Sample.cs"
        path.write_text(source, encoding="utf-8")
        return boundary.scan_file(path)


class Gates(unittest.TestCase):
    def test_tools_gate(self):
        self.assertTrue(boundary.is_tools_gate("TOOLS"))
        self.assertTrue(boundary.is_tools_gate("TOOLS || GODOT_MCP_UNIT_TESTS"))
        self.assertFalse(boundary.is_tools_gate("TOOLS || DEBUG"))
        self.assertFalse(boundary.is_tools_gate("!TOOLS"))
        # An opted-in game defines the code-exec symbol, so its guard is no TOOLS gate.
        self.assertFalse(boundary.is_tools_gate("TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME"))

    def test_code_exec_gate(self):
        self.assertTrue(boundary.is_code_exec_gate("TOOLS"))
        self.assertTrue(boundary.is_code_exec_gate("TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME"))
        self.assertTrue(boundary.is_code_exec_gate("TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME || GODOT_MCP_UNIT_TESTS"))
        self.assertTrue(boundary.is_code_exec_gate("GODOT_MCP_CODE_EXEC_IN_GAME"))
        self.assertFalse(boundary.is_code_exec_gate("GODOT_MCP_CODE_EXEC_IN_GAME || DEBUG"))
        self.assertFalse(boundary.is_code_exec_gate("!GODOT_MCP_CODE_EXEC_IN_GAME"))
        self.assertFalse(boundary.is_code_exec_gate("DEBUG"))


class PackageGuards(unittest.TestCase):
    def test_code_exec_under_the_opt_in_guard_is_allowed(self):
        _, packages, _ = scan("#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME\nusing com.IvanMurzak.CodeExec;\n"
                              "using Microsoft.CodeAnalysis;\n#endif\n")
        self.assertEqual([], packages)

    def test_code_exec_outside_any_guard_fails(self):
        _, packages, _ = scan("using com.IvanMurzak.CodeExec;\n")
        self.assertEqual([(1, "com.IvanMurzak.CodeExec")], [(p[0], p[1]) for p in packages])

    def test_code_exec_in_the_else_of_the_opt_in_guard_fails(self):
        _, packages, _ = scan("#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME\n#else\nusing Microsoft.CodeAnalysis;\n#endif\n")
        self.assertEqual([3], [p[0] for p in packages])

    def test_rql_stays_tools_only(self):
        _, packages, _ = scan("#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME\nusing Rql;\n#endif\n")
        self.assertEqual([(2, "Rql", "#if TOOLS")], [p[:3] for p in packages])
        _, packages, _ = scan("#if TOOLS\nusing Rql;\n#endif\n")
        self.assertEqual([], packages)

    def test_editor_api_under_the_opt_in_guard_fails(self):
        violations, _, _ = scan("#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME\nvar e = EditorInterface.Singleton;\n#endif\n")
        self.assertEqual([(2, "EditorInterface")], [v[:2] for v in violations])

    def test_nested_tools_inside_opt_in_guard(self):
        violations, packages, _ = scan("#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME\n#if TOOLS\nusing Rql;\n"
                                       "var e = EditorInterface.Singleton;\n#endif\n#endif\n")
        self.assertEqual(([], []), (violations, packages))


class CharLiterals(unittest.TestCase):
    def test_a_quote_char_literal_opens_no_string(self):
        violations, packages, _ = scan("var s = raw.Trim('\"');\nusing com.IvanMurzak.CodeExec;\nvar e = EditorInterface.Singleton;\n")
        self.assertEqual([3], [v[0] for v in violations])
        self.assertEqual([2], [p[0] for p in packages])

    def test_escaped_char_literals(self):
        code = boundary.strip_comments("a('\\'', '\\\\', '\\u0022', 'x') // c\n")
        self.assertEqual("a('  ', '  ', '      ', ' ')", code.rstrip())


class RepoHolds(unittest.TestCase):
    def test_addon_passes(self):
        self.assertEqual(0, boundary.main([]))


if __name__ == "__main__":
    unittest.main()
