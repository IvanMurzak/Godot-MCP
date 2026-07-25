// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

/**
 * Timeout budgets for the `cli/` vitest suite — the single source of truth shared by
 * `vitest.config.ts` and by every helper that waits on something.
 *
 * ## Why this file exists
 *
 * 17 of the suite's ~48 test files spawn a REAL `node` child process (the CLI itself,
 * or a sleeper used to probe process signalling), and vitest runs those files in
 * parallel. Under that load a spawn+exit round trip routinely takes several seconds
 * on a busy machine.
 *
 * The suite used to run on vitest's DEFAULT 5 s per-test timeout while its own internal
 * waits were budgeted far higher — `runCliAsync` gave a child 30 s, and
 * `godot-shutdown.test.ts` awaits `waitForExit(pid, 5000..8000)`. An inner budget that
 * meets or exceeds the outer test timeout can never actually fire: vitest kills the
 * test first, so the failure surfaces as a bare `Test timed out in 5000ms` with no
 * diagnostic, and it surfaces INTERMITTENTLY because it depends on machine load. That
 * is what made the suite flaky (observed locally: 4 of 5 consecutive full runs red,
 * 1-5 failures each, all `Test timed out in 5000ms`, every one of them green in
 * isolation).
 *
 * ## The invariant
 *
 * `CLI_SPAWN_TIMEOUT_MS < TEST_TIMEOUT_MS` — every internal wait budget must be
 * strictly smaller than the test timeout that contains it, so the inner guard fires
 * first and reports WHAT stalled instead of the runner reporting THAT something did.
 * `tests/test-timeout-budget.test.ts` enforces it, so a future edit that raises a
 * helper budget past the test timeout fails CI instead of re-introducing the flake.
 */

/**
 * Per-test timeout for the whole suite (`vitest.config.ts` → `test.testTimeout`).
 * Sized to dominate {@link CLI_SPAWN_TIMEOUT_MS} with headroom; the full suite still
 * completes in ~13 s wall-clock because the budget is only consumed on a real stall.
 */
export const TEST_TIMEOUT_MS = 30_000;

/** Per-hook timeout (`vitest.config.ts` → `test.hookTimeout`); hooks here are fs setup/teardown. */
export const HOOK_TIMEOUT_MS = 30_000;

/**
 * How long `tests/helpers/cli.ts` lets a spawned `godot-cli` child run before it kills
 * it and resolves with a `[runCliAsync] Process timed out.` marker in the captured
 * output. MUST stay strictly below {@link TEST_TIMEOUT_MS}.
 */
export const CLI_SPAWN_TIMEOUT_MS = 20_000;
