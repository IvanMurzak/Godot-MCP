import * as fs from 'fs';
import * as path from 'path';
import { getAgentById, getAgentIds } from '../utils/agents.js';
import { buildSkillFiles } from '../utils/skills.js';
import { emitProgress } from './progress.js';
import { requireExistingPath } from './validation.js';
import type { SetupSkillsOptions, SetupSkillsResult } from './types.js';

/**
 * Generate AI-agent skill files for a Godot project under the selected agent's
 * `skillsPath`. Library-safe: no stdout noise, no `process.exit`, no throws past
 * the public boundary.
 *
 * Unlike the Unity CLI (which POSTs to a running editor's skill-generate HTTP
 * endpoint), this generates the files LOCALLY from the Godot-MCP tool-family
 * catalog — no server and no running editor are required. The content describes
 * the `godot_mcp` addon's tool families (`Tool_Node`, `Tool_Scene`, …), NOT
 * Unity tool names.
 *
 * Idempotent: re-running rewrites the same bytes and prunes any
 * `godot-mcp*` skill directories the current catalog no longer emits (so a
 * shrunk catalog does not leave stale family dirs behind). A
 * `SKILL.md`-per-family directory is written under
 * `<projectPath>/<agent.skillsPath>`.
 */
export async function setupSkills(opts: SetupSkillsOptions): Promise<SetupSkillsResult> {
  const warnings: string[] = [];

  try {
    if (!opts || typeof opts.agentId !== 'string' || opts.agentId.length === 0) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(`agentId is required. Available agent IDs: ${getAgentIds().join(', ')}`),
      };
    }

    const agent = getAgentById(opts.agentId);
    if (!agent) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(
          `Unknown agent: "${opts.agentId}". Available agent IDs: ${getAgentIds().join(', ')}`,
        ),
      };
    }

    if (!agent.skillsPath) {
      return {
        kind: 'failure',
        success: false,
        warnings,
        error: new Error(`Agent "${agent.name}" does not support skills.`),
      };
    }

    // Resolve project path. If supplied it must exist; otherwise fall back to cwd.
    let projectPath: string;
    if (opts.godotProjectPath) {
      const validated = requireExistingPath(opts.godotProjectPath);
      if (!validated.ok) {
        return { kind: 'failure', success: false, warnings, error: validated.error };
      }
      projectPath = validated.projectPath;
    } else {
      projectPath = path.resolve(process.cwd());
    }

    const skillsDir = path.join(projectPath, agent.skillsPath);

    emitProgress(opts.onProgress, {
      phase: 'start',
      message: `Generating ${agent.name} skills in ${skillsDir}`,
    });

    const skillFiles = buildSkillFiles();
    const filesWritten: string[] = [];

    // Prune orphaned `godot-mcp*` family dirs the current catalog no longer
    // emits, so a shrunk catalog does not leave stale SKILL.md dirs behind.
    // Only our own generated dirs (top-level segment of each emitted path) are
    // ever removed — unrelated content under skillsDir is untouched.
    const emittedDirs = new Set(skillFiles.map((f) => f.relativePath.split('/')[0]));
    if (fs.existsSync(skillsDir)) {
      for (const entry of fs.readdirSync(skillsDir, { withFileTypes: true })) {
        if (
          entry.isDirectory() &&
          (entry.name === 'godot-mcp' || entry.name.startsWith('godot-mcp-')) &&
          !emittedDirs.has(entry.name)
        ) {
          fs.rmSync(path.join(skillsDir, entry.name), { recursive: true, force: true });
        }
      }
    }

    for (const file of skillFiles) {
      const absPath = path.join(skillsDir, file.relativePath);
      const dir = path.dirname(absPath);
      if (!fs.existsSync(dir)) {
        fs.mkdirSync(dir, { recursive: true });
      }
      // LF line endings; trailing newline. Deterministic → idempotent re-write.
      const content = file.content.endsWith('\n') ? file.content : file.content + '\n';
      fs.writeFileSync(absPath, content);
      filesWritten.push(absPath);
      emitProgress(opts.onProgress, {
        phase: 'manifest-patched',
        message: `Wrote ${absPath}`,
        manifestPath: absPath,
      });
    }

    emitProgress(opts.onProgress, {
      phase: 'done',
      message: `${agent.name} skills generated (${filesWritten.length} files).`,
    });

    return {
      kind: 'success',
      success: true,
      agentId: agent.id,
      skillsDir,
      filesWritten,
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
