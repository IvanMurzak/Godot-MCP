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

import { describe, it, expect, afterEach } from 'vitest';
import * as path from 'path';
import * as os from 'os';
import { fileURLToPath } from 'url';
import {
  catalogToolFamilyClassSuffixes,
  discoverAddonToolFamilies,
  catalogToolIds,
  discoverAddonToolIds,
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

  it('attributes the compound Tool_Editor_Selection sub-tool class to the Editor family (not dropped)', () => {
    // The addon ships `[AiToolType] partial class Tool_Editor_Selection` — a compound
    // sub-tool class whose `editor-selection-*` tools belong to the `editor` family.
    // A discovery regex that stopped capturing at the first `_` would SILENTLY DROP it
    // (parity then passes 11==11 only by accident). It must instead resolve to the
    // first-segment family `Editor`, where it dedups against the base `Tool_Editor`.
    const addonFamilies = discoverAddonToolFamilies(REPO_ROOT);
    expect(addonFamilies).toContain('Editor');
    // Discovery still equals the catalog at exactly 12 families — the compound class
    // collapses INTO `Editor` rather than adding a phantom 13th family.
    expect(addonFamilies).toEqual(catalogToolFamilyClassSuffixes());
    expect(addonFamilies).toHaveLength(12);
  });

  describe('discovery robustness against compound + misattributed [AiToolType] classes', () => {
    let tmpRoot: string | undefined;

    afterEach(() => {
      if (tmpRoot && fs.existsSync(tmpRoot)) fs.rmSync(tmpRoot, { recursive: true, force: true });
      tmpRoot = undefined;
    });

    // Write a synthetic addon tree under a temp repo root so we can probe discovery on
    // hand-crafted edge cases without depending on the real addon source. `relPath` is
    // resolved UNDER `Runtime/Tools/` and may contain sub-directories.
    function writeSyntheticAddon(relPath: string, contents: string): string {
      tmpRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-mcp-parity-'));
      const runtimeDir = addonToolDirs(tmpRoot)[0]; // .../Runtime/Tools
      const target = path.join(runtimeDir, relPath);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, contents, 'utf-8');
      return tmpRoot;
    }

    it('maps a compound Tool_<Seg1>_<Seg2> class to its first segment', () => {
      const root = writeSyntheticAddon(
        'Tool_Foo.Bar.cs',
        '[AiToolType]\n    public partial class Tool_Foo_Bar\n    {\n    }\n',
      );
      expect(discoverAddonToolFamilies(root)).toEqual(['Foo']);
    });

    it('ignores a C# sample embedded in a STRING literal (the [AiSkillBody] teaching sample)', () => {
      // `Tool_Skills.Create.SkillBody.cs` embeds a full `[AiToolType] partial class Tool_Sample`
      // example inside its [AiSkillBody] markdown so `godot-skill-create` can teach an agent the
      // shape. A raw-text scan would discover `Tool_Sample` as a real family and fail parity against
      // a phantom — discovery therefore blanks comments + string literals first.
      const root = writeSyntheticAddon(
        'Tool_Real.cs',
        '[AiToolType]\n    public partial class Tool_Real\n    {\n' +
          '        internal const string Body =\n' +
          '            "[AiToolType]\\n" +\n' +
          '            "public partial class Tool_Fake\\n" +\n' +
          '            "{\\n" +\n' +
          '            "    public const string FakeToolId = \\"fake-tool\\";\\n" +\n' +
          '            "}\\n";\n' +
          '    }\n',
      );
      expect(discoverAddonToolFamilies(root)).toEqual(['Real']);
      expect(discoverAddonToolIds(root)).toEqual([]);
    });

    it('ignores a C# sample embedded in a COMMENT or a verbatim string', () => {
      const root = writeSyntheticAddon(
        'Tool_Real.cs',
        '// [AiToolType] public partial class Tool_Commented { }\n' +
          '// public const string CommentedToolId = "commented-tool";\n' +
          '/* [AiToolType]\n   public partial class Tool_Blocked { } */\n' +
          '[AiToolType]\n    public partial class Tool_Real\n    {\n' +
          '        const string Verbatim = @"[AiToolType] public partial class Tool_Verbatim { }";\n' +
          '        public const string RealToolId = "real-tool";\n' +
          '    }\n',
      );
      expect(discoverAddonToolFamilies(root)).toEqual(['Real']);
      // The id scan KEEPS string literals (it matches the quoted id), so it relies on comment
      // stripping alone — the commented-out id must not be discovered, the real one must.
      expect(discoverAddonToolIds(root)).toEqual(['real-tool']);
    });

    it('discovers a tool family nested in a SUBFOLDER of Tools/ (the vacuous-parity trap)', () => {
      // Discovery used to be a NON-recursive `readdirSync`, so a family placed in a
      // subfolder of `{Runtime,Editor}/Tools/` — the layout Unity-MCP uses
      // (`API/SystemTool/`) and the obvious thing to reach for as the addon grows —
      // was INVISIBLE. The parity assertions then passed at STALE counts instead of
      // failing: the gate silently stopped protecting the catalog the moment anyone
      // nested a file. This fixture fails against the non-recursive scan.
      const root = writeSyntheticAddon(
        path.join('SystemTool', 'Nested', 'Tool_Nested.cs'),
        '[AiToolType]\n    public partial class Tool_Nested\n    {\n' +
          '        public const string NestedRunToolId = "nested-run";\n' +
          '    }\n',
      );
      expect(discoverAddonToolFamilies(root)).toEqual(['Nested']);
      expect(discoverAddonToolIds(root)).toEqual(['nested-run']);
    });

    it('discovers families/ids from BOTH a top-level file and a nested one (no shadowing)', () => {
      // Recursion must ADD the nested family, not replace the flat scan.
      const root = writeSyntheticAddon(
        'Tool_Flat.cs',
        '[AiToolType]\n    public partial class Tool_Flat\n    {\n' +
          '        public const string FlatGoToolId = "flat-go";\n' +
          '    }\n',
      );
      const nestedDir = path.join(addonToolDirs(root)[0], 'Sub', 'Deeper');
      fs.mkdirSync(nestedDir, { recursive: true });
      fs.writeFileSync(
        path.join(nestedDir, 'Tool_Deep.Run.cs'),
        '[AiToolType]\n    public partial class Tool_Deep\n    {\n' +
          '        public const string DeepRunToolId = "deep-run";\n' +
          '    }\n',
        'utf-8',
      );
      expect(discoverAddonToolFamilies(root)).toEqual(['Deep', 'Flat']);
      expect(discoverAddonToolIds(root)).toEqual(['deep-run', 'flat-go']);
    });

    it('does not misattribute a stray [AiToolType] on a non-Tool_ type to a later Tool_ class', () => {
      // The `[AiToolType]` here sits on a helper type; the unattributed `Tool_Bridged`
      // class below it must NOT be bridged to that attribute (cross-match guard).
      const root = writeSyntheticAddon(
        'Helper.cs',
        '[AiToolType]\n    public class HelperType { }\n\n    public partial class Tool_Bridged { }\n',
      );
      expect(discoverAddonToolFamilies(root)).toEqual([]);
    });
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

describe('skills.ts ⇄ addon tool-ID parity (per-tool drift-guard)', () => {
  // The family cross-check above passes when a NEW TOOL is added to an EXISTING
  // family (the `script-validate` regression: added to `Tool_Script`, never
  // advertised by the CLI catalog — the family set was unchanged, so the family
  // diff stayed green). This block diffs the per-TOOL id set so that gap is caught.

  it('the addon ships the script-validate tool (regression anchor for #197)', () => {
    // The specific tool whose absence from the catalog motivated this block.
    const addonIds = discoverAddonToolIds(REPO_ROOT);
    expect(addonIds).toContain('script-validate');
  });

  it('discovers exactly 41 addon tool ids', () => {
    // Anchors the ground-truth count so an addon tool added/removed without a
    // catalog update is caught here too (not only by the set-equality assert).
    expect(discoverAddonToolIds(REPO_ROOT)).toHaveLength(41);
  });

  it('the CLI catalog advertises exactly 41 tools', () => {
    expect(catalogToolIds()).toHaveLength(41);
  });

  it('catalog tool ids exactly match the addon `*ToolId` constants (no per-tool drift)', () => {
    const catalog = catalogToolIds(); // skills.ts tool names, sorted
    const addon = discoverAddonToolIds(REPO_ROOT); // addon `*ToolId` ids, sorted

    const inAddonNotInCatalog = addon.filter((t) => !catalog.includes(t));
    const inCatalogNotInAddon = catalog.filter((t) => !addon.includes(t));

    // Human-readable diagnostics pointing the maintainer at skills.ts + README.
    expect(
      inAddonNotInCatalog,
      `Addon tools with NO entry in cli/src/utils/skills.ts § SKILL_FAMILIES: ` +
        `[${inAddonNotInCatalog.join(', ')}]. Add the tool to its family's \`tools\` array ` +
        `(and update README.md's Tools Reference + count).`,
    ).toEqual([]);

    expect(
      inCatalogNotInAddon,
      `skills.ts catalog tools with NO backing addon \`*ToolId\` constant: ` +
        `[${inCatalogNotInAddon.join(', ')}]. Remove the stale catalog entry or fix its name.`,
    ).toEqual([]);

    // Belt-and-suspenders: the full sets are equal.
    expect(catalog).toEqual(addon);
  });
});

describe('docs single-source: counts + McpPlugin version derive from addon source + csproj', () => {
  // The doc surfaces (README headline + Asset Library submission template + its
  // mirrored SUBMISSION.md, plus CLAUDE.md) must state the SAME tool/family count
  // the addon actually ships, and the Asset Library template must state the SAME
  // McpPlugin version the csproj pins. Deriving the expected values from the addon
  // source + csproj (not hard-coding them) keeps the addon + pin the single source
  // of truth — so the next drift fails CI instead of shipping in a submission edit.

  const expectedToolCount = discoverAddonToolIds(REPO_ROOT).length;
  const expectedFamilyCount = discoverAddonToolFamilies(REPO_ROOT).length;

  function readRepoFile(rel: string): string {
    return fs.readFileSync(path.join(REPO_ROOT, rel), 'utf-8');
  }

  // Tolerances baked into the regexes (verified against the real files):
  // - `N built-in Tools` is CASE-INSENSITIVE — README:46 capitalizes "Tools".
  // - `N families` is bold/word-boundary tolerant — README:118 wraps "**12 families**".
  function toolCountRe(): RegExp {
    return new RegExp(`${expectedToolCount} built-in tools`, 'i');
  }
  function familyCountRe(): RegExp {
    return new RegExp(`\\b${expectedFamilyCount}\\b[^\\n]*?\\bfamilies\\b`, 'i');
  }

  it('expected counts derive non-vacuously from the addon source', () => {
    expect(expectedToolCount).toBe(41);
    expect(expectedFamilyCount).toBe(12);
  });

  const countDocs = [
    'README.md',
    '.github/assetlib/edit.hbs',
    'docs/assetlib/SUBMISSION.md',
    'CLAUDE.md',
  ];

  for (const rel of countDocs) {
    it(`${rel} states the addon-derived tool count (${expectedToolCount})`, () => {
      const text = readRepoFile(rel);
      expect(
        toolCountRe().test(text),
        `${rel} should contain "${expectedToolCount} built-in tools" (case-insensitive). ` +
          `If the addon's tool count changed, update ${rel} to match discoverAddonToolIds().`,
      ).toBe(true);
    });

    it(`${rel} states the addon-derived family count (${expectedFamilyCount})`, () => {
      const text = readRepoFile(rel);
      expect(
        familyCountRe().test(text),
        `${rel} should reference "${expectedFamilyCount} families" (bold/spacing tolerant). ` +
          `If the addon's family count changed, update ${rel} to match discoverAddonToolFamilies().`,
      ).toBe(true);
    });
  }

  it('the Asset Library template + mirror state the csproj-derived McpPlugin version', () => {
    // Single source of truth for the pin = Godot-MCP.csproj.
    const csproj = readRepoFile('Godot-MCP.csproj');
    // Capture the FULL version, including any pre-release suffix (e.g. `7.1.1`) — the
    // pin is not restricted to numeric-and-dot, so match anything up to the closing quote.
    const pinMatch = csproj.match(/McpPlugin"\s+Version="([^"]+)"/);
    expect(pinMatch, 'Godot-MCP.csproj must declare a com.IvanMurzak.McpPlugin PackageReference').not.toBeNull();
    const expectedMcpPlugin = pinMatch![1];
    expect(expectedMcpPlugin).toBe('7.5.0');

    // edit.hbs renders `com.IvanMurzak.McpPlugin     version 7.1.1` — tolerate the
    // literal lowercase word `version` and runs of spaces between name and version.
    const versionAdjacencyRe = new RegExp(
      `com\\.IvanMurzak\\.McpPlugin\\s+(?:version\\s+)?${expectedMcpPlugin.replace(/\./g, '\\.')}`,
      'i',
    );

    for (const rel of ['.github/assetlib/edit.hbs', 'docs/assetlib/SUBMISSION.md']) {
      const text = readRepoFile(rel);
      expect(
        versionAdjacencyRe.test(text),
        `${rel} should state McpPlugin version ${expectedMcpPlugin} adjacent to the package name ` +
          `(it renders "com.IvanMurzak.McpPlugin     version ${expectedMcpPlugin}"). ` +
          `Keep it in lockstep with Godot-MCP.csproj's pin.`,
      ).toBe(true);
    }
  });

  // The adjacency assert above covers ONLY the two Asset Library surfaces. The pin is
  // restated in four more consumer-facing docs, and none of them was gated — which is
  // exactly how `README.md` and `CLAUDE.md` shipped a stale `7.1.1` while the csproj
  // had moved to `7.3.0`. This block closes that hole for BOTH reused packages.
  //
  // Rule: on any LINE that names a reused package by its FULL id, every semantic
  // version on that line must equal the csproj pin. Line-scoping is what makes the
  // check precise — it never reaches across to an unrelated number, and it ignores
  // the many prose/`using`/URL mentions that carry no version at all. The full-id
  // requirement (plus a `(?!\.)` guard) skips sub-packages (`McpPlugin.Common`,
  // `ReflectorNet.Utils`) and bare-name prose such as the historical
  // `'ReflectorNet, Version=5.3.1.0'` exception text in CLAUDE.md.
  const PIN_DOCS = ['README.md', 'CLAUDE.md', 'docs/ARCHITECTURE.md', 'docs/RELEASING.md'];
  const PINNED_PACKAGES = ['com.IvanMurzak.ReflectorNet', 'com.IvanMurzak.McpPlugin'];

  /** Escape a literal package id so it cannot act as a regex pattern (`.` above all). */
  function reEscape(literal: string): string {
    return literal.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
  }

  /** The version `Godot-MCP.csproj` pins for `packageId` — the single source of truth. */
  function csprojPin(packageId: string): string {
    const csproj = readRepoFile('Godot-MCP.csproj');
    const m = csproj.match(new RegExp(`${reEscape(packageId)}"\\s+Version="([^"]+)"`));
    expect(m, `Godot-MCP.csproj must declare a ${packageId} PackageReference`).not.toBeNull();
    return m![1];
  }

  /** Every version literal stated on a line that names `packageId`, with line numbers. */
  function statedVersions(text: string, packageId: string): { line: number; version: string }[] {
    const idRe = new RegExp(`${reEscape(packageId)}(?!\\.)`);
    const semverRe = /\b\d+\.\d+\.\d+(?:[-.][A-Za-z0-9.]+)?\b/g;
    const hits: { line: number; version: string }[] = [];

    text.split(/\r?\n/).forEach((line, i) => {
      if (!idRe.test(line)) return;
      for (const m of line.matchAll(semverRe)) hits.push({ line: i + 1, version: m[0] });
    });
    return hits;
  }

  it('the pin-parity scan is non-vacuous (it finds a stated version in every gated doc)', () => {
    // Without this, a doc that stops mentioning the packages — or a regex that stops
    // matching — would make every assertion below pass by finding nothing.
    for (const rel of PIN_DOCS) {
      const text = readRepoFile(rel);
      const all = PINNED_PACKAGES.flatMap((pkg) => statedVersions(text, pkg));
      expect(all.length, `${rel} should state at least one reused-package version`).toBeGreaterThan(0);
    }
  });

  for (const packageId of PINNED_PACKAGES) {
    for (const rel of PIN_DOCS) {
      it(`${rel} states the csproj-pinned ${packageId} version`, () => {
        const expected = csprojPin(packageId);
        const stated = statedVersions(readRepoFile(rel), packageId);
        const stale = stated.filter((s) => s.version !== expected);
        expect(
          stale,
          `${rel} states ${packageId} version(s) that disagree with Godot-MCP.csproj's pin ` +
            `(${expected}): ${stale.map((s) => `line ${s.line}: ${s.version}`).join(', ')}. ` +
            `The csproj is the single source of truth — update the doc, never the pin.`,
        ).toEqual([]);
      });
    }
  }
});
