import * as fs from 'fs';
import * as path from 'path';
import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { CommandOutput, addFormatOption, exitWithError, parseFormat } from '../utils/output.js';
import { ExitCode } from '../utils/exit-codes.js';
import { collect } from '../utils/input.js';
import { formatDiagnostic } from '../lib/godot-log.js';
import { caseScript, failureLine, failureSummary, type JUnitCase } from '../lib/junit.js';
import { TEST_RUNNERS, runTests, type TestRunner } from '../lib/run-tests.js';
import {
  addHeadlessOptions,
  diagnosticErrors,
  finishHeadless,
  headlessProjectPath,
  headlessTimeout,
  printSteps,
  type HeadlessCliOptions,
} from './headless-common.js';

interface TestCliOptions extends HeadlessCliOptions {
  runner?: string;
  dir: string[];
  filter?: string;
  output?: string;
  import?: boolean;
}

/** GitHub Actions workflow-command escaping (`%`, CR, LF; plus `:` and `,` in properties). */
function ghEscape(text: string, property = false): string {
  let out = text.replace(/%/g, '%25').replace(/\r/g, '%0D').replace(/\n/g, '%0A');
  if (property) out = out.replace(/:/g, '%3A').replace(/,/g, '%2C');
  return out;
}

/** `::error file=…,line=…,title=…::message` for one failed case (file relative to the repo / cwd). */
export function githubAnnotation(c: JUnitCase, projectPath: string, cwd = process.cwd()): string {
  const script = caseScript(c);
  const props: string[] = [];
  if (script) {
    const rel = path.relative(cwd, path.join(projectPath, script.slice('res://'.length))).split(path.sep).join('/');
    props.push(`file=${ghEscape(rel, true)}`);
    const line = failureLine(c);
    if (line !== undefined) props.push(`line=${line}`);
  }
  props.push(`title=${ghEscape(`${c.suite || c.classname || ''} ${c.name}`.trim(), true)}`);
  const message = c.details ?? failureSummary(c);
  return `::error ${props.join(',')}::${ghEscape(message)}`;
}

export const testCommand = addFormatOption(
  addHeadlessOptions(
    new Command('test')
      .description('Run the project\'s GUT or gdUnit4 tests headless; the verdict comes from the JUnit report and the log')
      .argument('[path]', 'Godot project path')
      .option('--runner <runner>', 'gut | gdunit4 (default: the one installed)')
      .option('--dir <dir>', 'Test directory, res:// or project-relative (repeatable; default: res://test, res://tests)', collect, [])
      .option('--filter <text>', 'Only test scripts whose file name contains <text>')
      .option('--output <file>', 'Copy the JUnit XML report to <file>')
      .option('--no-import', 'Skip the --import step (the project is imported already)'),
    600_000,
  ),
  ['github'],
).action(async (positional: string | undefined, options: TestCliOptions) => {
  const format = parseFormat<'text' | 'json' | 'ndjson' | 'github'>(options.format, ['github']);
  const output = new CommandOutput('test', format);
  let runner: TestRunner | undefined;
  if (options.runner !== undefined) {
    const value = options.runner.toLowerCase();
    if (value !== 'gut' && value !== 'gdunit4') exitWithError(ExitCode.Usage, 'USAGE', `Invalid --runner value: "${options.runner}". Expected gut or gdunit4.`);
    runner = value;
  }

  const result = await runTests({
    projectPath: headlessProjectPath(positional, options),
    timeoutMs: headlessTimeout(options),
    allowOpenProject: options.allowOpen === true,
    ...(runner ? { runner } : {}),
    ...(options.dir.length > 0 ? { dirs: options.dir } : {}),
    ...(options.filter ? { filter: options.filter } : {}),
    skipImport: options.import === false,
    ...(options.editorPath ? { editorPath: options.editorPath } : {}),
    onProgress: (m) => output.progress(m),
  });

  if (options.output && result.junitFile) {
    const target = path.resolve(options.output);
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.copyFileSync(result.junitFile, target);
  }

  const report = result.report;
  const failed = report?.cases.filter((c) => c.status === 'failed' || c.status === 'error') ?? [];
  const loadErrors = result.loadErrors ?? [];

  if (format === 'github') {
    for (const c of failed) console.log(githubAnnotation(c, result.projectPath ?? ''));
    for (const d of loadErrors) {
      const file = d.file?.startsWith('res://') && result.projectPath
        ? path.relative(process.cwd(), path.join(result.projectPath, d.file.slice('res://'.length))).split(path.sep).join('/')
        : undefined;
      console.log(`::error ${file ? `file=${ghEscape(file, true)}${d.line ? `,line=${d.line}` : ''},` : ''}title=Test script failed to load::${ghEscape(d.message)}`);
    }
    if (report) console.log(`${TEST_RUNNERS[result.runner!].label}: ${report.passed} passed, ${report.failures + report.errors} failed, ${report.skipped} skipped (${report.tests} total)`);
  } else if (!output.machine) {
    ui.heading(`Godot Tests${result.runner ? ` (${TEST_RUNNERS[result.runner].label})` : ''}`);
    if (result.projectPath) ui.label('Project', result.projectPath);
    printSteps(result.steps ?? []);
    for (const c of failed) ui.error(`${c.suite ? `${c.suite} > ` : ''}${c.name}: ${failureSummary(c)}`);
    for (const d of loadErrors) ui.error(formatDiagnostic(d));
    if (report) ui.label('Result', `${report.passed} passed, ${report.failures + report.errors} failed, ${report.skipped} skipped (${report.tests} total)`);
    if (result.junitFile) ui.label('JUnit', options.output ? path.resolve(options.output) : result.junitFile);
  }

  const data = result.runner ? {
    projectPath: result.projectPath,
    runner: result.runner,
    junitFile: options.output && result.junitFile ? path.resolve(options.output) : result.junitFile ?? null,
    summary: report ? { tests: report.tests, passed: report.passed, failed: report.failures + report.errors, skipped: report.skipped, suiteFailures: report.suiteFailures } : null,
    failures: failed.map((c) => ({ suite: c.suite, name: c.name, file: caseScript(c) ?? null, line: failureLine(c) ?? null, message: failureSummary(c), details: c.details ?? null })),
    loadErrors,
    steps: result.steps,
  } : null;
  const errors = [
    ...failed.map((c) => ({ code: 'TESTS_FAILED' as const, message: `${c.suite ? `${c.suite} > ` : ''}${c.name}: ${failureSummary(c)}` })),
    ...diagnosticErrors('TEST_RUN_ERROR', loadErrors),
  ];
  finishHeadless(output, result, data, result.kind === 'failure' ? errors : [], report ? `${report.passed} test(s) passed` : undefined);
});
