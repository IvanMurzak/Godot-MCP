# godot-mcp-cli

Cross-platform CLI for **Godot-MCP** — the [Godot](https://godotengine.org/) editor addon that bridges
LLMs (Claude, Cursor, Copilot, …) with the Godot editor via the
[Model Context Protocol](https://modelcontextprotocol.io/).

The CLI is the Godot analog of [`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli): it resolves
and launches the Godot editor with the right `GODOT_MCP_*` connection environment variables, runs MCP and
system tools over the server's HTTP API, probes server health, writes AI-agent MCP-client config, and
enables/disables the `godot_mcp` addon in a project.

> **Licensed under Apache-2.0.**

## Install

```bash
npm install -g godot-mcp-cli
# or run ad-hoc:
npx godot-mcp-cli <command>
```

Requires Node `^20.19.0 || >=22.12.0`.

## Commands

| Command | What it does |
| --- | --- |
| `open [path]` | Resolve the Godot editor binary and launch `--editor --path <project>` with `GODOT_MCP_*` connection env vars. |
| `run-tool <tool> [path]` | POST to `<url>/api/tools/<tool>` with JSON input. |
| `run-system-tool <tool> [path]` | POST to `<url>/api/system-tools/<tool>` (tools not exposed to MCP clients). |
| `status [path]` | Detect a running Godot editor for the project and probe MCP-server health. |
| `wait-for-ready [path]` | Poll the MCP server until it answers `ping`. |
| `setup-mcp <agent> [path]` | Write the agent's MCP-client config pointing at the Godot server's `<host>/mcp` URL. |
| `configure [path]` | List / enable / disable tools, prompts, and resources in the project-local `.godot-mcp/features.json`. |
| `close [path]` | Gracefully stop the Godot editor running for a project (`--force` to hard-kill). |
| `install-plugin [path]` | Enable the `godot_mcp` addon in `project.godot` `[editor_plugins]`. |
| `remove-plugin [path]` | Disable the `godot_mcp` addon in `project.godot` `[editor_plugins]`. |
| `update` | Check npm for a newer `godot-mcp-cli` and install it. |

### Editor resolution (`open`)

`open` locates the Godot editor binary in this order:

1. `--editor-path <path>` (explicit).
2. `GODOT_BIN` / `GODOT4_BIN` environment variables.
3. The first matching Godot binary on `PATH` (the mono build is preferred on Windows).
4. Per-OS common install directories — Windows prefers the mono build; macOS resolves the
   `.app/Contents/MacOS/Godot` binary; Linux scans common extracted-binary locations.

### Connection env vars

`open` forwards these to the editor process (names match the addon's `GodotMcpConfig`):

| Flag | Env var |
| --- | --- |
| `--url <host>` | `GODOT_MCP_HOST` |
| `--cloud-url <url>` | `GODOT_MCP_CLOUD_URL` |
| `--token <token>` | `GODOT_MCP_TOKEN` |
| `--auth None\|Required` | `GODOT_MCP_AUTH_OPTION` |
| `--mode Cloud\|Custom` | `GODOT_MCP_CONNECTION_MODE` |
| `--log-level <level>` | `GODOT_MCP_LOG_LEVEL` |

### Server URL resolution (`run-tool` / `status` / `wait-for-ready`)

Unlike Unity, the Godot plugin is a **server-less client** whose persisted config lives in `user://`
(outside the project tree), so there is no deterministic project-path → port hash. The server base URL is
resolved as:

1. `--url <url>` (explicit override).
2. `GODOT_MCP_HOST` env (Custom-mode host).
3. `GODOT_MCP_CLOUD_URL` env / Cloud mode → cloud base (`https://ai-game.dev`).
4. Default custom host (`http://localhost:8080`).

Pass `--url http://localhost:<port>` to target a local/self-hosted server.

## `setup-skills` is not in v1

Unlike the Unity CLI, there is **no `setup-skills` command**. Godot skills are generated **addon-side** by
the McpPlugin engine on plugin boot (`GodotMcpConnection.Start` → `GenerateSkillFilesIfNeeded`, driven by
the `GodotMcpConfig.GenerateSkillFiles` toggle, with a manual "Generate" button in the dock's Skills card).
The Godot MCP server exposes **no** `skill-generate` HTTP endpoint for the CLI to call, so porting
`setup-skills` would have nothing to invoke. It is intentionally out of scope for v1.

## Library API

The package also exports a side-effect-free library (the `.` entry):

```ts
import { openProject, runTool, setupMcp, installPlugin } from 'godot-mcp-cli';
```

Every function returns a discriminated union (`{ kind: 'success', ... }` / `{ kind: 'failure', error }`)
and never throws past the public boundary.

## Development

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```
