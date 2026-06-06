// CLI entry-point re-exports (`godot-mcp-cli/cli`).
//
// Exposes the commander Command instances so a consumer can compose them into
// their own program. The runnable program lives in `index.ts` (imported by
// `bin/godot-mcp-cli.js`).

export { openCommand } from './commands/open.js';
export { runToolCommand } from './commands/run-tool.js';
export { runSystemToolCommand } from './commands/run-system-tool.js';
export { statusCommand } from './commands/status.js';
export { waitForReadyCommand } from './commands/wait-for-ready.js';
export { setupMcpCommand } from './commands/setup-mcp.js';
export { configureCommand } from './commands/configure.js';
export { closeCommand } from './commands/close.js';
export { createUpdateCommand } from './commands/update.js';
export { installPluginCommand } from './commands/install-plugin.js';
export { removePluginCommand } from './commands/remove-plugin.js';
