import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import {
  resolveConnection,
  CLOUD_MCP_URL,
  DEFAULT_CLOUD_BASE_URL,
  DEFAULT_CUSTOM_HOST,
  ENV_HOST,
  ENV_CLOUD_URL,
  ENV_TOKEN,
  ENV_CONNECTION_MODE,
} from '../src/utils/connection.js';

describe('resolveConnection', () => {
  const saved: Record<string, string | undefined> = {};
  const ENV_KEYS = [ENV_HOST, ENV_CLOUD_URL, ENV_TOKEN, ENV_CONNECTION_MODE];

  beforeEach(() => {
    for (const k of ENV_KEYS) {
      saved[k] = process.env[k];
      delete process.env[k];
    }
  });

  afterEach(() => {
    for (const k of ENV_KEYS) {
      if (saved[k] === undefined) delete process.env[k];
      else process.env[k] = saved[k];
    }
  });

  it('uses an explicit --url (trailing slash stripped) over everything', () => {
    process.env[ENV_HOST] = 'http://env-host:1111';
    const { url } = resolveConnection('/proj', { url: 'http://explicit:9000/' });
    expect(url).toBe('http://explicit:9000');
  });

  it('uses GODOT_MCP_HOST when no --url is given', () => {
    process.env[ENV_HOST] = 'http://localhost:5544/';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe('http://localhost:5544');
  });

  it('strips a trailing /mcp from GODOT_MCP_CLOUD_URL to get the base', () => {
    process.env[ENV_CLOUD_URL] = 'https://example.test/mcp';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe('https://example.test');
  });

  it('falls back to the cloud base when mode is Cloud', () => {
    process.env[ENV_CONNECTION_MODE] = 'Cloud';
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(DEFAULT_CLOUD_BASE_URL);
  });

  it('falls back to the default custom host otherwise', () => {
    const { url } = resolveConnection('/proj', {});
    expect(url).toBe(DEFAULT_CUSTOM_HOST);
  });

  it('resolves the token from --token, then GODOT_MCP_TOKEN', () => {
    process.env[ENV_TOKEN] = '"env-tok"';
    expect(resolveConnection('/proj', { token: 'flag-tok' }).token).toBe('flag-tok');
    // env value is normalized (wrapping quotes stripped)
    expect(resolveConnection('/proj', {}).token).toBe('env-tok');
  });

  it('exposes the cloud MCP-client URL constant as <base>/mcp', () => {
    expect(CLOUD_MCP_URL).toBe(`${DEFAULT_CLOUD_BASE_URL}/mcp`);
  });
});
