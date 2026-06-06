import * as fs from 'fs';
import {
  GODOT_MCP_PLUGIN_PATH,
  projectGodotPath as resolveProjectGodotPath,
  togglePluginInText,
} from '../utils/project-godot.js';
import { emitProgress } from './progress.js';
import { requireGodotProject } from './validation.js';
import type {
  InstallPluginOptions,
  InstallPluginResult,
  RemovePluginOptions,
  RemovePluginResult,
} from './types.js';

/**
 * Enable the `godot_mcp` addon in a project's `project.godot` `[editor_plugins]`
 * `enabled` array. Library-safe: no stdout noise, no process.exit, no throws
 * past the public boundary.
 *
 * v1 is intentionally minimal — it only flips the project.godot plugin-enable
 * state. It does NOT copy addon files or patch the consumer `.csproj` with the
 * NuGet PackageReferences the addon needs (that is the consumer's responsibility
 * per the addon's install docs); a warning surfaces that requirement.
 *
 * Idempotent: enabling an already-enabled plugin returns `changed: false`.
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
      message: `Enabling godot_mcp addon for ${projectPath}`,
    });

    const text = fs.readFileSync(projectGodotPath, 'utf-8');
    const result = togglePluginInText(text, true, GODOT_MCP_PLUGIN_PATH);

    if (result.kind === 'changed') {
      fs.writeFileSync(projectGodotPath, result.text);
      emitProgress(opts.onProgress, {
        phase: 'manifest-patched',
        message: `Wrote ${projectGodotPath}`,
        manifestPath: projectGodotPath,
      });
    }

    warnings.push(
      'Ensure the project .csproj declares the com.IvanMurzak.ReflectorNet and com.IvanMurzak.McpPlugin PackageReferences the addon needs (see the addon README).',
    );

    emitProgress(opts.onProgress, { phase: 'done', message: 'godot_mcp addon enabled.' });

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
