import * as fs from 'fs';
import * as path from 'path';
import {
  EXTENSIONS_CATALOG,
  findExtension,
  hasVersion,
  type ExtensionDescriptor,
} from '../utils/extensions-catalog.js';
import {
  planExtensionInstall,
  CsprojParseError,
  type ExtensionInstallPlan,
} from '../utils/extension-install.js';
import { emitProgress } from './progress.js';
import { requireGodotProject } from './validation.js';
import type {
  InstallExtensionOptions,
  InstallExtensionResult,
} from './types.js';

/**
 * Install (or update) a Godot-MCP extension into a consumer Godot C# project, as a
 * REAL installer that is behaviorally identical to the in-editor dock's
 * `ExtensionInstaller`:
 *
 *  1. Resolve `extensionId` to a descriptor in the SHARED catalog
 *     (`addons/godot_mcp/extensions.catalog.json`, mirrored by `EXTENSIONS_CATALOG`).
 *  2. Locate the consumer `.csproj` at the project root.
 *  3. Read-modify-write a `<PackageReference Include="<packageId>" Version="<version>" />`
 *     into it — added when absent, version-bumped only when the catalog (or `--version`)
 *     pins a NEWER version, and a no-op when already up to date.
 *  4. Tell the caller a rebuild is required (Godot has no programmatic restore).
 *
 * Library-safe: no stdout noise, no `process.exit`, no throws past the public boundary;
 * returns a `{ kind: 'success' | 'failure' }` union. Idempotent: a re-run that finds the
 * reference present at an equal/newer version reports `already-up-to-date` and makes no
 * write. Mirrors `installPlugin`'s shape exactly so the app can adopt it the same way.
 */
export async function installExtension(
  opts: InstallExtensionOptions,
): Promise<InstallExtensionResult> {
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
    const { projectPath } = validated;

    const catalog = opts.catalog ?? EXTENSIONS_CATALOG;
    const resolved = findExtension(opts.extensionId, catalog);
    if (resolved === null) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(unknownExtensionMessage(opts.extensionId, catalog)),
      };
    }

    // Apply the optional --version override (drives both the pin written and the bump decision).
    const descriptor = applyVersionOverride(resolved, opts.version, warnings);

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Installing extension ${descriptor.packageId}${hasVersion(descriptor) ? ` ${descriptor.version}` : ''} into ${projectPath}`,
    });

    const csprojPath = findConsumerCsproj(projectPath, warnings);
    if (csprojPath === null) {
      const message =
        'No project .csproj found at the project root — open this addon inside a Godot C# project to install extensions. ' +
        `Create one (e.g. \`godot-cli create-project --dotnet\`) and add the ${descriptor.packageId} PackageReference.`;
      emitProgress(opts.onProgress, { phase: 'done', message: 'No project .csproj — nothing installed.' });
      return {
        kind: 'success',
        success: true,
        outcome: 'no-project',
        changed: false,
        rebuildRequired: false,
        extensionId: opts.extensionId,
        packageId: descriptor.packageId,
        fromVersion: null,
        toVersion: hasVersion(descriptor) ? descriptor.version : null,
        message,
        warnings,
      };
    }

    const before = fs.readFileSync(csprojPath, 'utf-8');
    let plan: ExtensionInstallPlan;
    try {
      plan = planExtensionInstall(descriptor, before);
    } catch (err: unknown) {
      if (err instanceof CsprojParseError) {
        return {
          kind: 'failure',
          success: false,
          warnings,
          error: new Error(`Could not edit the project .csproj: ${err.message}`),
        };
      }
      throw err;
    }

    if (plan.action === 'noop') {
      emitProgress(opts.onProgress, { phase: 'done', message: `${descriptor.packageId} is already up to date.` });
      return {
        kind: 'success',
        success: true,
        outcome: 'already-up-to-date',
        changed: false,
        rebuildRequired: false,
        extensionId: opts.extensionId,
        packageId: descriptor.packageId,
        fromVersion: plan.fromVersion,
        toVersion: plan.toVersion,
        csprojPath,
        message: `${descriptor.name} is already installed and up to date.`,
        warnings,
      };
    }

    fs.writeFileSync(csprojPath, plan.resultingCsproj);
    emitProgress(opts.onProgress, {
      phase: 'csproj-patched',
      message: `${plan.action === 'add' ? 'Added' : 'Updated'} ${descriptor.packageId} in ${csprojPath}`,
      csprojPath,
    });
    emitProgress(opts.onProgress, { phase: 'done', message: 'Extension installed — rebuild solutions to restore.' });

    const message =
      plan.action === 'add'
        ? // Report the version actually written (the pin, or the "*" float marker), not the descriptor's
          // pin presence — an unpinned add now writes Version="*".
          `Added ${descriptor.packageId}${plan.toVersion ? ` ${plan.toVersion}` : ''}. Rebuild solutions to restore and compile the extension.`
        : `Updated ${descriptor.packageId} to ${plan.toVersion}. Rebuild solutions to restore the new version.`;

    return {
      kind: 'success',
      success: true,
      outcome: plan.action === 'add' ? 'added' : 'updated',
      changed: true,
      rebuildRequired: true,
      extensionId: opts.extensionId,
      packageId: descriptor.packageId,
      fromVersion: plan.fromVersion,
      toVersion: plan.toVersion,
      csprojPath,
      message,
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

/** Return a descriptor with the `--version` override applied (empty/whitespace override is ignored). */
function applyVersionOverride(
  descriptor: ExtensionDescriptor,
  version: string | undefined,
  warnings: string[],
): ExtensionDescriptor {
  const v = (version ?? '').trim();
  if (v === '') return descriptor;
  if (descriptor.version !== null && descriptor.version !== v) {
    warnings.push(
      `--version ${v} overrides the catalog pin ${descriptor.packageId}@${descriptor.version} for this install.`,
    );
  }
  return { ...descriptor, version: v };
}

/** A clear "unknown extension" error that lists what IS installable (or says the catalog is empty). */
function unknownExtensionMessage(
  id: string,
  catalog: readonly ExtensionDescriptor[],
): string {
  if (catalog.length === 0) {
    return (
      `Unknown extension "${id}". The Godot-MCP extension catalog is currently empty ` +
      '(no extension packages have been published yet), so there is nothing to install.'
    );
  }
  const available = catalog.map((d) => d.packageId).join(', ');
  return `Unknown extension "${id}". Available extensions: ${available}.`;
}

/**
 * Find the consumer's `.csproj` (the one Godot compiles every `.cs` into) — the single
 * `*.csproj` at the project root. When several exist, pick the first alphabetically and
 * warn (they all compile into the same Godot assembly). Returns null when none exist
 * (a GDScript-only project). Mirrors `install-plugin`'s `findConsumerCsproj`.
 */
function findConsumerCsproj(projectPath: string, warnings: string[]): string | null {
  let names: string[];
  try {
    names = fs.readdirSync(projectPath).filter((n) => n.toLowerCase().endsWith('.csproj'));
  } catch {
    return null;
  }
  if (names.length === 0) return null;
  names.sort();
  if (names.length > 1) {
    warnings.push(
      `Multiple .csproj files found at the project root (${names.join(', ')}); patched ${names[0]}. ` +
        'If a different one is the Godot project, add the extension PackageReference to it as well.',
    );
  }
  return path.join(projectPath, names[0]);
}
