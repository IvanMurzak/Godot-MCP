import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';
import { installPlugin } from '../src/lib/install-plugin.js';
import { GODOT_MCP_PLUGIN_PATH, parseEnabledPlugins } from '../src/utils/project-godot.js';
import { ADDON_PACKAGE_REFERENCES, ADDON_EMBEDDED_RESOURCES } from '../src/utils/addon-deps.js';
import { parseCsprojPackageReferences } from '../src/utils/addon-nuget-deps.js';
import { fileURLToPath } from 'url';

const MINIMAL_PROJECT = '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n';
const CSPROJ = `<Project Sdk="Godot.NET.Sdk/4.3.0">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
  </PropertyGroup>
</Project>
`;

// ---- in-memory zip builder (DEFLATE) ----
function crc32(buf: Buffer): number {
  let crc = ~0;
  for (let i = 0; i < buf.length; i++) {
    crc ^= buf[i];
    for (let j = 0; j < 8; j++) crc = (crc >>> 1) ^ (0xedb88320 & -(crc & 1));
  }
  return ~crc >>> 0;
}
function buildZip(files: { path: string; content: Buffer }[]): Buffer {
  const local: Buffer[] = [];
  const central: Buffer[] = [];
  let offset = 0;
  for (const f of files) {
    const name = Buffer.from(f.path, 'utf8');
    const comp = zlib.deflateRawSync(f.content);
    const crc = crc32(f.content);
    const lfh = Buffer.alloc(30);
    lfh.writeUInt32LE(0x04034b50, 0);
    lfh.writeUInt16LE(20, 4);
    lfh.writeUInt16LE(8, 8);
    lfh.writeUInt32LE(crc, 14);
    lfh.writeUInt32LE(comp.length, 18);
    lfh.writeUInt32LE(f.content.length, 22);
    lfh.writeUInt16LE(name.length, 26);
    const rec = Buffer.concat([lfh, name, comp]);
    local.push(rec);
    const cdh = Buffer.alloc(46);
    cdh.writeUInt32LE(0x02014b50, 0);
    cdh.writeUInt16LE(8, 10);
    cdh.writeUInt32LE(crc, 16);
    cdh.writeUInt32LE(comp.length, 20);
    cdh.writeUInt32LE(f.content.length, 24);
    cdh.writeUInt16LE(name.length, 28);
    cdh.writeUInt32LE(offset, 42);
    central.push(Buffer.concat([cdh, name]));
    offset += rec.length;
  }
  const lb = Buffer.concat(local);
  const cb = Buffer.concat(central);
  const eocd = Buffer.alloc(22);
  eocd.writeUInt32LE(0x06054b50, 0);
  eocd.writeUInt16LE(files.length, 8);
  eocd.writeUInt16LE(files.length, 10);
  eocd.writeUInt32LE(cb.length, 12);
  eocd.writeUInt32LE(lb.length, 16);
  return Buffer.concat([lb, cb, eocd]);
}

/** A mock fetch returning a 200 with the given zip bytes. */
function mockFetchZip(zip: Buffer): typeof fetch {
  return (async () =>
    ({
      ok: true,
      status: 200,
      statusText: 'OK',
      arrayBuffer: async () => zip.buffer.slice(zip.byteOffset, zip.byteOffset + zip.byteLength),
    }) as unknown as Response) as unknown as typeof fetch;
}

function mockFetch404(): typeof fetch {
  return (async () =>
    ({ ok: false, status: 404, statusText: 'Not Found', arrayBuffer: async () => new ArrayBuffer(0) }) as unknown as Response) as unknown as typeof fetch;
}

const VALID_ADDON_ZIP = buildZip([
  { path: 'addons/godot_mcp/plugin.cfg', content: Buffer.from('[plugin]\nname="godot_mcp"\nversion="9.9.9"\n') },
  { path: 'addons/godot_mcp/Editor/GodotMcpPlugin.cs', content: Buffer.from('// boot\n') },
  { path: 'addons/godot_mcp/Runtime/Ping.cs', content: Buffer.from('// ping\n') },
]);

describe('installPlugin — download path (mocked fetch)', () => {
  let tmp: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-dl-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), MINIMAL_PROJECT);
    fs.writeFileSync(path.join(tmp, 'MyGame.csproj'), CSPROJ);
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  it('downloads + extracts the addon, patches the csproj, and enables the plugin', async () => {
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '1.2.3',
      fetchImpl: mockFetchZip(VALID_ADDON_ZIP),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    // addon materialized
    expect(result.materialize.source).toBe('download');
    expect(result.materialize.downloadUrl).toContain('github.com');
    expect(result.materialize.downloadUrl).toContain('v1.2.3');
    expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp', 'plugin.cfg'))).toBe(true);
    expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp', 'Editor', 'GodotMcpPlugin.cs'))).toBe(true);

    // csproj patched with both pins
    expect(result.csproj.changed).toBe(true);
    const csprojText = fs.readFileSync(path.join(tmp, 'MyGame.csproj'), 'utf-8');
    for (const ref of ADDON_PACKAGE_REFERENCES) {
      expect(csprojText).toContain(`Include="${ref.id}" Version="${ref.version}"`);
    }

    // csproj also embeds the extension catalog (else the Extensions panel is empty — issue #246)
    for (const res of ADDON_EMBEDDED_RESOURCES) {
      expect(csprojText).toContain(`Include="${res.include}" LogicalName="${res.logicalName}"`);
      expect(result.csproj.embeds.find((e) => e.include === res.include)?.action).toBe('added');
    }

    // plugin enabled
    expect(result.enabledPlugins).toContain(GODOT_MCP_PLUGIN_PATH);
    expect(parseEnabledPlugins(fs.readFileSync(path.join(tmp, 'project.godot'), 'utf-8'))).toContain(
      GODOT_MCP_PLUGIN_PATH,
    );

    // no staging dir left behind
    const leftovers = fs.readdirSync(tmp).filter((n) => n.startsWith('.godot-mcp-addon-staging'));
    expect(leftovers).toHaveLength(0);
  });

  it('returns a structured failure on a 404, leaving the project untouched', async () => {
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '404.0.0',
      fetchImpl: mockFetch404(),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') {
      expect(result.error.message).toMatch(/HTTP 404/);
    }
    // No addon dir, plugin not enabled, csproj untouched.
    expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp'))).toBe(false);
    expect(parseEnabledPlugins(fs.readFileSync(path.join(tmp, 'project.godot'), 'utf-8'))).not.toContain(
      GODOT_MCP_PLUGIN_PATH,
    );
    expect(fs.readFileSync(path.join(tmp, 'MyGame.csproj'), 'utf-8')).toBe(CSPROJ);
  });

  it('rejects a download whose zip lacks plugin.cfg (not a valid addon), leaving the project untouched', async () => {
    const badZip = buildZip([{ path: 'addons/godot_mcp/README.md', content: Buffer.from('no manifest') }]);
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '1.0.0',
      fetchImpl: mockFetchZip(badZip),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/plugin\.cfg/);
    expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp'))).toBe(false);
  });

  it('rejects a zip-slip entry trying to escape the addon dir', async () => {
    const evil = buildZip([
      { path: 'addons/godot_mcp/plugin.cfg', content: Buffer.from('[plugin]\n') },
      { path: 'addons/godot_mcp/../../evil.txt', content: Buffer.from('pwned') },
    ]);
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '1.0.0',
      fetchImpl: mockFetchZip(evil),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/escaping/i);
    expect(fs.existsSync(path.join(tmp, 'evil.txt'))).toBe(false);
  });

  it('does not call fetch when no fetchImpl is needed (idempotent re-run after download)', async () => {
    await installPlugin({ godotProjectPath: tmp, version: '1.2.3', fetchImpl: mockFetchZip(VALID_ADDON_ZIP) });
    // Re-run: download again (overwrites), but plugin already enabled + pins present.
    const second = await installPlugin({
      godotProjectPath: tmp,
      version: '1.2.3',
      fetchImpl: mockFetchZip(VALID_ADDON_ZIP),
    });
    expect(second.kind).toBe('success');
    if (second.kind === 'success') {
      expect(second.changed).toBe(false); // plugin already enabled
      expect(second.csproj.changed).toBe(false); // pins + embed already present
      expect(second.csproj.packages.every((p) => p.action === 'unchanged')).toBe(true);
      expect(second.csproj.embeds.every((e) => e.action === 'unchanged')).toBe(true);
      // The embed appears exactly once — idempotent re-install never duplicates it.
      const csprojText = fs.readFileSync(path.join(tmp, 'MyGame.csproj'), 'utf-8');
      for (const res of ADDON_EMBEDDED_RESOURCES) {
        const occurrences = csprojText.split(`Include="${res.include}"`).length - 1;
        expect(occurrences).toBe(1);
      }
    }
  });
});

describe('installPlugin — host trust on download', () => {
  it('an empty version is rejected before any network call', async () => {
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-nover-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), MINIMAL_PROJECT);
    try {
      let fetchCalled = false;
      const spyFetch = (async () => {
        fetchCalled = true;
        throw new Error('should not be called');
      }) as unknown as typeof fetch;
      const result = await installPlugin({ godotProjectPath: tmp, version: '', fetchImpl: spyFetch });
      expect(result.kind).toBe('failure');
      expect(fetchCalled).toBe(false);
      if (result.kind === 'failure') expect(result.error.message).toMatch(/version|source/i);
    } finally {
      fs.rmSync(tmp, { recursive: true, force: true });
    }
  });
});

describe('installPlugin — --source local copy', () => {
  let tmp: string;
  let src: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-src-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), MINIMAL_PROJECT);
    fs.writeFileSync(path.join(tmp, 'MyGame.csproj'), CSPROJ);
    src = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-srcdir-'));
    const addon = path.join(src, 'addons', 'godot_mcp');
    fs.mkdirSync(path.join(addon, 'Editor'), { recursive: true });
    fs.writeFileSync(path.join(addon, 'plugin.cfg'), '[plugin]\nname="godot_mcp"\n');
    fs.writeFileSync(path.join(addon, 'Editor', 'GodotMcpPlugin.cs'), '// boot\n');
  });
  afterEach(() => {
    fs.rmSync(tmp, { recursive: true, force: true });
    fs.rmSync(src, { recursive: true, force: true });
  });

  it('copies the addon from a directory CONTAINING addons/godot_mcp and never hits the network', async () => {
    let fetchCalled = false;
    const spyFetch = (async () => {
      fetchCalled = true;
      throw new Error('network should not be used with --source');
    }) as unknown as typeof fetch;
    const result = await installPlugin({ godotProjectPath: tmp, source: src, fetchImpl: spyFetch });
    expect(result.kind).toBe('success');
    expect(fetchCalled).toBe(false);
    if (result.kind === 'success') {
      expect(result.materialize.source).toBe('local');
      expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp', 'plugin.cfg'))).toBe(true);
      expect(fs.existsSync(path.join(tmp, 'addons', 'godot_mcp', 'Editor', 'GodotMcpPlugin.cs'))).toBe(true);
      expect(result.csproj.changed).toBe(true);
      // csproj also embeds the extension catalog (else the Extensions panel is empty — issue #246)
      const csprojText = fs.readFileSync(path.join(tmp, 'MyGame.csproj'), 'utf-8');
      for (const res of ADDON_EMBEDDED_RESOURCES) {
        expect(csprojText).toContain(`Include="${res.include}" LogicalName="${res.logicalName}"`);
        expect(result.csproj.embeds.find((e) => e.include === res.include)?.action).toBe('added');
      }
      expect(result.enabledPlugins).toContain(GODOT_MCP_PLUGIN_PATH);
    }
  });

  it('accepts a --source path that IS the addon dir (contains plugin.cfg directly)', async () => {
    const directAddon = path.join(src, 'addons', 'godot_mcp');
    const result = await installPlugin({ godotProjectPath: tmp, source: directAddon });
    expect(result.kind).toBe('success');
    if (result.kind === 'success') expect(result.materialize.source).toBe('local');
  });

  it('fails clearly when --source has no plugin.cfg', async () => {
    const bare = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-bare-'));
    try {
      const result = await installPlugin({ godotProjectPath: tmp, source: bare });
      expect(result.kind).toBe('failure');
      if (result.kind === 'failure') expect(result.error.message).toMatch(/plugin\.cfg|does not contain/i);
    } finally {
      fs.rmSync(bare, { recursive: true, force: true });
    }
  });
});

describe('installPlugin — skipMaterialize', () => {
  it('patches csproj + enables but does not fetch or copy', async () => {
    const tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-skip-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), MINIMAL_PROJECT);
    fs.writeFileSync(path.join(tmp, 'MyGame.csproj'), CSPROJ);
    try {
      const result = await installPlugin({ godotProjectPath: tmp, skipMaterialize: true });
      expect(result.kind).toBe('success');
      if (result.kind === 'success') {
        expect(result.materialize.source).toBe('skipped');
        expect(result.csproj.changed).toBe(true);
        // csproj also embeds the extension catalog (else the Extensions panel is empty — issue #246)
        const csprojText = fs.readFileSync(path.join(tmp, 'MyGame.csproj'), 'utf-8');
        for (const res of ADDON_EMBEDDED_RESOURCES) {
          expect(csprojText).toContain(`Include="${res.include}" LogicalName="${res.logicalName}"`);
          expect(result.csproj.embeds.find((e) => e.include === res.include)?.action).toBe('added');
        }
        // warns about the absent addon files
        expect(result.warnings.some((w) => w.includes('plugin.cfg'))).toBe(true);
      }
    } finally {
      fs.rmSync(tmp, { recursive: true, force: true });
    }
  });
});

// ---- NuGet versions come from the INSTALLED addon, not from this CLI build ----
// Versions deliberately unlike ADDON_PACKAGE_REFERENCES, so a test can only pass when the
// installer read them from the addon (or the checkout) rather than the built-in constants.
const ADDON_MANIFEST_REFS = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '1.11.1' },
  { id: 'com.IvanMurzak.McpPlugin', version: '2.22.2-test' },
];
const CHECKOUT_CSPROJ_REFS = [
  { id: 'com.IvanMurzak.ReflectorNet', version: '3.33.3' },
  { id: 'com.IvanMurzak.McpPlugin', version: '4.44.4' },
];
const nugetDepsJson = (refs: { id: string; version: string }[]): Buffer =>
  Buffer.from(JSON.stringify({ schemaVersion: 1, packageReferences: refs }));

function expectPinned(csprojFile: string, refs: readonly { id: string; version: string }[]): void {
  const text = fs.readFileSync(csprojFile, 'utf-8');
  for (const ref of refs) expect(text).toContain(`Include="${ref.id}" Version="${ref.version}"`);
}

describe('installPlugin — NuGet versions from the installed addon', () => {
  let tmp: string;
  let csprojFile: string;
  beforeEach(() => {
    tmp = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-deps-'));
    fs.writeFileSync(path.join(tmp, 'project.godot'), MINIMAL_PROJECT);
    csprojFile = path.join(tmp, 'MyGame.csproj');
    fs.writeFileSync(csprojFile, CSPROJ);
  });
  afterEach(() => fs.rmSync(tmp, { recursive: true, force: true }));

  const zipWith = (manifest?: Buffer): Buffer =>
    buildZip([
      { path: 'addons/godot_mcp/plugin.cfg', content: Buffer.from('[plugin]\nname="godot_mcp"\nversion="9.9.9"\n') },
      ...(manifest ? [{ path: 'addons/godot_mcp/nuget-deps.json', content: manifest }] : []),
    ]);

  it("download: pins the versions from the zip's nuget-deps.json, not the CLI build", async () => {
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '9.9.9',
      fetchImpl: mockFetchZip(zipWith(nugetDepsJson(ADDON_MANIFEST_REFS))),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.csproj.packagesOrigin).toBe('addon-manifest');
    expect(result.csproj.packages).toEqual(ADDON_MANIFEST_REFS.map((r) => ({ ...r, action: 'added' })));
    expectPinned(csprojFile, ADDON_MANIFEST_REFS);
    expect(result.warnings.filter((w) => w.includes('nuget-deps.json'))).toEqual([]);
  });

  it("download: re-pins a consumer csproj that carries the CLI build's versions", async () => {
    await installPlugin({ godotProjectPath: tmp, skipMaterialize: true }); // built-in pins
    expectPinned(csprojFile, ADDON_PACKAGE_REFERENCES);
    const result = await installPlugin({
      godotProjectPath: tmp,
      version: '9.9.9',
      fetchImpl: mockFetchZip(zipWith(nugetDepsJson(ADDON_MANIFEST_REFS))),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.csproj.packages.map((p) => p.action)).toEqual(['updated', 'updated']);
    expectPinned(csprojFile, ADDON_MANIFEST_REFS);
  });

  it('download of an old zip without nuget-deps.json falls back to the built-in pins, with a warning', async () => {
    const result = await installPlugin({ godotProjectPath: tmp, version: '0.25.0', fetchImpl: mockFetchZip(zipWith()) });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.csproj.packagesOrigin).toBe('builtin');
    expectPinned(csprojFile, ADDON_PACKAGE_REFERENCES);
    expect(result.warnings.some((w) => w.includes('nuget-deps.json'))).toBe(true);
  });

  it('a malformed nuget-deps.json falls back to the built-in pins, with a warning', async () => {
    const bad = Buffer.from(JSON.stringify({ packageReferences: [{ id: 'x', version: '1" Evil="' }] }));
    const result = await installPlugin({ godotProjectPath: tmp, version: '9.9.9', fetchImpl: mockFetchZip(zipWith(bad)) });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.csproj.packagesOrigin).toBe('builtin');
    expectPinned(csprojFile, ADDON_PACKAGE_REFERENCES);
    expect(result.warnings.some((w) => w.includes('malformed'))).toBe(true);
  });

  it('skipMaterialize: reads nuget-deps.json from the addon already in the project', async () => {
    const addon = path.join(tmp, 'addons', 'godot_mcp');
    fs.mkdirSync(addon, { recursive: true });
    fs.writeFileSync(path.join(addon, 'plugin.cfg'), '[plugin]\n');
    fs.writeFileSync(path.join(addon, 'nuget-deps.json'), nugetDepsJson(ADDON_MANIFEST_REFS));
    const result = await installPlugin({ godotProjectPath: tmp, skipMaterialize: true });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.csproj.packagesOrigin).toBe('addon-manifest');
    expectPinned(csprojFile, ADDON_MANIFEST_REFS);
  });

  describe('--source', () => {
    let src: string;
    let addon: string;
    beforeEach(() => {
      src = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-deps-src-'));
      addon = path.join(src, 'addons', 'godot_mcp');
      fs.mkdirSync(addon, { recursive: true });
      fs.writeFileSync(path.join(addon, 'plugin.cfg'), '[plugin]\n');
    });
    afterEach(() => fs.rmSync(src, { recursive: true, force: true }));

    it('a Godot-MCP checkout: reads its Godot-MCP.csproj (over a stale generated manifest)', async () => {
      const refs = CHECKOUT_CSPROJ_REFS.map((r) => `    <PackageReference Include="${r.id}" Version="${r.version}" />`);
      fs.writeFileSync(
        path.join(src, 'Godot-MCP.csproj'),
        `<Project Sdk="Godot.NET.Sdk/4.3.0">\n  <ItemGroup>\n${refs.join('\n')}\n  </ItemGroup>\n</Project>\n`,
      );
      fs.writeFileSync(path.join(addon, 'nuget-deps.json'), nugetDepsJson(ADDON_MANIFEST_REFS));
      const result = await installPlugin({ godotProjectPath: tmp, source: addon });
      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;
      expect(result.csproj.packagesOrigin).toBe('source-csproj');
      expectPinned(csprojFile, CHECKOUT_CSPROJ_REFS);
    });

    it('an extracted release zip (no csproj): reads the copied nuget-deps.json', async () => {
      fs.writeFileSync(path.join(addon, 'nuget-deps.json'), nugetDepsJson(ADDON_MANIFEST_REFS));
      const result = await installPlugin({ godotProjectPath: tmp, source: src });
      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;
      expect(result.csproj.packagesOrigin).toBe('addon-manifest');
      expectPinned(csprojFile, ADDON_MANIFEST_REFS);
    });

    it("this repo's own addons/godot_mcp: pins exactly what Godot-MCP.csproj pins", async () => {
      const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');
      const result = await installPlugin({ godotProjectPath: tmp, source: path.join(repoRoot, 'addons', 'godot_mcp') });
      expect(result.kind).toBe('success');
      if (result.kind !== 'success') return;
      expect(result.csproj.packagesOrigin).toBe('source-csproj');
      const pinned = parseCsprojPackageReferences(fs.readFileSync(path.join(repoRoot, 'Godot-MCP.csproj'), 'utf-8'));
      expect(result.csproj.packages.map(({ id, version }) => ({ id, version }))).toEqual(
        pinned.map(({ id, version }) => ({ id, version })),
      );
      // The editor-only packages land conditioned in the consumer csproj too (exports ship no RQL / Roslyn).
      expect(pinned.some((r) => r.editorOnly)).toBe(true);
      expect(parseCsprojPackageReferences(fs.readFileSync(csprojFile, 'utf-8'))).toEqual(expect.arrayContaining(pinned));
    });
  });
});
