import * as fs from 'fs';
import * as path from 'path';

/**
 * Small, side-effect-free input-validation helpers shared by the
 * library-facing API functions. Each helper returns a discriminated union so
 * call sites can pattern-match without throwing across the public boundary.
 */

export type ValidatedPath =
  | { ok: true; projectPath: string }
  | { ok: false; error: Error };

export type ValidatedGodotProject =
  | { ok: true; projectPath: string; projectGodotPath: string }
  | { ok: false; projectGodotPath?: string; error: Error };

/** Require a non-empty string and resolve it to an absolute path. */
export function requireProjectPath(raw: unknown): ValidatedPath {
  if (typeof raw !== 'string' || raw.length === 0) {
    return {
      ok: false,
      error: new Error('godotProjectPath is required and must be a non-empty string.'),
    };
  }
  return { ok: true, projectPath: path.resolve(raw) };
}

/**
 * Require a non-empty path AND that the path hosts a Godot project (identified
 * by the presence of `project.godot`).
 */
export function requireGodotProject(raw: unknown): ValidatedGodotProject {
  const outer = requireProjectPath(raw);
  if (!outer.ok) return outer;
  const projectGodotPath = path.join(outer.projectPath, 'project.godot');
  if (!fs.existsSync(projectGodotPath)) {
    return {
      ok: false,
      projectGodotPath,
      error: new Error(`Not a valid Godot project (missing project.godot): ${outer.projectPath}`),
    };
  }
  return { ok: true, projectPath: outer.projectPath, projectGodotPath };
}

/** Require that the given path exists on disk (need not host a project.godot). */
export function requireExistingPath(raw: unknown): ValidatedPath {
  const outer = requireProjectPath(raw);
  if (!outer.ok) return outer;
  if (!fs.existsSync(outer.projectPath)) {
    return {
      ok: false,
      error: new Error(`Project path does not exist: ${outer.projectPath}`),
    };
  }
  return outer;
}
