// Library entry point for `godot-cli`.
//
// Constraints:
// - NO top-level side effects. Importing this file must not open sockets, spin
//   up spinners, write to stdout/stderr, or parse argv.
// - NO `commander` import reachable from this file.
// - Every result is a discriminated union keyed on `kind`. Successes are
//   `{ kind: 'success', success: true, ... }`; failures are
//   `{ kind: 'failure', success: false, error }`. Errors are never thrown past
//   the public boundary. Narrow on `kind` for type-safe access.
// - Progress is surfaced via an optional `onProgress` callback, not globals.
//
// Consumers: `import { openProject } from 'godot-cli'` (maps to this file
// via the `exports` field in package.json).

export { openProject } from './lib/open.js';
export { buildProject } from './lib/build.js';
export { createProject } from './lib/create-project.js';
export { runTool, runSystemTool } from './lib/run-tool.js';
export { setupMcp, listAgentIds } from './lib/setup-mcp.js';
export { setupSkills } from './lib/setup-skills.js';
export { installPlugin, removePlugin } from './lib/install-plugin.js';
export { installExtension } from './lib/install-extension.js';
export { installServer } from './lib/install-server.js';
export { enrollPlugin } from './lib/enroll.js';
export { configureAgentViaServer } from './lib/configure-agent.js';

// Shared extension catalog (single-sourced from addons/godot_mcp/extensions.catalog.json)
// + its lookup helpers, so the app can render the same list the dock + CLI install from.
export { EXTENSIONS_CATALOG, findExtension, hasVersion } from './utils/extensions-catalog.js';
export type { ExtensionDescriptor, ExtensionTool } from './utils/extensions-catalog.js';

export type {
  // Shared
  ProgressEvent,
  ProgressCallback,
  ResultKind,
  // open-project
  OpenProjectOptions,
  OpenProjectResult,
  OpenProjectSuccess,
  OpenProjectFailure,
  OpenProjectAuthOption,
  OpenProjectConnectionMode,
  // build-project
  BuildProjectOptions,
  BuildProjectResult,
  BuildProjectSuccess,
  BuildProjectFailure,
  BuildSkipReason,
  // create-project
  CreateProjectOptions,
  CreateProjectResult,
  CreateProjectSuccess,
  CreateProjectFailure,
  // run-tool / run-system-tool
  RunToolOptions,
  RunToolResult,
  RunToolSuccess,
  RunToolFailure,
  RunToolFailureReason,
  RunSystemToolOptions,
  RunSystemToolResult,
  RunSystemToolSuccess,
  RunSystemToolFailure,
  // setup-mcp
  SetupMcpOptions,
  SetupMcpResult,
  SetupMcpSuccess,
  SetupMcpFailure,
  // setup-skills
  SetupSkillsOptions,
  SetupSkillsResult,
  SetupSkillsSuccess,
  SetupSkillsFailure,
  // install-plugin / remove-plugin
  InstallPluginOptions,
  InstallPluginResult,
  InstallPluginSuccess,
  InstallPluginFailure,
  AddonMaterializeOutcome,
  CsprojPatchOutcome,
  RemovePluginOptions,
  RemovePluginResult,
  RemovePluginSuccess,
  RemovePluginFailure,
  // install-extension
  InstallExtensionOptions,
  InstallExtensionResult,
  InstallExtensionSuccess,
  InstallExtensionFailure,
  ExtensionInstallOutcome,
  // install-server (install-plugin --with-server)
  InstallServerOptions,
  InstallServerResult,
  InstallServerSuccess,
  InstallServerFailure,
  // enroll-plugin (install-plugin --enroll / --enroll-stdin)
  EnrollPluginOptions,
  EnrollPluginResult,
  EnrollPluginSuccess,
  EnrollPluginFailure,
  // configure-agent (configure --agent)
  ConfigureAgentOptions,
  ConfigureAgentResult,
  ConfigureAgentSuccess,
  ConfigureAgentFailure,
} from './lib/types.js';
