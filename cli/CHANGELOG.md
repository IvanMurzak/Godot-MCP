# Changelog

All notable changes to `godot-mcp-cli` are documented in this file.

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
