import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { CLOUD_MCP_URL, localProjectUrl } from '../src/utils/connection.js';
import {
  SIGNED_IN_BUT_UNUSABLE_MESSAGE,
  godotUserDataDir,
  projectConnectionMode,
  resolveEditorConnection,
} from '../src/utils/editor-connection.js';

const ENV_KEYS = ['GODOT_MCP_HOST', 'GODOT_MCP_CLOUD_URL', 'GODOT_MCP_CONNECTION_MODE', 'GODOT_MCP_TOKEN', 'APPDATA', 'XDG_DATA_HOME'];

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

const cloud = { connectionMode: () => 'Cloud' as const };

describe('resolveEditorConnection (owner rule: a signed-in machine is never switched to a local server)', () => {
  it('1. Cloud mode + working sign-in: the cloud hub with its token, never localhost', async () => {
    const c = await resolveEditorConnection(project, {}, { ...cloud, readCloudToken: async () => 'tok', credentialState: () => 'ok' });
    expect(c).toEqual({ url: CLOUD_MCP_URL, token: 'tok', cloud: true, localFallback: false });
  });

  it.each(['ok', 'unreadable'] as const)('2. Cloud mode + sign-in present but unusable (%s): NOT_AUTHENTICATED, never localhost', async (state) => {
    const c = await resolveEditorConnection(project, {}, { ...cloud, readCloudToken: async () => undefined, credentialState: () => state });
    expect(c.signInRequired).toBe(SIGNED_IN_BUT_UNUSABLE_MESSAGE);
    expect(c.url).toBe(CLOUD_MCP_URL);
    expect(c.localFallback).toBe(false);
  });

  it('3. Cloud mode + never signed in (store missing): the local server, flagged for a warning', async () => {
    const c = await resolveEditorConnection(project, {}, { ...cloud, readCloudToken: async () => undefined, credentialState: () => 'missing' });
    expect(c).toEqual({ url: localProjectUrl(project), token: undefined, cloud: false, localFallback: true });
  });

  it('Custom mode is unaffected: the local server, no credential lookup', async () => {
    const c = await resolveEditorConnection(project, {}, {
      connectionMode: () => 'Custom',
      readCloudToken: async () => { throw new Error('not consulted'); },
      credentialState: () => { throw new Error('not consulted'); },
    });
    expect(c).toEqual({ url: localProjectUrl(project), token: undefined, cloud: false, localFallback: false });
  });

  it('an explicit --url / --token is used as is', async () => {
    const c = await resolveEditorConnection(project, { url: 'http://localhost:5000', token: 't' }, { ...cloud, credentialState: () => 'unreadable' });
    expect(c).toEqual({ url: 'http://localhost:5000', token: 't', cloud: false, localFallback: false });
    const viaToken = await resolveEditorConnection(project, { token: 'explicit' }, { ...cloud, credentialState: () => 'unreadable' });
    expect(viaToken).toMatchObject({ url: CLOUD_MCP_URL, token: 'explicit', cloud: true });
  });
});

describe('projectConnectionMode', () => {
  it('is Cloud by default (the addon default), and reads the persisted config, the project .env and the env', () => {
    expect(projectConnectionMode(project)).toBe('Cloud');
    const dir = godotUserDataDir(project);
    expect(dir).toContain(path.join('app_userdata', 'My Game'));
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, 'godot-mcp-config.json'), JSON.stringify({ connectionMode: 'Custom' }));
    expect(projectConnectionMode(project)).toBe('Custom');
    fs.writeFileSync(path.join(project, '.env'), 'GODOT_MCP_CONNECTION_MODE=Cloud\n');
    expect(projectConnectionMode(project)).toBe('Cloud');
    process.env.GODOT_MCP_CONNECTION_MODE = 'custom';
    expect(projectConnectionMode(project)).toBe('Custom');
  });

  it('uses a custom user dir when the project names one', () => {
    fs.writeFileSync(path.join(project, 'project.godot'), '[application]\n\nconfig/name="X"\nconfig/use_custom_user_dir=true\nconfig/custom_user_dir_name="studio/x"\n');
    expect(godotUserDataDir(project)).toBe(path.join(dataRoot, 'studio-x'));
  });
});
