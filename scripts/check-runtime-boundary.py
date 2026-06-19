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
import re
import sys
from pathlib import Path

# Editor-only Godot APIs a runtime (game-shipped) file must never touch in real code.
EDITOR_TOKENS = ("EditorInterface", "EditorPlugin", "EditorFileSystem", "EditorScript")
_TOKEN_RE = re.compile(r"\b(" + "|".join(EDITOR_TOKENS) + r")\b")


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
            else:
                out.append(c)
                i += 1
    return "".join(out)


def scan_file(path: Path):
    """Return (violations, warnings) for one .cs file.

    A violation is an editor token in real code outside a `#if TOOLS` guard.
    A warning is a `#if TOOLS` guard present in a Runtime file (allowed shim, but flagged).
    """
    raw = path.read_text(encoding="utf-8")
    code = strip_comments(raw)
    violations = []
    warnings = []
    tools_depth = 0          # >0 while inside a #if TOOLS (...) block
    if_stack = []            # track whether each #if level is a TOOLS gate
    for lineno, line in enumerate(code.splitlines(), start=1):
        stripped = line.strip()
        m_if = re.match(r"#\s*if\s+(.*)$", stripped)
        if m_if:
            cond = m_if.group(1)
            is_tools = bool(re.search(r"\bTOOLS\b", cond))
            if_stack.append(is_tools)
            if is_tools:
                tools_depth += 1
                warnings.append((lineno, "#if TOOLS guard in Runtime/ (body stripped from game build)"))
            continue
        if re.match(r"#\s*endif", stripped):
            if if_stack:
                was_tools = if_stack.pop()
                if was_tools and tools_depth > 0:
                    tools_depth -= 1
            continue
        if re.match(r"#\s*else", stripped) or re.match(r"#\s*elif", stripped):
            # An #else of a #if TOOLS means the following lines compile when TOOLS is UNDEFINED
            # (i.e. they DO ship). Drop out of the TOOLS-guarded region for token checking.
            if if_stack and if_stack[-1] and tools_depth > 0:
                tools_depth -= 1
                if_stack[-1] = False
            continue
        if tools_depth > 0:
            continue  # inside #if TOOLS: stripped from the game build, not a leak
        for m in _TOKEN_RE.finditer(line):
            violations.append((lineno, m.group(1), line.strip()))
    return violations, warnings


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

    files = sorted(runtime_dir.rglob("*.cs"))
    total_violations = 0
    total_warnings = 0
    for f in files:
        violations, warnings = scan_file(f)
        rel = f.relative_to(repo_root).as_posix()
        for lineno, token, text in violations:
            total_violations += 1
            print(f"VIOLATION {rel}:{lineno}: editor-only API '{token}' in un-guarded runtime code")
            print(f"          {text}")
        if args.verbose:
            for lineno, msg in warnings:
                total_warnings += 1
                print(f"warning   {rel}:{lineno}: {msg}")

    scanned = len(files)
    if total_violations:
        print()
        print(f"FAILED: {total_violations} runtime/editor boundary violation(s) across {scanned} Runtime/ file(s).")
        print("A file under addons/godot_mcp/Runtime/ referenced an editor-only Godot API in code that")
        print("ships into a game build. Move the file (or the offending member) to addons/godot_mcp/Editor/,")
        print("or guard the editor-only code with #if TOOLS so it is stripped from the export build.")
        return 1

    print(f"OK: runtime/editor boundary holds ({scanned} Runtime/ file(s) scanned, 0 violations).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
