// `runTests`: run the project's GUT or gdUnit4 suite headless with a hard timeout (both runners are known to hang),
// and judge it from the JUnit XML report cross-checked with the log: a test script that failed to load never shows up
// in the report, so it must not pass. Library-safe.

import * as fs from 'fs';
import * as path from 'path';
import { parseGodotLog, type Diagnostic } from './godot-log.js';
import {
  engineRan,
  headlessFailure,
  notStarted,
  prepareHeadless,
  runGodotStep,
  runImportStep,
  type HeadlessBaseOptions,
  type HeadlessFailure,
  type HeadlessResult,
  type HeadlessStep,
} from './headless.js';
import { parseJUnit, type JUnitReport } from './junit.js';
import { isGodotProjectRoot } from '../utils/project-godot.js';

export type TestRunner = 'gut' | 'gdunit4';

export const TEST_RUNNERS: Record<TestRunner, { label: string; script: string; install: string }> = {
  gut: {
    label: 'GUT',
    script: 'addons/gut/gut_cmdln.gd',
    install: 'GUT: https://github.com/bitwes/Gut (Asset Library "GUT - Godot Unit Testing"), copied to res://addons/gut/',
  },
  gdunit4: {
    label: 'gdUnit4',
    script: 'addons/gdUnit4/bin/GdUnitCmdTool.gd',
    install: 'gdUnit4: https://github.com/godot-gdunit-labs/gdUnit4 (Asset Library "gdUnit4"), copied to res://addons/gdUnit4/',
  },
};

export interface RunTestsOptions extends HeadlessBaseOptions {
  /** Default: the one installed (GUT first when both are). */
  runner?: TestRunner;
  /** Test directories (res:// or project-relative). Default: GUT's .gutconfig.json, else res://test and/or res://tests. */
  dirs?: string[];
  /** Only test scripts whose file name contains this text. */
  filter?: string;
  /** Skip the `--import` step (the project was imported already). */
  skipImport?: boolean;
}

export interface TestsReport {
  projectPath: string;
  runner: TestRunner;
  steps: HeadlessStep[];
  /** The JUnit XML file the runner wrote. */
  junitFile?: string;
  report?: JUnitReport;
  /** Test scripts that failed to load (they never reach the report). */
  loadErrors: Diagnostic[];
}

export type RunTestsResult = HeadlessResult<TestsReport>;

/** The test runners installed in a project. */
export function detectTestRunners(projectPath: string): TestRunner[] {
  return (Object.keys(TEST_RUNNERS) as TestRunner[]).filter((r) => fs.existsSync(path.join(projectPath, TEST_RUNNERS[r].script)));
}

function toResPath(projectPath: string, dir: string): string {
  if (dir.startsWith('res://')) return dir.replace(/\/+$/, '') || 'res://';
  const rel = path.relative(projectPath, path.resolve(projectPath, dir)).split(path.sep).join('/');
  return rel === '' ? 'res://' : `res://${rel}`;
}

function resToFs(projectPath: string, res: string): string {
  return path.join(projectPath, res.replace(/^res:\/\//, ''));
}

/** Default test dirs: res://test and/or res://tests when they exist, else res://. */
export function defaultTestDirs(projectPath: string): string[] {
  const found = ['res://test', 'res://tests'].filter((d) => fs.existsSync(resToFs(projectPath, d)));
  return found.length > 0 ? found : ['res://'];
}

/** The `.gd` files under `dirs` whose name contains `filter` (for gdUnit4, which has no file filter of its own). */
export function findTestScripts(projectPath: string, dirs: string[], filter: string): string[] {
  const out: string[] = [];
  const walk = (res: string): void => {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(resToFs(projectPath, res), { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      if (e.name.startsWith('.') || (res === 'res://' && e.name === 'addons')) continue;
      const child = res === 'res://' ? `res://${e.name}` : `${res}/${e.name}`;
      if (e.isDirectory()) walk(child);
      else if (e.name.endsWith('.gd') && e.name.includes(filter)) out.push(child);
    }
  };
  for (const d of dirs) walk(d);
  return out.sort();
}

/** The runner's command line (after `--headless --path <project>`). */
export function runnerArgs(
  runner: TestRunner,
  options: { projectPath: string; dirs?: string[]; filter?: string; junitFile: string; reportDir: string },
): string[] {
  if (runner === 'gut') {
    const hasConfig = fs.existsSync(path.join(options.projectPath, '.gutconfig.json'));
    const dirs = options.dirs ?? (hasConfig ? [] : defaultTestDirs(options.projectPath));
    return [
      '-s', `res://${TEST_RUNNERS.gut.script}`,
      '-gexit',
      '-ginclude_subdirs',
      ...(dirs.length > 0 ? [`-gdir=${dirs.join(',')}`] : []),
      ...(options.filter ? [`-gselect=${options.filter}`] : []),
      `-gjunit_xml_file=${options.junitFile}`,
    ];
  }
  const dirs = options.dirs ?? defaultTestDirs(options.projectPath);
  const targets = options.filter ? findTestScripts(options.projectPath, dirs, options.filter) : dirs;
  return [
    '-s', `res://${TEST_RUNNERS.gdunit4.script}`,
    '--ignoreHeadlessMode',
    // gdUnit4 stops at the first failure unless told to continue.
    '-c',
    ...targets.flatMap((t) => ['-a', t]),
    '-rd', options.reportDir,
  ];
}

/** The newest `results.xml` under a gdUnit4 report dir (`<dir>/report_<n>/results.xml`). */
function findGdUnitReport(reportDir: string): string | undefined {
  let best: { file: string; mtime: number } | undefined;
  const walk = (dir: string, depth: number): void => {
    let entries: fs.Dirent[];
    try {
      entries = fs.readdirSync(dir, { withFileTypes: true });
    } catch {
      return;
    }
    for (const e of entries) {
      const full = path.join(dir, e.name);
      if (e.isDirectory() && depth < 3) walk(full, depth + 1);
      else if (e.isFile() && e.name === 'results.xml') {
        const mtime = fs.statSync(full).mtimeMs;
        if (!best || mtime > best.mtime) best = { file: full, mtime };
      }
    }
  };
  walk(reportDir, 0);
  return best?.file;
}

export async function runTests(options: RunTestsOptions): Promise<RunTestsResult> {
  const projectPath = path.resolve(options.projectPath);
  // Before prepareHeadless: a missing runner is reported without looking for a binary or an open editor.
  const installed = isGodotProjectRoot(projectPath) ? detectTestRunners(projectPath) : [];
  const runner = options.runner ?? installed[0];
  if (!runner || !installed.includes(runner)) {
    const wanted = options.runner ? [options.runner] : (Object.keys(TEST_RUNNERS) as TestRunner[]);
    return headlessFailure(
      'not-found',
      `No ${options.runner ? TEST_RUNNERS[options.runner].label : 'test runner (GUT or gdUnit4)'} installed in ${projectPath}. Install one:\n` +
        wanted.map((r) => `  ${TEST_RUNNERS[r].install}`).join('\n'),
    );
  }

  const ctx = prepareHeadless(options);
  if (ctx.kind === 'failure') return ctx;
  const progress = options.onProgress ?? (() => undefined);
  const dirs = options.dirs?.map((d) => toResPath(ctx.projectPath, d));
  // One report folder, cleared per run (the previous run's reports would otherwise pile up in .godot/).
  const runDir = path.join(ctx.workDir, 'test');
  fs.rmSync(runDir, { recursive: true, force: true });
  fs.mkdirSync(runDir, { recursive: true });
  const junitFile = path.join(runDir, 'junit.xml');
  const reportDir = path.join(runDir, 'gdunit-reports');
  const report: TestsReport = { projectPath: ctx.projectPath, runner, steps: ctx.steps, loadErrors: [] };
  const fail = (failure: HeadlessFailure): RunTestsResult => ({ ...failure, ...report });

  if (!options.skipImport) {
    progress('Importing the project (godot --headless --import)...');
    const imported = await runImportStep(ctx);
    if (imported.failure?.reason === 'timeout') return fail(imported.failure);
  }

  progress(`Running ${TEST_RUNNERS[runner].label} tests...`);
  const run = await runGodotStep(ctx, 'test', runnerArgs(runner, { projectPath: ctx.projectPath, ...(dirs ? { dirs } : {}), ...(options.filter ? { filter: options.filter } : {}), junitFile, reportDir }));
  report.loadErrors = parseGodotLog(run.output).filter((d) => d.severity === 'error' && d.source === 'gdscript' && d.phase === 'load' && !d.file?.startsWith('res://addons/'));
  if (run.timedOut) return fail(headlessFailure('timeout', `The ${TEST_RUNNERS[runner].label} run timed out (the Godot process tree was killed).`));
  if (!engineRan(run)) return fail(notStarted('test-run-error', run));

  const xmlFile = runner === 'gut' ? junitFile : findGdUnitReport(reportDir);
  if (!xmlFile || !fs.existsSync(xmlFile)) {
    return fail(headlessFailure('test-run-error', `${TEST_RUNNERS[runner].label} wrote no JUnit report (exit ${run.exitCode}): no verdict.`));
  }
  report.junitFile = xmlFile;
  try {
    report.report = parseJUnit(fs.readFileSync(xmlFile, 'utf-8'));
  } catch (err) {
    return fail(headlessFailure('test-run-error', `Unreadable JUnit report ${xmlFile}: ${err instanceof Error ? err.message : String(err)}`));
  }

  const { failures, errors, tests } = report.report;
  if (failures + errors > 0) return fail(headlessFailure('tests-failed', `${failures + errors} of ${tests} test(s) failed.`));
  if (report.loadErrors.length > 0) {
    return fail(headlessFailure('test-run-error', `${report.loadErrors.length} test script(s) failed to load, so their tests never ran.`));
  }
  if (tests === 0) return fail(headlessFailure('test-run-error', 'No tests ran: no verdict.'));
  return { kind: 'success', success: true, ...report };
}
