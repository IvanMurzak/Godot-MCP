// Shared public types for the godot-cli library API.
//
// This file is re-exported from `lib.ts` — consumers should import
// from `godot-cli` (the package root), NOT from deep paths.
//
// No top-level side effects, no runtime deps beyond TypeScript types.

import type { ExtensionDescriptor } from '../utils/extensions-catalog.js';

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
  // installPlugin (materialize addon) phases
  | { phase: 'addon-downloading'; message: string; url: string }
  | { phase: 'addon-extracting'; message: string }
  | { phase: 'addon-materialized'; message: string; addonDir: string; source: 'download' | 'local' }
  | { phase: 'csproj-patched'; message: string; csprojPath: string }
  // installServer (--with-server) phases
  | { phase: 'server-downloading'; message: string; url: string }
  | { phase: 'server-verifying'; message: string }
  | { phase: 'server-extracting'; message: string }
  | { phase: 'server-materialized'; message: string; serverDir: string; source: 'download' | 'local' }
  // enrollPlugin (--enroll) phases
  | { phase: 'enroll-redeeming'; message: string }
  | { phase: 'enroll-redeemed'; message: string; serverTarget: string }
  // createProject phases
  | { phase: 'project-scaffolded'; message: string; projectPath: string }
  // buildProject phases (also emitted by openProject when it builds first)
  | { phase: 'build-running'; message: string; csprojPath: string; command: string }
  | { phase: 'build-skipped'; message: string; reason: BuildSkipReason }
  | { phase: 'build-succeeded'; message: string; csprojPath: string; configuration: string }
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
// setup-skills
// ---------------------------------------------------------------------------

export interface SetupSkillsOptions {
  /** Agent to generate skills for. Use `listAgentIds()` to discover valid values. */
  agentId: string;
  /** Optional Godot project path. Defaults to `process.cwd()` if omitted. */
  godotProjectPath?: string;
  onProgress?: ProgressCallback;
}

export interface SetupSkillsSuccess {
  kind: 'success';
  success: true;
  agentId: string;
  /** Absolute path to the agent's skills directory the files were written into. */
  skillsDir: string;
  /** Absolute paths of every skill file written, in write order. */
  filesWritten: string[];
  warnings: string[];
}

export interface SetupSkillsFailure {
  kind: 'failure';
  success: false;
  warnings: string[];
  error: Error;
}

export type SetupSkillsResult = SetupSkillsSuccess | SetupSkillsFailure;

// ---------------------------------------------------------------------------
// build-project
// ---------------------------------------------------------------------------

/** Why a build was skipped rather than run. */
export type BuildSkipReason = 'no-csproj';

export interface BuildProjectOptions {
  /** Path to the Godot project to build. Defaults to `process.cwd()`. */
  projectPath?: string;
  /** MSBuild configuration to compile. Defaults to `Debug` (CI parity). */
  configuration?: string;
  /** Explicit `dotnet` executable path. Defaults to `dotnet` on `PATH`. */
  dotnetPath?: string;
  /** Injectable `child_process.spawn` for tests (mock the build). */
  spawnImpl?: typeof import('child_process').spawn;
  onProgress?: ProgressCallback;
}

export interface BuildProjectSuccess {
  kind: 'success';
  success: true;
  projectPath: string;
  /** True when the project had no C# to build (GDScript-only). */
  skipped: boolean;
  /** Set when `skipped` is true. */
  skipReason?: BuildSkipReason;
  /** The `.csproj` that was built (absent when skipped). */
  csprojPath?: string;
  /** The MSBuild configuration compiled (absent when skipped). */
  configuration?: string;
  /** Captured `dotnet build` stdout+stderr (absent when skipped). */
  output?: string;
  warnings: string[];
}

export interface BuildProjectFailure {
  kind: 'failure';
  success: false;
  projectPath?: string;
  warnings: string[];
  errorMessage: string;
  error: Error;
}

export type BuildProjectResult = BuildProjectSuccess | BuildProjectFailure;

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
  /**
   * Build the C# assembly (`dotnet build`) before launching the editor so a
   * fresh first open loads the addon instead of failing with "Unable to load
   * addon script … Disabling the addon". Defaults to `true`. Set `false` to skip
   * (GDScript-only projects are skipped automatically — see `buildProject`).
   */
  build?: boolean;
  /** MSBuild configuration for the pre-open build. Defaults to `Debug`. */
  buildConfiguration?: string;
  /** Explicit `dotnet` executable path for the pre-open build. */
  dotnetPath?: string;
  /** Injectable `child_process.spawn` for the pre-open build (tests). */
  buildSpawnImpl?: typeof import('child_process').spawn;
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
  /**
   * Whether the C# assembly was built before launch: `true` when a build ran,
   * `false` when skipped (`--no-build`, a GDScript-only project, or an
   * already-running editor short-circuit).
   */
  built?: boolean;
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
// create-project
// ---------------------------------------------------------------------------

export interface CreateProjectOptions {
  /** Target directory to scaffold the project into. Defaults to `process.cwd()`. */
  projectPath?: string;
  /**
   * Project name written to `config/name` (and the `[dotnet]` assembly name /
   * csproj filename when `dotnet` is set). Defaults to a name derived from the
   * target folder name.
   */
  name?: string;
  /** When `true`, additionally scaffold a `Godot.NET.Sdk` (C#) csproj. */
  dotnet?: boolean;
  onProgress?: ProgressCallback;
}

export interface CreateProjectSuccess {
  kind: 'success';
  success: true;
  projectPath: string;
  /** The resolved project name (explicit arg or folder-name derivation). */
  projectName: string;
  /** Whether the C# (`--dotnet`) csproj was scaffolded. */
  dotnet: boolean;
  /** Absolute paths of every file written, in write order. */
  filesWritten: string[];
  warnings: string[];
}

export interface CreateProjectFailure {
  kind: 'failure';
  success: false;
  projectPath?: string;
  warnings: string[];
  /**
   * Absolute paths of any files written before the failure, in write order.
   * Empty when the scaffold was refused before writing or after a successful
   * best-effort rollback; non-empty only when cleanup of a partial scaffold
   * could not be completed — lets the consumer finish the cleanup.
   */
  filesWritten: string[];
  errorMessage: string;
  error: Error;
}

export type CreateProjectResult = CreateProjectSuccess | CreateProjectFailure;

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
  /**
   * Local directory to copy `addons/godot_mcp/` from instead of downloading it
   * from the GitHub release (offline / dev / CI path). When set, no network call
   * is made; the directory must contain the addon files (a `plugin.cfg`).
   */
  source?: string;
  /**
   * Addon release version to download (`<version>` in
   * `godot-mcp-addon-<version>.zip`, tag `v<version>`). Defaults to the CLI's own
   * version. Ignored when `source` is set. Used only on the download path.
   */
  version?: string;
  /**
   * Skip materializing the addon files (download / copy) and only enable the
   * plugin + patch the csproj. Restores the pre-installer behavior for callers
   * that manage the addon files themselves.
   */
  skipMaterialize?: boolean;
  /**
   * Injectable fetch for tests (mock the GitHub download). Defaults to
   * `globalThis.fetch`. Used only on the download path.
   */
  fetchImpl?: typeof fetch;
  onProgress?: ProgressCallback;
}

/** What `install-plugin` did to materialize the `addons/godot_mcp/` files. */
export interface AddonMaterializeOutcome {
  /** Where the addon files came from. `skipped` when `skipMaterialize` was set. */
  source: 'download' | 'local' | 'skipped';
  /** Absolute path to the materialized `addons/godot_mcp/` directory. */
  addonDir: string;
  /** The download URL used (download path only). */
  downloadUrl?: string;
  /** The local source directory used (`--source` path only). */
  sourceDir?: string;
}

/** What `install-plugin` did to the consumer `.csproj`'s PackageReferences + EmbeddedResources. */
export interface CsprojPatchOutcome {
  /** Absolute path to the consumer `.csproj` that was patched, if one was found. */
  csprojPath?: string;
  /** True when the csproj text was modified (pins and/or embeds). */
  changed: boolean;
  /** Per-package summary: each addon pin's add / update / unchanged outcome. */
  packages: { id: string; action: 'added' | 'updated' | 'unchanged'; version: string }[];
  /** Per-resource summary: each addon `<EmbeddedResource>`'s add / update / unchanged outcome. */
  embeds: { include: string; action: 'added' | 'updated' | 'unchanged'; logicalName: string }[];
}

export interface InstallPluginSuccess {
  kind: 'success';
  success: true;
  /** True when the plugin was newly enabled; false when it was already enabled. */
  changed: boolean;
  projectGodotPath: string;
  enabledPlugins: string[];
  /** Addon-files materialization outcome (download / local copy / skipped). */
  materialize: AddonMaterializeOutcome;
  /** Consumer csproj PackageReference patch outcome. */
  csproj: CsprojPatchOutcome;
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

// ---------------------------------------------------------------------------
// install-extension
// ---------------------------------------------------------------------------

/**
 * What `installExtension` did to the consumer `.csproj` — the CLI mirror of the dock's
 * `ExtensionInstallOutcome`:
 *  - `added`             — the `<PackageReference>` was newly added (rebuild required).
 *  - `updated`           — its version was bumped up (rebuild required).
 *  - `already-up-to-date`— present at an equal/newer version, or the descriptor has no pin (no write).
 *  - `no-project`        — no consumer `.csproj` was found to install into (nothing written).
 */
export type ExtensionInstallOutcome = 'added' | 'updated' | 'already-up-to-date' | 'no-project';

export interface InstallExtensionOptions {
  /** Absolute or relative path to the Godot project root (holds `project.godot` + the consumer `.csproj`). */
  godotProjectPath: string;
  /** The extension `<id>` to install — matched against the catalog by `packageId` (then `name`), case-insensitive. */
  extensionId: string;
  /** Override the catalog's pinned version for this install (the `--version` flag). Ignored when empty. */
  version?: string;
  /**
   * Catalog to resolve `extensionId` against. Defaults to the bundled `EXTENSIONS_CATALOG`
   * (single-sourced from `addons/godot_mcp/extensions.catalog.json`). Injectable for tests.
   */
  catalog?: readonly ExtensionDescriptor[];
  onProgress?: ProgressCallback;
}

export interface InstallExtensionSuccess {
  kind: 'success';
  success: true;
  /** The classified outcome (added / updated / already-up-to-date / no-project). */
  outcome: ExtensionInstallOutcome;
  /** True when the `.csproj` was written (added or updated). */
  changed: boolean;
  /** True when a write happened, so the consumer must rebuild solutions (Godot has no programmatic restore). */
  rebuildRequired: boolean;
  /** The user-supplied id that resolved to the descriptor. */
  extensionId: string;
  /** The resolved descriptor's package id (the `<PackageReference Include>`). */
  packageId: string;
  /** Version currently referenced before this install: `null` when not installed, `''` when referenced unversioned. */
  fromVersion: string | null;
  /** The version this install targeted (catalog pin or `--version` override), or `null` when unpinned. */
  toVersion: string | null;
  /** Absolute path to the consumer `.csproj`, when one was located. */
  csprojPath?: string;
  /** A short human-readable status line, safe to print. */
  message: string;
  warnings: string[];
}

export interface InstallExtensionFailure {
  kind: 'failure';
  success: false;
  projectGodotPath?: string;
  warnings: string[];
  error: Error;
}

export type InstallExtensionResult = InstallExtensionSuccess | InstallExtensionFailure;

// ---------------------------------------------------------------------------
// install-server (install-plugin --with-server)
// ---------------------------------------------------------------------------

export interface InstallServerOptions {
  /** Absolute or relative path to the Godot project root (holds the managed server dir). */
  godotProjectPath: string;
  /**
   * Local path to install the server from instead of downloading it (offline /
   * dev / CI — the `--server-source` flag). May be a directory containing the
   * server binary, or a `.zip` archive of one. When set, no network call is made
   * and no checksum is verified (the local artifact is trusted).
   */
  source?: string;
  /**
   * Shared-server version to download (`v<version>` release on GameDev-MCP-Server).
   * Defaults to the addon's pinned `ServerVersion`. Ignored when `source` is set.
   */
  version?: string;
  /** Host RID override (`<os>-<arch>`). Defaults to the detected host RID. */
  rid?: string;
  /** Injectable fetch for tests (mock the GitHub download). Used only on the download path. */
  fetchImpl?: typeof fetch;
  onProgress?: ProgressCallback;
}

export interface InstallServerSuccess {
  kind: 'success';
  success: true;
  /** Where the server binary came from. */
  source: 'download' | 'local';
  /** The host RID the binary was resolved for. */
  rid: string;
  /** The downloaded server version (empty on the local path). */
  version: string;
  /** Absolute path to the CLI-managed server directory the binary was extracted into. */
  serverDir: string;
  /** Absolute path to the resolved server executable. */
  executablePath: string;
  /** The download URL used (download path only). */
  downloadUrl?: string;
  warnings: string[];
}

export interface InstallServerFailure {
  kind: 'failure';
  success: false;
  warnings: string[];
  error: Error;
}

export type InstallServerResult = InstallServerSuccess | InstallServerFailure;

// ---------------------------------------------------------------------------
// enroll-plugin (install-plugin --enroll / --enroll-stdin)
// ---------------------------------------------------------------------------

export interface EnrollPluginOptions {
  /** Absolute or relative path to the Godot project root (holds the project marker). */
  godotProjectPath: string;
  /** The D13 enrollment code to redeem. */
  code: string;
  /** Cloud AS base URL to redeem against (default https://ai-game.dev). */
  baseUrl?: string;
  /** Override the machine credential store directory (tests only). */
  storeBaseDir?: string;
  /** Injectable fetch for the redeem round-trip (tests). */
  fetchImpl?: typeof fetch;
  onProgress?: ProgressCallback;
}

export interface EnrollPluginSuccess {
  kind: 'success';
  success: true;
  /** The server target the enrollment code was minted for (recorded in the marker). */
  serverTarget: string;
  /** The D14 routing pin recorded in the marker. */
  pin: string;
  /** The deterministic local port recorded for a localhost target (undefined for a hosted target). */
  port?: number;
  /** Absolute path to the machine credential store the plugin credential was written to. */
  credentialsPath: string;
  /** Absolute path to the project marker written. */
  markerPath: string;
  /** Absolute paths of any existing project-local agent configs whose URL was pin-upserted. */
  pinnedConfigs: string[];
  warnings: string[];
}

export interface EnrollPluginFailure {
  kind: 'failure';
  success: false;
  warnings: string[];
  error: Error;
}

export type EnrollPluginResult = EnrollPluginSuccess | EnrollPluginFailure;

// ---------------------------------------------------------------------------
// configure-agent (configure --agent — proxies to the managed server binary)
// ---------------------------------------------------------------------------

export interface ConfigureAgentOptions {
  /** Absolute or relative path to the Godot project root (locates the managed server binary + is the config target). */
  godotProjectPath: string;
  /** The agent id to configure (forwarded verbatim to the server binary's `configure --agent`). */
  agentId: string;
  /** Explicit MCP server URL override (forwarded as `--url`). */
  url?: string;
  /** Injectable `child_process.spawn` for tests (mock the server binary). */
  spawnImpl?: typeof import('child_process').spawn;
  onProgress?: ProgressCallback;
}

export interface ConfigureAgentSuccess {
  kind: 'success';
  success: true;
  agentId: string;
  /** Absolute path to the managed server binary that was proxied to. */
  serverBinaryPath: string;
  /** The argv passed to the server binary (after the executable), for diagnostics. */
  args: string[];
  /** The server binary's exit code (0 on success). */
  exitCode: number;
  /** The server binary's captured stdout+stderr (surfaced to the user by the command). */
  output: string;
  warnings: string[];
}

export interface ConfigureAgentFailure {
  kind: 'failure';
  success: false;
  /** The server binary's exit code when it ran and failed; undefined when it could not be launched. */
  exitCode?: number;
  warnings: string[];
  error: Error;
}

export type ConfigureAgentResult = ConfigureAgentSuccess | ConfigureAgentFailure;
