"""Plan, pack and verify independent Godot NuGet releases from the monorepo."""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import time
import urllib.error
import urllib.request

from extension_live import download
from extensions import ROOT, SEMVER, modules, read_json, run, verify_package

REPO = 'IvanMurzak/Godot-MCP'
PUBLICATION_WAIT_SECONDS = 3600
PUBLICATION_POLL_SECONDS = 30


def tag(module):
    return module['name'].removeprefix('Godot-AI-').lower() + '-v' + module['version']


def nuget_url(module):
    package = module['packageId'].lower()
    return f"https://api.nuget.org/v3-flatcontainer/{package}/{module['version']}/{package}.{module['version']}.nupkg"


def published(module):
    url = 'https://api.nuget.org/v3-flatcontainer/' + module['packageId'].lower() + '/index.json'
    try:
        with urllib.request.urlopen(url, timeout=30) as response:
            versions = json.load(response)['versions']
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return False
        raise
    return module['version'] in versions


def selection(only):
    if only == 'all':
        return modules()
    values = only.split(',')
    if not all(values):
        raise ValueError('Choose all or a comma-separated list of module names/package IDs')
    return modules(only=values)


def pack(selected, output):
    output.mkdir(parents=True, exist_ok=True)
    files = []
    for module in selected:
        run(['dotnet', 'pack', module['project'], '-c', 'Release', '-o', output],
            log=output / (module['name'] + '-pack.log'))
        path = output / f"{module['packageId']}.{module['version']}.nupkg"
        verify_package(path, module)
        files.append(path)
    return files


def verify_registry(module, output):
    path = output / ('nuget-' + module['packageId'] + '.nupkg')
    download(nuget_url(module), path)
    verify_package(path, module)
    return path


def stage(selected, output):
    """Reuse immutable published packages; build only versions absent from NuGet."""
    output.mkdir(parents=True, exist_ok=True)
    pending = []
    for module in selected:
        if published(module):
            existing = verify_registry(module, output)
            existing.replace(output / f"{module['packageId']}.{module['version']}.nupkg")
        else:
            pack([module], output)
            pending.append(module['packageId'])
    (output / 'pending.json').write_text(json.dumps(pending))
    if os.environ.get('GITHUB_OUTPUT'):
        with open(os.environ['GITHUB_OUTPUT'], 'a') as handle:
            handle.write('pending=' + str(bool(pending)).lower() + '\n')


def push(selected, output):
    pending = read_json(output / 'pending.json')
    waiting = [module for module in selected if module['packageId'] in pending]
    # Submit the whole batch before waiting: NuGet validation/indexing can take
    # up to an hour. Sequential waits delay every other package and can expire
    # the one-hour OIDC publishing key before the final package is submitted.
    for module in waiting:
        if not published(module):
            key = os.environ.get('NUGET_API_KEY')
            if not key:
                raise ValueError('Missing short-lived NuGet publishing token')
            path = output / f"{module['packageId']}.{module['version']}.nupkg"
            verify_package(path, module)
            run(['dotnet', 'nuget', 'push', path, '--api-key', key,
                 '--source', 'https://api.nuget.org/v3/index.json'])
            print(f"Submitted {module['packageId']} {module['version']}; awaiting NuGet validation", flush=True)
    deadline = time.monotonic() + PUBLICATION_WAIT_SECONDS
    while waiting:
        for module in waiting[:]:
            if published(module):
                verify_registry(module, output)
                waiting.remove(module)
                print(f"Verified public NuGet payload: {module['packageId']} {module['version']}", flush=True)
        if not waiting:
            return
        missing = ', '.join(module['packageId'] + '@' + module['version'] for module in waiting)
        remaining = deadline - time.monotonic()
        if remaining <= 0:
            raise RuntimeError('NuGet publication not visible after validation wait: ' + missing)
        print('Waiting for NuGet validation/indexing: ' + missing, flush=True)
        time.sleep(min(PUBLICATION_POLL_SECONDS, remaining))


def find_release(release_tag):
    # REST's releases/tags endpoint returns only published releases. The CLI
    # resolves drafts too; fetch their full metadata through the numeric ID.
    response = subprocess.run(['gh', 'release', 'view', release_tag, '--repo', REPO, '--json', 'apiUrl'],
                              capture_output=True, text=True)
    if response.returncode:
        if response.stderr.strip().lower() == 'release not found':
            return None
        raise RuntimeError('Cannot inspect component release: ' + response.stderr)
    url = json.loads(response.stdout).get('apiUrl', '')
    prefix = f'https://api.github.com/repos/{REPO}/releases/'
    if not url.startswith(prefix) or not url.removeprefix(prefix).isdigit():
        raise ValueError('Invalid component release API coordinate')
    return json.loads(run(['gh', 'api', url.removeprefix('https://api.github.com/')]))


def publish_github(module, output):
    """Forward-only: prove existing registry bytes and never replace a published asset."""
    verify_registry(module, output)
    archive = output / f"{module['packageId']}.{module['version']}.nupkg"
    verify_package(archive, module)
    release_tag = tag(module)
    release = find_release(release_tag)
    if release:
        if release['prerelease']:
            raise ValueError('Existing component release is marked prerelease: ' + release_tag)
        assets = [asset for asset in release['assets'] if asset['name'] == archive.name]
        if len(assets) != 1 and not release['draft']:
            raise ValueError('Existing component release lacks its immutable package: ' + release_tag)
        if not release['draft'] and not any(asset['name'] == 'SHA256SUMS' for asset in release['assets']):
            raise ValueError('Published component release lacks its immutable checksums: ' + release_tag)
        if assets:
            restored = output / 'github' / archive.name
            restored.parent.mkdir(parents=True, exist_ok=True)
            # gh API is needed for draft assets, whose public URL is not live yet.
            content = subprocess.run(['gh', 'api', f"repos/{REPO}/releases/assets/{assets[0]['id']}",
                                      '-H', 'Accept: application/octet-stream'], check=True, capture_output=True).stdout
            restored.write_bytes(content)
            verify_package(restored, module)
            archive = restored
    sums = output / module['name'] / 'SHA256SUMS'
    sums.parent.mkdir(parents=True, exist_ok=True)
    asset_name = f"{module['packageId']}.{module['version']}.nupkg"
    sums.write_text(hashlib.sha256(archive.read_bytes()).hexdigest() + '  ' + asset_name + '\n')
    notes = output / (module['name'] + '-notes.md')
    notes.write_text(f"{module['packageId']} {module['version']}\n\n"
                     'Source-only NuGet package; independently versioned from core and other extensions.\n'
                     f"\n[README](https://github.com/{REPO}/blob/main/extensions/{module['name']}/README.md)\n")
    if not release:
        run(['gh', 'release', 'create', release_tag, '--draft', '--repo', REPO,
             '--target', run(['git', 'rev-parse', 'HEAD']).strip(), '--latest=false',
             '--title', f"{module['name']} {module['version']}", '--notes-file', notes])
        existing_names = set()
    else:
        existing_names = {asset['name'] for asset in release['assets']}
    if asset_name not in existing_names:
        run(['gh', 'release', 'upload', release_tag, str(archive), '--repo', REPO])
    if 'SHA256SUMS' not in existing_names:
        run(['gh', 'release', 'upload', release_tag, str(sums), '--repo', REPO])
    else:
        checksum_asset = next(asset for asset in release['assets'] if asset['name'] == 'SHA256SUMS')
        content = subprocess.run(['gh', 'api', f"repos/{REPO}/releases/assets/{checksum_asset['id']}",
                                  '-H', 'Accept: application/octet-stream'], check=True, capture_output=True).stdout
        if content != sums.read_bytes():
            raise ValueError('Existing component checksum differs; refusing to overwrite published bytes')
    # Prove uploaded bytes before making a draft public, including a resumed run.
    uploaded = find_release(release_tag)
    if uploaded is None:
        raise ValueError('Component draft is not visible after asset upload')
    for filename, expected in ((asset_name, archive.read_bytes()), ('SHA256SUMS', sums.read_bytes())):
        matches = [asset for asset in uploaded['assets'] if asset['name'] == filename]
        if len(matches) != 1:
            raise ValueError('Missing or ambiguous release asset: ' + filename)
        actual = subprocess.run(['gh', 'api', f"repos/{REPO}/releases/assets/{matches[0]['id']}",
                                 '-H', 'Accept: application/octet-stream'], check=True, capture_output=True).stdout
        if actual != expected:
            raise ValueError('Uploaded component asset differs: ' + filename)
    if uploaded['draft']:
        run(['gh', 'release', 'edit', release_tag, '--draft=false', '--latest=false', '--repo', REPO])


def bump(module, version, what_if=False):
    if not SEMVER.fullmatch(version):
        raise ValueError('Expected a stable x.y.z component version')
    if tuple(map(int, version.split('.'))) <= tuple(map(int, module['version'].split('.'))):
        raise ValueError('Component versions must increase')
    print(f"{module['name']}: {module['version']} -> {version}" + (' (what-if)' if what_if else ''))
    if what_if:
        return
    project = ROOT / module['project']
    text, count = re.subn(r'<Version>' + re.escape(module['version']) + r'</Version>',
                          '<Version>' + version + '</Version>', project.read_text())
    if count != 1:
        raise ValueError('Expected one authoritative package version: ' + str(project))
    project.write_text(text)
    metadata = ROOT / 'extensions' / module['name'] / 'extension.json'
    updated = {**module, 'version': version}
    metadata.write_text(json.dumps(updated, indent=2) + '\n')
    for path in (metadata.parent / 'README.md', project.parent / 'README.md'):
        path.write_text(path.read_text().replace(module['version'], version))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['plan', 'version', 'pack', 'stage', 'push', 'verify-registry',
                                            'publish-github', 'bump'])
    parser.add_argument('--only', required=True, help='all or comma-separated module names/package IDs')
    parser.add_argument('--output', type=Path, default=ROOT / '.artifacts/component-releases')
    parser.add_argument('--version', help='bump: new stable version for the selected module')
    parser.add_argument('--what-if', action='store_true', help='bump: print the change without writing it')
    args = parser.parse_args()
    if args.command in ('push', 'publish-github') and (os.environ.get('GITHUB_ACTIONS') != 'true'
                                            or os.environ.get('GITHUB_REF') != 'refs/heads/main'):
        raise ValueError('Publish component releases only through the main-branch GitHub workflow')
    selected = selection(args.only)
    if args.command == 'plan':
        print(json.dumps({'include': [{'name': m['name'], 'packageId': m['packageId'],
                                      'version': m['version'], 'tag': tag(m), 'published': published(m)}
                                     for m in selected]}))
    elif args.command == 'version':
        if len(selected) != 1:
            parser.error('version requires one module')
        print(selected[0]['version'])
    elif args.command == 'pack':
        print('\n'.join(str(path) for path in pack(selected, args.output.resolve())))
    elif args.command == 'stage':
        stage(selected, args.output.resolve())
    elif args.command == 'push':
        push(selected, args.output.resolve())
    elif args.command == 'bump':
        if len(selected) != 1 or not args.version:
            parser.error('bump requires one module and --version')
        bump(selected[0], args.version, args.what_if)
    else:
        args.output.mkdir(parents=True, exist_ok=True)
        for module in selected:
            if args.command == 'verify-registry':
                verify_registry(module, args.output)
            else:
                publish_github(module, args.output)
    return 0


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except (ValueError, RuntimeError, OSError) as error:
        print('Component release failed:', error, file=sys.stderr)
        raise SystemExit(1)
