import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as path from 'path';
import * as os from 'os';
import { spawn } from 'child_process';
import { findGodotBinary, launchEditor, GODOT_BIN_ENV_VARS } from '../src/utils/godot-editor.js';

// ESM module namespaces are non-configurable, so spying on a named export is
// not possible; mock the module instead and assert against the mocked spawn.
vi.mock('child_process', () => ({
  spawn: vi.fn(),
}));

const spawnMock = vi.mocked(spawn);

describe('findGodotBinary', () => {
  let tmpDir: string;
  const savedEnv: Record<string, string | undefined> = {};

  beforeEach(() => {
    tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-bin-'));
    for (const v of GODOT_BIN_ENV_VARS) {
      savedEnv[v] = process.env[v];
      delete process.env[v];
    }
    savedEnv['PATH'] = process.env['PATH'];
  });

  afterEach(() => {
    for (const v of [...GODOT_BIN_ENV_VARS, 'PATH']) {
      if (savedEnv[v] === undefined) delete process.env[v];
      else process.env[v] = savedEnv[v];
    }
    fs.rmSync(tmpDir, { recursive: true, force: true });
  });

  it('returns an explicit editorPath when it exists', () => {
    const bin = path.join(tmpDir, 'godot-bin');
    fs.writeFileSync(bin, '');
    expect(findGodotBinary(bin)).toBe(path.resolve(bin));
  });

  it('returns null for an explicit editorPath that does not exist', () => {
    expect(findGodotBinary(path.join(tmpDir, 'nope'))).toBeNull();
  });

  it('returns null for an empty/whitespace explicit editorPath', () => {
    expect(findGodotBinary('   ')).toBeNull();
  });

  it('resolves GODOT_BIN before PATH and common dirs', () => {
    const bin = path.join(tmpDir, 'godot_mono.exe');
    fs.writeFileSync(bin, '');
    process.env['GODOT_BIN'] = bin;
    expect(findGodotBinary(undefined, 'win32')).toBe(path.resolve(bin));
  });

  it('honors GODOT4_BIN as a fallback env var', () => {
    const bin = path.join(tmpDir, 'godot');
    fs.writeFileSync(bin, '');
    process.env['GODOT4_BIN'] = bin;
    expect(findGodotBinary(undefined, 'linux')).toBe(path.resolve(bin));
  });

  it('finds a binary on PATH when no env override is set', () => {
    // Use the host platform so the PATH separator matches the tmpDir's path
    // shape (a Windows absolute path contains ':' which the linux ':' splitter
    // would otherwise break apart). On Windows the PATH candidate name is
    // `godot.exe`; elsewhere `godot`.
    const hostOs = process.platform;
    const binName = hostOs === 'win32' ? 'godot.exe' : 'godot';
    const bin = path.join(tmpDir, binName);
    fs.writeFileSync(bin, '');
    process.env['PATH'] = tmpDir;
    expect(findGodotBinary(undefined, hostOs)).toBe(path.join(tmpDir, binName));
  });

  it('returns null when nothing resolves', () => {
    process.env['PATH'] = path.join(tmpDir, 'empty');
    // Pass an OS whose common dirs are unlikely to exist in the test sandbox.
    const result = findGodotBinary(undefined, 'linux');
    // Either null, or a real system godot if the runner has one — accept both,
    // but assert it never resolves a phantom path inside our empty PATH dir.
    if (result !== null) {
      expect(result).not.toContain(path.join(tmpDir, 'empty'));
    }
  });

  it('matches a version-stamped binary sitting directly on PATH (win32)', () => {
    const bin = path.join(tmpDir, 'Godot_v4.5.1-stable_mono_win64.exe');
    fs.writeFileSync(bin, '');
    process.env['PATH'] = tmpDir;
    expect(findGodotBinary(undefined, 'win32')).toBe(bin);
  });

  it('prefers the mono _console build over plainer ones on a single PATH dir (win32)', () => {
    // Drop several builds in one dir; the mono _console variant must win.
    fs.writeFileSync(path.join(tmpDir, 'Godot_v4.5.1-stable_win64.exe'), '');
    fs.writeFileSync(path.join(tmpDir, 'Godot_v4.5.1-stable_mono_win64.exe'), '');
    const consoleBin = path.join(tmpDir, 'Godot_v4.5.1-stable_mono_win64_console.exe');
    fs.writeFileSync(consoleBin, '');
    process.env['PATH'] = tmpDir;
    expect(findGodotBinary(undefined, 'win32')).toBe(consoleBin);
  });

  it('discovers a version-stamped binary nested two folders deep under Downloads (win32)', () => {
    // Mirror the real on-disk layout of an extracted official Windows zip:
    //   <Downloads>/Godot_v4.5.1-stable_mono_win64/
    //     Godot_v4.5.1-stable_mono_win64/
    //       Godot_v4.5.1-stable_mono_win64.exe  (+ _console companion)
    const ver = 'Godot_v4.5.1-stable_mono_win64';
    const downloads = path.join(tmpDir, 'Downloads');
    const nested = path.join(downloads, ver, ver);
    fs.mkdirSync(nested, { recursive: true });
    fs.writeFileSync(path.join(nested, `${ver}.exe`), '');
    const consoleBin = path.join(nested, `${ver}_console.exe`);
    fs.writeFileSync(consoleBin, '');

    // Point the Windows common-roots scan at our sandbox via USERPROFILE/HOME so
    // the real machine's Downloads isn't consulted, and clear PATH so step 4 runs.
    const savedUserProfile = process.env['USERPROFILE'];
    const savedHome = process.env['HOME'];
    const savedProgramFiles = process.env['PROGRAMFILES'];
    const savedLocalAppData = process.env['LOCALAPPDATA'];
    process.env['USERPROFILE'] = tmpDir;
    process.env['HOME'] = tmpDir;
    // Steer the other roots at an empty dir so only Downloads can hit.
    const emptyRoot = path.join(tmpDir, 'empty-root');
    fs.mkdirSync(emptyRoot, { recursive: true });
    process.env['PROGRAMFILES'] = emptyRoot;
    process.env['LOCALAPPDATA'] = emptyRoot;
    process.env['PATH'] = emptyRoot;
    try {
      // The mono _console build is preferred.
      expect(findGodotBinary(undefined, 'win32')).toBe(consoleBin);
    } finally {
      if (savedUserProfile === undefined) delete process.env['USERPROFILE'];
      else process.env['USERPROFILE'] = savedUserProfile;
      if (savedHome === undefined) delete process.env['HOME'];
      else process.env['HOME'] = savedHome;
      if (savedProgramFiles === undefined) delete process.env['PROGRAMFILES'];
      else process.env['PROGRAMFILES'] = savedProgramFiles;
      if (savedLocalAppData === undefined) delete process.env['LOCALAPPDATA'];
      else process.env['LOCALAPPDATA'] = savedLocalAppData;
    }
  });

  it('does not descend past the bounded scan depth (win32)', () => {
    // Bury a binary deeper than SCAN_MAX_DEPTH (3) below Downloads; it must NOT
    // be found, proving the scan is bounded.
    const downloads = path.join(tmpDir, 'Downloads');
    const tooDeep = path.join(downloads, 'a', 'b', 'c', 'd', 'e');
    fs.mkdirSync(tooDeep, { recursive: true });
    fs.writeFileSync(path.join(tooDeep, 'Godot_v4.5.1-stable_mono_win64.exe'), '');

    const savedUserProfile = process.env['USERPROFILE'];
    const savedHome = process.env['HOME'];
    const savedProgramFiles = process.env['PROGRAMFILES'];
    const savedLocalAppData = process.env['LOCALAPPDATA'];
    process.env['USERPROFILE'] = tmpDir;
    process.env['HOME'] = tmpDir;
    const emptyRoot = path.join(tmpDir, 'empty-root');
    fs.mkdirSync(emptyRoot, { recursive: true });
    process.env['PROGRAMFILES'] = emptyRoot;
    process.env['LOCALAPPDATA'] = emptyRoot;
    process.env['PATH'] = emptyRoot;
    try {
      expect(findGodotBinary(undefined, 'win32')).toBeNull();
    } finally {
      if (savedUserProfile === undefined) delete process.env['USERPROFILE'];
      else process.env['USERPROFILE'] = savedUserProfile;
      if (savedHome === undefined) delete process.env['HOME'];
      else process.env['HOME'] = savedHome;
      if (savedProgramFiles === undefined) delete process.env['PROGRAMFILES'];
      else process.env['PROGRAMFILES'] = savedProgramFiles;
      if (savedLocalAppData === undefined) delete process.env['LOCALAPPDATA'];
      else process.env['LOCALAPPDATA'] = savedLocalAppData;
    }
  });
});

describe('launchEditor', () => {
  beforeEach(() => {
    spawnMock.mockReset();
  });

  it('spawns the editor detached with --editor --path and merged env, then unrefs', () => {
    const unref = vi.fn();
    const on = vi.fn();
    const fakeChild = { on, unref, pid: 4321 } as unknown as ReturnType<typeof spawn>;
    spawnMock.mockReturnValue(fakeChild);

    const child = launchEditor('/path/to/godot', '/my/project', { GODOT_MCP_HOST: 'http://localhost:9000' });

    expect(spawnMock).toHaveBeenCalledTimes(1);
    const [bin, args, opts] = spawnMock.mock.calls[0];
    expect(bin).toBe('/path/to/godot');
    expect(args).toEqual(['--editor', '--path', path.resolve('/my/project')]);
    expect(opts).toMatchObject({ detached: true, stdio: 'ignore' });
    expect((opts as { env: Record<string, string> }).env['GODOT_MCP_HOST']).toBe('http://localhost:9000');
    expect(unref).toHaveBeenCalledTimes(1);
    expect(child).toBe(fakeChild);
  });
});
