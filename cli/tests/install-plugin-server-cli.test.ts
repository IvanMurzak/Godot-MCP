import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import * as fs from 'fs';
import * as os from 'os';
import * as path from 'path';
import { runCliAsync } from './helpers/cli.js';
import { getManagedServerExecutablePath } from '../src/lib/install-server.js';
import { serverExecutableFileName } from '../src/utils/server-source.js';

const MINIMAL_PROJECT = '; Engine configuration file.\nconfig_version=5\n\n[application]\n\nconfig/name="Test"\n';
const CSPROJ = '<Project Sdk="Godot.NET.Sdk/4.3.0">\n  <PropertyGroup>\n    <TargetFramework>net8.0</TargetFramework>\n  </PropertyGroup>\n</Project>\n';

describe('install-plugin / configure — new CLI surface (offline)', () => {
  let project: string;
  let addonSrc: string;
  let serverSrc: string;
  beforeEach(() => {
    project = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-cli-proj-'));
    fs.writeFileSync(path.join(project, 'project.godot'), MINIMAL_PROJECT);
    fs.writeFileSync(path.join(project, 'MyGame.csproj'), CSPROJ);

    // A local addon source (for --source, offline install).
    addonSrc = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-cli-addon-'));
    const addon = path.join(addonSrc, 'addons', 'godot_mcp');
    fs.mkdirSync(path.join(addon, 'Editor'), { recursive: true });
    fs.writeFileSync(path.join(addon, 'plugin.cfg'), '[plugin]\nname="godot_mcp"\n');
    fs.writeFileSync(path.join(addon, 'Editor', 'GodotMcpPlugin.cs'), '// boot\n');

    // A local server source (for --server-source, offline install).
    serverSrc = fs.mkdtempSync(path.join(os.tmpdir(), 'gmcp-cli-srv-'));
    fs.writeFileSync(path.join(serverSrc, serverExecutableFileName()), '#!/bin/sh\necho stub\n');
  });
  afterEach(() => {
    for (const d of [project, addonSrc, serverSrc]) fs.rmSync(d, { recursive: true, force: true });
  });

  it('install-plugin --source --with-server --server-source installs the addon AND the managed server binary (offline)', async () => {
    const { stdout, exitCode } = await runCliAsync([
      'install-plugin',
      '--source',
      addonSrc,
      '--with-server',
      '--server-source',
      serverSrc,
      project,
    ]);
    expect(exitCode, stdout).toBe(0);
    // Addon materialized.
    expect(fs.existsSync(path.join(project, 'addons', 'godot_mcp', 'plugin.cfg'))).toBe(true);
    // Managed server binary present.
    expect(fs.existsSync(getManagedServerExecutablePath(project))).toBe(true);
  });

  it('install-plugin --enroll and --enroll-stdin together is a usage error', async () => {
    const { stdout, exitCode } = await runCliAsync(['install-plugin', '--enroll', 'X', '--enroll-stdin', project]);
    expect(exitCode).toBe(1);
    expect(stdout).toMatch(/not both/i);
  });

  it('configure --agent without a managed server binary fails with an actionable message', async () => {
    const { stdout, exitCode } = await runCliAsync(['configure', '--agent', 'claude-code', project]);
    expect(exitCode).toBe(1);
    expect(stdout).toMatch(/install-plugin --with-server/);
  });
});
