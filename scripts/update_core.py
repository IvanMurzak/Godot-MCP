"""Set the core MCP / reflection / query pins of the extensions in lockstep.

The pins (com.IvanMurzak.McpPlugin, com.IvanMurzak.ReflectorNet, com.IvanMurzak.Rql = the RQL module API)
must EQUAL the core addon's (Godot-MCP.csproj); `extensions.core_pin_drift` fails CI otherwise. Run this in
the change that moves the core pins. `--only all` (the default) edits every csproj `core_pin_drift` checks:
each module and templates/extension. A module list edits only those modules.

  python scripts/update_core.py --mcp-plugin 6.11.0
  python scripts/update_core.py --mcp-plugin 6.11.0 --reflector-net 5.4.0 --what-if
  python scripts/update_core.py --rql 0.2.0 --only Godot-AI-Animation
"""
from __future__ import annotations

import argparse
import re

from extension_release import selection
from extensions import CORE_DEPENDENCIES, ROOT, pin_folders, read_text, write_text

# A NuGet version (x.y.z[-pre][+meta]); anything else would break the Version="..." attribute.
VERSION = re.compile(r'[0-9A-Za-z.+-]+')


def targets(only):
    folders = pin_folders() if only == 'all' else [ROOT / 'extensions' / m['name'] for m in selection(only)]
    return [csproj for folder in folders for csproj in sorted(folder.rglob('*.csproj'))]


def update(csprojs, pins, what_if=False):
    """Rewrite each PackageReference Version in `pins`, keeping every other byte (and the newlines)."""
    for csproj in csprojs:
        content = read_text(csproj)
        new = content
        for package, version in pins.items():
            new = re.sub(r'(<PackageReference\s+Include="' + re.escape(package) + r'"\s+Version=")[^"]+(")',
                         lambda m: m.group(1) + version + m.group(2), new)
        if new == content:
            continue
        print('Updating ' + csproj.relative_to(ROOT).as_posix())
        for package, version in pins.items():
            print(f'  {package} -> {version}')
        if not what_if:
            write_text(csproj, new)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--mcp-plugin')
    parser.add_argument('--reflector-net')
    parser.add_argument('--rql')
    parser.add_argument('--only', default='all', help='all (modules + template) or comma-separated module names/package IDs')
    parser.add_argument('--what-if', action='store_true', help='print the changes without writing them')
    args = parser.parse_args(argv)
    # CORE_DEPENDENCIES order: McpPlugin, ReflectorNet, Rql.
    pins = {package: version for package, version in
            zip(CORE_DEPENDENCIES, (args.mcp_plugin, args.reflector_net, args.rql)) if version}
    if not pins:
        parser.error('pass --mcp-plugin, --reflector-net and/or --rql')
    if not all(VERSION.fullmatch(version) for version in pins.values()):
        parser.error('a version may hold only letters, digits and . - +')
    try:
        csprojs = targets(args.only)
    except ValueError as error:
        parser.error(str(error))
    update(csprojs, pins, args.what_if)
    print('(what-if: no changes written)' if args.what_if else 'Done. Rebuild + test to confirm compatibility.')
    return 0


if __name__ == '__main__':
    raise SystemExit(main())
