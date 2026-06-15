// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { runCliAsync } from './helpers/cli.js';
import { setupSkills } from '../src/lib/setup-skills.js';
import { buildSkillFiles, SKILL_FAMILIES } from '../src/utils/skills.js';
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
// Skill content catalog
// ---------------------------------------------------------------------------

describe('skill content (Godot tool families)', () => {
  it('emits one root file plus one per family', () => {
    const files = buildSkillFiles();
    expect(files.length).toBe(SKILL_FAMILIES.length + 1);
    expect(files[0].relativePath).toBe('godot-mcp/SKILL.md');
  });

  it('describes Godot tool families, not Unity tool names', () => {
    const all = buildSkillFiles().map((f) => f.content).join('\n');
    // Godot tool families / tool names must be present
    expect(all).toContain('node-create');
    expect(all).toContain('scene-open');
    expect(all).toContain('resource-find');
    expect(all).toContain('screenshot-viewport');
    expect(all).toContain('reflection-method-call');
    expect(all).toContain('godot_mcp');
    // Unity-specific surface must NOT leak in
    expect(all).not.toContain('unity-skill-generate');
    expect(all.toLowerCase()).not.toContain('gameobject');
    expect(all).not.toContain('MenuItem');
  });

  it('every skill file has valid YAML frontmatter with a name + description', () => {
    for (const file of buildSkillFiles()) {
      expect(file.content.startsWith('---\n')).toBe(true);
      const fm = file.content.slice(4, file.content.indexOf('\n---', 4));
      expect(fm).toMatch(/^name: \S/m);
      expect(fm).toMatch(/^description: /m);
    }
  });
});

// ---------------------------------------------------------------------------
// Generation + idempotency (filesystem)
// ---------------------------------------------------------------------------

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
    expect(result.filesWritten.length).toBe(SKILL_FAMILIES.length + 1);

    // Root + every family file exist on disk under the agent's skills path.
    for (const file of result.filesWritten) {
      expect(fs.existsSync(file)).toBe(true);
      expect(file.startsWith(expectedDir)).toBe(true);
    }
    expect(fs.existsSync(path.join(expectedDir, 'godot-mcp', 'SKILL.md'))).toBe(true);
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

  it('prunes orphaned godot-mcp* family dirs on re-run, leaving foreign dirs', async () => {
    const first = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(first.kind).toBe('success');
    if (first.kind !== 'success') return;

    // A stale family dir the catalog no longer emits, plus a foreign dir.
    const orphanDir = path.join(first.skillsDir, 'godot-mcp-obsolete');
    const foreignDir = path.join(first.skillsDir, 'some-other-skill');
    fs.mkdirSync(orphanDir, { recursive: true });
    fs.writeFileSync(path.join(orphanDir, 'SKILL.md'), 'STALE\n');
    fs.mkdirSync(foreignDir, { recursive: true });
    fs.writeFileSync(path.join(foreignDir, 'SKILL.md'), 'KEEP\n');

    const second = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(second.kind).toBe('success');

    // The orphaned godot-mcp* dir is gone; the unrelated dir is untouched.
    expect(fs.existsSync(orphanDir)).toBe(false);
    expect(fs.existsSync(foreignDir)).toBe(true);
    // Current catalog dirs still present.
    expect(fs.existsSync(path.join(first.skillsDir, 'godot-mcp', 'SKILL.md'))).toBe(true);
  });

  it('overwrites stale content on re-run (regeneration, not append)', async () => {
    const first = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(first.kind).toBe('success');
    if (first.kind !== 'success') return;

    const rootFile = path.join(first.skillsDir, 'godot-mcp', 'SKILL.md');
    fs.writeFileSync(rootFile, 'STALE\n');

    const second = await setupSkills({ agentId: 'claude-code', godotProjectPath: tmpDir });
    expect(second.kind).toBe('success');
    const after = fs.readFileSync(rootFile, 'utf-8');
    expect(after).not.toContain('STALE');
    expect(after).toContain('# Godot-MCP');
  });
});
