import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { resolveProjectPath, DEFAULT_CLOUD_BASE_URL } from '../utils/connection.js';
import { readCloudToken, readCredentials } from '../utils/credentials.js';
import { runCloudLogin } from '../utils/cloud-login.js';

interface LoginOptions {
  path?: string;
  baseUrl?: string;
  force?: boolean;
}

export const loginCommand = new Command('login')
  .description(
    'Authenticate with the Godot-MCP cloud server (ai-game.dev) via the RFC 8628 device-authorization flow and persist the cloud token so `godot-cli open --mode Cloud` connects without a manual --token.',
  )
  .argument('[path]', 'Godot project path — where the token is saved (defaults to current directory)')
  .option('--path <path>', 'Godot project path (defaults to current directory)')
  .option('--base-url <url>', 'Override the cloud base URL (default: https://ai-game.dev)')
  .option('--force', 'Re-authenticate even if a cloud token is already saved')
  .action(async (positionalPath: string | undefined, options: LoginOptions) => {
    const projectPath = resolveProjectPath(positionalPath, options);
    verbose(`Project path: ${projectPath}`);

    const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');

    // Only short-circuit when a token is saved AND it was issued against the SAME
    // base URL. Otherwise `login --base-url <other>` (without --force) would silently
    // reuse a token minted for a different server. readCloudToken() truthy implies the
    // credentials file parsed, so the follow-up readCredentials() cannot throw here.
    if (!options.force && readCloudToken(projectPath) && readCredentials(projectPath)?.cloudBaseUrl === baseUrl) {
      ui.success('Already authenticated with the cloud server.');
      ui.info('Use --force to re-authenticate.');
      return;
    }

    ui.heading('Cloud Authentication');
    ui.label('Server', baseUrl);
    ui.divider();

    const token = await runCloudLogin(projectPath, { baseUrl });
    if (token) {
      ui.success('Authentication complete. Cloud token saved to .godot-mcp/credentials.json.');
      ui.info('Run: godot-cli open --mode Cloud   (no --token needed).');
    } else {
      process.exit(1);
    }
  });
