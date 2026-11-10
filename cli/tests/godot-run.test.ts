import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { spawn, type SpawnOptions } from 'child_process';
import { runGodot } from '../src/lib/godot-run.js';

// `node <script>` stands in for the Godot binary.
let dir: string;
beforeEach(() => {
  dir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-run-'));
});
afterEach(() => fs.rmSync(dir, { recursive: true, force: true }));

function script(name: string, source: string): string {
  const file = path.join(dir, name);
  fs.writeFileSync(file, source);
  return file;
}

describe('runGodot', () => {
  it('captures stdout + stderr without ANSI codes, streams lines and returns the exit code', async () => {
    const file = script('ok.js', "console.log('\\x1b[91mGodot Engine v4.5.1\\x1b[0m'); console.error('ERROR: x'); process.exit(3);");
    const lines: string[] = [];
    const r = await runGodot({ binary: process.execPath, args: [file], cwd: dir, timeoutMs: 20_000, onLine: (l) => lines.push(l) });
    expect(r.exitCode).toBe(3);
    expect(r.timedOut).toBe(false);
    expect(r.output).toContain('Godot Engine v4.5.1');
    expect(r.output).toContain('ERROR: x');
    expect(r.output).not.toContain('\x1b');
    expect(lines).toContain('Godot Engine v4.5.1');
  });

  it('kills a hung process tree at the timeout', async () => {
    const file = script('hang.js', 'setInterval(() => {}, 1000);');
    const started = Date.now();
    const r = await runGodot({ binary: process.execPath, args: [file], cwd: dir, timeoutMs: 500 });
    expect(r.timedOut).toBe(true);
    expect(r.exitCode).toBeNull();
    expect(Date.now() - started).toBeLessThan(10_000);
  });

  it('puts --log-file before the args (anything after -- belongs to the script) and falls back to the file', async () => {
    // Prints nothing; writes the "log" to the path after --log-file.
    const file = script('log.js', "const i = process.argv.indexOf('--log-file'); require('fs').writeFileSync(process.argv[i + 1], 'Godot Engine v4.5.1\\nfrom file\\n'); console.log('no banner here');");
    const logFile = path.join(dir, 'logs', 'x.log');
    let seen: string[] = [];
    // node would read a leading --log-file as its own option: put the script first.
    const spawnImpl = ((bin: string, args: string[], opts: SpawnOptions) => {
      seen = args;
      return spawn(bin, [file, ...args], opts);
    }) as unknown as typeof spawn;
    const r = await runGodot({ binary: process.execPath, args: ['-s', 'x.gd', '--', 'user'], cwd: dir, timeoutMs: 20_000, logFile, spawnImpl });
    expect(seen).toEqual(['--log-file', logFile, '-s', 'x.gd', '--', 'user']);
    expect(r.output).toContain('from file');
  });

  it('reports a binary that cannot start', async () => {
    const r = await runGodot({ binary: path.join(dir, 'no-such-godot.exe'), args: [], cwd: dir, timeoutMs: 5_000 });
    expect(r.exitCode).toBeNull();
    expect(r.spawnError).toBeTruthy();
  });
});
