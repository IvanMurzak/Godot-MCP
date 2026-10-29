#!/usr/bin/env python3
"""Update the core MCP / reflection / query pins in lockstep across package/tests/testbed.

The pins (com.IvanMurzak.McpPlugin, com.IvanMurzak.ReflectorNet, com.IvanMurzak.Rql = the RQL module API) must
EQUAL the core Godot-MCP addon's (Godot-MCP/Godot-MCP.csproj); scripts/extensions.py core_pin_drift fails CI otherwise.
Inside the Godot-MCP monorepo use `scripts/update_core.py`, which moves every module in lockstep.

Usage:
  python commands/update-core.py --mcp-plugin 6.11.0
  python commands/update-core.py --mcp-plugin 6.11.0 --reflector-net 5.4.0 --what-if
  python commands/update-core.py --rql 0.2.0
"""
from __future__ import annotations
import argparse
import re
from pathlib import Path


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--mcp-plugin")
    ap.add_argument("--reflector-net")
    ap.add_argument("--rql")
    ap.add_argument("--what-if", action="store_true")
    args = ap.parse_args()
    if not args.mcp_plugin and not args.reflector_net and not args.rql:
        raise SystemExit("error: pass --mcp-plugin, --reflector-net and/or --rql")

    pins = {}
    if args.mcp_plugin:
        pins["com.IvanMurzak.McpPlugin"] = args.mcp_plugin
    if args.reflector_net:
        pins["com.IvanMurzak.ReflectorNet"] = args.reflector_net
    if args.rql:
        pins["com.IvanMurzak.Rql"] = args.rql

    root = Path(__file__).resolve().parent.parent
    csprojs = []
    for sub in ("src", "tests", "testbed"):
        d = root / sub
        if d.is_dir():
            csprojs += sorted(d.rglob("*.csproj"))

    for csproj in csprojs:
        with open(csproj, encoding="utf-8", newline="") as handle:
            content = handle.read()
        new = content
        for pid, ver in pins.items():
            new = re.sub(
                r'(<PackageReference\s+Include="' + re.escape(pid) + r'"\s+Version=")[^"]+(")',
                lambda m, ver=ver: m.group(1) + ver + m.group(2), new)
        if new != content:
            print(f"Updating {csproj.relative_to(root).as_posix()}")
            for pid, ver in pins.items():
                print(f"  {pid} -> {ver}")
            if not args.what_if:
                with open(csproj, "w", encoding="utf-8", newline="") as handle:
                    handle.write(new)

    print("(what-if — no changes written)" if args.what_if else "Done. Rebuild + test to confirm compatibility.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
