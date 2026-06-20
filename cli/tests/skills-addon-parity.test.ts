// Copyright (c) 2026 Ivan Murzak. All rights reserved.
// Licensed under the Apache License, Version 2.0.

// CI cross-check: the CLI skill catalog (`src/utils/skills.ts` § SKILL_FAMILIES)
// MUST advertise exactly the tool families the addon actually ships. The addon's
// families are the `[AiToolType] partial class Tool_<Family>` classes under
// `addons/godot_mcp/{Runtime,Editor}/Tools/`. If a new family ships in the addon
// but nobody adds a catalog entry here (the `runtime-errors` regression this test
// was added to prevent — shipped in #161/#162, never advertised by the CLI), this
// test fails — so the CLI catalog can never silently fall behind the addon again.
//
// This runs in the existing `test-cli` CI leg (Node 20 & 22) — no Godot binary,
// no .NET build — because it only reads the addon's `.cs` SOURCE text.

import { describe, it, expect } from 'vitest';
import * as path from 'path';
import { fileURLToPath } from 'url';
import {
  catalogToolFamilyClassSuffixes,
  discoverAddonToolFamilies,
  addonToolDirs,
} from '../src/utils/skills.js';
import * as fs from 'fs';

// cli/tests/ -> cli/ -> <repo root> (the dir that holds both `cli/` and `addons/`).
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = path.resolve(__dirname, '..', '..');

describe('skills.ts ⇄ addon tool-family parity (CI cross-check)', () => {
  it('addon Tools/ source directories exist (guards against a moved/renamed addon layout)', () => {
    // If the addon layout moves, discovery would silently return an empty set and
    // the parity assert below could pass vacuously — assert the dirs are real first.
    const dirs = addonToolDirs(REPO_ROOT);
    for (const dir of dirs) {
      expect(fs.existsSync(dir), `expected addon Tools dir to exist: ${dir}`).toBe(true);
    }
  });

  it('discovers a non-empty set of addon tool families', () => {
    // Guard against a discovery regex regression that matches nothing (which would
    // make the parity assert pass vacuously when the catalog is also empty).
    const addonFamilies = discoverAddonToolFamilies(REPO_ROOT);
    expect(addonFamilies.length).toBeGreaterThan(5);
  });

  it('the addon ships the runtime-errors family (regression anchor for #167)', () => {
    // The specific family whose absence from the catalog motivated this test.
    const addonFamilies = discoverAddonToolFamilies(REPO_ROOT);
    expect(addonFamilies).toContain('RuntimeErrors');
  });

  it('catalog families exactly match the addon `Tool_<Family>` classes (no drift)', () => {
    const catalog = catalogToolFamilyClassSuffixes(); // skills.ts titles, sorted
    const addon = discoverAddonToolFamilies(REPO_ROOT); // Tool_<Family> suffixes, sorted

    const inAddonNotInCatalog = addon.filter((f) => !catalog.includes(f));
    const inCatalogNotInAddon = catalog.filter((f) => !addon.includes(f));

    // Build human-readable diagnostics so a failure tells the maintainer exactly
    // what to add/remove (and where).
    expect(
      inAddonNotInCatalog,
      `Addon tool families with NO entry in cli/src/utils/skills.ts § SKILL_FAMILIES: ` +
        `[${inAddonNotInCatalog.join(', ')}]. Add a SkillFamily whose \`title\` is the ` +
        `\`Tool_<Family>\` suffix (and update CLAUDE.md + README family count).`,
    ).toEqual([]);

    expect(
      inCatalogNotInAddon,
      `skills.ts catalog families with NO backing \`[AiToolType] Tool_<Family>\` addon class: ` +
        `[${inCatalogNotInAddon.join(', ')}]. Remove the stale catalog entry or fix its \`title\`.`,
    ).toEqual([]);

    // Belt-and-suspenders: the full sets are equal.
    expect(catalog).toEqual(addon);
  });
});
