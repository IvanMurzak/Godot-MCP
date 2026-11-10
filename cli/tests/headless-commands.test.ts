import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { checkProject, CHECK_SCRIPT, isAddonEnabled } from '../src/lib/check.js';
import { importProject } from '../src/lib/import-project.js';
import { prepareHeadless, type HeadlessDeps } from '../src/lib/headless.js';
import { detectTestRunners, runTests, runnerArgs } from '../src/lib/run-tests.js';
import { exportProject, readExportPresets, templatesRoot, templatesVersionDir } from '../src/lib/export-project.js';
import { runScript, scriptPathForGodot } from '../src/lib/run-script.js';
import type { GodotRunOptions, GodotRunResult } from '../src/lib/godot-run.js';
import type { BuildProjectResult } from '../src/lib/types.js';

const BANNER = 'Godot Engine v4.5.1.stable.mono.official.f62fdbde1 - https://godotengine.org\n';

type Responder = (options: GodotRunOptions) => Partial<GodotRunResult> | Promise<Partial<GodotRunResult>>;

/** Fake runGodot: records the calls, answers through `respond` (default: a clean run). */
function fakeDeps(respond: Responder = () => ({}), extra: Partial<HeadlessDeps> = {}) {
  const calls: GodotRunOptions[] = [];
  const deps: Partial<HeadlessDeps> = {
    findBinary: () => 'godot.exe',
    findOpenEditor: () => null,
    runGodot: async (options) => {
      calls.push(options);
      const r = await respond(options);
      return { exitCode: 0, timedOut: false, output: BANNER, durationMs: 1, ...r };
    },
    ...extra,
  };
  return { deps, calls };
}

const has = (o: GodotRunOptions, arg: string) => o.args.includes(arg);
const okBuild = async (): Promise<BuildProjectResult> => ({ kind: 'success', success: true, projectPath: '', skipped: false, output: 'Build succeeded.', warnings: [] });

let project: string;
beforeEach(() => {
  project = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-headless-'));
  fs.writeFileSync(path.join(project, 'project.godot'), 'config_version=5\n');
});
afterEach(() => fs.rmSync(project, { recursive: true, force: true }));

describe('prepareHeadless', () => {
  it('rejects a folder without project.godot as invalid options', () => {
    const r = prepareHeadless({ projectPath: os.tmpdir(), timeoutMs: 1000, deps: fakeDeps().deps });
    expect(r).toMatchObject({ kind: 'failure', reason: 'invalid-options' });
  });

  it('reports a missing binary as not-found', () => {
    const r = prepareHeadless({ projectPath: project, timeoutMs: 1000, deps: fakeDeps(undefined, { findBinary: () => null }).deps });
    expect(r).toMatchObject({ kind: 'failure', reason: 'not-found' });
  });

  it('refuses a project open in an editor unless allowed', () => {
    const { deps } = fakeDeps(undefined, { findOpenEditor: () => ({ pid: 42 }) });
    expect(prepareHeadless({ projectPath: project, timeoutMs: 1000, deps })).toMatchObject({ kind: 'failure', reason: 'project-open' });
    expect(prepareHeadless({ projectPath: project, timeoutMs: 1000, deps, allowOpenProject: true })).toMatchObject({ kind: 'context' });
  });
});

describe('importProject', () => {
  it('passes a clean import', async () => {
    const { deps, calls } = fakeDeps();
    const r = await importProject({ projectPath: project, timeoutMs: 10_000, deps });
    expect(r.kind).toBe('success');
    expect(calls[0].args).toEqual(['--headless', '--path', project, '--import']);
    expect(calls[0].logFile).toContain(path.join('.godot', 'godot-cli', 'logs'));
  });

  it('fails on "Error importing" although Godot exits 0', async () => {
    const { deps } = fakeDeps(() => ({ output: BANNER + "ERROR: Error importing 'res://bad.png'.\n   at: _reimport (editor/x.cpp:1)\n" }));
    expect(await importProject({ projectPath: project, timeoutMs: 10_000, deps })).toMatchObject({ kind: 'failure', reason: 'import-failed' });
  });

  it('fails on script errors, a missing banner and a timeout', async () => {
    const scriptErr = fakeDeps(() => ({ output: BANNER + 'SCRIPT ERROR: Parse Error: x\n   at: GDScript::reload (res://a.gd:1)\n' }));
    expect(await importProject({ projectPath: project, timeoutMs: 10_000, deps: scriptErr.deps })).toMatchObject({ reason: 'script-errors' });
    const silent = fakeDeps(() => ({ output: '' }));
    expect(await importProject({ projectPath: project, timeoutMs: 10_000, deps: silent.deps })).toMatchObject({ reason: 'import-failed' });
    const hung = fakeDeps(() => ({ timedOut: true, exitCode: null }));
    expect(await importProject({ projectPath: project, timeoutMs: 10_000, deps: hung.deps })).toMatchObject({ reason: 'timeout' });
  });
});

describe('checkProject', () => {
  const checkDone = (failed = 0, warned = 0) => `[godot-cli] check-done files=3 failed=${failed} warned=${warned}\n`;

  it('runs import, build, editor and the GDScript check, and passes a clean project', async () => {
    fs.writeFileSync(path.join(project, 'Game.csproj'), '<Project/>');
    const { deps, calls } = fakeDeps((o) => (has(o, '-s') ? { output: BANNER + checkDone() } : {}));
    const r = await checkProject({ projectPath: project, timeoutMs: 60_000, deps, build: okBuild });
    expect(r.kind).toBe('success');
    expect(calls.map((c) => c.args.slice(3))).toEqual([
      ['--import'],
      ['--editor', '--quit'],
      ['-s', 'res://.godot/godot-cli/check.gd'],
    ]);
    expect(r.steps?.map((s) => s.name)).toEqual(['import', 'dotnet-build', 'editor', 'gdscript']);
    expect(fs.readFileSync(path.join(project, '.godot', 'godot-cli', 'check.gd'), 'utf-8')).toBe(CHECK_SCRIPT);
  });

  it('reports GDScript errors with res://file:line', async () => {
    const { deps } = fakeDeps((o) => (has(o, '-s')
      ? { exitCode: 1, output: BANNER + 'SCRIPT ERROR: Parse Error: Expected expression.\n   at: GDScript::reload (res://b.gd:4)\n' +
          '       [0] _load_ok (res://.godot/godot-cli/check.gd:24)\n[godot-cli] script-failed res://b.gd\n' + checkDone(1) }
      : {}));
    const r = await checkProject({ projectPath: project, timeoutMs: 60_000, deps, build: okBuild });
    expect(r).toMatchObject({ kind: 'failure', reason: 'script-errors' });
    expect(r.diagnostics).toEqual([{ severity: 'error', source: 'gdscript', message: 'Expected expression.', file: 'res://b.gd', line: 4, phase: 'load' }]);
  });

  it('reports C# compiler errors and skips the editor boot', async () => {
    fs.writeFileSync(path.join(project, 'Game.csproj'), '<Project/>');
    const build = async (): Promise<BuildProjectResult> => ({
      kind: 'failure', success: false, warnings: [], error: new Error('x'),
      errorMessage: 'dotnet build failed (exit 1)\nC:\\p\\A.cs(4,33): error CS1002: ; expected [C:\\p\\Game.csproj]',
    });
    const { deps, calls } = fakeDeps((o) => (has(o, '-s') ? { output: BANNER + checkDone() } : {}));
    const r = await checkProject({ projectPath: project, timeoutMs: 60_000, deps, build });
    expect(r).toMatchObject({ kind: 'failure', reason: 'compilation-failed' });
    expect(r.diagnostics?.[0]).toMatchObject({ source: 'csharp', code: 'CS1002', line: 4, column: 33 });
    expect(calls.some((c) => has(c, '--editor'))).toBe(false);
  });

  it('requires the Godot-MCP load marker when the addon is enabled', async () => {
    fs.mkdirSync(path.join(project, 'addons', 'godot_mcp'), { recursive: true });
    fs.writeFileSync(path.join(project, 'addons', 'godot_mcp', 'plugin.cfg'), '[plugin]\n');
    fs.writeFileSync(path.join(project, 'project.godot'), '[editor_plugins]\n\nenabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")\n');
    expect(isAddonEnabled(project)).toBe(true);
    const silent = fakeDeps((o) => (has(o, '-s') ? { output: BANNER + checkDone() } : {}));
    const r = await checkProject({ projectPath: project, timeoutMs: 60_000, deps: silent.deps });
    expect(r).toMatchObject({ kind: 'failure', reason: 'script-errors', addon: { enabled: true, loaded: false } });

    const loaded = fakeDeps((o) => (has(o, '-s') ? { output: BANNER + checkDone() } : has(o, '--editor') ? { output: BANNER + '[Godot-MCP] plugin loaded\n' } : {}));
    expect(await checkProject({ projectPath: project, timeoutMs: 60_000, deps: loaded.deps })).toMatchObject({ kind: 'success', addon: { loaded: true } });
  });

  it('fails on warnings only with --strict, and passes --strict to the checker', async () => {
    const warnLog = BANNER + '[godot-cli] warnings-pass\nSCRIPT ERROR: Parse Error: unused var (Warning treated as error.)\n   at: GDScript::reload (res://w.gd:3)\n' + checkDone(0, 1);
    const lax = fakeDeps((o) => (has(o, '-s') ? { output: warnLog } : {}));
    expect((await checkProject({ projectPath: project, timeoutMs: 60_000, deps: lax.deps })).kind).toBe('success');
    const strict = fakeDeps((o) => (has(o, '-s') ? { output: warnLog } : {}));
    const r = await checkProject({ projectPath: project, timeoutMs: 60_000, deps: strict.deps, strict: true });
    expect(r).toMatchObject({ kind: 'failure', reason: 'script-errors' });
    expect(strict.calls.at(-1)!.args.slice(-2)).toEqual(['--', '--strict']);
  });

  it('has no verdict without the check-done marker, and times out', async () => {
    const noMarker = fakeDeps();
    expect(await checkProject({ projectPath: project, timeoutMs: 60_000, deps: noMarker.deps })).toMatchObject({ reason: 'script-errors' });
    const hung = fakeDeps((o) => (has(o, '--editor') ? { timedOut: true, exitCode: null } : {}));
    expect(await checkProject({ projectPath: project, timeoutMs: 60_000, deps: hung.deps })).toMatchObject({ reason: 'timeout' });
  });
});

describe('runTests', () => {
  const GUT_PASS = '<testsuites><testsuite name="test/t.gd"><testcase name="test_a" classname="test/t.gd"/></testsuite></testsuites>';
  const GUT_FAIL = '<testsuites><testsuite name="test/t.gd"><testcase name="test_a" classname="test/t.gd"><failure message="failed">at line 3</failure></testcase></testsuite></testsuites>';

  function installGut(): void {
    fs.mkdirSync(path.join(project, 'addons', 'gut'), { recursive: true });
    fs.writeFileSync(path.join(project, 'addons', 'gut', 'gut_cmdln.gd'), '');
    fs.mkdirSync(path.join(project, 'test'));
  }
  /** Writes `xml` where GUT was told to (`-gjunit_xml_file=`). */
  const gutWrites = (xml: string | null, extra: Partial<GodotRunResult> = {}): Responder => (o) => {
    const arg = o.args.find((a) => a.startsWith('-gjunit_xml_file='));
    if (arg && xml !== null) fs.writeFileSync(arg.slice('-gjunit_xml_file='.length), xml);
    return extra;
  };

  it('reports NOT_FOUND with install hints when no runner is installed', async () => {
    const r = await runTests({ projectPath: project, timeoutMs: 10_000, deps: fakeDeps().deps });
    expect(r).toMatchObject({ kind: 'failure', reason: 'not-found' });
    expect(r.message).toContain('GUT');
    expect(r.message).toContain('gdUnit4');
  });

  it('detects the installed runners', () => {
    installGut();
    expect(detectTestRunners(project)).toEqual(['gut']);
  });

  it('builds the GUT and gdUnit4 command lines', () => {
    installGut();
    expect(runnerArgs('gut', { projectPath: project, junitFile: 'J.xml', reportDir: 'R', filter: 'player' })).toEqual([
      '-s', 'res://addons/gut/gut_cmdln.gd', '-gexit', '-ginclude_subdirs', '-gdir=res://test', '-gselect=player', '-gjunit_xml_file=J.xml',
    ]);
    fs.writeFileSync(path.join(project, 'test', 'player_test.gd'), '');
    fs.writeFileSync(path.join(project, 'test', 'enemy_test.gd'), '');
    expect(runnerArgs('gdunit4', { projectPath: project, junitFile: 'J.xml', reportDir: 'R', filter: 'player' })).toEqual([
      '-s', 'res://addons/gdUnit4/bin/GdUnitCmdTool.gd', '--ignoreHeadlessMode', '-c', '-a', 'res://test/player_test.gd', '-rd', 'R',
    ]);
  });

  it('passes, fails (8-class verdict) and refuses a run whose test script did not load', async () => {
    installGut();
    const pass = fakeDeps(gutWrites(GUT_PASS));
    expect(await runTests({ projectPath: project, timeoutMs: 10_000, deps: pass.deps, skipImport: true })).toMatchObject({ kind: 'success', report: { tests: 1, passed: 1 } });

    const failing = fakeDeps(gutWrites(GUT_FAIL));
    expect(await runTests({ projectPath: project, timeoutMs: 10_000, deps: failing.deps, skipImport: true })).toMatchObject({ kind: 'failure', reason: 'tests-failed' });

    const brokenScript = fakeDeps(gutWrites(GUT_PASS, { output: BANNER + 'SCRIPT ERROR: Parse Error: x\n   at: GDScript::reload (res://test/broken.gd:2)\n' }));
    const r = await runTests({ projectPath: project, timeoutMs: 10_000, deps: brokenScript.deps, skipImport: true });
    expect(r).toMatchObject({ kind: 'failure', reason: 'test-run-error' });
    expect(r.loadErrors?.[0]).toMatchObject({ file: 'res://test/broken.gd', line: 2 });
  });

  it('has no verdict without a report or without tests, and maps a hang to timeout', async () => {
    installGut();
    const noXml = fakeDeps(gutWrites(null));
    expect(await runTests({ projectPath: project, timeoutMs: 10_000, deps: noXml.deps, skipImport: true })).toMatchObject({ reason: 'test-run-error' });
    const empty = fakeDeps(gutWrites('<testsuites></testsuites>'));
    expect(await runTests({ projectPath: project, timeoutMs: 10_000, deps: empty.deps, skipImport: true })).toMatchObject({ reason: 'test-run-error' });
    const hung = fakeDeps(() => ({ timedOut: true, exitCode: null }));
    expect(await runTests({ projectPath: project, timeoutMs: 10_000, deps: hung.deps, skipImport: true })).toMatchObject({ reason: 'timeout' });
  });

  it('imports first unless skipped', async () => {
    installGut();
    const { deps, calls } = fakeDeps(gutWrites(GUT_PASS));
    await runTests({ projectPath: project, timeoutMs: 10_000, deps });
    expect(calls[0].args).toContain('--import');
  });
});

describe('exportProject', () => {
  const PRESETS = '[preset.0]\n\nname="Windows Desktop"\nplatform="Windows Desktop"\n\n[preset.0.options]\n\ncustom_template/debug=""\ncustom_template/release=""\n';

  it('reads presets and template folder names', () => {
    fs.writeFileSync(path.join(project, 'export_presets.cfg'), PRESETS);
    expect(readExportPresets(project)).toEqual([{ name: 'Windows Desktop', platform: 'Windows Desktop', customTemplateDebug: '', customTemplateRelease: '' }]);
    expect(templatesVersionDir('4.5.1.stable.mono.official.f62fdbde1')).toBe('4.5.1.stable.mono');
    expect(templatesVersionDir('4.4.stable.official.abc')).toBe('4.4.stable');
    expect(templatesVersionDir('4.6.beta2.mono.official.x')).toBe('4.6.beta2.mono');
    expect(templatesRoot('/x/godot', 'linux')).toMatch(/godot[\\/]export_templates$/);
  });

  it('fails on an unknown preset and on missing templates before running Godot', async () => {
    fs.writeFileSync(path.join(project, 'export_presets.cfg'), PRESETS);
    const { deps, calls } = fakeDeps();
    expect(await exportProject({ projectPath: project, timeoutMs: 10_000, deps, preset: 'Nope', output: path.join(project, 'out', 'g.exe') }))
      .toMatchObject({ kind: 'failure', reason: 'not-found' });
    const oldAppData = process.env.APPDATA;
    const oldXdg = process.env.XDG_DATA_HOME;
    process.env.APPDATA = project;
    process.env.XDG_DATA_HOME = project;
    try {
      const r = await exportProject({ projectPath: project, timeoutMs: 10_000, deps, preset: 'Windows Desktop', output: path.join(project, 'out', 'g.exe'), godotVersion: '4.5.1.stable.mono.official.x' });
      if (process.platform !== 'darwin') expect(r).toMatchObject({ kind: 'failure', reason: 'not-found' });
    } finally {
      process.env.APPDATA = oldAppData;
      if (oldXdg === undefined) delete process.env.XDG_DATA_HOME;
      else process.env.XDG_DATA_HOME = oldXdg;
    }
    expect(calls).toHaveLength(0);
  });

  it('judges a pack export from the output file and the log, not the exit code', async () => {
    fs.writeFileSync(path.join(project, 'export_presets.cfg'), PRESETS);
    const out = path.join(project, 'out', 'g.pck');
    const writes = (output = BANNER): Responder => () => {
      fs.writeFileSync(out, 'PCK');
      return { output };
    };
    expect(await exportProject({ projectPath: project, timeoutMs: 10_000, deps: fakeDeps().deps, preset: 'Windows Desktop', output: out, mode: 'pack' }))
      .toMatchObject({ kind: 'failure', reason: 'export-failed' });
    expect(await exportProject({ projectPath: project, timeoutMs: 10_000, deps: fakeDeps(writes()).deps, preset: 'Windows Desktop', output: out, mode: 'pack' }))
      .toMatchObject({ kind: 'success', sizeBytes: 3 });
    const failedNet = writes(BANNER + 'ERROR: Export .NET Project: no solution file was found\n   at: add_message (editor/export/editor_export_platform.h:255)\n');
    expect(await exportProject({ projectPath: project, timeoutMs: 10_000, deps: fakeDeps(failedNet).deps, preset: 'Windows Desktop', output: out, mode: 'pack' }))
      .toMatchObject({ kind: 'failure', reason: 'export-failed' });
  });
});

describe('runScript', () => {
  it('maps paths inside the project to res://', () => {
    expect(scriptPathForGodot(project, path.join(project, 'tools', 'a.gd'))).toBe('res://tools/a.gd');
    expect(scriptPathForGodot(project, 'res://x.gd')).toBe('res://x.gd');
    expect(scriptPathForGodot(project, path.join(os.tmpdir(), 'elsewhere.gd'))).toMatch(/elsewhere\.gd$/);
  });

  it('passes on quit(0), fails on a non-zero code and on a load error, and keeps user args after --', async () => {
    fs.writeFileSync(path.join(project, 'tool.gd'), 'extends SceneTree\n');
    const ok = fakeDeps();
    expect(await runScript({ projectPath: project, timeoutMs: 10_000, deps: ok.deps, script: 'res://tool.gd', scriptArgs: ['a'] })).toMatchObject({ kind: 'success', scriptExitCode: 0 });
    expect(ok.calls[0].args.slice(-4)).toEqual(['-s', 'res://tool.gd', '--', 'a']);
    const three = fakeDeps(() => ({ exitCode: 3 }));
    expect(await runScript({ projectPath: project, timeoutMs: 10_000, deps: three.deps, script: 'res://tool.gd' })).toMatchObject({ reason: 'script-errors', scriptExitCode: 3 });
    const broken = fakeDeps(() => ({ output: BANNER + 'SCRIPT ERROR: Parse Error: x\n   at: GDScript::reload (res://tool.gd:2)\n' }));
    expect(await runScript({ projectPath: project, timeoutMs: 10_000, deps: broken.deps, script: 'res://tool.gd' })).toMatchObject({ reason: 'script-errors' });
    expect(await runScript({ projectPath: project, timeoutMs: 10_000, deps: ok.deps, script: 'res://missing.gd' })).toMatchObject({ reason: 'not-found' });
  });
});
