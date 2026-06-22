import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import * as zlib from 'zlib';
import { installPlugin } from '../src/lib/install-plugin.js';
import { GODOT_MCP_PLUGIN_PATH, parseEnabledPlugins } from '../src/utils/project-godot.js';
import { ADDON_PACKAGE_REFERENCES } from '../src/utils/addon-deps.js';

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
      expect(second.csproj.changed).toBe(false); // pins already present
      expect(second.csproj.packages.every((p) => p.action === 'unchanged')).toBe(true);
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
        // warns about the absent addon files
        expect(result.warnings.some((w) => w.includes('plugin.cfg'))).toBe(true);
      }
    } finally {
      fs.rmSync(tmp, { recursive: true, force: true });
    }
  });
});
