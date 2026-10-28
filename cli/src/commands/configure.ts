import { Command } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import {
  getOrCreateConfig,
  writeConfig,
  updateFeatures,
  readFeatures,
  type McpFeature,
  type GodotMcpFeaturesConfig,
} from '../utils/config.js';
import { configureAgentViaServer } from '../lib/configure-agent.js';

function parseCommaSeparated(value: string): string[] {
  return value.split(',').map((s) => s.trim()).filter(Boolean);
}

interface FeatureAction {
  enableNames?: string[];
  disableNames?: string[];
  enableAll?: boolean;
  disableAll?: boolean;
}

function buildAction(
  enable: string[] | undefined,
  disable: string[] | undefined,
  enableAll: boolean | undefined,
  disableAll: boolean | undefined,
): FeatureAction | undefined {
  if (!enable && !disable && !enableAll && !disableAll) return undefined;
  return { enableNames: enable, disableNames: disable, enableAll, disableAll };
}

function snapshotFeatures(config: GodotMcpFeaturesConfig, key: 'tools' | 'prompts' | 'resources'): McpFeature[] {
  return readFeatures(config[key]);
}

export const configureCommand = new Command('configure')
  .description(
    'Configure an AI agent for the project (--agent <id> proxies to the managed gamedev-mcp-server binary, writing that client\'s MCP config with the derived pin + port), OR list / show / hide MCP tools, prompts, and resources in the project-local .godot-mcp/features.json (visibility only decides what MCP clients see listed; a hidden tool still runs when called by name)',
  )
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .option('--agent <id>', 'Configure an AI agent by proxying to the managed gamedev-mcp-server binary (requires install-plugin --with-server)')
  .option('--url <url>', 'Explicit MCP server URL override forwarded to the server binary (--agent only)')
  .option('--enable-tools <names>', 'Show specific tools to MCP clients (comma-separated)', parseCommaSeparated)
  .option('--disable-tools <names>', 'Hide specific tools from MCP clients (comma-separated; still callable by name)', parseCommaSeparated)
  .option('--enable-all-tools', 'Show all tools')
  .option('--disable-all-tools', 'Hide all tools')
  .option('--enable-prompts <names>', 'Show specific prompts to MCP clients (comma-separated)', parseCommaSeparated)
  .option('--disable-prompts <names>', 'Hide specific prompts from MCP clients (comma-separated; still callable by name)', parseCommaSeparated)
  .option('--enable-all-prompts', 'Show all prompts')
  .option('--disable-all-prompts', 'Hide all prompts')
  .option('--enable-resources <names>', 'Show specific resources to MCP clients (comma-separated)', parseCommaSeparated)
  .option('--disable-resources <names>', 'Hide specific resources from MCP clients (comma-separated; still callable by name)', parseCommaSeparated)
  .option('--enable-all-resources', 'Show all resources')
  .option('--disable-all-resources', 'Hide all resources')
  .option('--list', 'List current configuration')
  .action(
    async (
      positionalPath: string | undefined,
      options: {
        path?: string;
        agent?: string;
        url?: string;
        enableTools?: string[];
        disableTools?: string[];
        enableAllTools?: boolean;
        disableAllTools?: boolean;
        enablePrompts?: string[];
        disablePrompts?: string[];
        enableAllPrompts?: boolean;
        disableAllPrompts?: boolean;
        enableResources?: string[];
        disableResources?: string[];
        enableAllResources?: boolean;
        disableAllResources?: boolean;
        list?: boolean;
      },
    ) => {
      const resolvedPath = positionalPath ?? options.path;
      if (!resolvedPath) {
        ui.error('Path is required. Usage: godot-cli configure <path> or --path <path>');
        process.exit(1);
      }

      const projectPath = path.resolve(resolvedPath);
      if (!fs.existsSync(projectPath)) {
        ui.error(`Project path does not exist: ${projectPath}`);
        process.exit(1);
      }

      // --agent: proxy to the managed gamedev-mcp-server binary's configurator surface.
      if (options.agent !== undefined) {
        ui.heading(`Configuring agent "${options.agent}"`);
        const spinner = ui.startSpinner('Proxying to the managed server binary...');
        const result = await configureAgentViaServer({
          godotProjectPath: projectPath,
          agentId: options.agent,
          url: options.url,
        });
        if (result.kind === 'failure') {
          spinner.error('Failed to configure agent');
          ui.error(result.error.message);
          process.exit(1);
        }
        spinner.success(`Configured "${result.agentId}" via ${result.serverBinaryPath}`);
        if (result.output.trim().length > 0) {
          console.log(result.output.trimEnd());
        }
        return;
      }

      verbose(`Loading config for project: ${projectPath}`);

      const toolsAction = options.list
        ? undefined
        : buildAction(options.enableTools, options.disableTools, options.enableAllTools, options.disableAllTools);
      const promptsAction = options.list
        ? undefined
        : buildAction(options.enablePrompts, options.disablePrompts, options.enableAllPrompts, options.disableAllPrompts);
      const resourcesAction = options.list
        ? undefined
        : buildAction(
            options.enableResources,
            options.disableResources,
            options.enableAllResources,
            options.disableAllResources,
          );

      const config = getOrCreateConfig(projectPath);

      const hasAny = (a: FeatureAction | undefined): boolean =>
        !!a && (!!a.enableNames?.length || !!a.disableNames?.length || a.enableAll === true || a.disableAll === true);

      if (hasAny(toolsAction)) updateFeatures(config, 'tools', toolsAction!);
      if (hasAny(promptsAction)) updateFeatures(config, 'prompts', promptsAction!);
      if (hasAny(resourcesAction)) updateFeatures(config, 'resources', resourcesAction!);

      if (hasAny(toolsAction) || hasAny(promptsAction) || hasAny(resourcesAction)) {
        writeConfig(projectPath, config);
      }

      if (options.list) {
        ui.heading('Current configuration');

        const printFeatures = (featureLabel: string, features: McpFeature[]) => {
          ui.heading(featureLabel);
          if (features.length === 0) {
            ui.info('(none configured - all visible by default)');
            return;
          }
          for (const f of features) {
            ui.featureRow(f.name, f.visible);
          }
        };

        printFeatures('Tools', snapshotFeatures(config, 'tools'));
        printFeatures('Prompts', snapshotFeatures(config, 'prompts'));
        printFeatures('Resources', snapshotFeatures(config, 'resources'));
        return;
      }

      verbose('Writing updated configuration');
      ui.success('Configuration updated successfully.');
    },
  );
