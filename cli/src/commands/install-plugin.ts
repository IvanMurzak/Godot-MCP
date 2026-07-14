import { Command } from 'commander';
import { createRequire } from 'module';
import * as fs from 'fs';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { DEFAULT_CLOUD_BASE_URL } from '../utils/connection.js';
import { installPlugin } from '../lib/install-plugin.js';
import { installServer } from '../lib/install-server.js';
import { enrollPlugin } from '../lib/enroll.js';

interface InstallPluginCliOptions {
  path?: string;
  source?: string;
  version?: string;
  withServer?: boolean;
  serverSource?: string;
  serverVersion?: string;
  enroll?: string;
  enrollStdin?: boolean;
  baseUrl?: string;
}

/** The CLI's own version — the default addon release version to download. */
function cliVersion(): string {
  try {
    const require = createRequire(import.meta.url);
    const pkg = require('../../package.json') as { version: string };
    return pkg.version;
  } catch {
    return '';
  }
}

/** Read a single enrollment code from stdin (so it never lands in argv / shell history). */
function readEnrollCodeFromStdin(): string {
  let content: string;
  try {
    content = fs.readFileSync(0, 'utf-8');
  } catch {
    ui.error('Failed to read the enrollment code from stdin.');
    process.exit(1);
  }
  const code = content.trim();
  if (code.length === 0) {
    ui.error('No enrollment code received on stdin.');
    process.exit(1);
  }
  return code;
}

export const installPluginCommand = new Command('install-plugin')
  .description(
    'Install the godot_mcp addon into a Godot C# project: materialize res://addons/godot_mcp/ (download the matching GitHub release, or --source a local copy), add the required NuGet PackageReferences and the extension-catalog <EmbeddedResource> to the project .csproj, and enable the plugin in project.godot. Optionally download the RID-matched gamedev-mcp-server binary (--with-server) and/or redeem an agent enrollment code (--enroll / --enroll-stdin).',
  )
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .option(
    '--source <path>',
    'Install the addon from a local directory (offline / dev / CI) instead of downloading it',
  )
  .option(
    '--version <x.y.z>',
    'Addon release version to download (defaults to this CLI version). Ignored with --source.',
  )
  .option(
    '--with-server',
    'Also download the RID-matched gamedev-mcp-server binary into the CLI-managed dir (verified against the release SHA256SUMS)',
  )
  .option(
    '--server-source <path>',
    'Install the server binary from a local directory or .zip (offline / dev / CI) instead of downloading it. Implies --with-server.',
  )
  .option(
    '--server-version <x.y.z>',
    "Server version to download (defaults to the addon's pinned ServerVersion). Ignored with --server-source.",
  )
  .option('--enroll <code>', 'Redeem an agent enrollment code (plants a plugin credential, no browser hop)')
  .option('--enroll-stdin', 'Read the enrollment code from stdin instead of argv (keeps it out of shell history)')
  .option('--base-url <url>', 'Cloud base URL to redeem the enrollment code against (default: https://ai-game.dev)')
  .action(async (positionalPath: string | undefined, options: InstallPluginCliOptions) => {
    const resolvedPath = positionalPath ?? options.path ?? process.cwd();
    const projectPath = path.resolve(resolvedPath);

    // Resolve the enrollment code (argv vs stdin) up-front so misuse fails fast.
    if (options.enroll !== undefined && options.enrollStdin) {
      ui.error('Use either --enroll <code> or --enroll-stdin, not both.');
      process.exit(1);
    }
    const enrollCode = options.enrollStdin ? readEnrollCodeFromStdin() : options.enroll;
    const wantServer = options.withServer === true || options.serverSource !== undefined;

    let failed = false;

    // 1. Addon install (the existing real installer — always runs).
    ui.heading('Installing godot_mcp addon');
    verbose(`Project path: ${projectPath}`);
    if (options.source) verbose(`--source: ${options.source}`);

    const spinner = ui.startSpinner('Installing addon...');
    const result = await installPlugin({
      godotProjectPath: projectPath,
      source: options.source,
      version: options.version ?? cliVersion(),
    });

    if (result.kind === 'failure') {
      spinner.error('Failed to install plugin');
      ui.error(result.error.message);
      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
      process.exit(1);
    }

    if (result.changed) {
      spinner.success('godot_mcp addon installed and enabled');
    } else {
      spinner.success('godot_mcp addon was already enabled');
    }

    // Addon files outcome.
    if (result.materialize.source === 'download') {
      ui.label('Addon files', `downloaded → ${result.materialize.addonDir}`);
    } else if (result.materialize.source === 'local') {
      ui.label('Addon files', `copied from ${result.materialize.sourceDir} → ${result.materialize.addonDir}`);
    }

    // csproj outcome.
    if (result.csproj.csprojPath) {
      const summary = result.csproj.packages.map((p) => `${p.id}@${p.version} (${p.action})`).join(', ');
      ui.label('NuGet packages', summary || '(none)');
      const embedSummary = result.csproj.embeds.map((e) => `${e.logicalName} (${e.action})`).join(', ');
      ui.label('Embedded resources', embedSummary || '(none)');
    }

    ui.label('project.godot', result.projectGodotPath);
    ui.label('Enabled plugins', result.enabledPlugins.join(', ') || '(none)');

    for (const warning of result.warnings) {
      console.log('');
      ui.warn(warning);
    }

    // 2. Server binary (--with-server / --server-source).
    if (wantServer) {
      console.log('');
      ui.heading('Downloading gamedev-mcp-server');
      const serverSpinner = ui.startSpinner('Installing server binary...');
      const serverResult = await installServer({
        godotProjectPath: projectPath,
        source: options.serverSource,
        version: options.serverVersion,
      });
      if (serverResult.kind === 'failure') {
        serverSpinner.error('Failed to install server binary');
        ui.error(serverResult.error.message);
        failed = true;
      } else {
        serverSpinner.success(
          serverResult.source === 'download'
            ? `gamedev-mcp-server ${serverResult.version} (${serverResult.rid}) verified + installed`
            : `gamedev-mcp-server installed from local source (${serverResult.rid})`,
        );
        ui.label('Server binary', serverResult.executablePath);
        for (const warning of serverResult.warnings) {
          console.log('');
          ui.warn(warning);
        }
      }
    }

    // 3. Enrollment (--enroll / --enroll-stdin).
    if (enrollCode !== undefined) {
      console.log('');
      ui.heading('Redeeming enrollment code');
      const enrollSpinner = ui.startSpinner('Redeeming...');
      const enrollResult = await enrollPlugin({
        godotProjectPath: projectPath,
        code: enrollCode,
        baseUrl: options.baseUrl ?? DEFAULT_CLOUD_BASE_URL,
      });
      if (enrollResult.kind === 'failure') {
        enrollSpinner.error('Enrollment failed');
        ui.error(enrollResult.error.message);
        failed = true;
      } else {
        enrollSpinner.success('Enrolled — plugin credential planted in the machine store');
        ui.label('Server target', enrollResult.serverTarget);
        ui.label('Project pin', enrollResult.pin);
        if (enrollResult.port !== undefined) ui.label('Local port', String(enrollResult.port));
        ui.label('Credential', enrollResult.credentialsPath);
        ui.label('Project marker', enrollResult.markerPath);
        if (enrollResult.pinnedConfigs.length > 0) {
          ui.label('Pinned configs', enrollResult.pinnedConfigs.join(', '));
        }
        for (const warning of enrollResult.warnings) {
          console.log('');
          ui.warn(warning);
        }
      }
    }

    if (failed) process.exit(1);
  });
