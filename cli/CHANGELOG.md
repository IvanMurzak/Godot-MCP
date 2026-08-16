# Changelog

All notable changes to `godot-cli` are documented in this file.

## Unreleased

- **Unified machine auth (unified-machine-auth f2).** The CLI adopts
  `@baizor/gamedev-cli-core@0.4.0`'s shared machine-auth stack; the CLI-local machine-credential
  store copy (`src/utils/machine-credentials.ts`) is **deleted** — cli-core is the only TS store
  implementation (families schema v2, atomic writes, cross-process lock, unreadable-store honesty).
  - `login` now runs the F1 agent login: an **agent-scope** (`mcp:agent`) device grant, committed
    under a first lock hold, with the tools (`mcp:plugin`) credential **derived via RFC 8693 token
    exchange** and committed (+ the v1 compat mirror) under a second hold. A failed exchange leaves
    the machine "partially authorized" (agent family committed) and the derivation is retried.
  - New **`login --tools-only`** (O10): CI/automation mode — mints and stores a plugin family
    ONLY, so the desktop App cannot pick the sign-in up and the runner is its own revocable device.
  - New **`login --yes`** + account-switch guard (D6/F7): signing in as a *different* account than
    the machine store holds now requires confirmation (TTY prompt, `--yes`, or fail-closed decline
    in non-interactive runs); declining revokes the just-minted credential and leaves the store
    untouched.
  - Commands that use a persisted cloud credential (`open --mode Cloud`, `run-tool`, `status`,
    `wait-for-ready`) now resolve it through cli-core's `MachineCredentialProvider` — an expired
    access token is **refreshed automatically** under the cross-process lock (presenting the
    family's stored `client_id`, `scope`/`resource` omitted) instead of being handed out stale.
  - The legacy per-project sink `<project>/.godot-mcp/credentials.json` is **no longer written
    anywhere**. It is still read as a fallback for one release and is **migrated into the machine
    store on first use** (as a legacy family, under the lock, only when the machine store is
    empty); `login --project` now persists to the per-project store
    `<project>/.ai-game-dev/credentials.json` (gitignored) instead.
  - `install-plugin --enroll` commits its redeemed credential through the same shared machinery
    (plugin family, own `client_id`, under the lock), with the account-switch guard failing closed.

- **BREAKING (requires a matching addon).** `status` and `wait-for-ready` now probe
  `/api/system-tools/ping` instead of `/api/tools/ping`. The addon's `ping` became a **System** tool
  (owner ruling 2026-07-25, for parity with Unity-MCP), and `McpPluginBuilder` partitions tools by
  `ToolType` into two disjoint registries — so `ping` no longer answers on the standard tool route at
  all. Pair this CLI with an addon of the same release; against an OLDER addon (where `ping` is still
  Standard) both commands report a healthy editor as unreachable. There is deliberately no fallback
  probe, matching the Unity CLI, which has always probed the system route.
- `install-extension <id> [path]` — install a Godot-MCP **extension** (an optional AI-tool-family package)
  into a Godot C# project: resolve `<id>` from the shared catalog, add/update its `<PackageReference>` in
  the project `.csproj` (added when absent, version-bumped only when newer, no-op when up to date), then
  ask the user to rebuild. Idempotent and behaviorally identical to the in-editor Extensions dock.
- Added `installExtension` to the library API, plus the shared `EXTENSIONS_CATALOG` + `findExtension`
  exports so the app can render/install the same list the dock + CLI use.
- The extension catalog (`addons/godot_mcp/extensions.catalog.json`) is now the single source of truth
  consumed by all three channels: the dock parses it via an embedded resource, the CLI mirrors it
  (`extensions-catalog.ts`, parity-tested), and the app imports it from the `godot-cli` library.

## 0.1.0

Initial release. A cross-platform CLI for Godot-MCP, mirroring `unity-mcp-cli`'s feature set and structure,
adapted for Godot.

- `open` — resolve the Godot editor binary (`GODOT_BIN`/`GODOT4_BIN` → PATH → per-OS common dirs) and
  launch `--editor --path <project>` with the `GODOT_MCP_*` connection env vars.
- `run-tool` / `run-system-tool` — POST to `<url>/api/tools/<name>` and `<url>/api/system-tools/<name>`.
- `status` — detect a running Godot editor and probe MCP-server health.
- `wait-for-ready` — poll the server until it answers `ping`.
- `setup-mcp` — write an AI-agent MCP-client config (claude-code, claude-desktop, cursor, vscode, custom)
  pointing at the Godot server's `<host>/mcp` URL.
- `configure` — list / enable / disable tools, prompts, and resources in the project-local
  `.godot-mcp/features.json`.
- `close` — gracefully terminate the Godot editor for a project (`--force` to hard-kill).
- `install-plugin` / `remove-plugin` — enable/disable the `godot_mcp` addon in `project.godot`
  `[editor_plugins]`.
- `update` — check npm for a newer version and install it.

`setup-skills` is intentionally not ported: Godot skills are generated addon-side by the McpPlugin engine,
and the Godot MCP server exposes no skill-generate HTTP endpoint.
