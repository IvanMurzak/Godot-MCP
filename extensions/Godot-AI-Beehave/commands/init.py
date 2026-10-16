#!/usr/bin/env python3
"""Initialize a new Godot-MCP extension from this template (POSIX/Python path; mirrors init.ps1).

From a single --feature name (PascalCase, e.g. "Particles") this substitutes every placeholder, renames
files/folders whose name contains YOUR_FEATURE, then activates CI (*.yml-sample -> *.yml).

Derived (override with the matching flag):
  package id   : com.IvanMurzak.Godot.MCP.<Feature>
  repo         : IvanMurzak/Godot-AI-<Feature>      (--github-repository)
  display name : "<Feature> Tools"                   (--display-name)
  description  : "AI MCP tools for Godot <Feature>."  (--description)
  tool prefix  : <feature lowercased>                 (--tool-prefix)

Usage:
  python commands/init.py --feature Particles
  python commands/init.py --feature Particles --github-repository myuser/Godot-AI-Particles
"""
from __future__ import annotations
import argparse
import re
import sys
from pathlib import Path

EXCLUDE_DIRS = {".git", "bin", "obj", ".godot", "local-nuget", ".vs"}
TEXT_EXT = {".cs", ".csproj", ".props", ".targets", ".md", ".json", ".yml", ".yaml",
            ".yml-sample", ".godot", ".config", ".sh", ".ps1", ".py", ".editorconfig",
            ".gitignore", ".txt"}
SELF = {"init.py", "init.ps1"}


def is_excluded(path: Path, root: Path) -> bool:
    rel_parts = path.relative_to(root).parts
    return any(part in EXCLUDE_DIRS for part in rel_parts)


def is_text(path: Path) -> bool:
    name = path.name
    if name == "LICENSE":
        return True
    if name.endswith(".yml-sample"):
        return True
    return path.suffix.lower() in TEXT_EXT


def main() -> int:
    ap = argparse.ArgumentParser(description="Initialize a Godot-MCP extension from the template.")
    ap.add_argument("--feature", required=True, help="PascalCase feature name, e.g. Particles")
    ap.add_argument("--display-name")
    ap.add_argument("--description")
    ap.add_argument("--github-repository")
    ap.add_argument("--tool-prefix")
    args = ap.parse_args()

    feature = args.feature
    if not re.match(r"^[A-Za-z][A-Za-z0-9]*$", feature):
        print(f"error: --feature must be alphanumeric PascalCase. Got: {feature!r}", file=sys.stderr)
        return 2

    display = args.display_name or f"{feature} Tools"
    description = args.description or f"AI MCP tools for Godot {feature}."
    repo = args.github_repository or f"IvanMurzak/Godot-AI-{feature}"
    prefix = args.tool_prefix or feature.lower()

    # Longest tokens first so no token is a prefix of another mid-replacement.
    replacements = [
        ("YOUR_GITHUB_USERNAME_REPOSITORY", repo),
        ("YOUR_DISPLAY_NAME", display),
        ("YOUR_DESCRIPTION", description),
        ("YOUR_TOOL_PREFIX", prefix),
        ("YOUR_FEATURE", feature),
    ]

    root = Path(__file__).resolve().parent.parent

    print("Initializing Godot-MCP extension:")
    print(f"  Feature      : {feature}")
    print(f"  Package id   : com.IvanMurzak.Godot.MCP.{feature}")
    print(f"  Repository   : {repo}")
    print(f"  Display name : {display}")
    print(f"  Tool prefix  : {prefix}-*\n")

    # 1) Replace placeholder content.
    print("Replacing placeholders in file content...")
    for path in root.rglob("*"):
        if not path.is_file() or is_excluded(path, root) or path.name in SELF or not is_text(path):
            continue
        try:
            text = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        new = text
        for token, value in replacements:
            new = new.replace(token, value)
        if new != text:
            path.write_text(new, encoding="utf-8", newline="")
            print(f"  updated {path.relative_to(root)}")

    # 2) Rename files/folders containing YOUR_FEATURE, deepest-first.
    print("Renaming files and folders...")
    entries = [p for p in root.rglob("*") if not is_excluded(p, root)]
    for path in sorted(entries, key=lambda p: len(str(p)), reverse=True):
        if "YOUR_FEATURE" in path.name:
            new_name = path.name.replace("YOUR_FEATURE", feature)
            path.rename(path.with_name(new_name))
            print(f"  renamed {path.name} -> {new_name}")

    # 3) Activate CI.
    print("Activating CI workflows (*.yml-sample -> *.yml)...")
    wf = root / ".github" / "workflows"
    if wf.is_dir():
        for sample in wf.glob("*.yml-sample"):
            target = sample.with_name(sample.name[: -len("-sample")])
            sample.replace(target)
            print(f"  activated {sample.name} -> {target.name}")

    print("\nDone. Next:")
    print(f"  dotnet build src/Godot-AI-{feature}/Godot-AI-{feature}.csproj")
    print(f"  dotnet test  tests/Godot-AI-{feature}.Tests/Godot-AI-{feature}.Tests.csproj")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
