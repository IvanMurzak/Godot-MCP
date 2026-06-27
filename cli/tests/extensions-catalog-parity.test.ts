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

interface CatalogJsonEntry {
  name?: string;
  description?: string;
  packageId?: string;
  version?: string;
  gitUrl?: string;
  tools?: { name?: string; description?: string }[];
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
    }));

    expect(
      actual,
      'cli/src/utils/extensions-catalog.ts drifted from addons/godot_mcp/extensions.catalog.json. ' +
        'Update EXTENSIONS_CATALOG to match the JSON source of truth.',
    ).toEqual(expected);
  });
});
