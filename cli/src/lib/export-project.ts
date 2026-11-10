// `exportProject`: `godot --headless --export-release|--export-debug|--export-pack <preset> <output>`. Godot can exit 0
// without writing anything, so the export templates are checked first and the output file afterwards. Library-safe.

import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { spawnSync } from 'child_process';
import { parseGodotLog, type Diagnostic } from './godot-log.js';
import {
  engineRan,
  headlessFailure,
  stoppedRun,
  notStarted,
  remainingMs,
  prepareHeadless,
  runGodotStep,
  type HeadlessBaseOptions,
  type HeadlessFailure,
  type HeadlessResult,
  type HeadlessStep,
} from './headless.js';

export type ExportMode = 'release' | 'debug' | 'pack';

/** Log errors that mean the export itself failed (other engine errors, e.g. a plugin's runtime noise, do not). */
const EXPORT_ERROR_RE = /export|solution file|publish|template|cannot save|failed to (?:save|write|open|copy)/i;

export interface ExportOptions extends HeadlessBaseOptions {
  preset: string;
  /** Output file (relative to the current directory). */
  output: string;
  mode?: ExportMode;
  /** Tests: the editor's version string (`4.5.1.stable.mono.official.f62fdbde1`). */
  godotVersion?: string;
}

export interface ExportReport {
  projectPath: string;
  preset: string;
  mode: ExportMode;
  output: string;
  sizeBytes?: number;
  templatesDir?: string;
  steps: HeadlessStep[];
  diagnostics: Diagnostic[];
}

export type ExportResult = HeadlessResult<ExportReport>;

export interface ExportPreset {
  name: string;
  platform: string;
  customTemplateDebug: string;
  customTemplateRelease: string;
}

/** The `[preset.N]` sections of export_presets.cfg. */
export function readExportPresets(projectPath: string): ExportPreset[] {
  let text: string;
  try {
    text = fs.readFileSync(path.join(projectPath, 'export_presets.cfg'), 'utf-8');
  } catch {
    return [];
  }
  const presets: ExportPreset[] = [];
  let current: ExportPreset | undefined;
  let inOptions = false;
  for (const raw of text.split(/\r?\n/)) {
    const line = raw.trim();
    const section = /^\[preset\.(\d+)(\.options)?\]$/.exec(line);
    if (section) {
      inOptions = section[2] !== undefined;
      if (!inOptions) {
        current = { name: '', platform: '', customTemplateDebug: '', customTemplateRelease: '' };
        presets.push(current);
      }
      continue;
    }
    if (line.startsWith('[')) {
      inOptions = false;
      continue;
    }
    const kv = /^([\w/]+)\s*=\s*"(.*)"$/.exec(line);
    if (!kv || !current) continue;
    const [, key, value] = kv;
    if (!inOptions && key === 'name') current.name = value;
    else if (!inOptions && key === 'platform') current.platform = value;
    else if (inOptions && key === 'custom_template/debug') current.customTemplateDebug = value;
    else if (inOptions && key === 'custom_template/release') current.customTemplateRelease = value;
  }
  return presets;
}

/** Export templates folder name for a `--version` string: `4.5.1.stable.mono.official.f62fdbde1` → `4.5.1.stable.mono`. */
export function templatesVersionDir(version: string): string {
  const parts = version.trim().split('.');
  const status = parts.findIndex((p) => /^(stable|dev\d*|alpha\d*|beta\d*|rc\d*)$/.test(p));
  if (status < 0) return version.trim();
  return parts.slice(0, status + 1 + (parts[status + 1] === 'mono' ? 1 : 0)).join('.');
}

/** Godot's export templates root for this OS (or the self-contained `editor_data` next to the binary). */
export function templatesRoot(binary: string, platform: NodeJS.Platform = process.platform): string {
  const binDir = path.dirname(binary);
  if (fs.existsSync(path.join(binDir, '._sc_')) || fs.existsSync(path.join(binDir, '_sc_'))) {
    return path.join(binDir, 'editor_data', 'export_templates');
  }
  const home = os.homedir();
  if (platform === 'win32') return path.join(process.env.APPDATA ?? path.join(home, 'AppData', 'Roaming'), 'Godot', 'export_templates');
  if (platform === 'darwin') return path.join(home, 'Library', 'Application Support', 'Godot', 'export_templates');
  return path.join(process.env.XDG_DATA_HOME ?? path.join(home, '.local', 'share'), 'godot', 'export_templates');
}

/**
 * The editor's version: `--version` (stdout; a GUI build may print nothing to a pipe), else the official file name
 * (`Godot_v4.5.1-stable_mono_win64.exe` → `4.5.1.stable.mono`).
 */
export function godotVersion(binary: string, timeoutMs: number): string | undefined {
  const run = spawnSync(binary, ['--version'], { encoding: 'utf-8', timeout: Math.max(1, Math.min(30_000, timeoutMs)), windowsHide: true });
  const line = (run.stdout ?? '').split(/\r?\n/).map((l) => l.trim()).find((l) => /^\d+\.\d+/.test(l));
  return line ?? versionFromFileName(path.basename(binary));
}

export function versionFromFileName(name: string): string | undefined {
  const m = /^godot_v(\d+\.\d+(?:\.\d+)?)-(stable|dev\d*|alpha\d*|beta\d*|rc\d*)(_mono)?/i.exec(name);
  return m ? `${m[1]}.${m[2]}${m[3] ? '.mono' : ''}` : undefined;
}

export async function exportProject(options: ExportOptions): Promise<ExportResult> {
  const ctx = prepareHeadless(options);
  if (ctx.kind === 'failure') return ctx;
  const mode = options.mode ?? 'release';
  const output = path.resolve(options.output);
  const report: ExportReport = { projectPath: ctx.projectPath, preset: options.preset, mode, output, steps: ctx.steps, diagnostics: [] };
  const fail = (failure: HeadlessFailure): ExportResult => ({ ...failure, ...report });

  const presets = readExportPresets(ctx.projectPath);
  const preset = presets.find((p) => p.name === options.preset);
  if (!preset) {
    const known = presets.map((p) => `"${p.name}"`).join(', ');
    return fail(headlessFailure('not-found', `No export preset named "${options.preset}" in export_presets.cfg${known ? ` (presets: ${known})` : ' (the file is missing or has no presets)'}.`));
  }

  // A .pck/.zip pack needs no templates; a preset with its own template file does not use the installed ones.
  const custom = mode === 'debug' ? preset.customTemplateDebug : preset.customTemplateRelease;
  if (mode !== 'pack' && custom === '') {
    const version = options.godotVersion ?? godotVersion(ctx.binary, remainingMs(ctx));
    if (!version) return fail(headlessFailure('export-failed', `Could not read the Godot version (${ctx.binary} --version).`));
    const versionDir = templatesVersionDir(version);
    const dir = path.join(templatesRoot(ctx.binary), versionDir);
    report.templatesDir = dir;
    let installed = false;
    try {
      installed = fs.readdirSync(dir).length > 0;
    } catch {
      installed = false;
    }
    if (!installed) {
      return fail(headlessFailure('not-found', `Export templates for Godot ${versionDir} are not installed (expected in ${dir}). Install them from the editor (Editor > Manage Export Templates) or download the .tpz from godotengine.org.`));
    }
  } else if (custom !== '' && mode !== 'pack' && !fs.existsSync(custom)) {
    return fail(headlessFailure('not-found', `The preset's custom ${mode} template does not exist: ${custom}`));
  }

  fs.mkdirSync(path.dirname(output), { recursive: true });
  const startedAt = Date.now();
  options.onProgress?.(`Exporting "${options.preset}" (${mode}) to ${output}...`);
  const run = await runGodotStep(ctx, 'export', [`--export-${mode}`, options.preset, output]);
  report.diagnostics = parseGodotLog(run.output).filter((d) => d.severity === 'error');
  const stopped = stoppedRun(run, 'The export');
  if (stopped) return fail(stopped);
  if (!engineRan(run)) return fail(notStarted('export-failed', run));

  let stat: fs.Stats | undefined;
  try {
    stat = fs.statSync(output);
  } catch {
    stat = undefined;
  }
  // Allow for coarse file-system timestamps when deciding the file is this run's.
  // A macOS export is an .app folder.
  if (!stat || !(stat.isFile() || stat.isDirectory()) || (stat.isFile() && stat.size === 0) || stat.mtimeMs < startedAt - 2_000) {
    const detail = report.diagnostics[0]?.message;
    return fail(headlessFailure('export-failed', `The export produced no output file at ${output} (exit ${run.exitCode})${detail ? `: ${detail}` : ''}.`));
  }
  if (stat.isFile()) report.sizeBytes = stat.size;
  // Godot writes the executable and the .pck even when a step failed (e.g. the C# publish: no .sln) and exits 0.
  const exportErrors = report.diagnostics.filter((d) => (d.source === 'gdscript' && d.phase === 'load') || EXPORT_ERROR_RE.test(d.message));
  if (exportErrors.length > 0) {
    report.diagnostics = exportErrors;
    return fail(headlessFailure('export-failed', `The export logged ${exportErrors.length} error(s): ${exportErrors[0].message}`));
  }
  if (run.exitCode !== 0) return fail(headlessFailure('export-failed', `Godot exited with code ${run.exitCode} during the export.`));
  return { kind: 'success', success: true, ...report };
}
