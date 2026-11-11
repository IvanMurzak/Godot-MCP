// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';
import { setupSkills } from '../src/lib/setup-skills.js';
import { EXIT_CODES } from '@baizor/gamedev-cli-core';
import { buildGuideFiles, EXIT_CODE_ACTIONS } from '../src/utils/skill-guides.js';
import { RQL_LANGUAGE, RQL_RECIPES } from '../src/lib/cli-guide.js';
import { getAgentById, agentRegistry } from '../src/utils/agents.js';

// ---------------------------------------------------------------------------
// CLI surface — help / list / error cases (no filesystem mutation needed)
// ---------------------------------------------------------------------------

describe('setup-skills command (CLI surface)', () => {
  it('shows help with --help', async () => {
    const { stdout, exitCode } = await runCliAsync(['setup-skills', '--help']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('setup-skills');
    expect(stdout).toContain('--list');
  });

  it('--list prints the agent/skills table and exits 0', async () => {
    const { stdout, exitCode } = await runCliAsync(['setup-skills', '--list']);
    expect(exitCode).toBe(0);
    expect(stdout).toContain('claude-code');
    // claude-code has a skills path; it should appear in the table
    expect(stdout).toContain('.claude/skills');
  });

  it('exits 1 with error when agent-id is missing', async () => {
    const { stdout, exitCode } = await runCliAsync(['setup-skills']);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Missing required argument');
  });

  it('exits 1 with error for unknown agent-id', async () => {
    const { stdout, exitCode } = await runCliAsync(['setup-skills', 'not-a-real-agent']);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('Unknown agent');
  });

  // Every registry agent WITHOUT a skillsPath must fail the no-skills path.
  const unskilledAgentIds = agentRegistry.filter((a) => !a.skillsPath).map((a) => a.id);

  it.each(unskilledAgentIds)('exits 1 when agent %s does not support skills', async (agentId) => {
    const { stdout, exitCode } = await runCliAsync(['setup-skills', agentId]);
    expect(exitCode).toBe(1);
    expect(stdout).toContain('does not support skills');
  });
});

// ---------------------------------------------------------------------------
// Skill content: the godot-cli cold-start skill, its references and the App guide
// ---------------------------------------------------------------------------

const FILES = ['godot-cli/SKILL.md', 'godot-cli/references/rql.md', 'godot-cli/references/install.md', 'ai-game-dev-app/SKILL.md'];
const skillMd = () => buildGuideFiles().find((f) => f.relativePath === 'godot-cli/SKILL.md')!.content;

describe('skill content', () => {
  it('emits the CLI skill, its on-demand references and the App guide, nothing per tool family', () => {
    expect(buildGuideFiles().map((f) => f.relativePath)).toEqual(FILES);
  });

  it('marks every skill for App exclusion and gives it a name + description', () => {
    for (const file of buildGuideFiles().filter((f) => f.relativePath.endsWith('SKILL.md'))) {
      const name = file.relativePath.split('/')[0];
      expect(file.content.startsWith(`---\nname: ${name}\ndescription: "`)).toBe(true);
      expect(file.content).toContain('metadata:\n  generated-by: godot-mcp\n  ai-game-dev: exclude\n---');
    }
  });

  it('shows every recipe; its RQL reference adds the language, not the recipes again (one source)', () => {
    for (const [query, what] of RQL_RECIPES) expect(skillMd()).toContain(`rql '${query}'  # ${what}`);
    const ref = buildGuideFiles().find((f) => f.relativePath === 'godot-cli/references/rql.md')!.content;
    for (const [query] of RQL_RECIPES) expect(ref).not.toContain(query);
    for (const line of RQL_LANGUAGE) expect(ref).toContain(line);
  });

  it('advises only on exit codes of the shared contract', () => {
    const contract = new Set(EXIT_CODES.map((e) => e.code));
    for (const code of Object.keys(EXIT_CODE_ACTIONS).map(Number)) {
      expect(contract.has(code as never)).toBe(true);
      expect(skillMd().split('\n').find((l) => l.startsWith('Exit codes: '))).toContain(` ${code} `);
    }
  });

  it('teaches the cold-start workflow, not the retired one', () => {
    expect(skillMd()).toContain('install-plugin');
    expect(skillMd()).not.toContain('rql-schema --input');
    expect(skillMd()).not.toMatch(/editor must be running/i);
  });

  it('scopes the description to editor work and names only check for code edits (naming tests sent agents into test fixes)', () => {
    const description = skillMd().split('\n').find((l) => l.startsWith('description: '))!;
    expect(description).toMatch(/^description: "Godot editor work with /);
    expect(description).toContain('Not needed to edit scripts: `godot-cli check` lists file:line errors.');
    expect(description).not.toMatch(/\btest\b/);
  });

  it('stays short (the agent re-reads every byte each turn) and uses neutral example names', () => {
    expect(Buffer.byteLength(skillMd())).toBeLessThan(2800);
    const all = buildGuideFiles().map((f) => f.content).join('\n');
    expect(all).not.toMatch(/\b(Arena|Crate|Platform|Player|speed|Level1|EnemyStats|GameMath)\b/);
  });
});

describe('setupSkills generation + idempotency', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-skills-test-'));
  });

  afterEach(() => {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  // Every agent in the registry that defines a skillsPath must generate cleanly.
  // Derived from the registry so new skilled agents are covered automatically.
  const skilledAgentIds = agentRegistry.filter((a) => a.skillsPath).map((a) => a.id);

  it.each(skilledAgentIds)('generates skill files for %s under its skillsPath', async (agentId) => {
    const agent = getAgentById(agentId);
    expect(agent).toBeDefined();
    expect(agent!.skillsPath).toBeTruthy();

    const result = await setupSkills({ agentId, godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    const expectedDir = path.join(tmpDir, agent!.skillsPath!);
    expect(result.skillsDir).toBe(expectedDir);
    expect(result.filesWritten.length).toBe(FILES.length);

    for (const file of result.filesWritten) {
      expect(fs.existsSync(file)).toBe(true);
      expect(file.startsWith(expectedDir)).toBe(true);
    }
    expect(fs.existsSync(path.join(expectedDir, 'godot-cli', 'SKILL.md'))).toBe(true);
  });

  const unskilledAgentIds = agentRegistry.filter((a) => !a.skillsPath).map((a) => a.id);

  it.each(unskilledAgentIds)('fails for agent %s without skills support', async (agentId) => {
    const result = await setupSkills({ agentId, godotProjectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toContain('does not support skills');
    }
  });

  it('fails for an unknown agent id', async () => {
    const result = await setupSkills({ agentId: 'nope', godotProjectPath: tmpDir });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toContain('Unknown agent');
    }
  });

  it('fails when the project path does not exist', async () => {
    const result = await setupSkills({
      agentId: 'claude-code',
      godotProjectPath: path.join(tmpDir, 'does-not-exist-xyz'),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toContain('does not exist');
    }
  });

  it('is idempotent — re-running writes identical bytes', async () => {
    const first = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(first.kind).toBe('success');
    if (first.kind !== 'success') return;

    const snapshot = first.filesWritten.map((f) => fs.readFileSync(f, 'utf-8'));

    const second = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(second.kind).toBe('success');
    if (second.kind !== 'success') return;

    expect(second.filesWritten).toEqual(first.filesWritten);
    second.filesWritten.forEach((f, i) => {
      expect(fs.readFileSync(f, 'utf-8')).toBe(snapshot[i]);
    });
  });

  it('removes the retired godot-mcp* family skills, leaving foreign dirs', async () => {
    const skillsDir = path.join(tmpDir, '.claude', 'skills');
    const ours = '---\nname: x\nmetadata:\n  generated-by: godot-mcp\n---\nOLD\n';
    for (const [name, body] of [['godot-mcp', ours], ['godot-mcp-node', ours], ['godot-mcp-notes', 'MINE\n'], ['some-other-skill', 'OLD\n'], ['rql-query', ours]]) {
      fs.mkdirSync(path.join(skillsDir, name), { recursive: true });
      fs.writeFileSync(path.join(skillsDir, name, 'SKILL.md'), body);
    }
    const result = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(result.kind).toBe('success');
    expect(result.kind === 'success' && result.removed.map((d) => path.basename(d)).sort()).toEqual(['godot-mcp', 'godot-mcp-node']);
    expect(fs.existsSync(path.join(skillsDir, 'godot-mcp-notes', 'SKILL.md'))).toBe(true);
    expect(fs.existsSync(path.join(skillsDir, 'godot-mcp'))).toBe(false);
    expect(fs.readFileSync(path.join(skillsDir, 'some-other-skill', 'SKILL.md'), 'utf-8')).toBe('OLD\n');
    expect(fs.existsSync(path.join(skillsDir, 'rql-query', 'SKILL.md'))).toBe(true);
  });

  it('warns when the project\'s addon still writes its own godot-cli skill', async () => {
    const guides = path.join(tmpDir, 'addons', 'godot_mcp', 'Runtime', 'Skills');
    fs.mkdirSync(guides, { recursive: true });
    fs.writeFileSync(path.join(guides, 'Skill_Guides.cs'), 'public const string SkillId = "godot-cli";');
    const result = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(result.kind === 'success' && result.warnings.join()).toContain('godot-cli update');
    fs.writeFileSync(path.join(guides, 'Skill_Guides.cs'), 'public const string SkillId = "godot-cli-install";');
    const current = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(current.kind === 'success' && current.warnings).toEqual([]);
  });

  it('overwrites stale content on re-run (regeneration, not append)', async () => {
    const first = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(first.kind).toBe('success');
    if (first.kind !== 'success') return;

    const skillFile = path.join(first.skillsDir, 'godot-cli', 'SKILL.md');
    fs.writeFileSync(skillFile, 'STALE\n');

    const second = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(second.kind).toBe('success');
    const after = fs.readFileSync(skillFile, 'utf-8');
    expect(after).not.toContain('STALE');
    expect(after).toContain('# godot-cli');
  });
});
