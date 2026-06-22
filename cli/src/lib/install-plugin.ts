import * as fs from 'fs';
import * as path from 'path';
import {
  GODOT_MCP_PLUGIN_PATH,
  projectGodotPath as resolveProjectGodotPath,
  togglePluginInText,
} from '../utils/project-godot.js';
import { addAddonPackageReferences } from '../utils/csproj-deps.js';
import { ADDON_PACKAGE_REFERENCES } from '../utils/addon-deps.js';
import {
  addonDownloadUrl,
  assertTrustedDownloadUrl,
} from '../utils/addon-source.js';
import { parseZip, type ZipEntry } from '../utils/unzip.js';
import { emitProgress } from './progress.js';
import { requireGodotProject } from './validation.js';
import type {
  AddonMaterializeOutcome,
  CsprojPatchOutcome,
  InstallPluginOptions,
  InstallPluginResult,
  ProgressCallback,
  RemovePluginOptions,
  RemovePluginResult,
} from './types.js';

/** Relative path of the addon dir inside a Godot project. */
const ADDON_REL_DIR = path.join('addons', 'godot_mcp');

/**
 * Install the `godot_mcp` addon into a Godot C# project as a REAL installer:
 *
 *  1. Materialize `res://addons/godot_mcp/` — by default downloading
 *     `godot-mcp-addon-<version>.zip` over HTTPS from github.com only (the
 *     `IvanMurzak/Godot-MCP` release `v<version>`), or, with `--source <path>`,
 *     copying the addon from a local directory (offline / dev / CI).
 *  2. Add the two NuGet `PackageReference`s the addon needs to the consumer
 *     `.csproj`, idempotently and single-sourced from the addon's own pins.
 *  3. Flip the `[editor_plugins] enabled` flag in `project.godot`.
 *
 * Library-safe: no stdout noise, no `process.exit`, no throws past the public
 * boundary; returns a `{ kind: 'success' | 'failure' }` union. Idempotent: a
 * re-run that finds the addon present, the pins present, and the plugin already
 * enabled reports `changed: false` and makes no writes. On a download/copy
 * failure the project is left as found (the addon dir is only swapped in after a
 * successful fetch+extract; a partial extraction is rolled back).
 */
export async function installPlugin(opts: InstallPluginOptions): Promise<InstallPluginResult> {
  const warnings: string[] = [];

  try {
    const validated = requireGodotProject(opts?.godotProjectPath);
    if (!validated.ok) {
      return {
        kind: 'failure',
        success: false,
        projectGodotPath: validated.projectGodotPath,
        warnings,
        error: validated.error,
      };
    }
    const { projectPath, projectGodotPath } = validated;

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Installing godot_mcp addon into ${projectPath}`,
    });

    // 1. Materialize the addon files (download / local copy / skip).
    const materialize = await materializeAddon(projectPath, opts, warnings);

    // 2. Patch the consumer csproj with the addon's NuGet PackageReferences.
    const csproj = patchConsumerCsproj(projectPath, warnings);

    // 3. Enable the plugin in project.godot.
    const text = fs.readFileSync(projectGodotPath, 'utf-8');
    const toggled = togglePluginInText(text, true, GODOT_MCP_PLUGIN_PATH);
    if (toggled.kind === 'changed') {
      fs.writeFileSync(projectGodotPath, toggled.text);
      emitProgress(opts.onProgress, {
        phase: 'manifest-patched',
        message: `Wrote ${projectGodotPath}`,
        manifestPath: projectGodotPath,
      });
    }

    emitProgress(opts.onProgress, { phase: 'done', message: 'godot_mcp addon installed.' });

    return {
      kind: 'success',
      success: true,
      changed: toggled.kind === 'changed',
      projectGodotPath,
      enabledPlugins: toggled.enabled,
      materialize,
      csproj,
      warnings,
    };
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}

/**
 * Materialize `addons/godot_mcp/` into the project from the GitHub release zip
 * (default) or a local `--source` directory. Idempotent for the local-copy and
 * download paths: the target dir is replaced atomically-ish (extract/copy into a
 * temp sibling, then swap), so a failure leaves the existing addon untouched.
 */
async function materializeAddon(
  projectPath: string,
  opts: InstallPluginOptions,
  warnings: string[],
): Promise<AddonMaterializeOutcome> {
  const addonDir = path.join(projectPath, ADDON_REL_DIR);

  if (opts.skipMaterialize === true) {
    if (!fs.existsSync(path.join(addonDir, 'plugin.cfg'))) {
      warnings.push(
        `skipMaterialize was set but ${path.join(addonDir, 'plugin.cfg')} is absent — the editor cannot load the plugin until the addon files are present.`,
      );
    }
    return { source: 'skipped', addonDir };
  }

  if (opts.source !== undefined && opts.source !== '') {
    const sourceDir = resolveAddonSourceDir(opts.source);
    copyAddonFromLocal(sourceDir, addonDir, opts.onProgress);
    emitProgress(opts.onProgress, {
      phase: 'addon-materialized',
      message: `Copied addon from ${sourceDir}`,
      addonDir,
      source: 'local',
    });
    return { source: 'local', addonDir, sourceDir };
  }

  // Download path. Default the version to the CLI's own version (passed by the
  // command via opts.version); fail clearly if neither is available.
  const version = (opts.version ?? '').trim();
  if (version === '') {
    throw new Error(
      'No addon version to download. Pass --version <x.y.z> or --source <path> to install from a local copy.',
    );
  }
  const url = addonDownloadUrl(version);
  assertTrustedDownloadUrl(url); // fail-closed: github.com + https only.

  emitProgress(opts.onProgress, {
    phase: 'addon-downloading',
    message: `Downloading addon ${version} from ${url}`,
    url,
  });

  const fetchImpl = opts.fetchImpl ?? globalThis.fetch;
  const response = await fetchImpl(url);
  if (!response.ok) {
    throw new Error(
      `Failed to download addon ${version}: HTTP ${response.status} ${response.statusText} from ${url}. ` +
        `Verify the release v${version} exists, or use --source <path> for an offline/dev install.`,
    );
  }
  const archive = Buffer.from(await response.arrayBuffer());

  emitProgress(opts.onProgress, { phase: 'addon-extracting', message: 'Extracting addon zip' });
  const entries = parseZip(archive);
  extractAddonEntries(entries, projectPath, addonDir);

  emitProgress(opts.onProgress, {
    phase: 'addon-materialized',
    message: `Downloaded + extracted addon to ${addonDir}`,
    addonDir,
    source: 'download',
  });
  return { source: 'download', addonDir, downloadUrl: url };
}

/** Resolve + validate a `--source` directory: it must exist and contain plugin.cfg. */
function resolveAddonSourceDir(source: string): string {
  const resolved = path.resolve(source);
  // Accept either a path that IS addons/godot_mcp (contains plugin.cfg directly)
  // or a path that CONTAINS addons/godot_mcp.
  if (fs.existsSync(path.join(resolved, 'plugin.cfg'))) {
    return resolved;
  }
  const nested = path.join(resolved, 'addons', 'godot_mcp');
  if (fs.existsSync(path.join(nested, 'plugin.cfg'))) {
    return nested;
  }
  throw new Error(
    `--source ${resolved} does not contain a godot_mcp addon (no plugin.cfg found at it or at addons/godot_mcp/).`,
  );
}

/** Recursively copy a local addon dir into the project's addons/godot_mcp/ (replacing). */
function copyAddonFromLocal(sourceDir: string, addonDir: string, onProgress?: ProgressCallback): void {
  emitProgress(onProgress, { phase: 'addon-extracting', message: `Copying addon from ${sourceDir}` });
  // Replace any existing addon dir to keep the copy deterministic + idempotent.
  fs.rmSync(addonDir, { recursive: true, force: true });
  fs.mkdirSync(addonDir, { recursive: true });
  fs.cpSync(sourceDir, addonDir, { recursive: true });
  if (!fs.existsSync(path.join(addonDir, 'plugin.cfg'))) {
    throw new Error(`Copy completed but ${path.join(addonDir, 'plugin.cfg')} is missing.`);
  }
}

/**
 * Write the zip entries that live under `addons/godot_mcp/` into the project. The
 * release zip expands to `addons/godot_mcp/...`, so entries are written relative
 * to the project root. Path-safety: every resolved target MUST stay inside the
 * project's addons/godot_mcp dir (reject zip-slip `../` traversal). The target
 * addon dir is replaced so the install is deterministic + idempotent.
 */
function extractAddonEntries(entries: ZipEntry[], projectPath: string, addonDir: string): void {
  const addonReal = path.resolve(addonDir);
  // Stage into a temp sibling, then swap, so a mid-extract failure leaves the
  // existing addon intact.
  const staging = path.resolve(projectPath, `.godot-mcp-addon-staging-${process.pid}-${Date.now()}`);
  fs.rmSync(staging, { recursive: true, force: true });
  fs.mkdirSync(staging, { recursive: true });

  let wrotePluginCfg = false;
  try {
    for (const entry of entries) {
      // Only the addons/godot_mcp/ subtree is relevant.
      const prefix = 'addons/godot_mcp/';
      if (!entry.path.startsWith(prefix)) continue;
      const rel = entry.path.slice(prefix.length);
      if (rel === '') continue; // the dir entry itself

      const target = path.resolve(staging, rel);
      // Zip-slip guard: target must remain under staging.
      if (target !== staging && !target.startsWith(staging + path.sep)) {
        throw new Error(`Refusing to extract entry escaping the addon dir: ${entry.path}`);
      }
      if (entry.isDirectory) {
        fs.mkdirSync(target, { recursive: true });
        continue;
      }
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, entry.bytes);
      if (rel === 'plugin.cfg') wrotePluginCfg = true;
    }

    if (!wrotePluginCfg) {
      throw new Error(
        'Downloaded addon zip did not contain addons/godot_mcp/plugin.cfg — the archive is not a valid godot_mcp addon release.',
      );
    }

    // Swap staging -> addonDir.
    fs.rmSync(addonReal, { recursive: true, force: true });
    fs.mkdirSync(path.dirname(addonReal), { recursive: true });
    fs.renameSync(staging, addonReal);
  } finally {
    // Best-effort cleanup if the swap didn't consume staging.
    fs.rmSync(staging, { recursive: true, force: true });
  }
}

/**
 * Find the consumer's `.csproj` (the one Godot compiles the addon into) and add
 * the two addon NuGet PackageReferences idempotently. The csproj is the single
 * `*.csproj` at the project root (the `--dotnet` scaffold writes
 * `<AssemblyName>.csproj` there). When there is no csproj (a GDScript-only
 * project), the patch is skipped with a warning — the addon's C# cannot compile
 * without one, but that is the consumer's project shape to fix.
 */
function patchConsumerCsproj(projectPath: string, warnings: string[]): CsprojPatchOutcome {
  const csprojPath = findConsumerCsproj(projectPath);
  if (csprojPath === null) {
    warnings.push(
      'No .csproj found at the project root — the godot_mcp addon is C# and needs a Godot.NET.Sdk project. ' +
        `Create one (e.g. \`godot-cli create-project --dotnet\`) and add the ${ADDON_PACKAGE_REFERENCES.map((r) => r.id).join(' + ')} PackageReferences.`,
    );
    return { changed: false, packages: [] };
  }

  const before = fs.readFileSync(csprojPath, 'utf-8');
  const patched = addAddonPackageReferences(before);
  if (patched.changed) {
    fs.writeFileSync(csprojPath, patched.text);
  }

  return {
    csprojPath,
    changed: patched.changed,
    packages: patched.changes.map((c) => ({
      id: c.id,
      action: c.action,
      version: c.version,
    })),
  };
}

/** The single `*.csproj` directly at the project root, or null when none/ambiguous. */
function findConsumerCsproj(projectPath: string): string | null {
  let names: string[];
  try {
    names = fs.readdirSync(projectPath).filter((n) => n.toLowerCase().endsWith('.csproj'));
  } catch {
    return null;
  }
  if (names.length === 0) return null;
  // A scaffolded Godot project has exactly one root csproj. If there are several,
  // pick deterministically (first alphabetically) — better than failing the whole
  // install; a warning is not added because all root csprojs compile into the same
  // Godot assembly and would all need the pins.
  names.sort();
  return path.join(projectPath, names[0]);
}

/**
 * Disable the `godot_mcp` addon in a project's `project.godot`
 * `[editor_plugins]` `enabled` array. Idempotent: disabling an already-absent
 * plugin returns `changed: false`. Does NOT delete addon files.
 */
export async function removePlugin(opts: RemovePluginOptions): Promise<RemovePluginResult> {
  const warnings: string[] = [];

  try {
    const validated = requireGodotProject(opts?.godotProjectPath);
    if (!validated.ok) {
      return {
        kind: 'failure',
        success: false,
        projectGodotPath: validated.projectGodotPath,
        warnings,
        error: validated.error,
      };
    }
    const { projectPath, projectGodotPath } = validated;

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Disabling godot_mcp addon for ${projectPath}`,
    });

    const text = fs.readFileSync(projectGodotPath, 'utf-8');
    const result = togglePluginInText(text, false, GODOT_MCP_PLUGIN_PATH);

    if (result.kind === 'changed') {
      fs.writeFileSync(projectGodotPath, result.text);
      emitProgress(opts.onProgress, {
        phase: 'manifest-patched',
        message: `Wrote ${projectGodotPath}`,
        manifestPath: projectGodotPath,
      });
    }

    emitProgress(opts.onProgress, { phase: 'done', message: 'godot_mcp addon disabled.' });

    return {
      kind: 'success',
      success: true,
      changed: result.kind === 'changed',
      projectGodotPath,
      enabledPlugins: result.enabled,
      warnings,
    };
  } catch (err: unknown) {
    return {
      kind: 'failure',
      success: false,
      warnings,
      error: err instanceof Error ? err : new Error(String(err)),
    };
  }
}

export { resolveProjectGodotPath };
