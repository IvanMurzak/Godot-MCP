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
- **Editor tool families cannot leak in.** The addon's 9 editor tool families (`Tool_Node`,
  `Tool_Scene`, `Tool_Resource`, `Tool_FileSystem`, `Tool_Script`, `Tool_Screenshot`, `Tool_Editor`,
  and the editor surfaces of others) are gated by `#if TOOLS` and do **not compile** into an exported
  game build. Even a `WithToolsFromAssembly(...)` over the addon's own assembly cannot register them.
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
- **Prefer loopback + a required token.** For local tooling, bind Custom mode to a loopback host
  (`http://localhost:…` / `127.0.0.1`) and set `AuthOption = GodotMcpAuthOption.Required` with a real
  `Token`:

  ```csharp
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // loopback
      config.AuthOption     = GodotMcpAuthOption.Required;
      config.Token          = "your-secret-token";
  });
  ```

  Or out-of-band, so the build carries no embedded secret:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=Required
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **Do not expose an unauthenticated MCP surface on a public interface in a release build** unless you
  have explicitly designed and secured that surface. Treat any non-loopback host as a deliberate,
  reviewed decision.
- **Do not embed long-lived secrets in the shipped binary.** Prefer `GODOT_MCP_TOKEN` from the
  environment over a hard-coded `config.Token` so the token is not extractable from the build.

## Environment variables

The runtime config (`GodotMcpConfig`) reads these `GODOT_MCP_*` variables live (process env or project
`.env`). They override values set in code at resolution time.

| Variable | Values | Description |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Connection mode (a loopback host implies `Custom`). |
| `GODOT_MCP_CLOUD_URL` | URL | Override the Cloud base URL (default `https://ai-game.dev`). |
| `GODOT_MCP_HOST` | URL | Custom-mode server host (default `http://localhost:8080`). |
| `GODOT_MCP_AUTH_OPTION` | `None` / `Required` | Whether Custom mode sends a bearer token. |
| `GODOT_MCP_TOKEN` | string | The bearer token (routed to Cloud or Custom by the active mode). |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | Log-verbosity threshold. |
