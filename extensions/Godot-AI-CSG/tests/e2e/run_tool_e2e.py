#!/usr/bin/env python3
"""Live `run-tool` E2E driver for a Godot-MCP extension (generic; data-driven).

This is the engine of the `e2e-godot` CI leg's tool-level gate. It does NOT boot anything: the
caller (the workflow) must already have

  1. a local MCP server running (the shared `gamedev-mcp-server`, Authorization=none), and
  2. a HEADLESS Godot editor booted in `Custom` connection mode and CONNECTED to that server
     (so the editor's `[AiTool]` handlers — including the `#if TOOLS` editor tools — can run).

Given a `--url` (the server base) and a `--manifest` (a per-extension JSON tool list), it calls each
tool via `godot-cli run-tool` / `run-system-tool` and asserts success. Every assertion is REAL: a tool
that throws comes back HTTP 500 → `godot-cli` exits non-zero → this driver fails the build. There is no
`continue-on-error` anywhere; a regression in any tool turns the leg red.

Manifest schema (JSON object):

    {
      "description": "free text (ignored)",
      "notes":       "free text (ignored)",
      "setup":  [ <call>, ... ],   # optional: run first, in order, each must succeed
      "tools":  [ <call>, ... ]    # the per-tool assertions (the gate)
    }

    <call> = {
      "tool":   "particles-create",      # required: the tool id
      "system": false,                    # optional: true => run-system-tool (/api/system-tools)
      "input":  { ... },                  # optional: tool arguments (default {})
      "expect": [ "\"Amount\":32", ... ]  # optional: substrings that MUST appear in the raw response
    }

The driver gates each call on THREE things: `godot-cli` exit code 0, the literal `"status":"success"`
in the raw response, and every `expect` substring present. Any miss fails the whole run.

`setup` exists for tools that need an edited scene root: most editor tools resolve nodes relative to
`EditorInterface.GetEditedSceneRoot()`, which is null on a freshly-booted headless editor. A
`scene-create` setup call (a core godot_mcp tool) opens a scene so the extension's editor tools have a
root to operate on. Pure-managed and version/UI-info tools need no setup.
"""
from __future__ import annotations

import argparse
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


def run_call(cli_cmd: list[str], url: str, call: dict[str, Any], timeout_s: int) -> tuple[bool, str]:
    """Invoke one tool via godot-cli; return (passed, human-readable detail)."""
    tool = call.get("tool")
    if not tool or not isinstance(tool, str):
        return False, "manifest entry is missing a string 'tool' id"

    subcmd = "run-system-tool" if call.get("system") else "run-tool"
    input_obj = call.get("input", {})
    input_json = json.dumps(input_obj, separators=(",", ":"))
    expects = call.get("expect", []) or []

    cmd = [*cli_cmd, subcmd, tool, "--url", url, "--input", input_json, "--raw", "--timeout", str(timeout_s * 1000)]

    try:
        proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout_s + 30)
    except subprocess.TimeoutExpired:
        return False, f"godot-cli timed out after {timeout_s + 30}s invoking {subcmd} {tool}"
    except FileNotFoundError as e:
        return False, f"could not launch godot-cli ({cmd[0]!r}): {e}"

    stdout = _strip_ansi(proc.stdout or "").strip()
    stderr = _strip_ansi(proc.stderr or "").strip()
    detail = f"exit={proc.returncode}\n    input:  {input_json}\n    stdout: {stdout or '<empty>'}"
    if stderr:
        detail += f"\n    stderr: {stderr}"

    if proc.returncode != 0:
        return False, detail
    if '"status":"success"' not in stdout:
        return False, detail + "\n    -> response is not a {\"status\":\"success\"} envelope"
    missing = [e for e in expects if e not in stdout]
    if missing:
        return False, detail + f"\n    -> missing expected substrings: {missing}"
    return True, detail


def run_phase(name: str, calls: list[dict[str, Any]], cli_cmd: list[str], url: str, timeout_s: int) -> bool:
    ok = True
    for idx, call in enumerate(calls, 1):
        tool = call.get("tool", "<no-tool>")
        passed, detail = run_call(cli_cmd, url, call, timeout_s)
        status = "PASS" if passed else "FAIL"
        print(f"[{status}] {name} {idx}/{len(calls)}: {tool}")
        print("    " + detail.replace("\n", "\n    "))
        if not passed:
            print(f"::error::run-tool E2E {name} assertion failed for tool '{tool}' (see detail above).")
            ok = False
    return ok


def main() -> int:
    ap = argparse.ArgumentParser(description="Live run-tool E2E driver for a Godot-MCP extension.")
    ap.add_argument("--url", required=True, help="MCP server base URL (e.g. http://localhost:5300).")
    ap.add_argument("--manifest", required=True, help="Path to the per-extension JSON tool list.")
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

    print(f"Run-tool E2E: {len(setup)} setup call(s), {len(tools)} tool assertion(s) against {args.url}")
    print(f"  godot-cli command: {cli_cmd}")

    setup_ok = run_phase("setup", setup, cli_cmd, args.url, args.timeout)
    if not setup_ok:
        print("::error::run-tool E2E setup phase failed — aborting before the tool assertions.")
        return 1

    tools_ok = run_phase("tool", tools, cli_cmd, args.url, args.timeout)
    if not tools_ok:
        return 1

    print(f"\nAll {len(tools)} run-tool assertion(s) passed.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
