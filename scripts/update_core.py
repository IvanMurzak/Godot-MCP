"""Set the core MCP / reflection / query pins of the extensions in lockstep.

The pins (com.IvanMurzak.McpPlugin, com.IvanMurzak.ReflectorNet, com.IvanMurzak.Rql = the RQL module API)
must EQUAL the core addon's (Godot-MCP.csproj); `extensions.core_pin_drift` fails CI otherwise. Run this in
the change that moves the core pins. It edits the src/, tests/ and testbed/ csproj files of every module in
extensions/manifest.json plus templates/extension, or only of the modules named with --only.

  python scripts/update_core.py --mcp-plugin 6.11.0
  python scripts/update_core.py --mcp-plugin 6.11.0 --reflector-net 5.4.0 --what-if
  python scripts/update_core.py --rql 0.2.0 --only Godot-AI-Animation
"""
from __future__ import annotations

import argparse
import re

from extensions import ROOT, modules

FOLDERS = ('src', 'tests', 'testbed')


def targets(root=ROOT, only=()):
    folders = [root / 'extensions' / m['name'] for m in modules(root, only=only)]
    if not only:
        folders.append(root / 'templates/extension')
    return [csproj for folder in folders for sub in FOLDERS if (folder / sub).is_dir()
            for csproj in sorted((folder / sub).rglob('*.csproj'))]


def update(csprojs, pins, what_if=False, root=ROOT):
    """Rewrite each PackageReference Version in `pins`; returns the changed files. Bytes and newlines are kept."""
    changed = []
    for csproj in csprojs:
        with open(csproj, encoding='utf-8', newline='') as handle:
            content = handle.read()
        new = content
        for package, version in pins.items():
            new = re.sub(r'(<PackageReference\s+Include="' + re.escape(package) + r'"\s+Version=")[^"]+(")',
                         lambda m: m.group(1) + version + m.group(2), new)
        if new == content:
            continue
        changed.append(csproj)
        print('Updating ' + csproj.relative_to(root).as_posix())
        for package, version in pins.items():
            print(f'  {package} -> {version}')
        if not what_if:
            with open(csproj, 'w', encoding='utf-8', newline='') as handle:
                handle.write(new)
    return changed


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--mcp-plugin')
    parser.add_argument('--reflector-net')
    parser.add_argument('--rql')
    parser.add_argument('--only', default='', help='comma-separated module names/package IDs (default: all + template)')
    parser.add_argument('--what-if', action='store_true', help='print the changes without writing them')
    args = parser.parse_args(argv)
    pins = {package: version for package, version in (('com.IvanMurzak.McpPlugin', args.mcp_plugin),
                                                      ('com.IvanMurzak.ReflectorNet', args.reflector_net),
                                                      ('com.IvanMurzak.Rql', args.rql)) if version}
    if not pins:
        parser.error('pass --mcp-plugin, --reflector-net and/or --rql')
    update(targets(only=[value for value in args.only.split(',') if value]), pins, args.what_if)
    print('(what-if: no changes written)' if args.what_if else 'Done. Rebuild + test to confirm compatibility.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
