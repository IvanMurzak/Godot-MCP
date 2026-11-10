import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { CLOUD_MCP_URL, localProjectUrl } from '../src/utils/connection.js';
import {
  PROJECT_SIGNED_IN_BUT_UNUSABLE_MESSAGE,
  SIGNED_IN_BUT_UNUSABLE_MESSAGE,
  godotUserDataDir,
  readProjectConnection,
  readProjectEnvFile,
  localEditorRefusal,
  resolveEditorConnection,
  signInState,
  type ProjectConnection,
} from '../src/utils/editor-connection.js';
import { execCode } from '../src/lib/exec.js';
import { runCliAsync } from './helpers/cli.js';

const ENV_KEYS = ['GODOT_MCP_HOST', 'GODOT_MCP_CLOUD_URL', 'GODOT_MCP_CONNECTION_MODE', 'GODOT_MCP_TOKEN', 'APPDATA', 'XDG_DATA_HOME', 'AIGD_HOME', 'AI_GAME_DEV_HOME'];

let project: string;
let dataRoot: string;
const saved: Record<string, string | undefined> = {};

beforeEach(() => {
  for (const k of ENV_KEYS) {
    saved[k] = process.env[k];
    delete process.env[k];
  }
  project = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-conn-'));
  dataRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-data-'));
  process.env.APPDATA = dataRoot;
  process.env.XDG_DATA_HOME = dataRoot;
  fs.writeFileSync(path.join(project, 'project.godot'), '[application]\n\nconfig/name="My Game"\n');
});

afterEach(() => {
  for (const k of ENV_KEYS) {
    if (saved[k] === undefined) delete process.env[k];
    else process.env[k] = saved[k];
  }
  fs.rmSync(project, { recursive: true, force: true });
  fs.rmSync(dataRoot, { recursive: true, force: true });
});

const cloudProject = (): ProjectConnection => ({ mode: 'Cloud', cloudUrl: CLOUD_MCP_URL });

describe('resolveEditorConnection (owner rule: a signed-in machine is never switched to a local server)', () => {
  it('1. Cloud mode + working sign-in: the cloud hub with its token, never localhost', async () => {
    const c = await resolveEditorConnection(project, {}, { projectConnection: cloudProject, readCloudToken: async () => 'tok', signInState: () => 'machine' });
    expect(c).toEqual({ url: CLOUD_MCP_URL, token: 'tok', cloud: true, localFallback: false });
  });

  it.each(['machine', 'project'] as const)('2. Cloud mode + %s sign-in present but unusable: NOT_AUTHENTICATED, never localhost', async (state) => {
    const c = await resolveEditorConnection(project, {}, { projectConnection: cloudProject, readCloudToken: async () => undefined, signInState: () => state });
    expect(c.signInRequired).toBe(state === 'project' ? PROJECT_SIGNED_IN_BUT_UNUSABLE_MESSAGE : SIGNED_IN_BUT_UNUSABLE_MESSAGE);
    expect(c.url).toBe(CLOUD_MCP_URL);
    expect(c.localFallback).toBe(false);
  });

  it('3. Cloud mode + never signed in: the local server, flagged for a warning', async () => {
    const c = await resolveEditorConnection(project, {}, { projectConnection: cloudProject, readCloudToken: async () => undefined, signInState: () => 'missing' });
    expect(c).toEqual({ url: localProjectUrl(project), token: undefined, cloud: false, localFallback: true });
  });

  it('Custom mode is unaffected: its local server, no credential lookup', async () => {
    const never = () => {
      throw new Error('not consulted');
    };
    const c = await resolveEditorConnection(project, {}, { projectConnection: () => ({ mode: 'Custom', cloudUrl: CLOUD_MCP_URL }), readCloudToken: never, signInState: never });
    expect(c).toEqual({ url: localProjectUrl(project), token: undefined, cloud: false, localFallback: false });
  });

  it('an explicit --url or GODOT_MCP_HOST is used as is; an explicit token goes to the cloud', async () => {
    const c = await resolveEditorConnection(project, { url: 'http://localhost:5000/', token: 't' }, { projectConnection: cloudProject, signInState: () => 'machine' });
    expect(c).toEqual({ url: 'http://localhost:5000', token: 't', cloud: false, localFallback: false });
    const viaToken = await resolveEditorConnection(project, { token: 'explicit' }, { projectConnection: cloudProject, signInState: () => 'machine' });
    expect(viaToken).toMatchObject({ url: CLOUD_MCP_URL, token: 'explicit', cloud: true });
  });
});

describe('readProjectConnection (the addon precedence: env > .env > persisted > marker > Cloud)', () => {
  const persist = (config: object) => {
    const dir = godotUserDataDir(project);
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, 'godot-mcp-config.json'), JSON.stringify(config));
  };
  const marker = (serverTarget: string) => {
    fs.mkdirSync(path.join(project, '.ai-game-dev'), { recursive: true });
    fs.writeFileSync(path.join(project, '.ai-game-dev', 'project.json'), JSON.stringify({ serverTarget }));
  };

  it('is Cloud on ai-game.dev by default', () => {
    expect(readProjectConnection(project)).toEqual({ mode: 'Cloud', cloudUrl: CLOUD_MCP_URL });
  });

  it('a persisted Cloud mode beats a localhost marker (the signed-in machine is not sent to localhost)', () => {
    marker('http://localhost:23000');
    persist({ connectionMode: 'Cloud' });
    expect(readProjectConnection(project).mode).toBe('Cloud');
  });

  it('a persisted Custom mode beats a hosted marker; the marker decides when nothing else does', () => {
    marker('https://staging.ai-game.dev');
    expect(readProjectConnection(project)).toMatchObject({ mode: 'Cloud', cloudUrl: 'https://staging.ai-game.dev/mcp' });
    persist({ connectionMode: 'Custom' });
    expect(readProjectConnection(project).mode).toBe('Custom');
  });

  it('the .env mode beats persisted, a loopback .env host means Custom, and the env beats both', () => {
    persist({ connectionMode: 'Custom', cloudBaseUrl: 'https://dev.example' });
    fs.writeFileSync(path.join(project, '.env'), "GODOT_MCP_CONNECTION_MODE='Custom'\nGODOT_MCP_CONNECTION_MODE=\"Cloud\"\n");
    expect(readProjectConnection(project)).toMatchObject({ mode: 'Cloud', cloudUrl: 'https://dev.example/mcp' });
    fs.writeFileSync(path.join(project, '.env'), 'GODOT_MCP_HOST=http://localhost:4567\nGODOT_MCP_TOKEN=abc\n');
    persist({ connectionMode: 'Cloud' });
    expect(readProjectConnection(project)).toMatchObject({ mode: 'Custom', customHost: 'http://localhost:4567', envFileToken: 'abc' });
    process.env.GODOT_MCP_CONNECTION_MODE = 'cloud';
    expect(readProjectConnection(project).mode).toBe('Cloud');
  });

  it('parses .env like the addon: comments, first "=", last duplicate wins, quotes stripped', () => {
    fs.writeFileSync(path.join(project, '.env'), '# c\nGODOT_MCP_TOKEN = "a=b"\nOTHER=1\nGODOT_MCP_HOST=x\nGODOT_MCP_HOST=\'y\'\n');
    expect(readProjectEnvFile(project)).toEqual({ GODOT_MCP_TOKEN: 'a=b', GODOT_MCP_HOST: 'y' });
  });

  it('finds the user dir by config/name, or the custom user dir (which keeps "/")', () => {
    expect(godotUserDataDir(project, 'win32')).toBe(path.join(dataRoot, 'Godot', 'app_userdata', 'My Game'));
    fs.writeFileSync(path.join(project, 'project.godot'), '[application]\n\nconfig/name="X"\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name="studio/x"\n');
    expect(godotUserDataDir(project, 'win32')).toBe(path.join(dataRoot, 'studio/x'));
  });
});

describe('signInState', () => {
  it('counts a per-project or legacy credential as a sign-in', () => {
    // The machine store lives under the real home: only assert the project-level cases.
    fs.mkdirSync(path.join(project, '.ai-game-dev'), { recursive: true });
    fs.writeFileSync(path.join(project, '.ai-game-dev', 'credentials.json'), '{ broken');
    expect(signInState(project)).toBe('project');
    fs.rmSync(path.join(project, '.ai-game-dev'), { recursive: true });
    fs.mkdirSync(path.join(project, '.godot-mcp'), { recursive: true });
    fs.writeFileSync(path.join(project, '.godot-mcp', 'credentials.json'), '{}');
    expect(signInState(project)).toBe('project');
  });
});

describe('execCode with remoteUrl (Cloud mode)', () => {
  it('runs on the given server with the token and starts no editor', async () => {
    const invoke = vi.fn(async () => ({ kind: 'success' as const, success: true as const, endpoint: '', httpStatus: 200, data: { status: 'success' } }));
    const ensure = vi.fn();
    const r = await execCode({ projectPath: project, code: '1', remoteUrl: CLOUD_MCP_URL, token: 'tok', deps: { invoke, ensure } });
    expect(r.kind).toBe('success');
    expect(ensure).not.toHaveBeenCalled();
    expect(invoke).toHaveBeenCalledWith(expect.objectContaining({ url: CLOUD_MCP_URL, token: 'tok' }));
  });
});

describe('localEditorRefusal (no headless editor on a local server for a signed-in Cloud project)', () => {
  const base = { token: undefined, localFallback: false };
  it('refuses an unusable sign-in (NOT_AUTHENTICATED) and a working one (CLOUD_PROJECT)', () => {
    expect(localEditorRefusal({ ...base, url: CLOUD_MCP_URL, cloud: true, signInRequired: SIGNED_IN_BUT_UNUSABLE_MESSAGE }))
      .toEqual({ code: 'NOT_AUTHENTICATED', message: SIGNED_IN_BUT_UNUSABLE_MESSAGE });
    expect(localEditorRefusal({ ...base, url: CLOUD_MCP_URL, token: 'tok', cloud: true })?.code).toBe('CLOUD_PROJECT');
  });

  it('allows a never-signed-in fallback, Custom mode and an explicit --url', () => {
    expect(localEditorRefusal({ url: localProjectUrl(project), token: undefined, cloud: false, localFallback: true })).toBeUndefined();
    expect(localEditorRefusal({ ...base, url: 'http://localhost:5000', cloud: false })).toBeUndefined();
  });
});

describe('the CLI on a Cloud project whose sign-in cannot be used (corrupted credentials)', () => {
  const HOME_KEYS = ['USERPROFILE', 'HOME'];
  const savedHome: Record<string, string | undefined> = {};
  let home: string;

  beforeEach(() => {
    // A throwaway home: the machine store of the real user is never read.
    home = fs.mkdtempSync(path.join(os.tmpdir(), 'godot-cli-home-'));
    for (const k of HOME_KEYS) {
      savedHome[k] = process.env[k];
      process.env[k] = home;
    }
    fs.mkdirSync(path.join(project, '.ai-game-dev'), { recursive: true });
    fs.writeFileSync(path.join(project, '.ai-game-dev', 'credentials.json'), '{ broken');
  });

  afterEach(() => {
    for (const k of HOME_KEYS) {
      if (savedHome[k] === undefined) delete process.env[k];
      else process.env[k] = savedHome[k];
    }
    fs.rmSync(home, { recursive: true, force: true });
  });

  // A missing --editor-path exits 1 if the CLI goes on to start an editor: exit 6 proves it stopped before that.
  const missingEditor = path.join(os.tmpdir(), 'no-such-godot.exe');

  it('open --headless: NOT_AUTHENTICATED (exit 6), no local editor', async () => {
    const r = await runCliAsync(['open', project, '--headless', '--no-build', '--editor-path', missingEditor]);
    expect(r.exitCode).toBe(6);
    expect(r.stdout).toContain('signed in to ai-game.dev, but the sign-in cannot be used');
  });

  it('open (windowed): NOT_AUTHENTICATED (exit 6), no editor on a connection it cannot use', async () => {
    const r = await runCliAsync(['open', project, '--no-build', '--editor-path', missingEditor]);
    expect(r.exitCode).toBe(6);
  });

  it('tools: NOT_AUTHENTICATED (exit 6), no started editor', async () => {
    const r = await runCliAsync(['tools', '--path', project, '--format', 'json', '--editor-path', missingEditor]);
    expect(r.exitCode).toBe(6);
    expect(r.stdout).toContain('"NOT_AUTHENTICATED"');
  });
});
