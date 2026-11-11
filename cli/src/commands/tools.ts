import { Command } from 'commander';
import { DEFAULT_SESSION_READY_TIMEOUT_MS } from '@baizor/gamedev-cli-core';
import * as ui from '../utils/ui.js';
import { verbose } from '../utils/ui.js';
import { isGodotProject, resolveAndValidateProjectPath } from '../utils/connection.js';
import { connectionForCommand } from '../utils/editor-connection.js';
import { isLocalhostUrl } from '../utils/project-marker.js';
import { ExitCode } from '../utils/exit-codes.js';
import { CommandOutput, addFormatOption, parseFormat, parseIntFlag } from '../utils/output.js';
import { ensureEditor } from '../lib/session.js';
import {
  exampleCall,
  listToolsLive,
  offlineTools,
  readToolsCache,
  writeToolsCache,
  type ToolInfo,
} from '../lib/list-tools.js';
import { ensureFailureExit } from './run-tool-builder.js';

interface ToolsCliOptions {
  path?: string;
  url?: string;
  token?: string;
  format?: string;
  start?: boolean;
  refresh?: boolean;
  editorPath?: string;
  readyTimeout: string;
}

function firstSentence(text: string): string {
  const line = text.split(/\r?\n/)[0] ?? '';
  const m = /^(.{20,}?[.!?])\s/.exec(line + ' ');
  return (m ? m[1] : line).slice(0, 160);
}

export const toolsCommand = addFormatOption(
  new Command('tools')
    .description("List the editor's AI tools (run them with run-tool), or show one tool's arguments and an example call")
    .argument('[name]', 'A tool name: show its description, input schema and an example run-tool call')
    .option('--path <path>', 'Godot project path (default: current directory)')
    .option('--refresh', 'Ask the editor again instead of using the cached list')
    .option('--no-start', 'Do not start a headless editor to ask it (use the cache or the built-in list)')
    .option('--editor-path <path>', 'Godot editor executable for a started editor')
    .option('--ready-timeout <ms>', 'How long to wait for a started editor to become ready', String(DEFAULT_SESSION_READY_TIMEOUT_MS))
    .option('--url <url>', 'MCP server base URL (default: the project\'s connection)')
    .option('--token <token>', 'Bearer token'),
).action(async (name: string | undefined, options: ToolsCliOptions) => {
  const output = new CommandOutput('tools', parseFormat(options.format));
  const readyTimeoutMs = parseIntFlag(options.readyTimeout, '--ready-timeout');
  const projectPath = resolveAndValidateProjectPath(undefined, options);
  const conn = await connectionForCommand(output, projectPath, options);

  // The project's own cache only: written for this project's local server, never for an explicit --url or the hub.
  const cacheable = !options.url && !conn.cloud && isGodotProject(projectPath);
  const cached = cacheable ? readToolsCache(projectPath) : undefined;
  const cache = cached && cached.url === conn.url ? cached : undefined;
  let tools: ToolInfo[] | undefined;
  let source: 'editor' | 'cache' | 'built-in' = 'editor';
  // A running editor is asked first (the list changes when tools are added); the cache serves a closed editor.
  let live = await listToolsLive(conn.url, conn.token, 5_000);
  if (live.kind === 'failure' && cache && !options.refresh) {
    tools = cache.tools;
    source = 'cache';
    verbose(`Using ${cache.tools.length} cached tools from ${cache.fetchedAt}`);
  } else {
    const canStart = options.start !== false && !conn.cloud && isLocalhostUrl(conn.url) && isGodotProject(projectPath);
    if (live.kind === 'failure' && live.reason === 'unreachable' && canStart) {
      output.progress('No editor answered; starting a headless editor to list its tools...');
      const editor = await ensureEditor({
        projectPath,
        url: conn.url,
        ...(conn.token ? { token: conn.token } : {}),
        ...(options.editorPath ? { editorPath: options.editorPath } : {}),
        readyTimeoutMs,
        onStatus: (m) => output.progress(m),
      });
      if (editor.kind === 'failure') {
        const mapped = ensureFailureExit(editor);
        return output.fail(mapped.exitCode, mapped.code, `Could not start an editor: ${editor.message}`);
      }
      live = await listToolsLive(editor.ensured.ready, conn.token);
    }
    if (live.kind === 'success') {
      tools = live.tools;
      if (cacheable) writeToolsCache(projectPath, conn.url, live.tools);
    } else if (cache) {
      output.warn(`${live.message} Showing the cached list from ${cache.fetchedAt}.`);
      tools = cache.tools;
      source = 'cache';
    } else if (live.reason === 'dropped') {
      // A server answered and dropped the connection: exit 7 UNREACHABLE (as run-tool and the other CLIs), not the
      // offline list, which stands in for an editor that is not running.
      return output.fail(ExitCode.Unreachable, 'UNREACHABLE', live.message);
    } else {
      output.warn(`${live.message} Showing the built-in list (no input schemas); run \`godot-cli tools --refresh\` with an editor running for the full list.`);
      tools = offlineTools();
      source = 'built-in';
    }
  }

  if (name !== undefined) {
    const tool = tools.find((t) => t.name === name);
    if (!tool) {
      const near = tools.filter((t) => t.name.includes(name) || name.includes(t.name.split('-')[0])).map((t) => t.name).slice(0, 8);
      return output.fail(ExitCode.Usage, 'NOT_FOUND', `No tool named "${name}".${near.length > 0 ? ` Did you mean: ${near.join(', ')}?` : ''} Run \`godot-cli tools\` for the list.`);
    }
    const example = tool.inputSchema ? exampleCall(tool) : `godot-cli run-tool ${tool.name} --input '{}'`;
    if (!output.machine) {
      ui.heading(tool.name);
      console.log(tool.description);
      const props = tool.inputSchema?.properties ?? {};
      const required = new Set(tool.inputSchema?.required ?? []);
      if (Object.keys(props).length > 0) {
        ui.heading('Arguments');
        for (const [key, p] of Object.entries(props)) {
          const type = Array.isArray(p.type) ? p.type.join('|') : p.type ?? 'any';
          console.log(`  ${key}${required.has(key) ? '' : '?'}: ${type}${p.description ? ` — ${p.description}` : ''}`);
        }
      } else if (source === 'built-in') {
        ui.info('Argument schema unavailable without a running editor (run with --refresh while one runs).');
      }
      ui.heading('Example');
      console.log(`  ${example}`);
      console.log('  (complex arguments: put the JSON in a file and pass --input-file <file>)');
    }
    return output.finish(ExitCode.Success, { source, tool, example });
  }

  if (!output.machine) {
    for (const t of tools) console.log(`${t.name.padEnd(28)} ${firstSentence(t.description)}`);
    console.log('');
    console.log(`${tools.length} tools (${source}). Details and an example call: godot-cli tools <name>. Run one: godot-cli run-tool <name> --input '{...}'.`);
  }
  return output.finish(ExitCode.Success, { source, tools: tools.map((t) => ({ name: t.name, description: t.description })) });
});
