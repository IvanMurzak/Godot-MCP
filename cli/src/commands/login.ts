import { Command } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { DEFAULT_CLOUD_BASE_URL } from '../utils/connection.js';
import {
  openMachineStore,
  openProjectStore,
  getMachineCredentialsPath,
  resolveProjectStoreDir,
} from '../utils/machine-store.js';
import {
  runCloudLogin,
  completePartialLogin,
  classifyLoginState,
  type CredentialSink,
  type LoginState,
} from '../utils/cloud-login.js';
import type { MachineCredentials, MachineCredentialStore } from '@baizor/gamedev-cli-core';

interface LoginOptions {
  path?: string;
  project?: string;
  baseUrl?: string;
  force?: boolean;
  toolsOnly?: boolean;
  yes?: boolean;
}

export const loginCommand = new Command('login')
  .description(
    'Authenticate with the Godot-MCP cloud server (ai-game.dev) via the RFC 8628 device-authorization flow. ' +
      'By default the credential is saved to the shared machine store (~/.ai-game-dev/credentials.json, 0600 / ' +
      'DPAPI) so you sign in once per machine and the editor plugin auto-adopts it. Use --project <path> to keep ' +
      'a per-project credential (project-local .ai-game-dev/credentials.json, gitignored) instead.',
  )
  .argument('[path]', 'Project path for a per-project credential override (alias of --project). Omit for the machine store.')
  .option('--project <path>', 'Save a per-project credential under <path>/.ai-game-dev/ instead of the machine store')
  .option('--path <path>', 'Alias of --project (kept for backward compatibility)')
  .option('--base-url <url>', 'Override the cloud base URL (default: https://ai-game.dev)')
  .option('--force', 'Re-authenticate even if a credential is already saved')
  .option(
    '--tools-only',
    'CI/automation mode: mint a tools (mcp:plugin) credential only — no agent-plane credential is ' +
      'stored, so the desktop App cannot pick this sign-in up and the runner appears as its own ' +
      'revocable device on ai-game.dev',
  )
  .option('--yes', 'Do not prompt: confirm replacing a credential stored for a different account')
  .action(async (positionalPath: string | undefined, options: LoginOptions) => {
    const baseUrl = (options.baseUrl ?? DEFAULT_CLOUD_BASE_URL).replace(/\/$/, '');

    const { sink, savedLocationLabel } = resolveSink(positionalPath, options);
    verbose(`Credential store: ${savedLocationLabel}`);

    // Only short-circuit when a usable PLUGIN-PLANE credential is saved in the SAME store AND it
    // was issued against the SAME base URL (an agent-only "partial" store must never read as
    // signed in — review f2 B1). And `login --base-url <other>` (without --force) never silently
    // reuses a credential minted for a different server.
    if (!options.force) {
      const state: LoginState = classifyLoginState(readStoreDocument(sink), baseUrl);
      if (state === 'signed-in') {
        ui.success('Already authenticated with the cloud server.');
        ui.info('Use --force to re-authenticate.');
        return;
      }
      if (state === 'partial' && !options.toolsOnly) {
        // F1 failure-path repair: the agent family is committed; finish the derivation leg
        // alone — no second device flow, no browser hop.
        ui.info('A previous sign-in was only partially authorized — finishing it now (no browser needed)...');
        const repaired = await completePartialLogin({ baseUrl, sink });
        if (repaired) {
          ui.success(`Authentication complete. Cloud credential saved to ${savedLocationLabel}.`);
          ui.info('Run: godot-cli open --mode Cloud   (no --token needed).');
          return;
        }
        ui.error('Could not finish the partially-authorized sign-in. Run `godot-cli login --force` to start a fresh one.');
        process.exit(1);
      }
    }

    ui.heading('Cloud Authentication');
    ui.label('Server', baseUrl);
    if (options.toolsOnly) {
      ui.info('Tools-only login: no agent credential will be stored on this machine.');
    }
    ui.divider();

    const token = await runCloudLogin({
      baseUrl,
      sink,
      toolsOnly: options.toolsOnly ?? false,
      assumeYes: options.yes ?? false,
    });
    if (token) {
      ui.success(`Authentication complete. Cloud credential saved to ${savedLocationLabel}.`);
      ui.info('Run: godot-cli open --mode Cloud   (no --token needed).');
    } else {
      process.exit(1);
    }
  });

/**
 * Resolve where the credential should be stored. A project override (via --project, the legacy
 * --path, or the positional arg) selects the per-project store (`<project>/.ai-game-dev/` — the
 * legacy `.godot-mcp/credentials.json` sink is read-fallback only and never written, 06 D7);
 * otherwise the shared machine store.
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
      savedLocationLabel: path.join(resolveProjectStoreDir(projectPath), 'credentials.json'),
    };
  }
  return { sink: { kind: 'machine' }, savedLocationLabel: getMachineCredentialsPath() };
}

/**
 * Read the sink's stored credential document; a missing OR unreadable store reads as null, i.e.
 * `signed-out` (04 §1: an explicit login may replace an unreadable store, so the flow proceeds).
 */
function readStoreDocument(sink: CredentialSink): MachineCredentials | null {
  try {
    const store: MachineCredentialStore =
      sink.kind === 'project' ? openProjectStore(sink.projectPath) : openMachineStore(sink.storeBaseDir);
    return store.read();
  } catch {
    return null;
  }
}
