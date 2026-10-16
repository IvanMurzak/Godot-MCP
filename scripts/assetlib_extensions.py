"""Independent source ZIPs and idempotent Godot Asset Library submissions.

Uses the documented Asset Library API (including Custom GitHub Release URLs).
Initial submissions and updates both require moderator approval; an accepted
submission is deliberately reported as submitted, not as a public asset.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import urllib.error
import urllib.parse
import urllib.request
import zipfile

from extension_release import REPO, selection, tag, verify_registry
from extensions import ROOT, SEMVER, read_json, run, verify_package

API = 'https://godotengine.org/asset-library/api'
RECEIPT = 'assetlib-submission.json'


def archive_name(module):
    return 'godot-mcp-' + tag(module) + '-assetlib.zip'


def archive_url(module):
    return f'https://github.com/{REPO}/releases/download/{tag(module)}/{archive_name(module)}'


def archive_files(module, package, *, root=ROOT, package_version=None):
    verify_package(package, module, root, version=package_version)
    prefix = module['name'] + '-' + module['version'] + '/addons/godot_mcp_extensions/'
    prefix += module['name'].removeprefix('Godot-AI-') + '/'
    with zipfile.ZipFile(package) as bundle:
        files = {prefix + name.removeprefix('src/'): bundle.read(name)
                 for name in bundle.namelist() if name.startswith('src/') and name.endswith('.cs')}
        files[prefix + 'README.md'] = bundle.read('README.md')
    files[prefix + 'LICENSE'] = (root / 'LICENSE').read_text(encoding='utf-8-sig').encode('utf-8')
    files[prefix + 'extension.json'] = json.dumps({key: module[key] for key in
        ('name', 'packageId', 'version', 'minGodotVersion')}, sort_keys=True, indent=2).encode()
    files[prefix + 'INSTALL.md'] = (
        f"# {module['name']} {module['version']}\n\n"
        f"Requires Godot .NET {module['minGodotVersion']} or later and an installed Godot MCP core.\n"
        "Set up the core's NuGet dependencies first, as described in its README.\n"
        "Install this archive's addons/ folder into the existing Godot project, then build the C# project.\n"
        "The core discovers the extension's tools from the compiled assembly.\n"
        "Consult README.md for any third-party addon prerequisites.\n\n"
        "Choose either this source archive or the extension's NuGet package for a project.\n"
        "If switching from NuGet, remove that extension's PackageReference before building to avoid duplicate C# types.\n"
        "The Godot MCP core and its NuGet dependencies must remain installed.\n\n"
        f"Source and documentation: https://github.com/{REPO}/blob/main/extensions/{module['name']}/README.md\n"
    ).encode()
    return files


def pack_archive(module, package, output, *, root=ROOT, package_version=None):
    output.mkdir(parents=True, exist_ok=True)
    archive = output / archive_name(module)
    files = archive_files(module, package, root=root, package_version=package_version)
    with zipfile.ZipFile(archive, 'w') as bundle:
        for name, data in sorted(files.items()):
            entry = zipfile.ZipInfo(name, (1980, 1, 1, 0, 0, 0))
            entry.compress_type = zipfile.ZIP_DEFLATED
            entry.external_attr = 0o100644 << 16
            bundle.writestr(entry, data)
    verify_archive(module, package, archive, root=root, package_version=package_version)
    return archive


def verify_archive(module, package, archive, *, root=ROOT, package_version=None):
    expected = archive_files(module, package, root=root, package_version=package_version)
    with zipfile.ZipFile(archive) as bundle:
        names = bundle.namelist()
        if len(names) != len(set(names)) or set(names) != set(expected):
            raise ValueError('Asset Library archive contains missing, duplicate or unexpected files')
        for name, data in expected.items():
            if bundle.read(name) != data:
                raise ValueError('Asset Library archive bytes differ: ' + name)
    return True


def extract_archive(archive, project):
    # Archives are verified before extraction. Keep the path guard here as a
    # separate boundary: the leading release folder is stripped by AssetLib.
    with zipfile.ZipFile(archive) as bundle:
        for name in bundle.namelist():
            parts = name.split('/')
            if any(part in ('', '.', '..') for part in parts) or len(parts) < 5:
                raise ValueError('Invalid Asset Library archive path')
            path = project.joinpath(*parts[1:]).resolve()
            if not path.is_relative_to(project.resolve()):
                raise ValueError('Asset Library archive escapes project')
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_bytes(bundle.read(name))


def payload(module):
    feature = module['name'].removeprefix('Godot-AI-')
    return {
        'title': 'Godot MCP - ' + feature,
        'description': f'{feature} tools for Godot MCP. Requires the .NET edition of Godot and '
            'an installed Godot MCP core with its NuGet dependencies configured. '
            'Install the addons/ folder and build the C# project. This download contains only '
            'this extension; consult its README for third-party addon prerequisites. '
            'Use either this source archive or the equivalent NuGet package in a project to '
            'avoid duplicate C# types.\n\nDocumentation: '
            f'https://github.com/{REPO}/blob/main/extensions/{module["name"]}/README.md',
        'category_id': '5', 'godot_version': '.'.join(module['minGodotVersion'].split('.')[:2]),
        'version_string': module['version'], 'cost': 'Apache-2.0',
        'download_provider': 'Custom', 'download_commit': archive_url(module),
        'browse_url': f'https://github.com/{REPO}/tree/main/extensions/{module["name"]}',
        'issues_url': f'https://github.com/{REPO}/issues',
        'icon_url': f'https://raw.githubusercontent.com/{REPO}/main/addons/godot_mcp/icon.png',
        'previews': [],
    }


class Api:
    def request(self, path, body=None):
        request = urllib.request.Request(API + path,
            data=json.dumps(body).encode() if body is not None else None,
            headers={'Content-Type': 'application/json', 'Accept': 'application/json',
                     'User-Agent': 'Godot-MCP-AssetLibrary-Publisher'})
        try:
            with urllib.request.urlopen(request, timeout=60) as response:
                result = json.load(response)
        except urllib.error.HTTPError as error:
            # Login errors and transport failures must never echo credentials,
            # tokens, response bodies or POST data into logs.
            raise RuntimeError(f'Asset Library HTTP {error.code} on {path.split("?")[0]}') from None
        except (OSError, ValueError):
            raise RuntimeError('Asset Library request failed on ' + path.split('?')[0]) from None
        if not isinstance(result, dict) or result.get('error'):
            raise RuntimeError('Asset Library rejected request on ' + path.split('?')[0])
        return result

    def listing(self, path, **query):
        rows = []
        page = 0
        while True:
            result = self.request(path + '?' + urllib.parse.urlencode({**query, 'page': page, 'max_results': 500}))
            if not isinstance(result.get('result'), list):
                raise ValueError('Malformed Asset Library listing')
            rows.extend(result['result'])
            page += 1
            if page >= int(result.get('pages', 0)):
                return rows
            if page > 100:
                raise ValueError('Asset Library listing exceeds safety bound')


def submit(module, api, username, token):
    desired = payload(module)
    owned = api.listing('/asset', user=username, type='addon', godot_version='any', filter=desired['title'])
    pending = api.listing('/asset/edit', user=username, filter=desired['title'])
    def matches(rows):
        found = [row for row in rows if row.get('title') == desired['title']
                 and row.get('author', '').casefold() == username.casefold()]
        if len(found) > 1:
            raise ValueError('Ambiguous Asset Library entries: ' + module['name'])
        return found[0] if found else None
    asset, edit = matches(owned), matches(pending)
    for row in (asset, edit):
        if row:
            current = row.get('version_string')
            if current and SEMVER.fullmatch(current):
                if tuple(map(int, current.split('.'))) > tuple(map(int, module['version'].split('.'))):
                    raise ValueError('Newer Asset Library version already exists: ' + module['name'])
    if asset and asset.get('version_string') == module['version']:
        actual = api.request('/asset/' + str(int(asset['asset_id'])))
        if actual.get('download_commit') != archive_url(module):
            raise ValueError('Existing Asset Library version points to a different archive')
        return {'status': 'published', 'assetId': int(asset['asset_id'])}
    if edit:
        detail = api.request('/asset/edit/' + str(int(edit['edit_id'])))
        effective = {**(detail.get('original') or {}), **{key: value for key, value in detail.items() if value is not None}}
        current = effective.get('version_string') or ''
        if SEMVER.fullmatch(current) and tuple(map(int, current.split('.'))) > tuple(map(int, module['version'].split('.'))):
            raise ValueError('Newer Asset Library edit already exists: ' + module['name'])
        if detail.get('status') not in ('new', 'in_review'):
            raise ValueError('Asset Library edit changed status; reconcile before retry: ' + module['name'])
        if (effective.get('version_string') == module['version']
                and effective.get('download_commit') == archive_url(module)):
            return {'status': 'submitted', 'editId': int(edit['edit_id'])}
        # Never duplicate an initial submission or overwrite an in-review edit.
        if detail.get('status') != 'new':
            raise ValueError('Asset Library edit is in review; await moderation: ' + module['name'])
        path = '/asset/edit/' + str(int(edit['edit_id']))
    else:
        path = '/asset/' + str(int(asset['asset_id'])) if asset else '/asset'
    result = api.request(path, {**desired, 'token': token})
    edit_id = int(result['id'])
    if edit_id <= 0:
        raise ValueError('Invalid Asset Library submission ID')
    return {'status': 'submitted', 'editId': edit_id}


def gh(*args):
    return json.loads(run(['gh', *args]))


def attach(release, path):
    found = [asset for asset in release['assets'] if asset['name'] == path.name]
    digest = 'sha256:' + hashlib.sha256(path.read_bytes()).hexdigest()
    if found:
        if len(found) != 1 or found[0].get('digest') != digest:
            raise ValueError('Existing immutable Asset Library attachment differs: ' + path.name)
        return
    run(['gh', 'release', 'upload', release['tag_name'], path, '--repo', REPO])


def sync(selected, output):
    if os.environ.get('GITHUB_ACTIONS') != 'true' or os.environ.get('GITHUB_REF') != 'refs/heads/main':
        raise ValueError('Submit Asset Library entries only through the main-branch GitHub workflow')
    api, token = Api(), None
    username = os.environ.get('GODOT_ASSETLIB_USERNAME', '')
    results = []
    for module in selected:
        folder = output / module['name']
        folder.mkdir(parents=True, exist_ok=True)
        release = gh('api', f'repos/{REPO}/releases/tags/{tag(module)}')
        if release['draft'] or release['prerelease']:
            raise ValueError('Publish the stable component before its Asset Library submission')
        package = verify_registry(module, folder)
        archive = folder / archive_name(module)
        if any(asset['name'] == archive.name for asset in release['assets']):
            run(['gh', 'release', 'download', tag(module), '--repo', REPO, '--dir', folder,
                 '--pattern', archive.name, '--clobber'])
            verify_archive(module, package, archive)
        else:
            archive = pack_archive(module, package, folder)
        checksum = folder / (archive.name + '.sha256')
        checksum.write_text(hashlib.sha256(archive.read_bytes()).hexdigest() + '  ' + archive.name + '\n')
        attach(release, archive)
        attach(release, checksum)
        receipt = folder / RECEIPT
        if any(asset['name'] == RECEIPT for asset in release['assets']):
            run(['gh', 'release', 'download', tag(module), '--repo', REPO, '--dir', folder,
                 '--pattern', RECEIPT, '--clobber'])
            saved = read_json(receipt)
            if any(saved.get(k) != v for k, v in {'schema': 1, 'packageId': module['packageId'],
                    'version': module['version'], 'archiveUrl': archive_url(module),
                    'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest()}.items()):
                raise ValueError('Existing Asset Library receipt targets different package coordinates')
            if saved.get('status') not in ('submitted', 'published') or not any(
                    type(saved.get(key)) is int and saved[key] > 0 for key in ('editId', 'assetId')):
                raise ValueError('Invalid Asset Library receipt')
        else:
            if token is None:
                password = os.environ.get('GODOT_ASSETLIB_PASSWORD', '')
                if not username or not password:
                    raise ValueError('Missing Asset Library repository credentials')
                login = api.request('/login', {'username': username, 'password': password})
                if login.get('authenticated') is not True or not login.get('token'):
                    raise ValueError('Asset Library login failed')
                token = login['token']
                print('::add-mask::' + token, flush=True)
            saved = {'schema': 1, 'packageId': module['packageId'], 'version': module['version'],
                     'archiveUrl': archive_url(module),
                     'archiveSha256': hashlib.sha256(archive.read_bytes()).hexdigest(),
                     **submit(module, api, username, token)}
            receipt.write_text(json.dumps(saved, sort_keys=True, indent=2) + '\n')
            attach(release, receipt)
        results.append(saved)
        print(f"Asset Library {module['name']} {module['version']}: {saved['status']}", flush=True)
    (output / 'submissions.json').write_text(json.dumps(results, indent=2))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('command', choices=['plan', 'sync'])
    parser.add_argument('--only', required=True)
    parser.add_argument('--output', type=Path, default=ROOT / '.artifacts/assetlib-extensions')
    args = parser.parse_args()
    selected = selection(args.only)
    if args.command == 'plan':
        print(json.dumps([payload(module) for module in selected], indent=2))
    else:
        sync(selected, args.output.resolve())


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, KeyError, subprocess.TimeoutExpired) as error:
        print('Asset Library submission failed: ' + str(error), flush=True)
        raise SystemExit(1)
