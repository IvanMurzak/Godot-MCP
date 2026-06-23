import { Command } from 'commander';
import * as path from 'path';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import {
  openProject,
  resolveProjectPath as libResolveProjectPath,
  isGodotProjectDir as libIsGodotProjectDir,
} from '../lib/open.js';
import type { OpenProjectAuthOption, OpenProjectConnectionMode } from '../lib/types.js';

export interface ResolveProjectPathResult {
  /** Absolute, resolved path to the project directory. */
  projectPath: string;
  /** True if no path was supplied and we fell back to `process.cwd()`. */
  usedCwdFallback: boolean;
}

/**
 * Resolve the project path from the positional argument, `--path` option, or
 * the current working directory when neither is provided. Re-exported for tests.
 */
export function resolveOpenProjectPath(
  positionalPath: string | undefined,
  optionPath: string | undefined,
  cwd: string,
): ResolveProjectPathResult {
  const explicit = positionalPath ?? optionPath;
  return libResolveProjectPath(explicit, cwd);
}

/** Re-export for backward compatibility with tests. */
export const isGodotProjectDir = libIsGodotProjectDir;

export const openCommand = new Command('open')
  .description(
    'Open a Godot project in the Godot editor, optionally passing GODOT_MCP_* connection env vars when connection options (--url, --token, etc.) are provided. Use --no-connect to suppress all MCP env vars.',
  )
  .argument('[path]', 'Path to the Godot project (defaults to current directory)')
  .option('--path <path>', 'Path to the Godot project (defaults to current directory)')
  .option('--editor-path <path>', 'Explicit path to the Godot editor executable (skips discovery)')
  .option('--no-build', 'Skip building the C# assembly before launching (GDScript-only projects skip automatically)')
  .option('--build-configuration <cfg>', 'MSBuild configuration for the pre-open build (default: Debug)')
  .option('--no-connect', 'Open without MCP connection environment variables')
  .option('--url <url>', 'MCP server host to connect to (sets GODOT_MCP_HOST)')
  .option('--cloud-url <url>', 'Cloud base URL override (sets GODOT_MCP_CLOUD_URL)')
  .option('--token <token>', 'Auth token (sets GODOT_MCP_TOKEN)')
  .option('--auth <option>', 'Auth option: None or Required (sets GODOT_MCP_AUTH_OPTION)')
  .option('--mode <mode>', 'Connection mode: Cloud or Custom (sets GODOT_MCP_CONNECTION_MODE)')
  .option('--log-level <level>', 'Log level: Trace/Debug/Info/Warning/Error/None (sets GODOT_MCP_LOG_LEVEL)')
  .action(
    async (
      positionalPath: string | undefined,
      options: {
        path?: string;
        editorPath?: string;
        build?: boolean;
        buildConfiguration?: string;
        connect?: boolean;
        url?: string;
        cloudUrl?: string;
        token?: string;
        auth?: string;
        mode?: string;
        logLevel?: string;
      },
    ) => {
      const { projectPath, usedCwdFallback } = resolveOpenProjectPath(
        positionalPath,
        options.path,
        process.cwd(),
      );

      const fs = await import('fs');
      if (!fs.existsSync(projectPath)) {
        ui.error(`Project path does not exist: ${projectPath}`);
        process.exit(1);
      }

      if (!libIsGodotProjectDir(projectPath)) {
        if (usedCwdFallback) {
          ui.error(`Current directory is not a Godot project: ${projectPath}`);
          ui.info('Run this command from a Godot project folder, or pass a path: godot-cli open <path>');
        } else {
          ui.error(`Not a Godot project (missing project.godot): ${projectPath}`);
        }
        process.exit(1);
      }

      if (usedCwdFallback) {
        verbose(`No path provided — using current directory: ${projectPath}`);
      }
      verbose(`open invoked for project: ${projectPath}`);
      verbose(`--no-connect: ${options.connect === false}`);

      // Validate auth/mode here so the CLI emits a clear error + exit code.
      if (options.auth !== undefined && options.auth !== 'None' && options.auth !== 'Required') {
        ui.error('--auth must be "None" or "Required"');
        process.exit(1);
      }
      if (options.mode !== undefined && options.mode !== 'Cloud' && options.mode !== 'Custom') {
        ui.error('--mode must be "Cloud" or "Custom"');
        process.exit(1);
      }

      const spinner = ui.startSpinner(
        options.build === false ? 'Locating Godot editor...' : 'Building C# assembly...',
      );

      const result = await openProject({
        projectPath,
        editorPath: options.editorPath,
        build: options.build !== false,
        buildConfiguration: options.buildConfiguration,
        noConnect: options.connect === false,
        url: options.url,
        cloudUrl: options.cloudUrl,
        token: options.token,
        auth: options.auth as OpenProjectAuthOption | undefined,
        mode: options.mode as OpenProjectConnectionMode | undefined,
        logLevel: options.logLevel,
        onProgress: (event) => {
          switch (event.phase) {
            case 'build-running': {
              spinner.text = `Building ${event.csprojPath} ...`;
              verbose(`Build command: ${event.command}`);
              break;
            }
            case 'build-skipped': {
              verbose('No .csproj at project root — skipping build (GDScript-only).');
              break;
            }
            case 'build-succeeded': {
              spinner.success('C# assembly built');
              verbose(`Built: ${event.csprojPath} (${event.configuration})`);
              spinner.text = 'Locating Godot editor...';
              spinner.start();
              break;
            }
            case 'editor-resolved': {
              spinner.success('Godot editor located');
              verbose(`Editor path: ${event.editorPath}`);
              break;
            }
            case 'connection-details': {
              const envVars = event.envVars;
              if (Object.keys(envVars).length > 0) {
                ui.heading('Connection Details');
                ui.label('Project', event.projectPath);
                ui.label('Editor', event.editorPath);
                ui.heading('Environment Variables');
                for (const [key, value] of Object.entries(envVars)) {
                  const display = key === 'GODOT_MCP_TOKEN' ? '***' : value;
                  ui.label(key, display);
                }
                ui.divider();
              } else {
                verbose('MCP connection disabled via --no-connect or no options');
              }
              break;
            }
            case 'launching-editor': {
              ui.label('Project', event.projectPath);
              ui.label('Editor', event.editorPath);
              break;
            }
            case 'editor-launched': {
              ui.success(`Launched Godot editor (PID: ${event.pid ?? 'unknown'})`);
              break;
            }
            default:
              break;
          }
        },
      });

      if (result.kind === 'failure') {
        // The spinner.success() on editor-resolved only fires on the happy
        // path; on failure stop it so the error renders cleanly.
        spinner.stop();
        ui.error(result.errorMessage);
        process.exit(1);
      }

      if (result.alreadyRunning) {
        spinner.stop();
        ui.success(`Godot is already running with this project (PID: ${result.editorPid ?? 'unknown'})`);
        ui.info('Skipping launch. Use the running instance or close it first.');
        process.exit(0);
      }

      verbose(`Resolved project path: ${path.resolve(result.projectPath)}`);
    },
  );
