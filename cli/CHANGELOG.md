# Changelog

All notable changes to `godot-cli` are documented in this file.

## Unreleased

- **In-game `script-execute` opt-in.** `install-plugin` writes Code-Exec, its Roslyn adapter and
  `Microsoft.CodeAnalysis.CSharp` with `'$(GodotMcpCodeExecInGame)' == 'true' or (<editor-only condition>)`, so a game
  that sets `GodotMcpCodeExecInGame=true` keeps them in its exports (RQL stays editor-only). Entries an older
  version wrote with the editor-only condition are upgraded; a `Condition` the project set is kept. The addon marks
  those packages `inGameOptIn` in `nuget-deps.json` (an older `godot-cli` ignores the flag and writes them
  editor-only; with `--source` it reads the new csproj condition as none and writes them unconditioned, so use
  this version for `--source` installs). A newer Roslyn the project pins keeps its version but moves between the
  two canonical conditions too.
- **McpPlugin 8.11.0; `exec` waits for the server's tool timeout.** `install-plugin` pins `com.IvanMurzak.McpPlugin`
  8.11.0 (fallback for addons without `nuget-deps.json`). The addon starts its local server with
  `tool-timeout=300000` (on a server built on McpPlugin 8.11.0+ a call is sent to the editor once and may run
  5 minutes; `plugin-timeout` is only the connection timeout), so `exec`'s default `--timeout` goes from 180000 to
  310000 ms and a slow call ends with the server's timeout error rather than a client abort. Older servers ignore
  `tool-timeout` and still give up after about 110 s.
- **No Roslyn in exported games.** `install-plugin` writes the addon's editor-only packages (RQL, Code-Exec, Roslyn)
  with a Condition that leaves them out of Godot's export builds, and adds it to entries an older version wrote (a
  `Condition` the project set is kept). The installed addon says which packages are editor-only (`editorOnly` in
  `nuget-deps.json`); an older addon marks none.
- **Editor sessions on `@baizor/gamedev-cli-core` 0.7.1.** The pre-launch C# build of `exec` / `open --headless`
  runs in the session's `beforeLaunch` hook, under its heartbeated startup lock and only when the call starts the
  editor: parallel cold calls build once (the CLI's own `.ai-game-dev/build.lock` is gone). The lease records the
  server URL and connection mode; while cli-core verifies that lease live (same host, pid and process start time),
  `exec` / `open --headless` talk to that server whatever `--url` / `GODOT_MCP_HOST` say (a crashed editor's lease
  is never followed), and `close --owned-only` / a windowed `open` send `session-close` there with the lease's nonce
  (`close --url` is now a hidden fallback for a lease written by 0.30.0). A user's editor that does not answer is
  reported after the session's own `foreign` wait (now 60 s, as in unity-mcp-cli). An editor started by another
  call that does not answer is no longer stopped (that call may still wait for it). The session files are listed
  in `.ai-game-dev/.gitignore`. Library: `createGodotSession` and the `GodotSession` type are exported;
  `GodotSession.probe` is removed (the session probes itself).
- **`exec`: run C# in the editor.** `godot-cli exec '<code>'` (or `-f <file>` / stdin, repeatable `--using` /
  `--include`) calls the addon's new `script-execute` tool. A running editor of the project is reused; otherwise the
  C# assembly is built and a **headless** editor starts (`--window` for a GUI one) on the project's local server,
  which its plugin starts itself (`GODOT_MCP_START_SERVER`); a headless one quits by itself after `--idle` seconds
  (default 300) without tool calls. A user's editor whose local server does not answer is reported after 20 s. Built on `@baizor/gamedev-cli-core` 0.7.0 `EditorSession` (lease
  `.ai-game-dev/session.json`, startup lock: parallel calls start one editor). Library: `execCode`, `ensureEditor`,
  `createGodotSession`.
- **`open --headless [--idle-timeout <s>]`** starts that session without running code; a windowed `open` first
  closes a headless editor the CLI started for the project. **`close --owned-only`** saves and quits only an editor
  the CLI started, through the plugin's `session-close` system tool (a windowless editor gets no WM_CLOSE).
- `install-plugin` keeps a newer third-party package the project already pins (e.g. its own Roslyn) instead of
  downgrading it to the addon's pin; `com.IvanMurzak.*` pins are still reconciled exactly.
- `install-plugin` writes the addon's three new NuGet pins (`com.IvanMurzak.CodeExec` and `.Roslyn` 0.2.1,
  `Microsoft.CodeAnalysis.CSharp` 4.12.0); `setup-skills` lists `script-execute` and the `session` family.

- **Extension catalog: the extensions are RQL modules.** The `EXTENSIONS_CATALOG` mirror follows the shared
  `extensions.catalog.json`: every Godot-AI-* extension (1.0.0) contributes no MCP tools any more (`tools: []`) and
  its description names the RQL surface it adds to `rql-query` / `rql-mutate` instead.

- **`install-plugin` pins the NuGet packages of the addon it installs.** The consumer `.csproj`'s
  `PackageReference` versions now come from the installed addon — `addons/godot_mcp/nuget-deps.json`,
  which the release workflow generates from `Godot-MCP.csproj` into the addon zip — instead of the
  versions baked into this CLI build, so `--version <other>` and `skipMaterialize` over a different
  addon no longer pin mismatched packages. `--source <checkout>/addons/godot_mcp` reads the checkout's
  `Godot-MCP.csproj`. Addon zips without the manifest (releases up to and including 0.25.2) fall
  back to the built-in pins with a warning. The library result gains `csproj.packagesOrigin`
  (`addon-manifest` | `source-csproj` | `builtin`).

- **Antigravity: two config file locations.** Antigravity reads its global MCP config from
  `~/.gemini/config/mcp_config.json` *or* `~/.gemini/antigravity/mcp_config.json` (which one differs per
  install), so `setup-mcp antigravity` now writes the entry into **both** (creating missing files,
  preserving every other entry), prints both paths, and fails naming the path it could not write.
  The library result gains `configPaths`.
- **`setup-mcp <agent> --regenerate-key` no longer breaks the project's other agent configs.** Before
  revoking the previous key it rewrites every other agent config whose `ai-game-developer` entry still
  carries it (pinned or `--no-pin` URL); if any cannot be rewritten — or still holds the key anywhere
  else — the previous key is NOT revoked and the configs are named in a warning. The library result gains `rewrittenConfigPaths`.

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
  - The `login` "already authenticated" gate is **plugin-plane-gated**, and a **partially
    authorized** machine (agent family committed, plugin derivation failed) is repaired by
    `login` in place: it re-runs ONLY the derivation leg from the committed agent family — no
    second device flow, no browser — or fails with an actionable message naming `--force`.
    An agent-only store never reads as "already authenticated" (review f2 B1).
  - An **unreadable per-project credential store** now surfaces "re-authorize this project"
    instead of silently falling back to the machine account (review f2 A1 — a command must not
    run as a different account than the project chose).

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
