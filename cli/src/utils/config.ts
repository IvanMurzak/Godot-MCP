import * as fs from 'fs';
import * as path from 'path';

/**
 * Project-local feature-override config the CLI manages. Godot's plugin
 * persists its live config in `user://godot-mcp-config.json` (outside the
 * project tree), which the CLI cannot reliably reach. This file is the CLI's
 * own in-project surface for declaring which tools/prompts/resources are
 * visible (listed to MCP clients) or hidden — a small, explicit,
 * version-controllable override list a user can commit alongside the project.
 * Visibility is not an access control: a hidden tool still runs through
 * tools/call. Entries are written as `{ name, visible }`; a file written before
 * the rename (`{ name, enabled }`) is still read.
 */
export const CONFIG_RELATIVE_PATH = '.godot-mcp/features.json';

export interface McpFeature {
  name: string;
  visible: boolean;
}

/**
 * The well-formed entries of a feature group, as `{ name, visible }`. Accepts the pre-rename key `enabled`;
 * `visible` wins when both are present. Anything else (non-objects, a non-string name, no boolean flag) is dropped.
 */
export function readFeatures(raw: unknown): McpFeature[] {
  if (!Array.isArray(raw)) return [];
  const features: McpFeature[] = [];
  for (const entry of raw as unknown[]) {
    if (typeof entry !== 'object' || entry === null) continue;
    const { name, visible, enabled } = entry as { name?: unknown; visible?: unknown; enabled?: unknown };
    if (typeof name !== 'string') continue;
    const flag = typeof visible === 'boolean' ? visible : typeof enabled === 'boolean' ? enabled : undefined;
    if (flag === undefined) continue;
    features.push({ name, visible: flag });
  }
  return features;
}

export interface GodotMcpFeaturesConfig {
  tools?: McpFeature[];
  prompts?: McpFeature[];
  resources?: McpFeature[];
  [key: string]: unknown;
}

export function getConfigPath(projectPath: string): string {
  return path.join(projectPath, CONFIG_RELATIVE_PATH);
}

/** Create a default (empty) features config. */
export function createDefaultConfig(): GodotMcpFeaturesConfig {
  return { tools: [], prompts: [], resources: [] };
}

/** Read the features config from a project. Returns null when absent. */
export function readConfig(projectPath: string): GodotMcpFeaturesConfig | null {
  const configPath = getConfigPath(projectPath);
  if (!fs.existsSync(configPath)) {
    return null;
  }
  const json = fs.readFileSync(configPath, 'utf-8');
  try {
    return JSON.parse(json) as GodotMcpFeaturesConfig;
  } catch (err) {
    if (err instanceof SyntaxError) {
      throw new SyntaxError(`Malformed JSON in config file: ${configPath}\n${err.message}`);
    }
    throw err;
  }
}

/** Write the features config, creating the parent directory if needed. */
export function writeConfig(projectPath: string, config: GodotMcpFeaturesConfig): void {
  const configPath = getConfigPath(projectPath);
  const dir = path.dirname(configPath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  fs.writeFileSync(configPath, JSON.stringify(config, null, 2) + '\n');
}

/** Read the config, creating it with defaults if it does not exist. */
export function getOrCreateConfig(projectPath: string): GodotMcpFeaturesConfig {
  if (fs.existsSync(getConfigPath(projectPath))) {
    return readConfig(projectPath) as GodotMcpFeaturesConfig;
  }
  const config = createDefaultConfig();
  writeConfig(projectPath, config);
  return config;
}

/**
 * Update a feature group (tools / prompts / resources) in the config.
 *   - enableNames: set these to visible=true
 *   - disableNames: set these to visible=false (hidden)
 *   - enableAll / disableAll: override every feature already present
 */
export function updateFeatures(
  config: GodotMcpFeaturesConfig,
  featureType: 'tools' | 'prompts' | 'resources',
  options: {
    enableNames?: string[];
    disableNames?: string[];
    enableAll?: boolean;
    disableAll?: boolean;
  },
): void {
  const features = readFeatures(config[featureType]);

  if (options.enableAll) {
    for (const f of features) f.visible = true;
    config[featureType] = features;
    return;
  }

  if (options.disableAll) {
    for (const f of features) f.visible = false;
    config[featureType] = features;
    return;
  }

  if (options.enableNames) {
    for (const name of options.enableNames) {
      const existing = features.find((f) => f.name === name);
      if (existing) existing.visible = true;
      else features.push({ name, visible: true });
    }
  }

  if (options.disableNames) {
    for (const name of options.disableNames) {
      const existing = features.find((f) => f.name === name);
      if (existing) existing.visible = false;
      else features.push({ name, visible: false });
    }
  }

  config[featureType] = features;
}
