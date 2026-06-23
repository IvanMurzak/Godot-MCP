# godot-cli

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
npm install -g godot-cli
# or run ad-hoc:
npx godot-cli <command>
```

Requires Node `^20.19.0 || >=22.12.0`.

## Commands

| Command | What it does |
| --- | --- |
| `open [path]` | Build the project's C# assembly (so the addon loads on first open — see below), resolve the Godot editor binary, and launch `--editor --path <project>` with `GODOT_MCP_*` connection env vars. `--no-build` skips the build. |
| `build [path]` | Build the project's C# assembly (`dotnet build`) so the `godot_mcp` addon loads on the next editor open. GDScript-only projects (no `.csproj`) are a no-op. This is the same build `open` runs before launching. |
| `run-tool <tool> [path]` | POST to `<url>/api/tools/<tool>` with JSON input. |
| `run-system-tool <tool> [path]` | POST to `<url>/api/system-tools/<tool>` (tools not exposed to MCP clients). |
| `status [path]` | Detect a running Godot editor for the project and probe MCP-server health. |
| `wait-for-ready [path]` | Poll the MCP server until it answers `ping`. |
| `setup-mcp <agent> [path]` | Write the agent's MCP-client config pointing at the Godot server's `<host>/mcp` URL. |
| `setup-skills <agent> [path]` | Generate Godot-MCP skill files (a `SKILL.md`-per-tool-family) under the agent's skills path. `--list` shows each agent's skills support. |
| `configure [path]` | List / enable / disable tools, prompts, and resources in the project-local `.godot-mcp/features.json`. |
| `close [path]` | Gracefully stop the Godot editor running for a project (`--force` to hard-kill). |
| `install-plugin [path]` | Install the `godot_mcp` addon end-to-end: materialize `res://addons/godot_mcp/` (download the matching GitHub release, or `--source <path>` a local copy), add the required NuGet `PackageReference`s to the project `.csproj`, and enable the plugin. Idempotent. |
| `remove-plugin [path]` | Disable the `godot_mcp` addon in `project.godot` `[editor_plugins]` (does not delete the addon files). |
| `update` | Check npm for a newer `godot-cli` and install it. |

### Build before open (`open` / `build`)

`open` **builds the project's C# assembly before launching the editor**. Godot instantiates an
enabled addon's `EditorPlugin` as soon as the editor loads — so on a *fresh* first open of a C#
project that was never built, no assembly exists yet and Godot fails with
`Unable to load addon script 'res://addons/godot_mcp/Editor/GodotMcpPlugin.cs' … Disabling the addon`.
Building first guarantees the assembly is present when the addon is instantiated.

- The build is the same one `godot-cli build` runs: `dotnet build <project>.csproj --configuration Debug`.
- **GDScript-only projects** (no `.csproj` at the root) are skipped automatically — there is nothing to compile.
- The build runs **unconditionally** for C# projects; `dotnet build` is incremental, so an up-to-date
  project is a fast no-op. Pass `--no-build` to `open` to skip it (e.g. you already built, or want to
  open as fast as possible).
- If the build **fails**, `open` does **not** launch the editor (launching would just reproduce the
  disable-addon failure); the error is surfaced instead.

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

## `setup-skills`

`godot-cli setup-skills <agent> [path]` generates AI-agent **skill files** for a Godot project under the
selected agent's skills path (e.g. Claude Code's `.claude/skills`). Use `--list` to see every agent and its
skills support.

```bash
godot-cli setup-skills claude-code            # generate into ./.claude/skills
godot-cli setup-skills cursor ./MyGame        # generate into MyGame/.cursor/skills
godot-cli setup-skills --list                 # list agents + their skills paths
```

The command writes a `SKILL.md`-per-tool-family directory (a `godot-mcp/` overview plus one per family:
`godot-mcp-node`, `godot-mcp-scene`, `godot-mcp-resource`, …) describing the `godot_mcp` addon's tool
families. It is **idempotent** — re-running rewrites the same bytes.

Unlike the Unity CLI — which POSTs to a running editor's `/api/system-tools/unity-skill-generate`
endpoint — the Godot CLI generates the files **locally** from a built-in catalog: the Godot MCP server
exposes no skill-generate HTTP endpoint, so no server or running editor is required. (The addon
*additionally* auto-generates skills in-process on plugin boot via
`GodotMcpConnection.Start` → `GenerateSkillFilesIfNeeded`; the CLI command is the
server-less, scriptable path that does not need a live editor.)

## `install-plugin`

`godot-cli install-plugin [path]` is a **real installer** — it makes a from-scratch terminal install
produce a working project, in one idempotent command:

```bash
godot-cli install-plugin ./MyGodotProject                      # download the matching release + install
godot-cli install-plugin --version 0.11.1 ./MyGodotProject     # pin a specific addon release
godot-cli install-plugin --source ./Godot-MCP/addons/godot_mcp ./MyGodotProject   # offline / dev copy
```

It performs three steps:

1. **Materialize `res://addons/godot_mcp/`.** By default it downloads `godot-mcp-addon-<version>.zip`
   over HTTPS from **github.com only** (the `IvanMurzak/Godot-MCP` release `v<version>`; the version
   defaults to the CLI's own version). Non-`github.com` hosts and plain `http` are rejected. With
   `--source <path>` it copies the addon from a local directory instead (no network) — `<path>` may be a
   directory that *is* `addons/godot_mcp` or one that *contains* it.
2. **Add the NuGet packages** the addon needs (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`)
   to the project's `.csproj`, idempotently — adding when missing, reconciling a stale version, and
   leaving a correct pin untouched. The versions are single-sourced from the addon's own
   `Godot-MCP.csproj` pins, so the scaffold can never drift.
3. **Enable the plugin** in `project.godot` `[editor_plugins]`.

It is library-safe (returns a `{ kind: 'success' | 'failure' }` union; never throws past the public
boundary) and idempotent — re-running on an already-installed project reports no change.

## Library API

The package also exports a side-effect-free library (the `.` entry):

```ts
import { openProject, runTool, setupMcp, installPlugin } from 'godot-cli';
```

Every function returns a discriminated union (`{ kind: 'success', ... }` / `{ kind: 'failure', error }`)
and never throws past the public boundary.

## Development

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```
