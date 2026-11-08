import type { spawn } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';
import { EXTENSIONS_CATALOG } from '../utils/extensions-catalog.js';
import { FLOAT_VERSION, parsePackageReferences } from '../utils/extension-install.js';
import { isNewerVersion } from '../utils/semver.js';
import { fetchLatestVersion } from '../utils/update-check.js';
import { runDotnet } from './build.js';
import { findGodotProcess } from './close.js';
import { ADDON_REL_DIR, findConsumerCsproj, installPlugin } from './install-plugin.js';
import { emitProgress } from './progress.js';
import { requireGodotProject } from './validation.js';
import type { ProgressCallback } from './types.js';

/**
 * `outdated`: an update applies. `current`: nothing newer. `local`: a linked addon, never replaced.
 * `unknown`: the lookup failed. `floating`: a `Version="*"` extension, re-resolved by a forced restore.
 * `pinned`: an extension at a concrete version, kept as the user chose.
 */
export type ProjectUpdateStatus = 'outdated' | 'current' | 'local' | 'unknown' | 'floating' | 'pinned';

export interface ProjectUpdateItem {
  kind: 'plugin' | 'extension';
  name: string;
  packageId?: string;
  from: string;
  to: string | null;
  status: ProjectUpdateStatus;
}

export interface UpdateProjectOptions {
  godotProjectPath: string;
  /** Leave the installed extensions as they are. */
  noExtensions?: boolean;
  /** Only report what would change; write nothing and restore nothing. */
  dryRun?: boolean;
  /** The addon version to move to. Defaults to the latest published release. */
  version?: string;
  /** Test seam: the editor that has this project open, if any. */
  findEditor?: (projectPath: string) => { pid: number } | null | undefined;
  /** Injectable spawn for tests (mock the `dotnet restore`). */
  spawnImpl?: typeof spawn;
  onProgress?: ProgressCallback;
}

export interface UpdateProjectSuccess {
  kind: 'success';
  success: true;
  /** Everything checked, as found before any write. */
  items: ProjectUpdateItem[];
  /** The items this call updated (always empty with `dryRun`). A floating extension may not have actually moved. */
  updated: ProjectUpdateItem[];
  warnings: string[];
  nextSteps: string[];
}

export interface UpdateProjectFailure {
  kind: 'failure';
  success: false;
  warnings: string[];
  error: Error;
}

export type UpdateProjectResult = UpdateProjectSuccess | UpdateProjectFailure;

function asError(err: unknown): Error {
  return err instanceof Error ? err : new Error(String(err));
}

/** The `version` in the project's addon `plugin.cfg`; null when the addon is absent or has none. */
function readAddonVersion(projectPath: string): string | null {
  try {
    const cfg = fs.readFileSync(path.join(projectPath, ADDON_REL_DIR, 'plugin.cfg'), 'utf-8');
    return /^\s*version\s*=\s*"([^"]*)"/m.exec(cfg)?.[1] ?? null;
  } catch {
    return null;
  }
}

function isLink(target: string): boolean {
  try {
    return fs.lstatSync(target).isSymbolicLink();
  } catch {
    return false;
  }
}

async function planAddon(projectPath: string, opts: UpdateProjectOptions, warnings: string[]): Promise<ProjectUpdateItem | null> {
  const from = readAddonVersion(projectPath);
  if (from === null) return null;

  const base = { kind: 'plugin' as const, name: 'godot_mcp addon', from };
  if (isLink(path.join(projectPath, ADDON_REL_DIR))) return { ...base, to: null, status: 'local' };
  try {
    // godot-cli ships as the last leg of an addon release, so its latest version always names a published addon.
    const to = opts.version ?? await fetchLatestVersion();
    return { ...base, to, status: isNewerVersion(from, to) ? 'outdated' : 'current' };
  } catch (err: unknown) {
    warnings.push(`Could not look up the latest addon: ${asError(err).message}`);
    return { ...base, to: null, status: 'unknown' };
  }
}

function planExtensions(csprojPath: string): ProjectUpdateItem[] {
  const references = parsePackageReferences(fs.readFileSync(csprojPath, 'utf-8'));
  return EXTENSIONS_CATALOG.flatMap((extension): ProjectUpdateItem[] => {
    const from = references.get(extension.packageId.toLowerCase());
    if (from === undefined) return [];
    return [{
      kind: 'extension',
      name: extension.name,
      packageId: extension.packageId,
      from,
      to: null,
      status: from.includes(FLOAT_VERSION) ? 'floating' : 'pinned',
    }];
  });
}

/**
 * Bring a Godot project's addon and its installed extensions to their latest versions. The one
 * function behind `godot-cli update`. Extensions float (`Version="*"`), but a no-op restore keeps the
 * version it already resolved, so a forced restore that skips NuGet's HTTP cache re-resolves them; a
 * concrete pin is the user's choice and is kept. A failed restore is a warning. Library-safe: no
 * stdout, no `process.exit`, no throws past the boundary.
 */
export async function updateProject(opts: UpdateProjectOptions): Promise<UpdateProjectResult> {
  const warnings: string[] = [];
  const nextSteps: string[] = [];
  try {
    const validated = requireGodotProject(opts?.godotProjectPath);
    if (!validated.ok) return { kind: 'failure', success: false, warnings, error: validated.error };
    const { projectPath } = validated;

    const addon = await planAddon(projectPath, opts, warnings);
    const csprojPath = findConsumerCsproj(projectPath, warnings);
    const extensions = opts.noExtensions || csprojPath === null ? [] : planExtensions(csprojPath);
    const items = [...(addon ? [addon] : []), ...extensions];
    const updated: ProjectUpdateItem[] = [];

    if (!opts.dryRun) {
      if (addon?.status === 'outdated') {
        // Replacing the addon's sources under an open editor makes it reload half-replaced code.
        const running = (opts.findEditor ?? findGodotProcess)(projectPath);
        if (running) throw new Error(`Close the Godot editor first (PID ${running.pid}): it has this project open.`);

        const result = await installPlugin({
          godotProjectPath: projectPath,
          version: addon.to!,
          onProgress: opts.onProgress,
        });
        if (result.kind === 'failure') {
          warnings.push(`Could not update the addon: ${result.error.message}`, ...result.warnings);
        } else {
          warnings.push(...result.warnings);
          updated.push(addon);
        }
      }

      const pinned = extensions.filter((item) => item.status === 'pinned');
      if (pinned.length > 0) {
        warnings.push(`Pinned extensions were not updated: ${pinned.map((item) => item.packageId).join(', ')}. Set their Version to "*" to follow the latest.`);
      }

      const floating = extensions.filter((item) => item.status === 'floating');
      if (floating.length > 0 && csprojPath !== null) {
        emitProgress(opts.onProgress, { phase: 'extensions-refreshing', message: `Updating ${floating.length} installed extension(s)` });
        const run = await runDotnet('dotnet', ['restore', csprojPath, '--force', '--no-cache'], projectPath, opts.spawnImpl);
        if (run.code === 0) {
          updated.push(...floating);
        } else {
          warnings.push(`Could not update the installed extensions (dotnet restore exited with ${run.code}). If an extension needs a newer McpPlugin than this addon pins (NU1605), the project will not build until you update the addon or pin that extension to a compatible version. ${run.output.trim().slice(-500)}`);
        }
      }

      if (updated.length > 0) nextSteps.push('Rebuild solutions to compile the new versions.');
    }

    return { kind: 'success', success: true, items, updated, warnings, nextSteps };
  } catch (err: unknown) {
    return { kind: 'failure', success: false, warnings, error: asError(err) };
  }
}

/** The nearest folder at or above `start` that is a Godot project, or null. */
export function findGodotProjectRoot(start: string): string | null {
  for (let dir = path.resolve(start); ; dir = path.dirname(dir)) {
    if (fs.existsSync(path.join(dir, 'project.godot'))) return dir;
    if (dir === path.dirname(dir)) return null;
  }
}
