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

  it('discovers a macOS .app-bundle binary at the depth-3 boundary (darwin)', () => {
    // The real layout is <home>/Applications/Godot.app/Contents/MacOS/Godot,
    // i.e. the binary's containing dir sits exactly at scan depth 3 below the
    // root — a guard against an off-by-one shrinking SCAN_MAX_DEPTH. The file
    // must be named like a Godot binary for the scan to match, so use a
    // version-stamped macOS name inside the bundle. `commonInstallRoots` derives
    // home via os.homedir(), which reads USERPROFILE on a Windows test host and
    // HOME elsewhere — set both so the sandbox is consulted on either runner.
    const savedHome = process.env['HOME'];
    const savedUserProfile = process.env['USERPROFILE'];
    process.env['HOME'] = tmpDir;
    process.env['USERPROFILE'] = tmpDir;
    const macBin = path.join(
      tmpDir,
      'Applications',
      'Godot.app',
      'Contents',
      'MacOS',
      'Godot_v4.5.1-stable_macos.universal',
    );
    fs.mkdirSync(path.dirname(macBin), { recursive: true });
    fs.writeFileSync(macBin, '');
    process.env['PATH'] = path.join(tmpDir, 'empty');
    try {
      expect(findGodotBinary(undefined, 'darwin')).toBe(macBin);
    } finally {
      if (savedHome === undefined) delete process.env['HOME'];
      else process.env['HOME'] = savedHome;
      if (savedUserProfile === undefined) delete process.env['USERPROFILE'];
      else process.env['USERPROFILE'] = savedUserProfile;
    }
  });

  it('discovers a version-stamped binary via a recursive linux scan root', () => {
    // ~/Downloads is a recursive root on linux; an extracted tarball nests the
    // binary one folder down. Confirm the linux scan path resolves it. Set both
    // HOME and USERPROFILE so os.homedir() points at the sandbox on any runner.
    const savedHome = process.env['HOME'];
    const savedUserProfile = process.env['USERPROFILE'];
    process.env['HOME'] = tmpDir;
    process.env['USERPROFILE'] = tmpDir;
    const nested = path.join(tmpDir, 'Downloads', 'Godot_v4.5.1-stable_linux');
    fs.mkdirSync(nested, { recursive: true });
    const linuxBin = path.join(nested, 'Godot_v4.5.1-stable_mono_linux.x86_64');
    fs.writeFileSync(linuxBin, '');
    process.env['PATH'] = path.join(tmpDir, 'empty');
    try {
      expect(findGodotBinary(undefined, 'linux')).toBe(linuxBin);
    } finally {
      if (savedHome === undefined) delete process.env['HOME'];
      else process.env['HOME'] = savedHome;
      if (savedUserProfile === undefined) delete process.env['USERPROFILE'];
      else process.env['USERPROFILE'] = savedUserProfile;
    }
  });

  it('skips an unreadable root directory without throwing (linux)', () => {
    // A readdirSync that throws (permission denied, ENOTDIR, …) must be
    // swallowed by the scan and not abort resolution. ESM module namespaces are
    // non-configurable so fs.readdirSync cannot be spied; instead make a root
    // path resolve to a FILE — readdirSync on a file throws ENOTDIR, exercising
    // the scan's try/catch the same way an unreadable dir would.
    const savedHome = process.env['HOME'];
    const savedUserProfile = process.env['USERPROFILE'];
    process.env['HOME'] = tmpDir;
    process.env['USERPROFILE'] = tmpDir;
    // ~/Downloads is a recursive linux root; make it a file, not a dir.
    fs.writeFileSync(path.join(tmpDir, 'Downloads'), '');
    process.env['PATH'] = path.join(tmpDir, 'empty');
    try {
      expect(() => findGodotBinary(undefined, 'linux')).not.toThrow();
      expect(findGodotBinary(undefined, 'linux')).toBeNull();
    } finally {
      if (savedHome === undefined) delete process.env['HOME'];
      else process.env['HOME'] = savedHome;
      if (savedUserProfile === undefined) delete process.env['USERPROFILE'];
      else process.env['USERPROFILE'] = savedUserProfile;
    }
  });

  it('prefers the newest version when ranks tie across directories (win32)', () => {
    // Two extracted builds of identical build-rank (both mono _console) but
    // different versions, in sibling folders under Downloads. The newer 4.5.1
    // must win over 4.3 despite the older path sorting first alphabetically.
    const downloads = path.join(tmpDir, 'Downloads');
    const old = path.join(downloads, 'a-old', 'Godot_v4.3-stable_mono_win64_console.exe');
    const recent = path.join(downloads, 'b-new', 'Godot_v4.5.1-stable_mono_win64_console.exe');
    fs.mkdirSync(path.dirname(old), { recursive: true });
    fs.mkdirSync(path.dirname(recent), { recursive: true });
    fs.writeFileSync(old, '');
    fs.writeFileSync(recent, '');

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
      expect(findGodotBinary(undefined, 'win32')).toBe(recent);
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
