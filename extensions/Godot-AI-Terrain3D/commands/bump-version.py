#!/usr/bin/env python3
"""Set the extension package version (the package csproj <Version>). POSIX/Python mirror of bump-version.ps1.

Usage:
  python commands/bump-version.py 1.0.1
  python commands/bump-version.py 1.0.1 --what-if
  python commands/bump-version.py --get        # print current version
"""
from __future__ import annotations
import argparse
import re
import sys
from pathlib import Path

SEMVER = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$")


def find_package_csproj(root: Path) -> Path:
    for p in (root / "src").rglob("*.csproj"):
        if "<PackageId>" in p.read_text(encoding="utf-8"):
            return p
    raise SystemExit("error: could not find the package csproj (with <PackageId>) under src/")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("version", nargs="?", help="new semantic version, e.g. 1.0.1")
    ap.add_argument("--what-if", action="store_true")
    ap.add_argument("--get", action="store_true", help="print current version and exit")
    args = ap.parse_args()

    root = Path(__file__).resolve().parent.parent
    csproj = find_package_csproj(root)
    content = csproj.read_text(encoding="utf-8")
    m = re.search(r"<Version>([^<]+)</Version>", content)
    if not m:
        raise SystemExit(f"error: no <Version> element in {csproj.name}")
    current = m.group(1)

    if args.get:
        print(current)
        return 0

    if not args.version or not SEMVER.match(args.version):
        print("error: pass a semantic version major.minor.patch[-prerelease]", file=sys.stderr)
        return 2

    print(f"Package: {csproj.name}")
    print(f"Version: {current} -> {args.version}")
    if args.what_if:
        print("(what-if — no changes written)")
        return 0
    csproj.write_text(re.sub(r"<Version>[^<]+</Version>", f"<Version>{args.version}</Version>", content),
                       encoding="utf-8", newline="")
    print("Updated.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
