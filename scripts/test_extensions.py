"""Isolated NuGet consumers followed by one live Godot editor for all modules."""
from __future__ import annotations

import argparse
import json
import os
import re
from pathlib import Path
import shutil
import subprocess
import sys
import uuid
import xml.etree.ElementTree as ET

from extensions import ROOT, core_pins, modules, read_json, run, verify_package


def compatible(module, sdk):
    return tuple(map(int, sdk.split('.'))) >= tuple(map(int, module['minGodotVersion'].split('.')))


def unit_tests(output):
    run(['dotnet', 'build', 'Godot-MCP.sln', '-c', 'Debug'], log=output / 'core-build.log')
    # Preserve the legacy authoring gate too: Release packing excludes #if TOOLS,
    # while xUnit links only pure-managed files. Build every complete module in
    # Debug against its declared dependency floors before testing consumers.
    for module in modules():
        run(['dotnet', 'build', module['project'], '-c', 'Debug'],
            log=output / (module['name'] + '-authoring-build.log'))
    projects = ['Godot-MCP.Tests/Godot-MCP.Tests.csproj'] + [m['tests'] for m in modules()]
    for index, project in enumerate(projects):
        text = run(['dotnet', 'test', project, '-c', 'Debug', '--logger', 'trx',
                    '--results-directory', str(output / f'unit-{index}')], log=output / f'unit-{index}.log')
        summary = [line for line in text.splitlines() if 'Passed!' in line or 'Total tests:' in line]
        print(project, '\n'.join(summary), flush=True)


def consumer(folder, sdk, selected, version, feed, cache):
    folder.mkdir(parents=True)
    shutil.copytree(ROOT / 'addons/godot_mcp', folder / 'addons/godot_mcp',
                    ignore=shutil.ignore_patterns('bin', 'obj', '.godot', '*.uid'))
    assembly = 'GodotExtensionTests'
    project = ET.Element('Project', Sdk='Godot.NET.Sdk/' + sdk)
    properties = ET.SubElement(project, 'PropertyGroup')
    for key, value in {'TargetFramework': 'net8.0', 'EnableDynamicLoading': 'true',
                       'Nullable': 'enable', 'AssemblyName': assembly}.items():
        ET.SubElement(properties, key).text = value
    group = ET.SubElement(project, 'ItemGroup')
    for package, pin in core_pins().items():
        ET.SubElement(group, 'PackageReference', Include=package, Version='[' + pin + ']')
    for module in selected:
        ET.SubElement(group, 'PackageReference', Include=module['packageId'], Version='[' + version + ']')
    ET.SubElement(group, 'EmbeddedResource', Include='addons/godot_mcp/extensions.catalog.json',
                  LogicalName='Godot-MCP.extensions.catalog.json')
    csproj = folder / 'GodotExtensionTests.csproj'
    ET.ElementTree(project).write(csproj, encoding='utf-8', xml_declaration=True)
    config = ET.Element('configuration')
    sources = ET.SubElement(config, 'packageSources')
    ET.SubElement(sources, 'clear')
    ET.SubElement(sources, 'add', key='candidate', value=str(feed))
    ET.SubElement(sources, 'add', key='nuget.org', value='https://api.nuget.org/v3/index.json')
    # Train supplies private candidate packages through its generated root
    # config. Keep that feed visible after adding the extension's local feed.
    chain_config = ROOT / 'nuget.config'
    if os.environ.get('CHAIN_LOCK') and chain_config.is_file():
        for entry in ET.parse(chain_config).findall('.//packageSources/add'):
            if entry.attrib.get('value') not in {node.attrib.get('value') for node in sources}:
                ET.SubElement(sources, 'add', **entry.attrib)
    ET.ElementTree(config).write(folder / 'nuget.config', encoding='utf-8')
    (folder / 'project.godot').write_text(
        'config_version=5\n\n[application]\nconfig/name="GodotExtensionTests"\n'
        f'config/features=PackedStringArray("{sdk}", "C#")\n\n'
        f'[dotnet]\nproject/assembly_name="{assembly}"\n\n'
        '[editor_plugins]\nenabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")\n', encoding='utf-8')
    for module in selected:
        testbed = ROOT / 'extensions' / module['name'] / 'testbed'
        for fixture in testbed.glob('*.tres'):
            target = folder / fixture.name
            if target.exists():
                raise ValueError('Conflicting E2E fixture name: ' + fixture.name)
            shutil.copy2(fixture, target)
        for relative in module.get('testDirectories', []):
            directory = (folder / relative).resolve()
            if not directory.is_relative_to(folder.resolve()):
                raise ValueError('Test directory escapes project: ' + relative)
            directory.mkdir(parents=True, exist_ok=True)
    autoloads = {}
    for module in selected:
        for name, path in module.get('testAutoloads', {}).items():
            if (name in autoloads or not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]*', name)
                    or not re.fullmatch(r'res://addons/[A-Za-z0-9_./-]+', path)
                    or '..' in Path(path.removeprefix('res://')).parts):
                raise ValueError('Invalid or conflicting test autoload: ' + name)
            autoloads[name] = path
    if autoloads:
        with (folder / 'project.godot').open('a', encoding='utf-8') as config:
            config.write('\n[autoload]\n')
            for name, path in autoloads.items():
                config.write(f'{name}="*{path}"\n')
    return csproj


def compile_consumer(project, selected, version, cache, log):
    env = {**os.environ, 'NUGET_PACKAGES': str(cache)}
    run(['dotnet', 'build', project, '-c', 'Debug'], env=env, log=log)
    # Evaluate the actual NuGet-imported Compile items after restore. A cache may
    # contain every package; only declared packages may inject source into this assembly.
    evaluated = json.loads(run(['dotnet', 'msbuild', project, '-getItem:Compile', '-verbosity:quiet'], env=env))
    paths = {Path(item['FullPath']).resolve() for item in evaluated['Items']['Compile']}
    expected_pins = core_pins()
    assets = read_json(project.parent / '.godot/mono/temp/obj/project.assets.json')['libraries']
    for package, pin in expected_pins.items():
        if package + '/' + pin not in assets:
            raise ValueError('Consumer resolved the wrong core library: ' + package + '/' + pin)
    selected_ids = {module['packageId'].lower() for module in selected}
    for module in modules():
        package_root = cache / module['packageId'].lower() / version
        actual = {path for path in paths if path.is_relative_to(package_root)}
        if module['packageId'].lower() in selected_ids:
            expected = {path.resolve() for path in (package_root / 'src').rglob('*.cs')}
            if not expected or actual != expected:
                raise ValueError('Consumer did not compile exact package sources: ' + module['name'])
        elif actual:
            raise ValueError('Undeclared extension sources leaked into consumer: ' + module['name'])


def isolation_canary(isolated, combined, cache, output):
    text = ('public static class IsolationCanary { public static System.Type Sibling => '
            'typeof(com.IvanMurzak.Godot.MCP.Beehave.Tool_Beehave); }\n')
    paths = [project.parent / 'IsolationCanary.cs' for project in (isolated, combined)]
    for path in paths:
        if path.exists():
            raise ValueError('Canary must not overwrite existing work: ' + str(path))
    env = {**os.environ, 'NUGET_PACKAGES': str(cache)}
    try:
        for path in paths:
            path.write_text(text)
        run(['dotnet', 'build', combined, '-c', 'Debug', '--no-restore'], env=env,
            log=output / 'canary-combined.log')
        result = subprocess.run(['dotnet', 'build', str(isolated), '-c', 'Debug', '--no-restore'],
                                env=env, capture_output=True, text=True, encoding='utf-8', errors='replace')
        (output / 'canary-isolated.log').write_text(result.stdout + result.stderr, encoding='utf-8')
        if result.returncode == 0 or 'CS0234' not in result.stdout or 'Beehave' not in result.stdout:
            raise ValueError('Isolation canary did not fail on the undeclared sibling type')
        print('Isolation canary: combined succeeds; Animation-only consumer rejects Beehave (CS0234)', flush=True)
    finally:
        for path in paths:
            path.unlink(missing_ok=True)
    # Restore the combined binary after removing the canary.
    run(['dotnet', 'build', combined, '-c', 'Debug', '--no-restore'], env=env,
        log=output / 'canary-clean-build.log')


def prepare(sdk, output, *, prove_isolation=False):
    selected = [module for module in modules() if compatible(module, sdk)]
    feed, cache = output / 'feed', output / 'packages'
    feed.mkdir(parents=True)
    version = '0.0.0-ci.' + uuid.uuid4().hex
    for module in selected:
        run(['dotnet', 'pack', module['project'], '-c', 'Release', '-p:Version=' + version,
             '-o', feed], log=output / (module['name'] + '-pack.log'))
        archive = feed / f"{module['packageId']}.{version}.nupkg"
        verify_package(archive, module, version=version)
    projects = {}
    for name, subset in [('core', [])] + [(m['name'], [m]) for m in selected] + [('combined', selected)]:
        project = consumer(output / name, sdk, subset, version, feed, cache)
        compile_consumer(project, subset, version, cache, output / (name + '-build.log'))
        projects[name] = project
        print(f'Compiled {name}: {len(subset)} extension(s), exact packed-source inclusion verified', flush=True)
    if prove_isolation:
        isolation_canary(projects['Godot-AI-Animation'], projects['combined'], cache, output)
    plan = {'sdk': sdk, 'ciVersion': version, 'modules': [m['name'] for m in selected],
            'project': str(projects['combined']), 'nugetCache': str(cache)}
    (output / 'suite.json').write_text(json.dumps(plan, indent=2) + '\n')
    return plan


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--sdk', default='4.5.1')
    parser.add_argument('--godot', help='Godot mono executable matching --sdk')
    parser.add_argument('--server', help='Released gamedev-mcp-server executable')
    parser.add_argument('--prepare-only', action='store_true', help='Compile/package verification only; no live editor')
    parser.add_argument('--unit-only', action='store_true')
    parser.add_argument('--skip-unit', action='store_true', help='CI: unit tests already ran in the required parent job')
    parser.add_argument('--prove-isolation', action='store_true')
    args = parser.parse_args()
    if not args.unit_only and not args.prepare_only and not args.godot:
        parser.error('Full suite requires --godot; --prepare-only explicitly omits live tests')
    output = ROOT / '.artifacts/extensions' / args.sdk / uuid.uuid4().hex[:12]
    output.mkdir(parents=True)
    print('Suite output:', output, flush=True)
    if not args.skip_unit:
        unit_tests(output)
    if not args.unit_only:
        plan = prepare(args.sdk, output, prove_isolation=args.prove_isolation)
        if not args.prepare_only:
            from extension_live import live, released_server
            server = args.server or os.environ.get('GODOT_MCP_SERVER_PATH') or released_server(output / 'server')
            live(plan, args.godot, server, output)
    print('PASS:', 'unit tests' if args.unit_only else
          'compile/package verification (live tests explicitly omitted)' if args.prepare_only else
          'unit, isolated consumers and combined live-editor suite', flush=True)
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (ValueError, RuntimeError, subprocess.TimeoutExpired) as error:
        print('FAIL:', error, file=sys.stderr)
        raise SystemExit(1)
