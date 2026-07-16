# Runtime (in-game) security contract

Godot-MCP can run **inside a running / exported game build** (debug or release), not only the editor —
see [Runtime usage (in-game)](../README.md#runtime-usage-in-game) for the full how-to. Running an MCP
connection inside a shipped game opens a **remote-control surface**: anything your registered
`[AiToolType]` tools can do, a connected MCP client can drive. This document is the security contract
for that mode. Read it before shipping a build that calls `GodotMcpRuntime.Initialize(...)`.

## Guarantees the runtime gives you

- **Opt-in only — never auto-connects.** The editor plugin connects on boot; a game build does **not**.
  `GodotMcpRuntime.Initialize(...).Build()` returns a **default-OFF** `GodotMcpRuntimeHandle`. Nothing
  connects until *your* code explicitly calls `handle.Connect()`. There is no auto-connect code path in
  a non-editor build.
- **Zero tools by default — strictly manual.** A freshly-built runtime registers **no** MCP tools. The
  attack surface is exactly the set of `[AiToolType]` classes you opt in via
  `builder.WithToolsFromAssembly(...)` / `builder.WithTools(...)` — no more. With none registered, the
  connection still builds and connects with an empty tool set (exactly Unity-MCP's model).
- **Editor tool families cannot leak in.** The addon's 7 editor tool families (`Tool_Node`,
  `Tool_Scene`, `Tool_Resource`, `Tool_FileSystem`, `Tool_Script`, `Tool_Screenshot`, `Tool_Editor`)
  are gated by `#if TOOLS` and do **not compile** into an exported game build. Even a
  `WithToolsFromAssembly(...)` over the addon's own assembly cannot register them.
- **No persisted-config auto-load.** A game build never silently reads a saved (`user://`) config file —
  that is an editor-only convenience. Host and token come only from your code (`WithConfig`) or from
  `GODOT_MCP_*` process environment / a project `.env`, which `GodotMcpConfig` reads live.

## What you must do when enabling it

- **Connect deliberately.** Only call `handle.Connect()` when you actually intend to expose tools (a dev
  build, a QA harness, an explicitly-designed in-game agent feature). Call `handle.Disconnect()` /
  `handle.Dispose()` to stop exposing them.
- **Register the minimum tool set.** Prefer `WithTools(typeof(MyToolFamily))` over a broad
  `WithToolsFromAssembly(...)` when you only need a few tools. Every registered tool is reachable by a
  connected client.
- **Prefer loopback + a token.** For local tooling, bind Custom mode to a loopback host
  (`http://localhost:…` / `127.0.0.1`) and set `AuthOption = Consts.MCP.Server.AuthOption.token` with a real
  `Token`:

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // loopback
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // discouraged: see the env-var form below — don't ship a hard-coded secret
  });
  ```

  Prefer the out-of-band form below, so the build carries no embedded secret:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **Do not expose an unauthenticated MCP surface on a public interface in a release build** unless you
  have explicitly designed and secured that surface. Treat any non-loopback host as a deliberate,
  reviewed decision.
- **Do not embed long-lived secrets in the shipped binary.** Prefer `GODOT_MCP_TOKEN` from the
  environment over a hard-coded `config.Token` so the token is not extractable from the build.
- **Enable runtime-error capture only on a trusted connection.** `builder.WithRuntimeErrorCapture()` is
  **OFF by default**. When enabled, captured errors forward the **full** message and (for C# faults) the
  **full managed stack trace** to the connected agent via the `runtime-errors-get` tool. Those strings can
  embed sensitive runtime data — absolute filesystem paths, machine/user names, query strings, or a
  secret/token that surfaced in an exception message or argument. That is the intended diagnostic value,
  but it widens the data exposed over the connection. Enable it only on a loopback host with
  `AuthOption = Consts.MCP.Server.AuthOption.token` and a real token — never on an unauthenticated public
  interface in a release build.

## Environment variables

The runtime config (`GodotMcpConfig`) reads these `GODOT_MCP_*` variables live (process env or project
`.env`). They override values set in code at resolution time.

| Variable | Values | Description |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Connection mode (a loopback host implies `Custom`). |
| `GODOT_MCP_CLOUD_URL` | URL | Override the Cloud base URL (default `https://ai-game.dev`). |
| `GODOT_MCP_HOST` | URL | Custom-mode server host (default `http://localhost:8080`). |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Custom-mode auth: anonymous / account (oauth) / offline bearer (`token`). |
| `GODOT_MCP_TOKEN` | string | The bearer token (routed to Cloud or Custom by the active mode). |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | Log-verbosity threshold. |

## Editor-side surfaces (accepted posture)

The contract above is for a **game build**. Two editor-side surfaces are documented here for completeness;
both are **by design** today (no encryption / no shared-secret is implemented yet — those are deferred,
parity-constrained decisions to coordinate across the Unity / Godot / Unreal siblings).

### Editor token storage is plaintext at rest

When you connect the **editor plugin** (Cloud device-auth, or a Custom-mode token), it persists your
connection config to **`user://godot-mcp-config.json`** (Godot resolves `user://` to the per-platform user
data directory via `ProjectSettings.GlobalizePath`). The bearer token (`token`) and Cloud token
(`cloudToken`) are written as **plaintext JSON** — they are **not** encrypted and **not** stored in an OS
keystore.

- **Trust assumption: the local user account.** Anyone who can read your user data directory can read the
  token. Treat `user://godot-mcp-config.json` as a secret — don't commit it, sync it to a shared drive, or
  paste it.
- **Rotation.** Clear the saved token in the dock (or delete the file) and reconnect.
- **Precedence.** A `GODOT_MCP_TOKEN` process-env / `.env` value always shadows the persisted token at
  resolution time and is **not** written back into this file — so a CI / shared machine can run without
  ever persisting a token to disk.

### The dev-control bridge is unauthenticated but gated OFF

A development-only inject/control HTTP bridge (`DevControlServer`) exists for driving the editor dock from
tests / a terminal. It is **unauthenticated**, but its security boundary is threefold:

1. **Editor-only.** It is compiled behind `#if TOOLS`, so it does not exist in an exported game build.
2. **Loopback-only.** It binds **`127.0.0.1`** exclusively — never a routable interface.
3. **Env-gated OFF.** It is constructed and started **only when `GODOT_MCP_DEV_CONTROL=1`** (exactly `1`).
   An unset / any-other value means the bridge never listens — a shipped addon, and any normal editor
   session, never opens the surface.

That env gate is **load-bearing**: it is the single thing standing between "dev scaffolding" and "an
unauthenticated control surface on the live dock". It is enforced by a pure-managed predicate
(`DevControlGate.IsEnabled`) with a unit test (`DevControlGateTests`) **and** a boot-time assertion at the
construction site (`DevControlGate.AssertEnabledOrThrow`), so a regression that wires the bridge on without
the env var fails fast in CI and at runtime rather than silently shipping enabled.
