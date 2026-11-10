// `checkProject`: a CI gate without a running editor. Four separate processes (a cold single-process
// `--build-solutions --quit` SIGSEGVs on C# [Tool] plugins, godot #81790 / #100805):
//   1. `--headless --import`            materialise .godot/ (global class cache)
//   2. `dotnet build` (C# projects)     compiler errors with file(line,col)
//   3. `--headless --editor --quit`     the editor boots; the Godot-MCP addon must print its load marker
//   4. `--headless -s <checker>`        load every GDScript, report parse errors with res://file:line
// The verdict comes from the logs and markers, never from Godot's exit code. Library-safe.

import * as fs from 'fs';
import * as path from 'path';
import { buildProject, findProjectCsproj } from './build.js';
import { GODOT_MCP_PLUGIN_PATH, parseEnabledPlugins, projectGodotPath } from '../utils/project-godot.js';
import {
  ADDON_LOADED_MARKER,
  cliMarkerLines,
  parseGodotLog,
  parseMsbuildOutput,
  type Diagnostic,
} from './godot-log.js';
import {
  engineRan,
  headlessFailure,
  notStarted,
  prepareHeadless,
  remainingMs,
  runGodotStep,
  runImportStep,
  writeCliScript,
  type HeadlessBaseOptions,
  type HeadlessFailure,
  type HeadlessResult,
  type HeadlessStep,
} from './headless.js';
import type { BuildProjectResult } from './types.js';

/** Loads every `.gd` under res:// (addons only with --include-addons) and prints one marker per failing file. */
export const CHECK_SCRIPT = `extends SceneTree
# Written by godot-cli check. Loads every GDScript and prints a [godot-cli] marker per failure.

const MARK := "[godot-cli] "
const WARN_PREFIX := "debug/gdscript/warnings/"

func _walk(dir: String, out: Array, include_addons: bool) -> void:
\tvar d := DirAccess.open(dir)
\tif d == null:
\t\treturn
\td.list_dir_begin()
\tvar name := d.get_next()
\twhile name != "":
\t\tvar p := dir.path_join(name)
\t\tif name.begins_with("."):
\t\t\tpass
\t\telif d.current_is_dir():
\t\t\tif include_addons or p != "res://addons":
\t\t\t\t_walk(p, out, include_addons)
\t\telif name.get_extension() == "gd":
\t\t\tout.append(p)
\t\tname = d.get_next()

func _load_ok(path: String) -> bool:
\tvar s = ResourceLoader.load(path, "", ResourceLoader.CACHE_MODE_IGNORE)
\treturn s != null and s is Script and s.can_instantiate()

func _init() -> void:
\tvar args := OS.get_cmdline_user_args()
\tvar strict := args.has("--strict")
\tvar files: Array = []
\t_walk("res://", files, args.has("--include-addons"))
\tvar clean: Array = []
\tvar failed := 0
\tfor f in files:
\t\tif _load_ok(f):
\t\t\tclean.append(f)
\t\telse:
\t\t\tfailed += 1
\t\t\tprint(MARK + "script-failed " + f)
\tvar warned := 0
\tif strict and clean.size() > 0:
\t\t# GDScript warnings only show in the script editor: raise the Warn-level ones to Error in memory (never saved).
\t\tfor prop in ProjectSettings.get_property_list():
\t\t\tvar n: String = prop["name"]
\t\t\tif n.begins_with(WARN_PREFIX) and typeof(ProjectSettings.get_setting(n)) == TYPE_INT and ProjectSettings.get_setting(n) == 1:
\t\t\t\tProjectSettings.set_setting(n, 2)
\t\tprint(MARK + "warnings-pass")
\t\tfor f in clean:
\t\t\tif not _load_ok(f):
\t\t\t\twarned += 1
\t\t\t\tprint(MARK + "script-warned " + f)
\tprint(MARK + "check-done files=%d failed=%d warned=%d" % [files.size(), failed, warned])
\tquit(1 if failed > 0 else 0)
`;

export interface CheckOptions extends HeadlessBaseOptions {
  /** Warnings (GDScript, C# compiler) fail the check. */
  strict?: boolean;
  /** Also load the GDScript under res://addons/ (skipped by default: editor-only addon scripts do not load outside the editor). */
  includeAddons?: boolean;
  /** Tests. */
  build?: (projectPath: string) => Promise<BuildProjectResult>;
}

export interface CheckReport {
  projectPath: string;
  steps: HeadlessStep[];
  /** Script and compiler diagnostics (the ones that decide the verdict). */
  diagnostics: Diagnostic[];
  /** Engine errors/warnings that do not decide the verdict. */
  engineMessages: Diagnostic[];
  /** Whether the Godot-MCP addon is enabled in the project, and whether it printed its load marker. */
  addon: { enabled: boolean; loaded: boolean | null };
  gdscript: { files: number; failed: number; warned: number } | null;
  csharp: { built: boolean; skipped: boolean } | null;
}

export type CheckResult = HeadlessResult<CheckReport>;

/** True when project.godot enables the Godot-MCP addon. */
export function isAddonEnabled(projectPath: string): boolean {
  try {
    return parseEnabledPlugins(fs.readFileSync(projectGodotPath(projectPath), 'utf-8')).includes(GODOT_MCP_PLUGIN_PATH)
      && fs.existsSync(path.join(projectPath, 'addons', 'godot_mcp', 'plugin.cfg'));
  } catch {
    return false;
  }
}

export async function checkProject(options: CheckOptions): Promise<CheckResult> {
  const prepared = prepareHeadless(options);
  if (prepared.kind === 'failure') return prepared;
  const ctx = prepared;
  const progress = options.onProgress ?? (() => undefined);
  const diagnostics: Diagnostic[] = [];
  const engineMessages: Diagnostic[] = [];
  const addon = { enabled: isAddonEnabled(ctx.projectPath), loaded: null as boolean | null };
  let gdscript: CheckReport['gdscript'] = null;
  let csharp: CheckReport['csharp'] = null;
  const report = (): CheckReport => ({ projectPath: ctx.projectPath, steps: ctx.steps, diagnostics, engineMessages, addon, gdscript, csharp });
  const fail = (failure: HeadlessFailure): CheckResult => ({ ...failure, ...report() });
  const collect = (list: Diagnostic[]): void => {
    for (const d of list) (d.source === 'engine' ? engineMessages : diagnostics).push(d);
  };

  // 1. Import.
  progress('Importing the project (godot --headless --import)...');
  const imported = await runImportStep(ctx);
  collect(imported.diagnostics);
  if (imported.failure && imported.failure.reason !== 'script-errors') return fail(imported.failure);

  // 2. C# build.
  let compileFailed = false;
  if (findProjectCsproj(ctx.projectPath, []) !== null) {
    progress('Building C# (dotnet build)...');
    const started = Date.now();
    const built = await (options.build ?? ((p: string) => buildProject({ projectPath: p })))(ctx.projectPath);
    const output = built.kind === 'success' ? built.output ?? '' : built.errorMessage;
    ctx.steps.push({ name: 'dotnet-build', command: ['dotnet', 'build'], exitCode: built.kind === 'success' ? 0 : 1, timedOut: false, durationMs: Date.now() - started });
    const compiler = parseMsbuildOutput(output);
    diagnostics.push(...compiler);
    compileFailed = built.kind === 'failure';
    if (compileFailed && !compiler.some((d) => d.severity === 'error')) {
      diagnostics.push({ severity: 'error', source: 'csharp', message: output.split(/\r?\n/)[0] });
    }
    csharp = { built: !compileFailed, skipped: false };
    if (remainingMs(ctx) <= 0) return fail(headlessFailure('timeout', 'The check timed out during the C# build.'));
  } else {
    csharp = { built: false, skipped: true };
  }

  // 3. Editor boot (skipped when C# does not compile: every C# script, the addon included, would fail to load).
  if (!compileFailed) {
    progress('Booting the editor (godot --headless --editor --quit)...');
    const editor = await runGodotStep(ctx, 'editor', ['--editor', '--quit']);
    if (editor.timedOut) return fail(headlessFailure('timeout', 'The editor boot timed out (the Godot process tree was killed).'));
    if (!engineRan(editor)) return fail(headlessFailure('script-errors', `The editor did not start (exit ${editor.exitCode}).`));
    collect(parseGodotLog(editor.output));
    if (addon.enabled) {
      addon.loaded = editor.output.includes(ADDON_LOADED_MARKER);
      if (!addon.loaded) {
        diagnostics.push({ severity: 'error', source: 'gdscript', file: GODOT_MCP_PLUGIN_PATH, message: `The Godot-MCP addon did not load (no "${ADDON_LOADED_MARKER}" line in the editor log).` });
      }
    }
  }

  // 4. GDScript load check.
  progress('Checking GDScript (godot --headless -s)...');
  const script = writeCliScript(ctx, 'check.gd', CHECK_SCRIPT);
  const userArgs = [...(options.strict ? ['--strict'] : []), ...(options.includeAddons ? ['--include-addons'] : [])];
  const checked = await runGodotStep(ctx, 'gdscript', ['-s', script, ...(userArgs.length > 0 ? ['--', ...userArgs] : [])]);
  if (checked.timedOut) return fail(headlessFailure('timeout', 'The GDScript check timed out (the Godot process tree was killed).'));
  const markers = cliMarkerLines(checked.output);
  const done = markers.map((m) => /^check-done files=(\d+) failed=(\d+) warned=(\d+)/.exec(m)).find((m) => m !== null);
  if (!engineRan(checked) || !done) {
    diagnostics.push({ severity: 'error', source: 'gdscript', message: `The GDScript check did not finish (exit ${checked.exitCode}, no check-done marker).` });
  } else {
    gdscript = { files: Number(done[1]), failed: Number(done[2]), warned: Number(done[3]) };
  }
  // The checker's own frames ("at: _load_ok (res://.godot/godot-cli/check.gd)") never name the failing script.
  collect(parseGodotLog(checked.output).filter((d) => !d.file?.startsWith('res://.godot/')));
  for (const marker of markers) {
    const failed = /^script-failed (\S+)/.exec(marker);
    if (failed && !diagnostics.some((d) => d.file === failed[1] && d.severity === 'error')) {
      diagnostics.push({ severity: 'error', source: 'gdscript', file: failed[1], message: 'The script failed to load.' });
    }
  }

  const failing = failingDiagnostics(diagnostics, options.strict === true);
  const errors = failing.filter((d) => d.severity === 'error');
  const compileErrors = errors.filter((d) => d.source === 'csharp').length;
  if (compileErrors > 0) return fail(headlessFailure('compilation-failed', `C# compilation failed with ${compileErrors} error(s).`));
  if (errors.length > 0) return fail(headlessFailure('script-errors', `${errors.length} script error(s).`));
  if (failing.length > 0) return fail(headlessFailure('script-errors', `${failing.length} warning(s) (--strict).`));
  return { kind: 'success', success: true, ...report() };
}

/**
 * The diagnostics that fail a check: every error, plus with `strict` the GDScript warnings and C# compiler (CS)
 * warnings (package and engine warnings never).
 */
export function failingDiagnostics(diagnostics: Diagnostic[], strict: boolean): Diagnostic[] {
  return diagnostics.filter((d) => d.severity === 'error'
    || (strict && (d.source === 'gdscript' || (d.source === 'csharp' && /^CS\d+$/.test(d.code ?? '')))));
}
