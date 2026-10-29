#!/usr/bin/env python3
"""Set the extension package version (the single source of truth: the package csproj <Version>).

The release workflow is version-gated on this value: it publishes only when <Version> increases AND the
tag does not exist yet. Inside the Godot-MCP monorepo use `scripts/extension_release.py bump` instead.

Usage:
  python commands/bump-version.py 1.0.1
  python commands/bump-version.py 1.0.1 --what-if
  python commands/bump-version.py --get        # print the current version (CI's version gate)
"""
from __future__ import annotations
import argparse
import re
from pathlib import Path

SEMVER = re.compile(r"^\d+\.\d+\.\d+(-[0-9A-Za-z.\-]+)?$")


def read(path: Path) -> str:
    with open(path, encoding="utf-8", newline="") as handle:
        return handle.read()


def find_package_csproj(root: Path) -> "tuple[Path, str]":
    for p in sorted((root / "src").rglob("*.csproj")):
        content = read(p)
        if "<PackageId>" in content:
            return p, content
    raise SystemExit("error: could not find the package csproj (with <PackageId>) under src/")


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("version", nargs="?", help="new semantic version, e.g. 1.0.1")
    ap.add_argument("--what-if", action="store_true")
    ap.add_argument("--get", action="store_true", help="print current version and exit")
    args = ap.parse_args()

    root = Path(__file__).resolve().parent.parent
    csproj, content = find_package_csproj(root)
    m = re.search(r"<Version>([^<]+)</Version>", content)
    if not m:
        raise SystemExit(f"error: no <Version> element in {csproj.name}")
    current = m.group(1)

    if args.get:
        print(current)
        return 0

    if not args.version or not SEMVER.match(args.version):
        ap.error("pass a semantic version major.minor.patch[-prerelease]")

    print(f"Package: {csproj.name}")
    print(f"Version: {current} -> {args.version}")
    if args.what_if:
        print("(what-if — no changes written)")
        return 0
    with open(csproj, "w", encoding="utf-8", newline="") as handle:
        handle.write(re.sub(r"<Version>[^<]+</Version>", lambda _: f"<Version>{args.version}</Version>", content))
    print("Updated.")
    print("Remember to commit, then push to main to trigger the version-gated release.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
