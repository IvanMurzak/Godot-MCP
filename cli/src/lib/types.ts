// Shared public types for the godot-cli library API.
//
// This file is re-exported from `lib.ts` — consumers should import
// from `godot-cli` (the package root), NOT from deep paths.
//
// No top-level side effects, no runtime deps beyond TypeScript types.

// ---------------------------------------------------------------------------
// Progress events
// ---------------------------------------------------------------------------

/**
 * Discriminated union describing every progress event the library can emit to
 * the optional `onProgress` callback. Consumers narrow on `event.phase`.
 */
export type ProgressEvent =
  | { phase: 'start'; message: string }
  | { phase: 'manifest-patched'; message: string; manifestPath: string }
  // openProject phases
  | { phase: 'editor-resolved'; message: string; editorPath: string }
  | {
      phase: 'connection-details';
      message: string;
      projectPath: string;
      editorPath: string;
      envVars: Record<string, string>;
    }
  | { phase: 'launching-editor'; message: string; editorPath: string; projectPath: string }
  | { phase: 'editor-launched'; message: string; pid?: number }
  | { phase: 'done'; message: string };

export type ProgressCallback = (event: ProgressEvent) => void;

// ---------------------------------------------------------------------------
// Result discriminator
// ---------------------------------------------------------------------------

export type ResultKind = 'success' | 'failure';

// ---------------------------------------------------------------------------
// setup-mcp
// ---------------------------------------------------------------------------

export interface SetupMcpOptions {
  /** Agent to configure. Use `listAgentIds()` to discover valid values. */
  agentId: string;
  /** Optional Godot project path. Defaults to `process.cwd()` if omitted. */
  godotProjectPath?: string;
  /** Explicit MCP server URL override (the `<host>/mcp` client endpoint). */
  url?: string;
  /** Auth token override. */
  token?: string;
  onProgress?: ProgressCallback;
}

export interface SetupMcpSuccess {
  kind: 'success';
  success: true;
  agentId: string;
  configPath: string;
  serverUrl: string;
  warnings: string[];
}

export interface SetupMcpFailure {
  kind: 'failure';
  success: false;
  warnings: string[];
  error: Error;
}

export type SetupMcpResult = SetupMcpSuccess | SetupMcpFailure;

// ---------------------------------------------------------------------------
// open-project
// ---------------------------------------------------------------------------

/** Auth option propagated to the editor as `GODOT_MCP_AUTH_OPTION`. */
export type OpenProjectAuthOption = 'None' | 'Required';

/** Connection mode propagated to the editor as `GODOT_MCP_CONNECTION_MODE`. */
export type OpenProjectConnectionMode = 'Cloud' | 'Custom';

export interface OpenProjectOptions {
  /** Path to the Godot project to open. Defaults to `process.cwd()`. */
  projectPath?: string;
  /** Explicit Godot editor executable path (skips resolution). */
  editorPath?: string;
  /** If `true`, skip wiring the GODOT_MCP_* env vars onto the editor process. */
  noConnect?: boolean;
  /** MCP server host — sets `GODOT_MCP_HOST`. */
  url?: string;
  /** Cloud base URL override — sets `GODOT_MCP_CLOUD_URL`. */
  cloudUrl?: string;
  /** Auth token — sets `GODOT_MCP_TOKEN`. */
  token?: string;
  /** Auth option — sets `GODOT_MCP_AUTH_OPTION`. */
  auth?: OpenProjectAuthOption;
  /** Connection mode — sets `GODOT_MCP_CONNECTION_MODE`. */
  mode?: OpenProjectConnectionMode;
  /** Log level — sets `GODOT_MCP_LOG_LEVEL`. */
  logLevel?: string;
  onProgress?: ProgressCallback;
}

export interface OpenProjectSuccess {
  kind: 'success';
  success: true;
  editorPath: string;
  editorPid?: number;
  projectPath: string;
  warnings: string[];
  /** True when an editor was already running for this project; launch skipped. */
  alreadyRunning?: boolean;
}

export interface OpenProjectFailure {
  kind: 'failure';
  success: false;
  projectPath?: string;
  editorPath?: string;
  warnings: string[];
  errorMessage: string;
  error: Error;
}

export type OpenProjectResult = OpenProjectSuccess | OpenProjectFailure;

/**
 * Subset of `OpenProjectOptions` consumed by `buildOpenEnv` — every field here
 * maps to a `GODOT_MCP_*` environment variable.
 */
export interface OpenEnvInputs {
  noConnect?: OpenProjectOptions['noConnect'];
  url?: OpenProjectOptions['url'];
  cloudUrl?: OpenProjectOptions['cloudUrl'];
  token?: OpenProjectOptions['token'];
  auth?: OpenProjectOptions['auth'];
  mode?: OpenProjectOptions['mode'];
  logLevel?: OpenProjectOptions['logLevel'];
}

// ---------------------------------------------------------------------------
// run-tool / run-system-tool
// ---------------------------------------------------------------------------

export type RunToolFailureReason =
  | 'invalid-input'
  | 'connection-refused'
  | 'connection-reset'
  | 'network-error'
  | 'timeout'
  | 'http-error'
  | 'unknown';

/**
 * Options accepted by both {@link runTool} and {@link runSystemTool}.
 *
 * `url` (the MCP server base URL) MUST be provided — unlike the Unity CLI there
 * is no deterministic project-path→port fallback, because the Godot plugin is a
 * server-less client whose persisted config lives in `user://` (outside the
 * project tree).
 */
export interface RunToolOptions {
  /** Tool name to invoke. URL-encoded into the `{name}` route segment. */
  toolName: string;
  /** MCP server base URL (no trailing slash required). REQUIRED. */
  url?: string;
  /** Bearer token override. */
  token?: string;
  /** Tool arguments serialized as the JSON request body. Defaults to `{}`. */
  input?: unknown;
  /** Per-request timeout in milliseconds. Defaults to `60000`. */
  timeoutMs?: number;
  /** Optional abort signal. */
  signal?: AbortSignal;
  /** Optional fetch injection point for tests. */
  fetchImpl?: typeof fetch;
}

export interface RunToolSuccess {
  kind: 'success';
  success: true;
  endpoint: string;
  httpStatus: number;
  data: unknown;
}

export interface RunToolFailure {
  kind: 'failure';
  success: false;
  endpoint: string;
  reason: RunToolFailureReason;
  httpStatus?: number;
  data?: unknown;
  message: string;
  error?: Error;
}

export type RunToolResult = RunToolSuccess | RunToolFailure;

export type RunSystemToolOptions = RunToolOptions;
export type RunSystemToolResult = RunToolResult;
export type RunSystemToolSuccess = RunToolSuccess;
export type RunSystemToolFailure = RunToolFailure;

// ---------------------------------------------------------------------------
// install-plugin / remove-plugin
// ---------------------------------------------------------------------------

export interface InstallPluginOptions {
  /** Absolute or relative path to the Godot project root. */
  godotProjectPath: string;
  onProgress?: ProgressCallback;
}

export interface InstallPluginSuccess {
  kind: 'success';
  success: true;
  /** True when the plugin was newly enabled; false when it was already enabled. */
  changed: boolean;
  projectGodotPath: string;
  enabledPlugins: string[];
  warnings: string[];
}

export interface InstallPluginFailure {
  kind: 'failure';
  success: false;
  projectGodotPath?: string;
  warnings: string[];
  error: Error;
}

export type InstallPluginResult = InstallPluginSuccess | InstallPluginFailure;

export interface RemovePluginOptions {
  godotProjectPath: string;
  onProgress?: ProgressCallback;
}

export interface RemovePluginSuccess {
  kind: 'success';
  success: true;
  /** True when the plugin was disabled; false when it was already absent. */
  changed: boolean;
  projectGodotPath: string;
  enabledPlugins: string[];
  warnings: string[];
}

export interface RemovePluginFailure {
  kind: 'failure';
  success: false;
  projectGodotPath?: string;
  warnings: string[];
  error: Error;
}

export type RemovePluginResult = RemovePluginSuccess | RemovePluginFailure;
