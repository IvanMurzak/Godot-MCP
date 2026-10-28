"""Run all compatible module manifests against ONE owned Godot editor process."""
from __future__ import annotations

import contextlib
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import signal
import socket
import subprocess
import sys
import time
import urllib.request
import zipfile

import extension_e2e
from extensions import ROOT, modules, run


def download(url, path, sha256=None):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.download')
    try:
        with urllib.request.urlopen(url, timeout=60) as response, temporary.open('wb') as output:
            shutil.copyfileobj(response, output)
        digest = hashlib.sha256(temporary.read_bytes()).hexdigest()
        if sha256 and digest != sha256:
            raise ValueError('Downloaded bytes do not match SHA-256: ' + url)
        temporary.replace(path)
        return digest
    finally:
        temporary.unlink(missing_ok=True)


def extract(archive, destination):
    destination.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(archive) as bundle:
        for item in bundle.infolist():
            if not (destination / item.filename).resolve().is_relative_to(destination.resolve()):
                raise ValueError('Archive path escapes destination: ' + item.filename)
        bundle.extractall(destination)


def released_server(output):
    source = (ROOT / 'addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs').read_text()
    version = re.search(r'ServerVersion\s*=\s*"([0-9.]+)"', source).group(1)
    system = 'win' if os.name == 'nt' else 'osx' if sys.platform == 'darwin' else 'linux'
    arch = 'arm64' if platform.machine().lower() in ('arm64', 'aarch64') else 'x64'
    name = f'gamedev-mcp-server-{system}-{arch}.zip'
    base = f'https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v{version}/'
    sums = output / 'SHA256SUMS'
    download(base + 'SHA256SUMS', sums)
    entries = [line.split()[0] for line in sums.read_text().splitlines()
               if len(line.split()) == 2 and line.split()[1].lstrip('*') == name]
    if len(entries) != 1:
        raise ValueError('Missing or ambiguous server checksum: ' + name)
    archive = output / name
    download(base + name, archive, entries[0])
    extract(archive, output / 'extracted')
    executable = 'gamedev-mcp-server.exe' if os.name == 'nt' else 'gamedev-mcp-server'
    matches = list((output / 'extracted').rglob(executable))
    if len(matches) != 1:
        raise ValueError('Expected one released server executable')
    matches[0].chmod(0o755)
    return str(matches[0])


def install_test_addons(selected, project, output, sdk):
    receipts = []
    for module in selected:
        addon = module.get('testAddon')
        if not addon:
            continue
        addon = dict(addon)
        for minimum, tag in sorted(addon.get('tagFromSdk', {}).items(),
                                   key=lambda item: tuple(map(int, item[0].split('.')))):
            if tuple(map(int, sdk.split('.'))) >= tuple(map(int, minimum.split('.'))):
                addon['tag'] = tag
        name = module['name']
        if addon['kind'] == 'gdextension':
            # The source archive cannot contain the platform's native library.
            release = json.loads(run(['gh', 'api', f"repos/{addon['repo']}/releases/tags/{addon['tag']}"]))
            assets = [a for a in release['assets'] if a['name'].endswith('.zip')]
            if len(assets) != 1:
                raise ValueError('Expected one pinned native-addon ZIP: ' + name)
            url = assets[0]['browser_download_url']
            expected = assets[0].get('digest', '').removeprefix('sha256:') or None
        else:
            url = f"https://codeload.github.com/{addon['repo']}/zip/refs/tags/{addon['tag']}"
            expected = None
        archive = output / 'third-party' / (name + '.zip')
        digest = download(url, archive, expected)
        expanded = output / 'third-party' / name
        extract(archive, expanded)
        matches = [p for p in expanded.rglob(addon['dir']) if p.is_dir() and p.parent.name == 'addons']
        if len(matches) != 1:
            raise ValueError('Expected one addon directory in pinned archive: ' + name)
        target = project / 'addons' / addon['dir']
        shutil.copytree(matches[0], target)
        if addon['kind'] == 'gdextension':
            suffix = '.dll' if os.name == 'nt' else '.dylib' if sys.platform == 'darwin' else '.so'
            if not list(target.rglob('*' + suffix)):
                raise ValueError('Native addon lacks a library for this platform: ' + name)
        receipts.append({'module': name, 'url': url, 'sha256': digest})
    (output / 'third-party.json').write_text(json.dumps(receipts, indent=2) + '\n')


def stop(process):
    if process.poll() is not None:
        return
    if os.name == 'nt':
        result = subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True)
        if result.returncode and process.poll() is None:
            raise RuntimeError(f'Could not stop owned process tree PID {process.pid}: {result.stderr!r}')
    else:
        os.killpg(process.pid, signal.SIGTERM)
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            os.killpg(process.pid, signal.SIGKILL)
    process.wait(timeout=30)
    print('Stopped owned process tree PID', process.pid, flush=True)


def check_editor_log(text, sdk=None, *, startup_lines=0, failure_windows=()):
    """Fail on script / assembly / engine errors. `failure_windows` are the [start, end) line ranges of the
    manifest's expectFailure calls: each must hold exactly one McpPlugin error response (the rejected value);
    an error response anywhere else is fatal."""
    if re.search(r'SCRIPT ERROR:|FileNotFoundException|Failed to load script|Unable to load addon|'
                 r'Cannot resolve relative SkillsPath|Unhandled exception', text, re.I):
        raise ValueError('Script/assembly failure in combined editor log; see editor.log')
    lines = text.splitlines()
    diagnostics = {'godot44ColdImportProgress': 0}
    window_hits = [0] * len(failure_windows)
    progress_messages = {
        'ERROR: Do not use progress dialog (task) while flushing the message queue or using call_deferred()!',
        'ERROR: Condition "!tasks.has(p_task)" is true. Returning: canceled',
        'ERROR: Condition "!tasks.has(p_task)" is true.'}
    for index, line in enumerate(lines):
        if not line.startswith('ERROR:'):
            continue
        context = '\n'.join(lines[index:index + 20])
        # A manifest call marked expectFailure (a value the module must reject) is logged by McpPlugin as an
        # error response carrying the RQL error text; it is tolerated only inside that call's own log window.
        if (line == 'ERROR: [McpPlugin] McpToolManager: Error Response to AI:'
                and index + 1 < len(lines) and lines[index + 1].startswith('error ')):
            window = next((i for i, (start, end) in enumerate(failure_windows) if start <= index < end), None)
            if window is None or window_hits[window]:
                raise ValueError('Unexpected tool error response in combined editor log:\n' + context)
            window_hits[window] += 1
            continue
        # Upstream godotengine/godot#103398: native cold-import progress UI
        # diagnostics in 4.4. Match only the exact native frames during startup;
        # the same messages during tool calls or script failures remain fatal.
        if (sdk == '4.4.0' and index < startup_lines
                and line in progress_messages and index + 1 < len(lines)
                and re.fullmatch(r'\s+at: (add_task|task_step|end_task) \(editor/progress_dialog.cpp:\d+\)', lines[index + 1])):
            diagnostics['godot44ColdImportProgress'] += 1
            continue
        raise ValueError('Unexpected engine error in combined editor log:\n' + context)
    if not all(window_hits):
        raise ValueError(f'{window_hits.count(0)} expectFailure call(s) left no error response in the editor log')
    return diagnostics


def read_log(path):
    return path.read_text(encoding='utf-8', errors='replace')


def log_lines(path):
    return len(read_log(path).splitlines())


def live(plan, godot, server, output):
    project = Path(plan['project']).parent
    selected = [m for m in modules() if m['name'] in plan['modules']]
    install_test_addons(selected, project, output, plan['sdk'])
    cli = ['node', str(ROOT / 'cli/bin/godot-cli.js')]
    if not (ROOT / 'cli/dist/index.js').is_file():
        raise ValueError('Build the candidate CLI first: cd cli && npm ci && npm run build')
    for executable in (godot, server):
        if not Path(executable).is_file():
            raise ValueError('Missing test executable: ' + executable)
    with socket.socket() as probe:
        probe.bind(('127.0.0.1', 0))
        port = probe.getsockname()[1]
    url = f'http://127.0.0.1:{port}'
    home = output / 'home'
    folders = {'HOME': home, 'USERPROFILE': home, 'APPDATA': home / 'AppData/Roaming',
               'LOCALAPPDATA': home / 'AppData/Local', 'XDG_CONFIG_HOME': home / '.config',
               'XDG_DATA_HOME': home / '.local/share', 'XDG_CACHE_HOME': home / '.cache'}
    for folder in folders.values():
        folder.mkdir(parents=True, exist_ok=True)
    env = {**os.environ, **{key: str(value) for key, value in folders.items()},
           'NUGET_PACKAGES': plan['nugetCache'], 'GODOT_MCP_CONNECTION_MODE': 'Custom',
           'GODOT_MCP_HOST': url, 'GODOT_MCP_AUTH_OPTION': 'None', 'ASPNETCORE_URLS': url}
    env.pop('GODOT_MCP_TOKEN', None)
    # dotnet/runtime#135441 (.NET 10 until dotnet/runtime#136481 ships): idle thread-pool threads stay alive in the editors.
    if not env.get('DOTNET_ThreadPool_ThreadsToKeepAlive', '').strip():  # an empty value counts as unset, as in godot-cli
        env['DOTNET_ThreadPool_ThreadsToKeepAlive'] = '-1'
    processes, handles, identities = [], [], []
    def launch(name, args):
        handle = (output / (name + '.log')).open('w', encoding='utf-8')
        handles.append(handle)
        process = subprocess.Popen([str(arg) for arg in args], cwd=project, env=env,
                                   stdout=handle, stderr=subprocess.STDOUT,
                                   start_new_session=os.name != 'nt')
        processes.append(process)
        identities.append({'name': name, 'pid': process.pid})
        (output / 'owned-processes.json').write_text(json.dumps(identities, indent=2))
        print(f'Started owned {name} PID {process.pid}', flush=True)
        return process
    try:
        service = launch('server', [server, f'port={port}', 'plugin-timeout=10000',
                                    'client-transport=streamableHttp', 'authorization=none'])
        deadline = time.monotonic() + 60
        while True:
            if service.poll() is not None:
                raise RuntimeError('Test server exited before readiness')
            try:
                with urllib.request.urlopen(url + '/help', timeout=2) as response:
                    if response.status == 200:
                        break
            except OSError:
                pass
            if time.monotonic() > deadline:
                raise RuntimeError('Test server readiness timed out')
            time.sleep(.25)
        # C# is prebuilt before the FIRST editor boot. No --build-solutions and
        # no separate per-module import/smoke editor process are needed here.
        editor = launch('editor', [godot, '--headless', '--path', project, '--editor'])
        run([*cli, 'wait-for-ready', '--url', url, '--timeout', '120000', '--interval', '1000'],
            cwd=project, env=env, log=output / 'ready.log', timeout=150)
        text = (output / 'editor.log').read_text(encoding='utf-8', errors='replace')
        version = re.search(r'Godot Engine v(\d+\.\d+(?:\.\d+)?)', text)
        actual = version.group(1).split('.') if version else []
        if len(actual) == 2:
            actual.append('0')
        if '.'.join(actual) != plan['sdk'] or '[Godot-MCP] plugin loaded' not in text:
            raise ValueError('Editor identity or addon-load proof missing: ' + text[-6000:])
        startup_lines = len(text.splitlines())
        check_editor_log(text, plan['sdk'], startup_lines=startup_lines)
        log_path = output / 'editor.log'
        windows = []

        @contextlib.contextmanager
        def failure_window(call):
            # Record the editor-log lines an expectFailure call's error response may occupy.
            if not call.get('expectFailure'):
                yield
                return
            start = log_lines(log_path)
            yield
            deadline = time.monotonic() + 10
            while time.monotonic() < deadline:
                if 'Error Response to AI:' in '\n'.join(read_log(log_path).splitlines()[start:]):
                    break
                time.sleep(.2)
            windows.append((start, log_lines(log_path)))

        total = 0
        for module in selected:
            manifest = json.loads((ROOT / 'extensions' / module['name'] / 'tests/e2e/e2e-tools.json').read_text())
            for phase in ('setup', 'tools'):
                calls = manifest.get(phase, [])
                if not extension_e2e.run_phase(module['name'] + '/' + phase, calls, cli, url, 60, env=env,
                                               around=failure_window):
                    raise RuntimeError('Live tool assertions failed: ' + module['name'])
                total += len(calls)
            if editor.poll() is not None:
                raise RuntimeError('Combined editor exited during module assertions')
        diagnostics = check_editor_log(read_log(log_path), plan['sdk'], startup_lines=startup_lines,
                                       failure_windows=windows)
        (output / 'live-result.json').write_text(json.dumps({
            'sdk': plan['sdk'], 'editorLaunches': 1, 'editorPid': editor.pid,
            'modules': plan['modules'], 'assertions': total, 'expectedFailures': len(windows), 'success': True,
            'nativeDiagnostics': diagnostics}, indent=2) + '\n')
        if any(diagnostics.values()):
            print(f'NOTE: explicitly reported native headless diagnostics: {diagnostics}', flush=True)
        print(f'PASS: {len(selected)} modules, {total} live calls, ONE editor PID {editor.pid}', flush=True)
    finally:
        failures = []
        for process in reversed(processes):
            try:
                stop(process)
            except (OSError, RuntimeError, subprocess.TimeoutExpired) as error:
                failures.append(str(error))
        for handle in handles:
            handle.close()
        if failures:
            raise RuntimeError('; '.join(failures))
