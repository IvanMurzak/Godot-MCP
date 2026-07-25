// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

import { defineConfig } from 'vitest/config';
import { TEST_TIMEOUT_MS, HOOK_TIMEOUT_MS } from './tests/helpers/timeouts.js';

// The suite spawns real `node` child processes in 17 of its test files and runs those
// files in PARALLEL, so vitest's default 5 s per-test timeout sat BELOW the suite's own
// internal wait budgets and fired first under load — an intermittent `Test timed out in
// 5000ms` that always passed in isolation. See `tests/helpers/timeouts.ts` for the full
// rationale and the budget invariant (`tests/test-timeout-budget.test.ts` enforces it).
//
// Nothing else is configured here: `npm test` (`vitest run`) and CI's
// `npm test -- --coverage` keep vitest's defaults for discovery, pooling and coverage.
export default defineConfig({
  test: {
    testTimeout: TEST_TIMEOUT_MS,
    hookTimeout: HOOK_TIMEOUT_MS,
  },
});
