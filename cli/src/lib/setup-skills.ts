import * as fs from 'fs';
import * as path from 'path';
import { getAgentById, getAgentIds } from '../utils/agents.js';
import { buildGuideFiles, isRetiredSkillName, SKILL_METADATA } from '../utils/skill-guides.js';
import { emitProgress } from './progress.js';
import { requireExistingPath } from './validation.js';
import type { SetupSkillsOptions, SetupSkillsResult } from './types.js';

/**
 * Write the agent skills into the selected agent's `skillsPath` under a Godot project: the `godot-cli` skill (cold-start
 * workflow, RQL recipes, exit codes, `references/` for depth) and `ai-game-dev-app`. Generated LOCALLY from the CLI's
 * own data (`lib/cli-guide.ts`, also the source of the root --help): no server, no editor, no network.
 * Library-safe: no stdout noise, no `process.exit`, no throws past the public boundary.
 *
 * Per-tool skills are the addon's job (it generates them from its registered tools whenever the editor runs). The
 * `godot-mcp` / `godot-mcp-<family>` folders earlier versions wrote from a hand-written catalog are removed; anything
 * else under the skills folder is untouched.
 *
 * Idempotent: re-running rewrites the same bytes.
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

    const skillFiles = buildGuideFiles();
    const filesWritten: string[] = [];

    // `godot-mcp` / `godot-mcp-<family>` is this CLI's retired namespace (the addon never writes those names); a folder
    // there is removed only when it carries our generated-by marker, so a user's own skill of that name stays.
    const marker = `generated-by: ${SKILL_METADATA['generated-by']}`;
    const removed: string[] = [];
    if (fs.existsSync(skillsDir)) {
      for (const entry of fs.readdirSync(skillsDir, { withFileTypes: true })) {
        if (!entry.isDirectory() || !isRetiredSkillName(entry.name)) continue;
        const dir = path.join(skillsDir, entry.name);
        const skillMd = path.join(dir, 'SKILL.md');
        if (fs.existsSync(skillMd) && fs.readFileSync(skillMd, 'utf-8').includes(marker)) {
          fs.rmSync(dir, { recursive: true, force: true });
          removed.push(dir);
        }
      }
    }

    // An addon from before godot-cli-install writes its install guide over godot-cli/SKILL.md on every editor start.
    const addonGuides = path.join(projectPath, 'addons', 'godot_mcp', 'Runtime', 'Skills', 'Skill_Guides.cs');
    if (fs.existsSync(addonGuides) && fs.readFileSync(addonGuides, 'utf-8').includes('SkillId = "godot-cli";')) {
      warnings.push('The project\'s Godot-MCP addon is older and replaces the godot-cli skill whenever the editor starts: update it (godot-cli update).');
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
      removed,
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
