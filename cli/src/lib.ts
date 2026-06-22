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
export { createProject } from './lib/create-project.js';
export { runTool, runSystemTool } from './lib/run-tool.js';
export { setupMcp, listAgentIds } from './lib/setup-mcp.js';
export { setupSkills } from './lib/setup-skills.js';
export { installPlugin, removePlugin } from './lib/install-plugin.js';

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
} from './lib/types.js';
