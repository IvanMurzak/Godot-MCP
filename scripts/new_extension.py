"""Create and register a source-only module inside this monorepo."""
from __future__ import annotations

import argparse
import json
import re
import xml.etree.ElementTree as ET

from extensions import ROOT, modules, read_json


def create(feature, root=ROOT):
    if not re.fullmatch(r'[A-Z][A-Za-z0-9]*', feature):
        raise ValueError('Feature must be PascalCase alphanumeric')
    name = 'Godot-AI-' + feature
    package = 'com.IvanMurzak.Godot.MCP.' + feature
    folder = root / 'extensions' / name
    inventory_path = root / 'extensions/manifest.json'
    inventory = read_json(inventory_path)
    catalog_path = root / 'addons/godot_mcp/extensions.catalog.json'
    catalog = read_json(catalog_path)
    mirror_path = root / 'cli/src/utils/extensions-catalog.ts'
    mirror = mirror_path.read_text(encoding='utf-8')
    boundary = '] as const;'
    if folder.exists() or any(m['packageId'].lower() == package.lower() for m in modules(root)):
        raise ValueError('Module already exists: ' + name)
    if mirror.count(boundary) != 1:
        raise ValueError('Expected one typed catalog boundary')
    replacements = {'YOUR_GITHUB_USERNAME_REPOSITORY': 'IvanMurzak/Godot-MCP',
                    'YOUR_DISPLAY_NAME': feature + ' Tools',
                    'YOUR_DESCRIPTION': 'RQL module for Godot ' + feature + '.',
                    'YOUR_SOURCE': feature.lower(), 'YOUR_FEATURE': feature}
    def replace(text):
        for token, value in replacements.items():
            text = text.replace(token, value)
        return text
    template = root / 'templates/extension'
    for source in template.rglob('*'):
        if not source.is_file() or any(part in ('.github', 'bin', 'obj', '.godot', 'local-nuget')
                                       for part in source.relative_to(template).parts):
            continue
        if source.name in ('init.py', 'init.ps1'):
            continue
        target = folder / replace(source.relative_to(template).as_posix())
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(replace(source.read_text(encoding='utf-8-sig')), encoding='utf-8')
    project = folder / 'src' / name / (name + '.csproj')
    version = ET.parse(project).findtext('.//Version')
    metadata = {'name': name, 'packageId': package, 'version': version,
                'project': project.relative_to(root).as_posix(),
                'tests': f'extensions/{name}/tests/{name}.Tests/{name}.Tests.csproj',
                'minGodotVersion': '4.3.0'}
    (folder / 'extension.json').write_text(json.dumps(metadata, indent=2) + '\n', encoding='utf-8')
    descriptor = {'name': feature + ' Tools', 'description': replacements['YOUR_DESCRIPTION'],
                  'packageId': package, 'version': None,
                  'gitUrl': f'https://github.com/IvanMurzak/Godot-MCP/blob/main/extensions/{name}/README.md',
                  'tools': []}
    inventory['extensions'].append(name)
    catalog['extensions'].append(descriptor)
    inventory_path.write_text(json.dumps(inventory, indent=2) + '\n', encoding='utf-8')
    catalog_path.write_text(json.dumps(catalog, indent=2) + '\n', encoding='utf-8')
    mirror_path.write_text(mirror.replace(boundary, json.dumps(descriptor, indent=2) + ',\n' + boundary), encoding='utf-8')
    modules(root)
    return folder


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('feature')
    print(create(parser.parse_args().feature))
