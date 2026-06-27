# Changelog

All notable changes to `godot-cli` are documented in this file.

## Unreleased

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
