import { Command } from 'commander';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { getAgentById, getAgentIds, listAgentTable, MCP_SERVER_NAME } from '../utils/agents.js';
import { setupMcp } from '../lib/setup-mcp.js';

interface SetupMcpCliOptions {
  url?: string;
  token?: string;
  /** Commander maps `--no-pin` to `pin` (default true; false when the flag is passed). */
  pin?: boolean;
  oauth?: boolean;
  regenerateKey?: boolean;
  list?: boolean;
}

/** Human description of the credential a written config carries. */
function describeCredential(result: { credential: string; projectKeySource?: string }): string {
  switch (result.credential) {
    case 'project-key':
      return `project key (${result.projectKeySource === 'minted' ? 'newly created' : 'reused'})`;
    case 'token':
      return 'access token (--token)';
    default:
      return 'none (URL-only; the agent signs in with its own OAuth)';
  }
}

function listAgents(): void {
  listAgentTable('Available AI Agents', 'Config Path', (a) => a.configPathDisplay);
}

export const setupMcpCommand = new Command('setup-mcp')
  .description('Write MCP client config for an AI agent, pointing at the Godot MCP server')
  .argument('[agent-id]', 'Agent to configure (use --list to see all)')
  .argument('[path]', 'Godot project path (defaults to cwd)')
  .option('--url <url>', 'MCP server host override (the <host>/mcp client URL is derived from it)')
  .option('--token <token>', 'Auth token override (defaults to GODOT_MCP_TOKEN)')
  .option('--no-pin', 'Write an unpinned <host>/mcp URL (default pins /p/<pin> to route to this project)')
  .option('--oauth', 'Cloud: write a URL-only config (the agent signs in with its own OAuth) instead of the project key')
  .option('--regenerate-key', "Cloud: mint a new project key for this project, rewrite the config, then revoke the old key")
  .option('--list', 'List all available agent IDs')
  .action(
    async (
      agentId: string | undefined,
      positionalPath: string | undefined,
      options: SetupMcpCliOptions,
    ) => {
      if (options.list) {
        listAgents();
        return;
      }

      if (!agentId) {
        ui.error('Missing required argument: agent-id');
        ui.info(`Available agent IDs: ${getAgentIds().join(', ')}`);
        process.exit(1);
      }

      const agent = getAgentById(agentId);
      if (!agent) {
        ui.error(`Unknown agent: "${agentId}"`);
        ui.info(`Available agent IDs: ${getAgentIds().join(', ')}`);
        process.exit(1);
      }

      const spinner = ui.startSpinner(`Configuring ${agent.name}...`);

      const result = await setupMcp({
        agentId,
        godotProjectPath: positionalPath,
        url: options.url,
        token: options.token,
        noPin: options.pin === false,
        oauth: options.oauth === true,
        regenerateKey: options.regenerateKey === true,
      });

      if (result.kind === 'failure') {
        spinner.error('Failed to write config');
        ui.error(result.error.message);
        process.exit(1);
      }

      if (positionalPath) {
        verbose(`Project path: ${positionalPath}`);
      }
      verbose(`Config file: ${result.configPath}`);

      spinner.success(`${agent.name} configured successfully`);

      console.log('');
      ui.label('Config file', result.configPath);
      ui.label('Server URL', result.serverUrl);
      ui.label('Server name', MCP_SERVER_NAME);
      ui.label('Routing', result.pinned ? 'pinned to this project (/p/<pin>)' : 'unpinned (--no-pin)');
      ui.label('Credential', describeCredential(result));

      for (const warning of result.warnings) {
        console.log('');
        ui.warn(warning);
      }
    },
  );
