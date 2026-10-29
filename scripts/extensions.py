"""Shared inventory and package verification for independently released Godot modules."""
from __future__ import annotations

import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
import zipfile

ROOT = Path(__file__).resolve().parents[1]
SEMVER = re.compile(r'^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)$')
# The only package dependencies an extension may declare (the MCP stack and the RQL module API); their pins must
# equal the core's.
CORE_DEPENDENCIES = ('com.IvanMurzak.McpPlugin', 'com.IvanMurzak.ReflectorNet', 'com.IvanMurzak.Rql')


def read_text(path):
    """UTF-8 text with its newlines untouched (a Windows runner would otherwise decode cp1252 and write CRLF)."""
    with open(path, encoding='utf-8', newline='') as handle:
        return handle.read()


def write_text(path, text):
    with open(path, 'w', encoding='utf-8', newline='') as handle:
        handle.write(text)


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8-sig'))


def modules(root=ROOT, only=()):
    inventory = read_json(root / 'extensions/manifest.json')
    if inventory.get('schemaVersion') != 1:
        raise ValueError('Unsupported extension inventory schema')
    names = inventory['extensions']
    if not names or len(names) != len(set(names)):
        raise ValueError('Extension inventory must be nonempty and unique')
    catalog = read_json(root / 'addons/godot_mcp/extensions.catalog.json')['extensions']
    rows = []
    for name in names:
        if not re.fullmatch(r'Godot-AI-[A-Za-z][A-Za-z0-9]*', name):
            raise ValueError('Invalid extension folder: ' + str(name))
        folder = root / 'extensions' / name
        row = read_json(folder / 'extension.json')
        if row['name'] != name or not SEMVER.fullmatch(row['version']):
            raise ValueError('Invalid module identity/version: ' + name)
        for field in ('project', 'tests'):
            path = (root / row[field]).resolve()
            if not path.is_relative_to(folder.resolve()) or not path.is_file():
                raise ValueError('Invalid module project path: ' + row[field])
        project = ET.parse(root / row['project'])
        if project.findtext('.//PackageId') != row['packageId'] or project.findtext('.//Version') != row['version']:
            raise ValueError('Package identity/version drift: ' + name)
        if project.findtext('.//IncludeBuildOutput') != 'false':
            raise ValueError('Extension must remain source-only: ' + name)
        descriptor = next((entry for entry in catalog if entry['packageId'] == row['packageId']), None)
        if descriptor is None:
            raise ValueError('Missing shared catalog entry: ' + name)
        tools = read_json(folder / 'tests/e2e/e2e-tools.json')['tools']
        missing = {t['name'] for t in descriptor['tools']} - {t['tool'] for t in tools}
        if missing:
            raise ValueError(f'{name}: E2E manifest omits tools: {sorted(missing)}')
        rows.append(row)
    if len({row['packageId'].lower() for row in rows}) != len(rows):
        raise ValueError('Duplicate NuGet package identity')
    if only:
        selected = [row for row in rows if set(only).intersection((row['name'], row['packageId']))]
        matched = {value for row in selected for value in (row['name'], row['packageId'])}
        if set(only) - matched:
            raise ValueError('Unknown extension selection: ' + ', '.join(sorted(set(only) - matched)))
        return selected
    return rows


def run(args, *, cwd=ROOT, env=None, log=None, timeout=600):
    result = subprocess.run([str(arg) for arg in args], cwd=cwd, env=env,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                            encoding='utf-8', errors='replace', timeout=timeout)
    if log:
        Path(log).parent.mkdir(parents=True, exist_ok=True)
        Path(log).write_text(result.stdout, encoding='utf-8')
    if result.returncode:
        raise RuntimeError(f'Command failed ({result.returncode}): {args[:4]}\n{result.stdout[-16000:]}')
    return result.stdout


def verify_package(archive, module, root=ROOT, version=None):
    """Check the actual nupkg bytes, not just the authoring csproj settings."""
    source = (root / module['project']).parent
    with zipfile.ZipFile(archive) as bundle:
        names = bundle.namelist()
        if any(name.lower().endswith('.dll') for name in names):
            raise ValueError('Source-only package contains a DLL: ' + str(archive))
        specs = [name for name in names if name.endswith('.nuspec')]
        if len(specs) != 1:
            raise ValueError('Expected one nuspec: ' + str(archive))
        spec = ET.fromstring(bundle.read(specs[0]))
        metadata = {node.tag.rsplit('}', 1)[-1]: node.text for node in spec.iter()
                    if node.tag.rsplit('}', 1)[-1] in ('id', 'version')}
        if metadata.get('id') != module['packageId'] or metadata.get('version') != (version or module['version']):
            raise ValueError('Packaged identity/version differs from requested module: ' + str(archive))
        deps = {node.attrib['id']: node for node in spec.iter() if node.tag.rsplit('}', 1)[-1] == 'dependency'}
        expected = set(CORE_DEPENDENCIES)
        if set(deps) != expected:
            raise ValueError(f'Unexpected package dependencies: {set(deps)}; expected {expected}')
        # RQL is editor-only (TOOLS code): a consumer's game export must not get its DLLs through this package.
        if 'runtime' not in {part.strip().lower() for part in deps['com.IvanMurzak.Rql'].attrib.get('exclude', '').split(',')}:
            raise ValueError('The com.IvanMurzak.Rql dependency must exclude Runtime assets (RQL never ships in a game): ' + str(archive))
        floors = package_pins(root / module['project'], expected)
        floors.update({package: pin for package, pin in chain_pins(root).items() if package in floors})
        for package, node in deps.items():
            if node.attrib.get('version') != floors[package]:
                raise ValueError('Packaged dependency floor differs from source: ' + package)
        required = {f'build/{module["packageId"]}.props', 'README.md'}
        required.update('src/' + path.relative_to(source).as_posix()
                        for path in (source / 'Editor').rglob('*.cs'))
        if not required.issubset(names):
            raise ValueError('Package omits required files: ' + str(required - set(names)))
        if len(names) != len(set(names)) or {name for name in names if name.endswith('.cs')} != {
                name for name in required if name.endswith('.cs')}:
            raise ValueError('Package contains duplicate entries or unexpected sources')
        for name in required:
            relative = name.removeprefix('src/') if name.startswith('src/') else name
            if bundle.read(name) != (source / relative).read_bytes():
                raise ValueError('Packaged bytes differ from source: ' + name)
    return True


def package_pins(csproj, only=None):
    """`Include` -> `Version` of a csproj's PackageReferences, optionally limited to `only`."""
    return {node.attrib['Include']: node.attrib['Version'] for node in ET.parse(csproj).findall('.//PackageReference')
            if 'Include' in node.attrib and (only is None or node.attrib['Include'] in only)}


def core_pins(root=ROOT):
    pins = package_pins(root / 'Godot-MCP.csproj')
    pins.update({package: pin.strip('[]') for package, pin in chain_pins(root).items() if package in pins})
    return pins


def pin_folders(root=ROOT):
    """Every folder whose csproj files must pin the core versions: each module and the template."""
    return [*sorted((root / 'extensions').glob('Godot-AI-*')), root / 'templates/extension']


def core_pin_drift(root=ROOT):
    """Extension/template csproj pins of CORE_DEPENDENCIES that differ from Godot-MCP.csproj.

    A lower ReflectorNet pin than the McpPlugin in use requires is a NU1605 downgrade error,
    and a pin above the core's cannot restore in a consumer that pins the core versions.
    Reads the committed files only: a chain run's Directory.Build.targets override applies
    to core and extensions alike.
    """
    core = package_pins(root / 'Godot-MCP.csproj', CORE_DEPENDENCIES)
    if set(core) != set(CORE_DEPENDENCIES):
        raise ValueError(f'Godot-MCP.csproj must pin {CORE_DEPENDENCIES}; found {sorted(core)}')
    drift = []
    # Fail closed: a moved or empty module folder must not turn the scan into a silent pass.
    for folder in pin_folders(root):
        csprojs = sorted(folder.rglob('*.csproj'))
        if not csprojs:
            drift.append(f'{folder.relative_to(root).as_posix()}: no csproj found')
        drift += [f'{csproj.relative_to(root).as_posix()}: {package} {pin} != core {core[package]}'
                  for csproj in csprojs for package, pin in package_pins(csproj, core).items()
                  if pin != core[package]]
    return drift


def chain_active():
    # chain_feed exports CHAIN_ACTIVE once an override is applied, whatever the lock source.
    return os.environ.get('CHAIN_ACTIVE') == '1'


def chain_pins(root):
    overrides = root / 'Directory.Build.targets'
    pins = {}
    if chain_active() and overrides.is_file():
        for entry in ET.parse(overrides).findall('.//PackageReference'):
            if entry.attrib.get('Update'):
                pins[entry.attrib['Update']] = entry.attrib['Version']
    return pins
