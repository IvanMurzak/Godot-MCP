<div align="center" width="100%">
  <h1>Godot MCP — <i>CLI</i></h1>

[![npm](https://img.shields.io/npm/v/godot-cli?label=npm&labelColor=333A41 'npm package')](https://www.npmjs.com/package/godot-cli)
[![Node.js](https://img.shields.io/badge/Node.js-%5E20.19.0%20%7C%7C%20%3E%3D22.12.0-5FA04E?logo=nodedotjs&labelColor=333A41 'Node.js')](https://nodejs.org/)
[![License](https://img.shields.io/github/license/IvanMurzak/Godot-MCP?label=License&labelColor=333A41)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE)
[![Website](https://img.shields.io/badge/website-ai--game.dev-bc6c25?labelColor=333A41)](https://ai-game.dev)
[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/badges/StandWithUkraine.svg)](https://stand-with-ukraine.pp.ua)

  <img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/promo/ai-developer-banner-glitch.gif" alt="AI Game Developer" title="Godot MCP CLI" width="100%">

  <p>
    <a href="https://claude.ai/download"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/claude-64.png" alt="Claude" title="Claude" height="36"></a>&nbsp;&nbsp;
    <a href="https://openai.com/index/introducing-codex/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/codex-64.png" alt="Codex" title="Codex" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.cursor.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/cursor-64.png" alt="Cursor" title="Cursor" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/docs/copilot/overview"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/github-copilot-64.png" alt="GitHub Copilot" title="GitHub Copilot" height="36"></a>&nbsp;&nbsp;
    <a href="https://gemini.google.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/gemini-64.png" alt="Gemini" title="Gemini" height="36"></a>&nbsp;&nbsp;
    <a href="https://antigravity.google/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/antigravity-64.png" alt="Antigravity" title="Antigravity" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/vs-code-64.png" alt="VS Code" title="VS Code" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.jetbrains.com/rider/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/rider-64.png" alt="Rider" title="Rider" height="36"></a>
  </p>

</div>

Cross-platform CLI for **[Godot-MCP](https://github.com/IvanMurzak/Godot-MCP)** — the
[Godot](https://godotengine.org/) editor addon that bridges LLMs (Claude, Cursor, Copilot, …) with the
Godot editor via the [Model Context Protocol](https://modelcontextprotocol.io/). Resolve and launch the
Godot editor with active MCP connections, run tools, configure AI agents, and manage the `godot_mcp`
addon — all from a single command line.

The CLI is the Godot analog of [`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli) and
[`unreal-mcp-cli`](https://www.npmjs.com/package/unreal-mcp-cli). Backed by **[ai-game.dev](https://ai-game.dev)**.

> **Licensed under Apache-2.0.**

## ![AI Game Developer — Godot SKILLS and MCP](https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/img/promo/hazzard-features.svg?raw=true)

- :white_check_mark: **Open & Connect** — build the project's C# assembly and launch the Godot editor with `GODOT_MCP_*` connection env vars
- :white_check_mark: **Install plugin** — install the `godot_mcp` addon end-to-end (download release, add NuGet pins + catalog, enable)
- :white_check_mark: **Install extensions** — add optional Godot-MCP AI tool-family packages to a project
- :white_check_mark: **Remove plugin** — disable the `godot_mcp` addon in `project.godot`
- :white_check_mark: **Configure** — enable/disable MCP tools, prompts, and resources
- :white_check_mark: **Status check** — detect a running Godot editor and probe MCP-server health
- :white_check_mark: **Run tools** — execute MCP and system tools directly over the server's HTTP API
- :white_check_mark: **Setup MCP** — write AI-agent MCP-client config for any supported agent
- :white_check_mark: **Setup skills** — generate Godot-MCP skill files locally (no live editor required)
- :white_check_mark: **Wait for ready** — poll until the Godot MCP server answers `ping`
- :white_check_mark: **Cross-platform** — Windows, macOS, and Linux
- :white_check_mark: **Library API** — a side-effect-free, typed library surface for embedding

![divider](https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Quick Start

```bash
# Install globally
npm install -g godot-cli

# Install the godot_mcp addon into a project
godot-cli install-plugin ./MyGodotProject

# Sign in to the ai-game.dev cloud (OAuth 2.1 device login — once per machine)
godot-cli login

# Open the project (builds C# first, then launches the editor with MCP connection)
godot-cli open ./MyGodotProject

# Wait until the MCP server is ready to accept tool calls
godot-cli wait-for-ready ./MyGodotProject
```

Or run any command ad-hoc with `npx` — no global install required:

```bash
npx godot-cli install-plugin /path/to/godot/project
```

> **Requirements:** [Node.js](https://nodejs.org/) `^20.19.0 || >=22.12.0`.

![divider](https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Commands

| Command | What it does |
| --- | --- |
| `open [path]` | Build the project's C# assembly (so the addon loads on first open — see below), resolve the Godot editor binary, and launch `--editor --path <project>` with `GODOT_MCP_*` connection env vars. `--no-build` skips the build. |
| `build [path]` | Build the project's C# assembly (`dotnet build`) so the `godot_mcp` addon loads on the next editor open. GDScript-only projects (no `.csproj`) are a no-op. This is the same build `open` runs before launching. |
| `run-tool <tool> [path]` | POST to `<url>/api/tools/<tool>` with JSON input. |
| `run-system-tool <tool> [path]` | POST to `<url>/api/system-tools/<tool>` (tools not exposed to MCP clients). |
| `status [path]` | Detect a running Godot editor for the project and probe MCP-server health. |
| `wait-for-ready [path]` | Poll the MCP server until it answers `ping`. |
| `login [path]` | Authenticate with the ai-game.dev cloud via the OAuth 2.1 device-authorization flow (RFC 8628) — opens a browser, then saves a cloud credential to the shared machine store (`~/.ai-game-dev/credentials.json`) the editor plugin auto-adopts. See the `login` section below for its `--project` / `--base-url` / `--force` flags. |
| `setup-mcp <agent> [path]` | Write the agent's MCP-client config pointing at the project-pinned `<host>/mcp/p/<pin>` URL (so the agent routes to *this* project's editor). Add `--no-pin` for the bare `<host>/mcp` URL. |
| `setup-skills <agent> [path]` | Generate Godot-MCP skill files (a `SKILL.md`-per-tool-family) under the agent's skills path. `--list` shows each agent's skills support. |
| `configure [path]` | List / enable / disable tools, prompts, and resources in the project-local `.godot-mcp/features.json`. |
| `close [path]` | Gracefully stop the Godot editor running for a project (`--force` to hard-kill). |
| `install-plugin [path]` | Install the `godot_mcp` addon end-to-end: materialize `res://addons/godot_mcp/` (download the matching GitHub release, or `--source <path>` a local copy), add the required NuGet `PackageReference`s + the extension-catalog `<EmbeddedResource>` to the project `.csproj`, and enable the plugin. Idempotent. |
| `install-extension <id> [path]` | Install a Godot-MCP **extension** (an optional AI-tool-family package) into the project: resolve `<id>` from the shared catalog, add/update its `<PackageReference>` in the project `.csproj`, then rebuild to restore. Idempotent — behaviorally identical to the in-editor dock. |
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

![divider](https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

## `login`

`godot-cli login` authenticates the editor plugin's **cloud** connection to
[ai-game.dev](https://ai-game.dev) using the **OAuth 2.1 device-authorization flow** (RFC 8628) — it prints
a short user code + verification URL, opens your browser, and polls until you approve:

```bash
godot-cli login                       # sign in once per machine (default)
godot-cli login --project ./MyGame    # keep a per-project credential instead
godot-cli login --base-url <url>      # authenticate against a non-default server
godot-cli login --force               # re-authenticate over an existing credential
```

- By default the credential is saved to the shared **machine store** `~/.ai-game-dev/credentials.json`
  (`0600` on POSIX / DPAPI on Windows) — sign in **once per machine** and the Godot editor plugin
  auto-adopts it, so `godot-cli open --mode Cloud` connects with **no `--token`**.
- `--project <path>` (or the positional `[path]`) keeps a per-project credential
  (`<path>/.godot-mcp/credentials.json`, gitignored) for per-project accounts.
- The flow persists the **full** credential set (access token + rotating refresh token + expiry) — **no
  personal access token (PAT) is ever minted**. On any failure nothing is written, so an existing
  credential survives a denied / expired / network error intact.

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

It performs four steps:

1. **Materialize `res://addons/godot_mcp/`.** By default it downloads `godot-mcp-addon-<version>.zip`
   over HTTPS from **github.com only** (the `IvanMurzak/Godot-MCP` release `v<version>`; the version
   defaults to the CLI's own version). Non-`github.com` hosts and plain `http` are rejected. With
   `--source <path>` it copies the addon from a local directory instead (no network) — `<path>` may be a
   directory that *is* `addons/godot_mcp` or one that *contains* it.
2. **Add the NuGet packages** the addon needs (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`)
   to the project's `.csproj`, idempotently — adding when missing, reconciling a stale version, and
   leaving a correct pin untouched. The versions are single-sourced from the addon's own
   `Godot-MCP.csproj` pins, so the scaffold can never drift.
3. **Embed the extension catalog** (`<EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json"
   LogicalName="Godot-MCP.extensions.catalog.json" />`) into the project's `.csproj`, idempotently. Without
   it the addon's extension registry reads no catalog at editor runtime and the **Extensions panel is
   empty**. Single-sourced + parity-tested against the addon csproj alongside the pins.
4. **Enable the plugin** in `project.godot` `[editor_plugins]`.

It is library-safe (returns a `{ kind: 'success' | 'failure' }` union; never throws past the public
boundary) and idempotent — re-running on an already-installed project reports no change.

## `install-extension`

`godot-cli install-extension <id> [path]` installs an optional Godot-MCP **extension** (a package that
adds more AI tool families) into a Godot C# project — the terminal/library channel for the same install
the in-editor **Extensions** dock performs:

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.ProBuilder ./MyGodotProject   # add/update the PackageReference
godot-cli install-extension "ProBuilder Tools"                                      # resolve by name; default cwd
godot-cli install-extension com.IvanMurzak.Godot.MCP.ProBuilder --version 1.3.0     # override the catalog pin
```

- Resolves `<id>` against the **shared extension catalog** (`addons/godot_mcp/extensions.catalog.json` —
  the single source of truth the dock, the CLI, and the app all consume), matching by package id (then
  name), case-insensitive.
- Read-modify-writes a `<PackageReference Include="<packageId>" Version="<version>" />` into the project's
  root `.csproj`: **added** when absent, version **bumped** only when the catalog (or `--version`) pins a
  newer version, and a **no-op** when already up to date — then asks you to rebuild (`godot-cli build`) so
  Godot restores + compiles the new package.
- Behaviorally identical to the dock's `ExtensionInstaller` (the same add / update / no-op + numeric
  version-compare rules; verified by a shared scenario set on both sides).

> The catalog ships **empty** until the first Godot-MCP extension package is published, so every `<id>` is
> currently reported as an unknown extension — there is nothing to install yet.

## Library API

The package also exports a side-effect-free library (the `.` entry):

```ts
import { openProject, runTool, setupMcp, installPlugin, installExtension } from 'godot-cli';

// The shared extension catalog + lookup helpers are exported too, so a GUI (the app)
// can render the same list the dock + CLI install from:
import { EXTENSIONS_CATALOG, findExtension } from 'godot-cli';
```

Every function returns a discriminated union (`{ kind: 'success', ... }` / `{ kind: 'failure', error }`)
and never throws past the public boundary.

## Development

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```

![divider](https://github.com/IvanMurzak/Unity-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Supported AI Agents

`godot-cli` writes ready-to-use MCP client configs for every major AI coding agent — run
`godot-cli setup-mcp <agent> [path]` to wire one up (and `godot-cli setup-skills <agent> [path]` to
generate its skill files). Use `--list` on either command to see every supported agent.

<div align="center">
  <p>
    <a href="https://claude.ai/download"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/claude-64.png" alt="Claude" title="Claude" height="36"></a>&nbsp;&nbsp;
    <a href="https://openai.com/index/introducing-codex/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/codex-64.png" alt="Codex" title="Codex" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.cursor.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/cursor-64.png" alt="Cursor" title="Cursor" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/docs/copilot/overview"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/github-copilot-64.png" alt="GitHub Copilot" title="GitHub Copilot" height="36"></a>&nbsp;&nbsp;
    <a href="https://gemini.google.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/gemini-64.png" alt="Gemini" title="Gemini" height="36"></a>&nbsp;&nbsp;
    <a href="https://antigravity.google/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/antigravity-64.png" alt="Antigravity" title="Antigravity" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/vs-code-64.png" alt="VS Code" title="VS Code" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.jetbrains.com/rider/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/rider-64.png" alt="Rider" title="Rider" height="36"></a>&nbsp;&nbsp;
    <a href="https://visualstudio.microsoft.com/"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/visual-studio-64.png" alt="Visual Studio" title="Visual Studio" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/anthropics/claude-code"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/open-code-64.png" alt="Open Code" title="Open Code" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/cline/cline"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/cline-64.png" alt="Cline" title="Cline" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/Kilo-Org/kilocode"><img src="https://github.com/IvanMurzak/Unity-MCP/raw/main/docs/img/mcp-clients/kilo-code-64.png" alt="Kilo Code" title="Kilo Code" height="36"></a>
  </p>
</div>

```bash
godot-cli setup-mcp --list                    # list every supported agent id
godot-cli setup-mcp claude-code ./MyGame      # write the agent's MCP client config (pinned to this project)
godot-cli setup-mcp claude-code ./MyGame --no-pin   # write the bare, unpinned <host>/mcp URL instead
```

OAuth-capable agents (Claude Code, Cursor, Codex, Copilot, …) authenticate to the cloud with their **own**
OAuth handshake, so `setup-mcp` writes them a credential-free, URL-only config — no bearer token is stored
in the agent file. A static `Authorization` header is written only for a client that cannot OAuth, or when
you pass an explicit `--token` (a deliberate personal-token opt-in for a self-hosted, required-auth server).

> For the full Godot-MCP project documentation, see the
> [main README](https://github.com/IvanMurzak/Godot-MCP/blob/main/README.md). Backed by
> **[ai-game.dev](https://ai-game.dev)**.

<div align="center">
  <sub>Made with :orange_heart: for game developers — <a href="https://ai-game.dev">ai-game.dev</a></sub>
</div>
