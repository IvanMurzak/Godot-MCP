import { Command } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { DEFAULT_CLOUD_BASE_URL } from '../utils/connection.js';
import { readCredentials } from '../utils/credentials.js';
import { readMachineCredentials, getMachineCredentialsPath } from '../utils/machine-credentials.js';
import { runCloudLogin, type CredentialSink } from '../utils/cloud-login.js';

interface LoginOptions {
  path?: string;
  project?: string;
  baseUrl?: string;
  force?: boolean;
}

export const loginCommand = new Command('login')
  .description(
    'Authenticate with the Godot-MCP cloud server (ai-game.dev) via the RFC 8628 device-authorization flow. ' +
      'By default the credential is saved to the shared machine store (~/.ai-game-dev/credentials.json, 0600 / ' +
      'DPAPI) so you sign in once per machine and the editor plugin auto-adopts it. Use --project <path> to keep ' +
      'a per-project credential (project-local .godot-mcp/credentials.json, gitignored) instead.',
  )
  .argument('[path]', 'Project path for a per-project credential override (alias of --project). Omit for the machine store.')
  .option('--project <path>', 'Save a per-project credential under <path>/.godot-mcp/ instead of the machine store')
  .option('--path <path>', 'Alias of --project (kept for backward compatibility)')
  .option('--base-url <url>', 'Override the cloud base URL (default: https://ai-game.dev)')
  .option('--force', 'Re-authenticate even if a credential is already saved')
  .action(async (positionalPath: string | undefined, options: LoginOptions) => {
    const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');

    const { sink, savedLocationLabel } = resolveSink(positionalPath, options);
    verbose(`Credential store: ${savedLocationLabel}`);

    // Only short-circuit when a credential is saved in the SAME store AND it was issued against the
    // SAME base URL. Otherwise `login --base-url <other>` (without --force) would silently reuse a
    // credential minted for a different server.
    if (!options.force && isAlreadyAuthenticated(sink, baseUrl)) {
      ui.success('Already authenticated with the cloud server.');
      ui.info('Use --force to re-authenticate.');
      return;
    }

    ui.heading('Cloud Authentication');
    ui.label('Server', baseUrl);
    ui.divider();

    const token = await runCloudLogin({ baseUrl, sink });
    if (token) {
      ui.success(`Authentication complete. Cloud token saved to ${savedLocationLabel}.`);
      ui.info('Run: godot-cli open --mode Cloud   (no --token needed).');
    } else {
      process.exit(1);
    }
  });

/**
 * Resolve where the credential should be stored. A project override (via --project, the legacy
 * --path, or the positional arg) selects the project-local store; otherwise the shared machine store.
 */
function resolveSink(
  positionalPath: string | undefined,
  options: LoginOptions,
): { sink: CredentialSink; savedLocationLabel: string } {
  const overrideRaw = options.project ?? options.path ?? positionalPath;
  if (overrideRaw !== undefined) {
    const projectPath = path.resolve(overrideRaw);
    if (!fs.existsSync(projectPath)) {
      ui.error(`Project path does not exist: ${projectPath}`);
      process.exit(1);
    }
    return {
      sink: { kind: 'project', projectPath },
      savedLocationLabel: path.join(projectPath, '.godot-mcp', 'credentials.json'),
    };
  }
  return { sink: { kind: 'machine' }, savedLocationLabel: getMachineCredentialsPath() };
}

function isAlreadyAuthenticated(sink: CredentialSink, baseUrl: string): boolean {
  try {
    if (sink.kind === 'project') {
      const creds = readCredentials(sink.projectPath);
      return !!creds?.cloudToken && creds.cloudBaseUrl === baseUrl;
    }
    const creds = readMachineCredentials(sink.storeBaseDir);
    return !!creds?.accessToken && creds.serverTarget === baseUrl;
  } catch {
    return false;
  }
}
