#!/usr/bin/env python3
"""
Export-build package guard for the Godot-MCP addon.

Builds Godot-MCP.csproj in Godot's export configurations (no `TOOLS`) and checks which NuGet assemblies land in the
output, the proof behind docs/ARCHITECTURE.md "Editor-only NuGet packages":

  * default export        -> McpPlugin + ReflectorNet, and NO RQL, Code-Exec or Roslyn;
  * `-p:GodotMcpCodeExecInGame=true` (the in-game script-execute opt-in)
                          -> also Code-Exec + Roslyn, still NO RQL, and the in-game tool compiled in.

No Godot binary is needed: Godot.NET.Sdk restores from nuget.org. Every build writes to its own fresh output folder.

Usage:
    python scripts/check-export-assemblies.py                        # ExportRelease + ExportDebug
    python scripts/check-export-assemblies.py --configuration ExportRelease
    python scripts/check-export-assemblies.py --dir <output> --code-exec   # only check an existing output folder

Exit codes: 0 = all checks hold, 1 = a check failed, 2 = a build failed.
"""
from __future__ import annotations

import argparse
import re
import subprocess
import sys
import tempfile
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parent.parent
CSPROJ = REPO_ROOT / "Godot-MCP.csproj"
ADDON_ASSEMBLY = "Godot-MCP.dll"

REQUIRED = ("McpPlugin.dll", "ReflectorNet.dll")
RQL = re.compile(r"^com\.IvanMurzak\.Rql.*\.dll$|^Rql.*\.dll$", re.IGNORECASE)
CODE_EXEC = re.compile(r"^com\.IvanMurzak\.CodeExec.*\.dll$", re.IGNORECASE)
ROSLYN = re.compile(r"^Microsoft\.CodeAnalysis.*\.dll$", re.IGNORECASE)
# The in-game tool's type name, present in the addon assembly's metadata only when it is compiled in.
IN_GAME_TOOL = b"Tool_GameScript"


def check_output(out_dir: Path, code_exec: bool) -> list[str]:
    """The failed checks for one build output folder (empty = all hold)."""
    names = sorted(p.name for p in out_dir.glob("*.dll"))
    failures = [f"missing {name}" for name in (ADDON_ASSEMBLY, *REQUIRED) if name not in names]
    failures += [f"unexpected RQL assembly {n}" for n in names if RQL.match(n)]
    if code_exec:
        if not any(CODE_EXEC.match(n) for n in names):
            failures.append("missing com.IvanMurzak.CodeExec*.dll")
        if "Microsoft.CodeAnalysis.CSharp.dll" not in names:
            failures.append("missing Microsoft.CodeAnalysis.CSharp.dll")
    else:
        failures += [f"unexpected Code-Exec assembly {n}" for n in names if CODE_EXEC.match(n)]
        failures += [f"unexpected Roslyn assembly {n}" for n in names if ROSLYN.match(n)]
    addon = out_dir / ADDON_ASSEMBLY
    if addon.is_file() and (IN_GAME_TOOL in addon.read_bytes()) != code_exec:
        failures.append(f"{ADDON_ASSEMBLY} {'lacks' if code_exec else 'contains'} the in-game script-execute tool")
    return failures


def build(configuration: str, code_exec: bool, out_dir: Path) -> bool:
    cmd = ["dotnet", "build", str(CSPROJ), "-c", configuration, "-o", str(out_dir), "-nologo", "-v", "q"]
    if code_exec:
        cmd.append("-p:GodotMcpCodeExecInGame=true")
    print("$ " + " ".join(cmd), flush=True)
    return subprocess.run(cmd, cwd=REPO_ROOT).returncode == 0


def report(label: str, failures: list[str]) -> bool:
    if failures:
        print(f"FAILED {label}:")
        for failure in failures:
            print(f"  - {failure}")
        return False
    print(f"OK     {label}")
    return True


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description="Godot-MCP export-build package guard")
    parser.add_argument("--configuration", action="append", choices=("ExportRelease", "ExportDebug"),
                        help="export configuration to build (repeatable; default: both)")
    parser.add_argument("--dir", type=Path, help="check this existing output folder instead of building")
    parser.add_argument("--code-exec", action="store_true", help="with --dir: the folder is an opt-in build")
    args = parser.parse_args(argv)

    if args.dir is not None:
        return 0 if report(str(args.dir), check_output(args.dir, args.code_exec)) else 1

    ok = True
    with tempfile.TemporaryDirectory(prefix="godot-mcp-export-") as tmp:
        for configuration in args.configuration or ["ExportRelease", "ExportDebug"]:
            for code_exec in (False, True):
                label = f"{configuration}{' + GodotMcpCodeExecInGame' if code_exec else ''}"
                out_dir = Path(tmp) / label.replace(" + ", "-")
                if not build(configuration, code_exec, out_dir):
                    print(f"FAILED {label}: dotnet build failed")
                    return 2
                ok = report(label, check_output(out_dir, code_exec)) and ok
    return 0 if ok else 1


if __name__ == "__main__":
    raise SystemExit(main())
