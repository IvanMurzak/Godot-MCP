import { describe, it, expect } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import { fileURLToPath } from 'url';
import { EXTENSIONS_CATALOG, type ExtensionDescriptor } from '../src/utils/extensions-catalog.js';

// The CLI's EXTENSIONS_CATALOG mirror MUST stay byte-equivalent to the SHARED source of
// truth `addons/godot_mcp/extensions.catalog.json` (consumed by the dock via an embedded
// resource). If the catalog gains/changes an entry, this test fails until the mirror is
// updated — the drift tripwire that keeps the dock + CLI + app from diverging. Same
// discipline as addon-deps-parity.test.ts for the NuGet pins.
const __dirname = path.dirname(fileURLToPath(import.meta.url));
const CATALOG_JSON = path.resolve(__dirname, '..', '..', 'addons', 'godot_mcp', 'extensions.catalog.json');

interface CatalogJsonAddonRequired {
  name?: string;
  assetLibId?: string | number;
  repo?: string;
  license?: string;
}

interface CatalogJsonEntry {
  name?: string;
  description?: string;
  packageId?: string;
  version?: string;
  gitUrl?: string;
  tools?: { name?: string; description?: string }[];
  addonRequired?: CatalogJsonAddonRequired;
}

/** Trim a string-or-number-or-absent field to a non-empty string, else null (assetLibId may be a JSON number). */
function trimOrNull(v: string | number | undefined): string | null {
  if (v === undefined || v === null) return null;
  const s = String(v).trim();
  return s === '' ? null : s;
}

/** Normalize the optional CLASS-B `addonRequired` block; absent / nameless → null (mirrors the C# parser's drop rule). */
function normalizeAddonRequired(a: CatalogJsonAddonRequired | undefined) {
  if (!a || !a.name || a.name.trim() === '') return null;
  return {
    name: a.name.trim(),
    assetLibId: trimOrNull(a.assetLibId),
    repo: trimOrNull(a.repo),
    license: trimOrNull(a.license),
  };
}

/** Normalize a raw JSON entry to the canonical ExtensionDescriptor shape (mirrors the C# parser's mapping). */
function normalizeJsonEntry(e: CatalogJsonEntry): ExtensionDescriptor {
  return {
    name: (e.name ?? '').trim(),
    description: (e.description ?? '').trim(),
    packageId: (e.packageId ?? '').trim(),
    version: e.version && e.version.trim() !== '' ? e.version.trim() : null,
    gitUrl: e.gitUrl && e.gitUrl.trim() !== '' ? e.gitUrl.trim() : null,
    tools: (e.tools ?? [])
      .filter((t) => t.name && t.name.trim() !== '')
      .map((t) => ({ name: (t.name ?? '').trim(), description: (t.description ?? '').trim() })),
    addonRequired: normalizeAddonRequired(e.addonRequired),
  };
}

describe('extensions-catalog parity with addons/godot_mcp/extensions.catalog.json', () => {
  it('the catalog JSON is reachable from the test (relative layout sanity)', () => {
    expect(fs.existsSync(CATALOG_JSON)).toBe(true);
  });

  it('the TS mirror EXTENSIONS_CATALOG matches the JSON source of truth exactly', () => {
    const raw = JSON.parse(fs.readFileSync(CATALOG_JSON, 'utf-8')) as {
      extensions?: CatalogJsonEntry[];
    };
    const expected = (raw.extensions ?? [])
      .filter((e) => e.name && e.name.trim() !== '' && e.packageId && e.packageId.trim() !== '')
      .map(normalizeJsonEntry);

    // Compare as plain objects (EXTENSIONS_CATALOG is readonly; spread to mutable for deep-equal).
    const actual = EXTENSIONS_CATALOG.map((d) => ({
      name: d.name,
      description: d.description,
      packageId: d.packageId,
      version: d.version,
      gitUrl: d.gitUrl,
      tools: d.tools.map((t) => ({ name: t.name, description: t.description })),
      // Class-A entries omit addonRequired (undefined) — coalesce to null so they match the JSON side,
      // which normalizes an absent block to null. A Class-B entry carries the {name, assetLibId, repo, license} block.
      addonRequired: d.addonRequired
        ? {
            name: d.addonRequired.name,
            assetLibId: d.addonRequired.assetLibId,
            repo: d.addonRequired.repo,
            license: d.addonRequired.license,
          }
        : null,
    }));

    expect(
      actual,
      'cli/src/utils/extensions-catalog.ts drifted from addons/godot_mcp/extensions.catalog.json. ' +
        'Update EXTENSIONS_CATALOG to match the JSON source of truth.',
    ).toEqual(expected);
  });
});
