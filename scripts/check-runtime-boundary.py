#!/usr/bin/env python3
"""
Runtime/Editor boundary guard for the Godot-MCP addon.

Godot compiles the WHOLE addon into one assembly, but the editor-only surface is stripped
from an exported game build because the editor `TOOLS` compilation symbol is undefined for
the `ExportDebug`/`ExportRelease` configurations (it is defined only for the editor `Debug`
config). The addon's source is organised so that everything which must SHIP into a game
(`GodotMcpRuntime.Initialize()` + the runtime-safe tools) lives under
`addons/godot_mcp/Runtime/`, and everything editor-only (the `EditorPlugin`, the dock UI,
and the editor tool families) lives under `addons/godot_mcp/Editor/`.

This script enforces the one rule that keeps that split honest:

    NO file under addons/godot_mcp/Runtime/ may reference an editor-only Godot API in
    code that actually compiles into a game build.

Concretely it FAILS (exit 1) when a `Runtime/**/*.cs` file, OUTSIDE comments, contains:

  * a real `EditorInterface` / `EditorPlugin` / `EditorFileSystem` / `EditorScript`
    reference that is NOT inside a `#if TOOLS` ... #endif guard, OR
  * a `#if TOOLS` guard that is not immediately closed (i.e. it would gate real code) —
    runtime code SHOULD be unconditional; a `#if TOOLS` block in Runtime/ is allowed only
    as a narrow, documented editor-coupling shim (see GodotMcpConnection.cs) and is reported
    as a WARNING, not a failure, because the guarded body is stripped from the game build.

It also FAILS when ANY `addons/godot_mcp/**/*.cs` file uses an editor-only NuGet package outside its guard:
Godot-MCP.csproj leaves those packages out of the export configurations. RQL must sit inside `#if TOOLS`; Code-Exec and
Roslyn inside a guard that may also allow the in-game opt-in, e.g. `#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME` (an
opted-in game references them, docs/ARCHITECTURE.md). Editor APIs stay TOOLS-only: an opted-in game has no editor.

The check is comment-aware: editor type names mentioned in `//` / `///` / `/* */`
documentation (which the addon's runtime files use heavily to EXPLAIN the boundary) never
trip the guard — only real code does. This mirrors exactly what the ExportRelease compile
strips, but runs in milliseconds with no .NET build, so it is cheap to gate every PR on.

Usage:
    python scripts/check-runtime-boundary.py            # scan, exit 1 on any violation
    python scripts/check-runtime-boundary.py --verbose  # also print the WARNING shims

Exit codes: 0 = boundary holds, 1 = at least one hard violation found.
"""
from __future__ import annotations

import argparse
import itertools
import re
import sys
from pathlib import Path

# Editor-only Godot APIs a runtime (game-shipped) file must never touch in real code.
EDITOR_TOKENS = ("EditorInterface", "EditorPlugin", "EditorFileSystem", "EditorScript")
_TOKEN_RE = re.compile(r"\b(" + "|".join(EDITOR_TOKENS) + r")\b")

# Namespaces of the editor-only NuGet packages. Godot-MCP.csproj leaves them out of ExportDebug/ExportRelease, so ANY
# addon file (Runtime/ or Editor/) may use them only inside their guard, or the exported game fails to compile.
# `Rql` is the root namespace of every RQL assembly (`using Rql;`, `Rql.Execution`, `global::Rql.RqlParser`);
# `(?<![.\w])` keeps the addon's own `...Editor.Rql` namespace from matching. RQL never ships in a game (`#if TOOLS`).
_RQL_RE = re.compile(r"(?<![.\w])(Rql(?=\s*[.;]))")
# Code-Exec + Roslyn also ship in a game built with the in-game opt-in (`GodotMcpCodeExecInGame=true` plus the
# GODOT_MCP_CODE_EXEC_IN_GAME symbol), so their guard may allow that symbol.
_CODE_EXEC_RE = re.compile(r"(?<![.\w])(com\.IvanMurzak\.CodeExec|Microsoft\.CodeAnalysis)\b")

# Symbols no game build defines: TOOLS (the editor build) and the xUnit suite's own symbol.
_NON_GAME_SYMBOLS = frozenset({"TOOLS", "GODOT_MCP_UNIT_TESTS"})
# ... plus the in-game code-execution opt-in: undefined in every game that does not reference Code-Exec.
CODE_EXEC_SYMBOL = "GODOT_MCP_CODE_EXEC_IN_GAME"
_NON_CODE_EXEC_GAME_SYMBOLS = _NON_GAME_SYMBOLS | {CODE_EXEC_SYMBOL}
_PP_OPS = {"&&": " and ", "||": " or ", "==": " == ", "!=": " != ", "!": " not "}


def _is_gate(cond: str, off: frozenset) -> bool:
    """True when an `#if` condition names one of `off` and is false whenever every `off` symbol is undefined, for any
    value of the other symbols (`#if TOOLS || DEBUG` compiles into an ExportDebug game, `#if !TOOLS` into every one)."""
    if not any(re.search(rf"\b{s}\b", cond) for s in off):
        return False
    expr = re.sub(r"&&|\|\||==|!=|!", lambda m: _PP_OPS[m.group()], cond)
    others = sorted(set(re.findall(r"\b[A-Za-z_]\w*\b", expr)) - {"and", "or", "not", "true", "false"} - off)
    for values in itertools.product((False, True), repeat=len(others)):
        env = {**dict(zip(others, values)), **dict.fromkeys(off, False), "true": True, "false": False}
        try:
            if eval(expr, {"__builtins__": {}}, env):  # a preprocessor expression from a repo file
                return False
        except SyntaxError:
            return False
    return True


def is_tools_gate(cond: str) -> bool:
    """True when an `#if` condition is false in every game build (TOOLS and GODOT_MCP_UNIT_TESTS undefined)."""
    return bool(re.search(r"\bTOOLS\b", cond)) and _is_gate(cond, _NON_GAME_SYMBOLS)


def is_code_exec_gate(cond: str) -> bool:
    """True when an `#if` condition is false in every game build without the in-game code-execution opt-in, e.g.
    `#if TOOLS || GODOT_MCP_CODE_EXEC_IN_GAME` (every TOOLS gate is one too)."""
    return _is_gate(cond, _NON_CODE_EXEC_GAME_SYMBOLS)


# A C# char literal: one char or one escape (`'"'`, `'\''`, `'\\'`, `'\u0022'`).
_CHAR_LITERAL_RE = re.compile(r"'(?:\\(?:u[0-9A-Fa-f]{4}|x[0-9A-Fa-f]{1,4}|.)|[^'\\\n])'")


def strip_comments(text: str) -> str:
    """Blank out everything that is NOT real C# code on each line — `//` line comments,
    `/* */` block comments, AND string literals (regular `"..."`, verbatim `@"..."`, and the
    text spans of interpolated `$"..."`) — replacing them with spaces while preserving
    newlines so reported line numbers stay accurate.

    Why strings too: the addon's runtime files mention editor type names heavily, both in
    doc-comments AND inside `[Description("... EditorInterface.IsPlayingScene() ...")]`
    attribute strings, to DOCUMENT the boundary. Those are not real editor-API references —
    only an unquoted, uncommented `EditorInterface`/`EditorPlugin`/... token is. Blanking
    comments and string contents leaves exactly the real code for the token scan, matching
    what the ExportRelease compile actually strips.

    This is a deliberately small, single-purpose scanner — not a full C# lexer — but it
    handles the constructs this addon uses (line/block comments, regular/verbatim/raw-adjacent
    interpolated strings, escaped quotes, doubled `""` in verbatim strings)."""
    out = []
    i, n = 0, len(text)
    in_block = False
    in_line = False
    in_str = False        # regular "..."
    in_verbatim = False   # @"..." (doubled "" is an escaped quote)
    while i < n:
        c = text[i]
        nxt = text[i + 1] if i + 1 < n else ""
        if in_line:
            out.append("\n" if c == "\n" else " ")
            in_line = c != "\n"
            i += 1
        elif in_block:
            if c == "*" and nxt == "/":
                in_block = False
                out.append("  ")
                i += 2
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
        elif in_str:
            if c == "\\":                      # escaped char inside a regular string
                out.append("  ")
                i += 2
            elif c == '"':
                in_str = False
                out.append('"')
                i += 1
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
        elif in_verbatim:
            if c == '"' and nxt == '"':        # doubled quote = literal quote, stay inside
                out.append("  ")
                i += 2
            elif c == '"':
                in_verbatim = False
                out.append('"')
                i += 1
            else:
                out.append("\n" if c == "\n" else " ")
                i += 1
        else:
            if c == "/" and nxt == "/":
                in_line = True
                out.append("  ")
                i += 2
            elif c == "/" and nxt == "*":
                in_block = True
                out.append("  ")
                i += 2
            elif c == "@" and nxt == '"':       # verbatim string @"..."
                in_verbatim = True
                out.append("  ")
                i += 2
            elif c == "$" and nxt == '"':       # interpolated string $"..." (blank the text spans)
                in_str = True
                out.append("  ")
                i += 2
            elif c == '"':
                in_str = True
                out.append('"')
                i += 1
            elif (m := _CHAR_LITERAL_RE.match(text, i)) is not None:  # '"', '\'', 'x': never opens a string
                out.append("'" + " " * (len(m.group()) - 2) + "'")
                i = m.end()
            else:
                out.append(c)
                i += 1
    return "".join(out)


def scan_file(path: Path):
    """Return (violations, package_violations, warnings) for one .cs file.

    A violation is an editor token in real code outside a `#if TOOLS` guard; a package violation is RQL outside
    `#if TOOLS`, or Code-Exec / Roslyn outside a guard that is off in every game without the in-game opt-in. A warning
    is a `#if TOOLS` guard (an allowed shim when the file is under Runtime/).
    """
    raw = path.read_text(encoding="utf-8")
    code = strip_comments(raw)
    violations = []
    package_violations = []
    warnings = []
    if_stack = []            # per open #if level: (is a TOOLS gate, is a code-exec gate)
    for lineno, line in enumerate(code.splitlines(), start=1):
        stripped = line.strip()
        m_if = re.match(r"#\s*if\s+(.*)$", stripped)
        if m_if:
            cond = m_if.group(1)
            if_stack.append((is_tools_gate(cond), is_code_exec_gate(cond)))
            if if_stack[-1][0]:
                warnings.append((lineno, "#if TOOLS guard in Runtime/ (body stripped from game build)"))
            continue
        if re.match(r"#\s*endif", stripped):
            if if_stack:
                if_stack.pop()
            continue
        if re.match(r"#\s*else", stripped) or re.match(r"#\s*elif", stripped):
            # An #else of a gate compiles when the gate is off (i.e. it DOES ship): no longer guarded.
            if if_stack:
                if_stack[-1] = (False, False)
            continue
        if any(tools for tools, _ in if_stack):
            continue  # inside #if TOOLS: stripped from the game build, not a leak
        for m in _TOKEN_RE.finditer(line):
            violations.append((lineno, m.group(1), line.strip()))
        for m in _RQL_RE.finditer(line):
            package_violations.append((lineno, m.group(1), "#if TOOLS", line.strip()))
        if not any(code_exec for _, code_exec in if_stack):
            for m in _CODE_EXEC_RE.finditer(line):
                package_violations.append((lineno, m.group(1), f"#if TOOLS || {CODE_EXEC_SYMBOL}", line.strip()))
    return violations, package_violations, warnings


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Godot-MCP Runtime/Editor boundary guard")
    parser.add_argument("--root", default=None, help="addon root (default: auto-detect)")
    parser.add_argument("--verbose", action="store_true", help="print WARNING shims too")
    args = parser.parse_args(argv)

    repo_root = Path(args.root) if args.root else Path(__file__).resolve().parent.parent
    runtime_dir = repo_root / "addons" / "godot_mcp" / "Runtime"
    if not runtime_dir.is_dir():
        print(f"ERROR: runtime dir not found: {runtime_dir}", file=sys.stderr)
        return 2

    # One pass over the addon: editor-only packages are checked everywhere, editor APIs under Runtime/ only.
    addon_files = sorted((repo_root / "addons" / "godot_mcp").rglob("*.cs"))
    scanned = 0
    total_violations = 0
    package_violations = 0
    for f in addon_files:
        violations, packages, warnings = scan_file(f)
        rel = f.relative_to(repo_root).as_posix()
        for lineno, token, guard, text in packages:
            package_violations += 1
            print(f"VIOLATION {rel}:{lineno}: editor-only package '{token}' used outside {guard}")
            print(f"          {text}")
        if not f.is_relative_to(runtime_dir):
            continue
        scanned += 1
        for lineno, token, text in violations:
            total_violations += 1
            print(f"VIOLATION {rel}:{lineno}: editor-only API '{token}' in un-guarded runtime code")
            print(f"          {text}")
        if args.verbose:
            for lineno, msg in warnings:
                print(f"warning   {rel}:{lineno}: {msg}")

    if total_violations:
        print()
        print(f"FAILED: {total_violations} runtime/editor boundary violation(s) across {scanned} Runtime/ file(s).")
        print("A file under addons/godot_mcp/Runtime/ referenced an editor-only Godot API in code that")
        print("ships into a game build. Move the file (or the offending member) to addons/godot_mcp/Editor/,")
        print("or guard the editor-only code with #if TOOLS so it is stripped from the export build.")
    if package_violations:
        print()
        print(f"FAILED: {package_violations} use(s) of an editor-only package outside its guard.")
        print("Godot-MCP.csproj leaves RQL / Code-Exec / Roslyn out of ExportDebug/ExportRelease, so the exported")
        print(f"game would not compile. Wrap RQL code in #if TOOLS, Code-Exec / Roslyn code in #if TOOLS || {CODE_EXEC_SYMBOL}")
        print("(|| GODOT_MCP_UNIT_TESTS for xUnit-compiled sources).")
    if total_violations or package_violations:
        return 1

    print(
        f"OK: runtime/editor boundary holds ({scanned} Runtime/ file(s) scanned, 0 violations; "
        f"no editor-only package use outside its guard in {len(addon_files)} addon file(s))."
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
