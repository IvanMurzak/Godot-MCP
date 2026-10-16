#!/usr/bin/env python3
"""Update the core MCP/reflection pins in lockstep across package/tests/testbed. Mirror of update-core.ps1.

Keep these MIN-versions matching the core Godot-MCP addon (Godot-MCP/Godot-MCP.csproj).

Usage:
  python commands/update-core.py --mcp-plugin 6.11.0
  python commands/update-core.py --mcp-plugin 6.11.0 --reflector-net 5.4.0 --what-if
"""
from __future__ import annotations
import argparse
import re
from pathlib import Path


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--mcp-plugin")
    ap.add_argument("--reflector-net")
    ap.add_argument("--what-if", action="store_true")
    args = ap.parse_args()
    if not args.mcp_plugin and not args.reflector_net:
        raise SystemExit("error: pass --mcp-plugin and/or --reflector-net")

    pins = {}
    if args.mcp_plugin:
        pins["com.IvanMurzak.McpPlugin"] = args.mcp_plugin
    if args.reflector_net:
        pins["com.IvanMurzak.ReflectorNet"] = args.reflector_net

    root = Path(__file__).resolve().parent.parent
    csprojs = []
    for sub in ("src", "tests", "testbed"):
        d = root / sub
        if d.is_dir():
            csprojs += list(d.rglob("*.csproj"))

    for csproj in csprojs:
        content = csproj.read_text(encoding="utf-8")
        new = content
        for pid, ver in pins.items():
            new = re.sub(
                r'(<PackageReference\s+Include="' + re.escape(pid) + r'"\s+Version=")[^"]+(")',
                r"\g<1>" + ver + r"\g<2>", new)
        if new != content:
            print(f"Updating {csproj.relative_to(root)}")
            for pid, ver in pins.items():
                print(f"  {pid} -> {ver}")
            if not args.what_if:
                csproj.write_text(new, encoding="utf-8", newline="")

    print("(what-if — no changes written)" if args.what_if else "Done. Rebuild + test to confirm.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
