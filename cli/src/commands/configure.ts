import { Command } from 'commander';
import * as fs from 'fs';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import {
  getOrCreateConfig,
  writeConfig,
  updateFeatures,
  type McpFeature,
  type GodotMcpFeaturesConfig,
} from '../utils/config.js';

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
  const raw = config[key];
  if (!Array.isArray(raw)) return [];
  return raw
    .filter(
      (f): f is McpFeature =>
        typeof f === 'object' && f !== null && typeof f.name === 'string' && typeof f.enabled === 'boolean',
    )
    .map((f) => ({ name: f.name, enabled: f.enabled }));
}

export const configureCommand = new Command('configure')
  .description('List / enable / disable MCP tools, prompts, and resources in the project-local .godot-mcp/features.json')
  .argument('[path]', 'Path to the Godot project')
  .option('--path <path>', 'Path to the Godot project')
  .option('--enable-tools <names>', 'Enable specific tools (comma-separated)', parseCommaSeparated)
  .option('--disable-tools <names>', 'Disable specific tools (comma-separated)', parseCommaSeparated)
  .option('--enable-all-tools', 'Enable all tools')
  .option('--disable-all-tools', 'Disable all tools')
  .option('--enable-prompts <names>', 'Enable specific prompts (comma-separated)', parseCommaSeparated)
  .option('--disable-prompts <names>', 'Disable specific prompts (comma-separated)', parseCommaSeparated)
  .option('--enable-all-prompts', 'Enable all prompts')
  .option('--disable-all-prompts', 'Disable all prompts')
  .option('--enable-resources <names>', 'Enable specific resources (comma-separated)', parseCommaSeparated)
  .option('--disable-resources <names>', 'Disable specific resources (comma-separated)', parseCommaSeparated)
  .option('--enable-all-resources', 'Enable all resources')
  .option('--disable-all-resources', 'Disable all resources')
  .option('--list', 'List current configuration')
  .action(
    async (
      positionalPath: string | undefined,
      options: {
        path?: string;
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
        ui.error('Path is required. Usage: godot-mcp-cli configure <path> or --path <path>');
        process.exit(1);
      }

      const projectPath = path.resolve(resolvedPath);
      if (!fs.existsSync(projectPath)) {
        ui.error(`Project path does not exist: ${projectPath}`);
        process.exit(1);
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
            ui.info('(none configured - all enabled by default)');
            return;
          }
          for (const f of features) {
            ui.featureRow(f.name, f.enabled);
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
