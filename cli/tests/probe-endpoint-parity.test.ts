// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// CI cross-check: the CLI's health-probe route (`src/utils/probe.ts` § PING_ENDPOINT)
// MUST target the REST surface the addon's `ping` tool actually registers on.
//
// `McpPluginBuilder` partitions tools by `ToolType` into two DISJOINT registries —
// Standard tools answer on `/api/tools/<name>`, System tools on `/api/system-tools/<name>`
// — so flipping `Tool_Ping`'s `ToolType` silently MOVES the route and every hardcoded
// caller 404s/500s. That regression shipped once: when `ping` became a System tool
// (owner ruling 2026-07-25, for parity with Unity-MCP), `probe.ts`, the runtime-harness
// workflow and the CLAUDE.md runbook still POSTed `/api/tools/ping` — all five
// `runtime-harness-4-*` CI legs went red, and `godot-cli status` / `wait-for-ready`
// would have reported a healthy editor as unreachable.
//
// The pre-existing `status` / `wait-for-ready` tests CANNOT catch this: their stub HTTP
// server answers `pong` to ANY path, so they are vacuous with respect to the route.
// These tests pin it three ways — against the addon source, against a real request, and
// against the CI harness workflow that actually went red.
//
// Runs in the existing `test-cli` CI leg (Node 20 & 22) — no Godot binary, no .NET build,
// because it only reads `.cs` / `.yml` SOURCE text.

import { describe, it, expect } from 'vitest';
import * as path from 'path';
import * as fs from 'fs';
import http from 'http';
import { fileURLToPath } from 'url';
import { PING_ENDPOINT, probe } from '../src/utils/probe.js';
import { stripNonCode } from '../src/utils/skills.js';

// cli/tests/ -> cli/ -> <repo root> (the dir that holds both `cli/` and `addons/`).
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '..', '..');

const PING_TOOL_SOURCE = path.join(
  REPO_ROOT, 'addons', 'godot_mcp', 'Runtime', 'Tools', 'Tool_Ping.cs',
);
const HARNESS_WORKFLOW = path.join(
  REPO_ROOT, '.github', 'workflows', 'test_godot_runtime_harness.yml',
);

/** The REST prefix McpPluginBuilder routes each `McpToolType` onto. */
const SURFACE_FOR_TOOL_TYPE: Record<string, string> = {
  System: '/api/system-tools',
  Standard: '/api/tools',
};

/**
 * Read `Tool_Ping.cs`'s DECLARED `ToolType`, ignoring comments and strings.
 * Stripping is essential: the file's own XML doc comment discusses
 * `McpToolType.System` in prose, so a raw regex would match the documentation
 * rather than the attribute and pass even if the real declaration were dropped.
 */
function declaredPingToolType(): string | null {
  const code = stripNonCode(fs.readFileSync(PING_TOOL_SOURCE, 'utf8'), true);
  const matches = [...code.matchAll(/ToolType\s*=\s*McpToolType\.(\w+)/g)];
  if (matches.length !== 1) return null;
  return matches[0][1];
}

describe('probe endpoint ⇄ addon `ping` ToolType parity (CI cross-check)', () => {
  it('the addon `Tool_Ping.cs` source exists (guards against a vacuous scan)', () => {
    // If the addon layout moves, every scan below would silently find nothing and
    // the parity asserts could pass vacuously — assert the file is real first.
    expect(
      fs.existsSync(PING_TOOL_SOURCE),
      `expected the ping tool source to exist: ${PING_TOOL_SOURCE}`,
    ).toBe(true);
  });

  it('declares `ping` with exactly one recognised ToolType', () => {
    // Non-vacuity guard for the parity assert below: a missing/duplicated/renamed
    // ToolType must fail loudly here rather than silently skip the comparison.
    const toolType = declaredPingToolType();
    expect(
      toolType,
      'expected exactly one `ToolType = McpToolType.<X>` declaration in Tool_Ping.cs',
    ).not.toBeNull();
    expect(Object.keys(SURFACE_FOR_TOOL_TYPE)).toContain(toolType);
  });

  it('PING_ENDPOINT targets the surface the addon ToolType implies', () => {
    // THE regression assert. Flip `Tool_Ping`'s ToolType without moving the CLI
    // probe (or vice versa) and this fails locally, in `npm test`, before CI.
    const toolType = declaredPingToolType()!;
    const expectedPrefix = SURFACE_FOR_TOOL_TYPE[toolType];
    expect(PING_ENDPOINT).toBe(`${expectedPrefix}/ping`);
  });

  it('probe() actually POSTs to PING_ENDPOINT (closes the stub-server path vacuity)', async () => {
    // The `status` / `wait-for-ready` stubs answer ANY path, so only a stub that
    // RECORDS the request path can prove the probe hits the intended route.
    const seen: { url?: string; method?: string } = {};
    const server = http.createServer((req, res) => {
      seen.url = req.url;
      seen.method = req.method;
      req.on('data', () => {});
      req.on('end', () => {
        res.writeHead(200, { 'Content-Type': 'application/json' });
        res.end(JSON.stringify({ status: 'success', structured: { result: 'pong' } }));
      });
    });

    const baseUrl: string = await new Promise((resolve) => {
      server.listen(0, '127.0.0.1', () => {
        const addr = server.address();
        const port = typeof addr === 'object' && addr ? addr.port : 0;
        resolve(`http://127.0.0.1:${port}`);
      });
    });

    try {
      const result = await probe(baseUrl, { 'Content-Type': 'application/json' }, 5000);
      expect(result.ok).toBe(true);
      expect(seen.method).toBe('POST');
      expect(seen.url).toBe(PING_ENDPOINT);
    } finally {
      await new Promise<void>((r) => server.close(() => r()));
    }
  });

  it('the runtime-harness CI workflow probes the same route as the CLI', () => {
    // This workflow's hardcoded curl is the site that actually went red across all
    // five Godot legs — pin it to the same source of truth so the two cannot drift.
    const workflow = fs.readFileSync(HARNESS_WORKFLOW, 'utf8');
    expect(workflow).toContain(`${PING_ENDPOINT}"`);
    expect(workflow).not.toContain('/api/tools/ping');
  });
});
