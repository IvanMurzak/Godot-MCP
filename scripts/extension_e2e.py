#!/usr/bin/env python3
"""Live E2E driver for the Godot-MCP extensions (generic; data-driven).

scripts/extension_live.py runs it once per module manifest (`extensions/Godot-AI-*/tests/e2e/e2e-tools.json`)
against ONE headless Godot editor connected to a local gamedev-mcp-server (Authorization=none); it can also be
run by hand against such an editor. It boots nothing itself.

Extensions are RQL modules, so every call is a core tool (`rql-query`, `rql-mutate`, `rql-schema`) invoked through
`godot-cli run-tool … --raw`. Results are the default compact RQL text (rows `a|b|c`, objects `Class#id(name)`)
inside godot-cli's raw JSON envelope.

Manifest schema (JSON object):

    {
      "description": "free text (ignored)",
      "notes":       "free text (ignored)",
      "setup":  [ <call>, ... ],   # optional: run first, in order, each must succeed
      "tools":  [ <call>, ... ]    # the assertions (the gate)
    }

    <call> = {
      "tool":   "rql-query",              # required: the tool id
      "input":  { ... },                  # optional: tool arguments (default {})
      "expect": [ "FX|32", ... ],         # optional: substrings that MUST appear in the raw response
      "expectFailure": false              # optional: true => the call MUST fail (a rejected value)
    }

A call passes on THREE things: `godot-cli` exit code 0, the literal `"status":"success"` in the raw response,
and every `expect` substring present. A call marked `expectFailure` is inverted: it passes only when the tool
fails AND every `expect` substring appears in its output (a validation rule proven live, not just any failure).
`setup` opens the edited scene the calls need (`root` / `nodes` are empty on a freshly booted editor):
`rql-mutate` `editor | call NewScene("res://e2e.tscn", "Node3D", "Root")`.
"""
from __future__ import annotations

import argparse
import contextlib
import json
import shlex
import shutil
import subprocess
import sys
from pathlib import Path
from typing import Any


def _resolve_cli(cli_cmd: list[str]) -> list[str]:
    """Resolve the launcher's first token to a concrete executable path.

    On Windows a globally-installed `godot-cli` is a `godot-cli.cmd` shim that
    `subprocess` can't find by bare name; `shutil.which` resolves the real file
    (incl. the `.cmd`/`.exe` extension). On Linux it returns the script as-is.
    Leaves the token untouched when it can't be resolved (e.g. `npx`, which is
    found via PATH at spawn time, or an explicit path)."""
    if not cli_cmd:
        return cli_cmd
    resolved = shutil.which(cli_cmd[0])
    return [resolved, *cli_cmd[1:]] if resolved else cli_cmd


def _strip_ansi(s: str) -> str:
    out, i, n = [], 0, len(s)
    while i < n:
        c = s[i]
        if c == "\x1b" and i + 1 < n and s[i + 1] == "[":
            j = i + 2
            while j < n and not s[j].isalpha():
                j += 1
            i = j + 1
            continue
        out.append(c)
        i += 1
    return "".join(out)


def run_call(cli_cmd: list[str], url: str, call: dict[str, Any], timeout_s: int, *, env=None) -> tuple[bool, str]:
    """Invoke one tool via godot-cli; return (passed, human-readable detail)."""
    tool = call.get("tool")
    if not tool or not isinstance(tool, str):
        return False, "manifest entry is missing a string 'tool' id"

    input_obj = call.get("input", {})
    input_json = json.dumps(input_obj, separators=(",", ":"))
    expects = call.get("expect", []) or []

    cmd = [*cli_cmd, "run-tool", tool, "--url", url, "--input", input_json, "--raw", "--timeout", str(timeout_s * 1000)]

    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout_s + 30, env=env)
    except subprocess.TimeoutExpired:
        return False, f"godot-cli timed out after {timeout_s + 30}s invoking run-tool {tool}"
    except FileNotFoundError as e:
        return False, f"could not launch godot-cli ({cmd[0]!r}): {e}"

    stdout = _strip_ansi(proc.stdout or "").strip()
    stderr = _strip_ansi(proc.stderr or "").strip()
    detail = f"exit={proc.returncode}\n    input:  {input_json}\n    stdout: {stdout or '<empty>'}"
    if stderr:
        detail += f"\n    stderr: {stderr}"

    if call.get("expectFailure"):
        if proc.returncode == 0 and '"status":"success"' in stdout:
            return False, detail + "\n    -> expected the call to FAIL, but it succeeded"
        missing = [e for e in expects if e not in stdout + "\n" + stderr]
        if missing:
            return False, detail + f"\n    -> the call failed, but without the expected substrings: {missing}"
        return True, detail

    if proc.returncode != 0:
        return False, detail
    if '"status":"success"' not in stdout:
        return False, detail + "\n    -> response is not a {\"status\":\"success\"} envelope"
    missing = [e for e in expects if e not in stdout]
    if missing:
        return False, detail + f"\n    -> missing expected substrings: {missing}"
    return True, detail


def run_phase(name: str, calls: list[dict[str, Any]], cli_cmd: list[str], url: str, timeout_s: int, *, env=None,
              around=None) -> bool:
    """Run the calls in order; `around(call)` may return a context manager wrapped around one call (the suite uses it
    to record where an expectFailure call's error response lands in the editor log)."""
    ok = True
    for idx, call in enumerate(calls, 1):
        tool = call.get("tool", "<no-tool>")
        with (around(call) if around else contextlib.nullcontext()):
            passed, detail = run_call(cli_cmd, url, call, timeout_s, env=env)
        status = "PASS" if passed else "FAIL"
        print(f"[{status}] {name} {idx}/{len(calls)}: {tool}")
        print("    " + detail.replace("\n", "\n    "))
        if not passed:
            print(f"::error::E2E {name} assertion failed for tool '{tool}' (see detail above).")
            ok = False
    return ok


def main() -> int:
    ap = argparse.ArgumentParser(description="Live E2E driver for a Godot-MCP extension manifest.")
    ap.add_argument("--url", required=True, help="MCP server base URL (e.g. http://localhost:5300).")
    ap.add_argument("--manifest", required=True, help="Path to a module's tests/e2e/e2e-tools.json.")
    ap.add_argument(
        "--cli-cmd",
        default="godot-cli",
        help="Command used to invoke godot-cli (shlex-split). Default 'godot-cli' (install it globally "
        "first). Use e.g. 'npx --yes godot-cli@latest' to run without a global install.",
    )
    ap.add_argument("--timeout", type=int, default=60, help="Per-tool request timeout in seconds (default 60).")
    args = ap.parse_args()

    manifest_path = Path(args.manifest)
    if not manifest_path.is_file():
        print(f"::error::manifest not found: {manifest_path}", file=sys.stderr)
        return 2
    try:
        manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    except json.JSONDecodeError as e:
        print(f"::error::manifest is not valid JSON ({manifest_path}): {e}", file=sys.stderr)
        return 2

    cli_cmd = _resolve_cli(shlex.split(args.cli_cmd))
    setup = manifest.get("setup", []) or []
    tools = manifest.get("tools", []) or []

    if not tools:
        print(f"::error::manifest '{manifest_path}' has an empty 'tools' list — nothing to assert.", file=sys.stderr)
        return 2

    print(f"E2E: {len(setup)} setup call(s), {len(tools)} tool assertion(s) against {args.url}")
    print(f"  godot-cli command: {cli_cmd}")

    setup_ok = run_phase("setup", setup, cli_cmd, args.url, args.timeout)
    if not setup_ok:
        print("::error::E2E setup phase failed — aborting before the tool assertions.")
        return 1

    tools_ok = run_phase("tool", tools, cli_cmd, args.url, args.timeout)
    if not tools_ok:
        return 1

    print(f"\nAll {len(tools)} assertion(s) passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
