// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// Guards the suite's timeout budget invariant. The `cli/` suite was intermittently red
// under parallel load with bare `Test timed out in 5000ms` failures that every affected
// test passed in isolation: 17 test files spawn REAL `node` child processes, while
// vitest's DEFAULT 5 s per-test timeout sat BELOW the suite's own internal wait budgets
// (`runCliAsync` allowed a child 30 s). An inner budget that meets or exceeds the outer
// test timeout can never fire — the runner kills the test first and reports nothing
// useful, intermittently, as a function of machine load.
//
// These asserts keep the ordering intact, so a future edit that raises a helper budget
// (or lowers the test timeout) fails CI here instead of re-introducing the flake.

import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { TEST_TIMEOUT_MS, HOOK_TIMEOUT_MS, CLI_SPAWN_TIMEOUT_MS } from './helpers/timeouts.js';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CLI_ROOT = path.resolve(__dirname, '..');

describe('vitest timeout budgets', () => {
  it('every internal wait budget is strictly below the per-test timeout', () => {
    expect(
      CLI_SPAWN_TIMEOUT_MS,
      'runCliAsync must be able to kill a stalled child and report it BEFORE vitest ' +
        'kills the test — otherwise the failure is an opaque "Test timed out".',
    ).toBeLessThan(TEST_TIMEOUT_MS);
  });

  it('the per-test timeout leaves real headroom over a spawn round trip', () => {
    // A CLI smoke test costs ~1.5-3.5 s of spawn time on an idle machine; the observed
    // flaky failures overshot 5 s. Anything below ~15 s is back in flake territory.
    expect(TEST_TIMEOUT_MS).toBeGreaterThanOrEqual(15_000);
    expect(HOOK_TIMEOUT_MS).toBeGreaterThanOrEqual(10_000);
  });

  it('vitest.config.ts actually wires the shared budgets (they are not dead constants)', () => {
    // Source-text check rather than importing the config: importing it would pull vite
    // into the worker for no gain. Same technique the addon-parity suite uses to pin a
    // value against its real declaration site.
    const config = fs.readFileSync(path.join(CLI_ROOT, 'vitest.config.ts'), 'utf-8');
    expect(config).toMatch(/testTimeout:\s*TEST_TIMEOUT_MS/);
    expect(config).toMatch(/hookTimeout:\s*HOOK_TIMEOUT_MS/);
    expect(config).toMatch(/from\s+'\.\/tests\/helpers\/timeouts\.js'/);
  });

  it('the CLI spawn helper consumes the shared budget rather than a local literal', () => {
    const helper = fs.readFileSync(path.join(CLI_ROOT, 'tests', 'helpers', 'cli.ts'), 'utf-8');
    expect(helper).toMatch(/const\s+timeoutMs\s*=\s*CLI_SPAWN_TIMEOUT_MS/);
  });
});
