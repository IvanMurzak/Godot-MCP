import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { enrollPlugin } from '../src/lib/enroll.js';
import { readMachineCredentials } from '../src/utils/machine-credentials.js';
import { readProjectMarker } from '../src/utils/project-marker.js';
import { derivePinV2, derivePortV2 } from '../src/utils/project-identity.js';

function mockRedeem(serverUrl: string): typeof fetch {
  return (async () =>
    ({
      ok: true,
      status: 200,
      statusText: 'OK',
      json: async () => ({
        access_token: 'plugin.jwt',
        token_type: 'Bearer',
        expires_in: 3600,
        refresh_token: 'refresh.jwt',
        scope: 'mcp:plugin',
        server_url: serverUrl,
      }),
    }) as unknown as Response) as unknown as typeof fetch;
}

function mockRedeemError(): typeof fetch {
  return (async () =>
    ({
      ok: false,
      status: 400,
      statusText: 'Bad Request',
      json: async () => ({ error: 'invalid_grant', error_description: 'Invalid or expired enrollment code.' }),
    }) as unknown as Response) as unknown as typeof fetch;
}

describe('enrollPlugin', () => {
  let project: string;
  let store: string;
  beforeEach(() => {
    project = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-enroll-proj-'));
    store = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-enroll-store-'));
  });
  afterEach(() => {
    fs.rmSync(project, { recursive: true, force: true });
    fs.rmSync(store, { recursive: true, force: true });
  });

  it('redeems, plants the credential in the machine store, and writes the marker (hosted target — no port)', async () => {
    const result = await enrollPlugin({
      godotProjectPath: project,
      code: 'CODE-1',
      storeBaseDir: store,
      fetchImpl: mockRedeem('https://ai-game.dev'),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;

    expect(result.serverTarget).toBe('https://ai-game.dev');
    expect(result.pin).toBe(derivePinV2(project));
    expect(result.port).toBeUndefined(); // hosted → no derived local port

    const creds = readMachineCredentials(store);
    expect(creds?.accessToken).toBe('plugin.jwt');
    expect(creds?.refreshToken).toBe('refresh.jwt');
    expect(creds?.serverTarget).toBe('https://ai-game.dev');
    expect(creds?.expiresAt).toBeTruthy();

    const marker = readProjectMarker(project);
    expect(marker?.serverTarget).toBe('https://ai-game.dev');
    expect(marker?.pin).toBe(derivePinV2(project));
    expect(marker?.port).toBeUndefined();
  });

  it('records the deterministic local port for a localhost target', async () => {
    const result = await enrollPlugin({
      godotProjectPath: project,
      code: 'CODE-1',
      storeBaseDir: store,
      fetchImpl: mockRedeem('http://localhost:20001'),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.port).toBe(derivePortV2(project));
    const marker = readProjectMarker(project);
    expect(marker?.port).toBe(derivePortV2(project));
  });

  it('upserts the D14 pin into an existing project-local .mcp.json', async () => {
    const mcpJson = path.join(project, '.mcp.json');
    fs.writeFileSync(
      mcpJson,
      JSON.stringify({ mcpServers: { 'ai-game-developer': { type: 'http', url: 'https://ai-game.dev/mcp' } } }, null, 2),
    );
    const result = await enrollPlugin({
      godotProjectPath: project,
      code: 'CODE-1',
      storeBaseDir: store,
      fetchImpl: mockRedeem('https://ai-game.dev'),
    });
    expect(result.kind).toBe('success');
    if (result.kind !== 'success') return;
    expect(result.pinnedConfigs).toContain(mcpJson);
    const root = JSON.parse(fs.readFileSync(mcpJson, 'utf-8'));
    expect(root.mcpServers['ai-game-developer'].url).toBe(`https://ai-game.dev/mcp/p/${derivePinV2(project)}`);
  });

  it('writes NOTHING on a redeem failure (no marker, no credential)', async () => {
    const result = await enrollPlugin({
      godotProjectPath: project,
      code: 'BAD',
      storeBaseDir: store,
      fetchImpl: mockRedeemError(),
    });
    expect(result.kind).toBe('failure');
    if (result.kind === 'failure') expect(result.error.message).toMatch(/Invalid or expired/);
    expect(readMachineCredentials(store)).toBeNull();
    expect(readProjectMarker(project)).toBeNull();
  });
});
