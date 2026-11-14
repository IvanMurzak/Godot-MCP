**English** · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · [Español](connection.es.md) · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

# Connection and server

The editor plugin connects to an MCP server in one of two modes. Set the mode in the editor dock, or override it at process start with `GODOT_MCP_*` environment variables (useful for CI, headless runs and local development). The environment always wins over the saved config. `godot-cli open` passes these for you through `--mode`, `--url`, `--cloud-url` and `--token`.

## Cloud mode (default): ai-game.dev

The plugin connects to `https://ai-game.dev`; you run no server.

Sign in once with `godot-cli login`. It uses the OAuth 2.1 device flow (RFC 8628): it prints a short code, opens your browser and, after you approve, saves a credential to the machine store (`~/.ai-game-dev/credentials.json`). The editor plugin picks it up, so there is no token to copy.

| Variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` or `Custom` (case-insensitive). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | Cloud base URL. A trailing `/mcp` is stripped. | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | Absolute credential-store directory, for isolated testing. Also read from the project `.env`. | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | Bearer token for the active mode; a manual override for CI and headless runs. | none |

## Custom mode: your own server

The plugin connects to a server URL you choose: a local server, or a self-hosted one.

| Variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Set to `Custom`. | `Cloud` |
| `GODOT_MCP_HOST` | Server URL, an absolute http(s) URL. | `http://localhost:<port>`, a port derived from the project path |
| `GODOT_MCP_TOKEN` | Bearer token sent to that server. Setting it selects `token` authorization. | none |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`; overrides the rule above. | saved choice, else `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # only if the server requires auth
```

- A token set in the environment (or in the project `.env` in Custom mode) also applies to a local server the editor starts and to agent configs written with **Configure**. It is not saved to the config file.
- With `oauth`, your ai-game.dev account token is sent **only to a localhost server** (`localhost`, `127.0.0.0/8`, `::1`). For any other host set `GODOT_MCP_TOKEN`.
- A server in `token` mode rejects a connection without a token; the plugin logs what to set.

## The server

The server is the shared [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) (`gamedev-mcp-server`), one binary for Unity-MCP, Godot-MCP and Unreal-MCP, released from its own repository. The addon pins the version it uses (`ServerVersion` in `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

### Let the addon run it

In Custom mode, open the dock's **Server** card and press **Start Server**. The addon:

- downloads `gamedev-mcp-server-<rid>.zip` for the pinned version over HTTPS from the GameDev-MCP-Server GitHub release (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`);
- caches it in `.godot/mcp-server/<rid>/` and downloads again only when the pinned version changes;
- starts it on the port of your **Server URL** (by default a port derived from the project path) and connects to it.

The server listens on loopback only and answers `403` to a request with a non-allowed `Origin` header (DNS-rebinding defense). It uses the authorization chosen in the dock: with `none` any process on your machine can call it, with `token` it requires the bearer token. Bind, authorization and Origin rules: [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

**Stop Server** stops it; closing the editor stops it too. The download URL comes only from the pinned version and your platform. Offline, the addon logs a warning and the server does not start. Under CI (`CI` / `GITHUB_ACTIONS`) the download is skipped.

Tool calls may run up to 5 minutes. `script-execute` stops a snippet itself when its `timeout` runs out (30 s by default).

For development, `GODOT_MCP_SERVER_PATH=<absolute path>` makes the editor start that existing `gamedev-mcp-server` binary instead of the pinned release (no download, no version check, no orphan cleanup). It is read once per plugin assembly load, from the process environment or the project `.env`; a path that names no file is ignored with a warning. On Linux and macOS the file must already be executable.

### Run it yourself

Download a [GameDev-MCP-Server release](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) or use the [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) Docker image:

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # an MCP client launches it directly
```

Then point the plugin at it in Custom mode (`GODOT_MCP_HOST=http://localhost:8080`). Every argument and environment variable is in the [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

## Editor-side security notes

- **The editor token is stored as plaintext.** The plugin saves its connection config, including `token` and `cloudToken`, as plain JSON in `user://godot-mcp-config.json`. Treat that file as a secret: do not commit or share it. To rotate, clear the token in the dock (or delete the file) and reconnect. A token from the process environment, or from `.env` in Custom mode, is not written to this file.
- **The dev-control bridge is off unless `GODOT_MCP_DEV_CONTROL=1`.** It is a development-only HTTP bridge for driving the dock in tests: editor-only, bound to `127.0.0.1`, unauthenticated, and never started without that variable.

Details: [runtime-security.md](runtime-security.md#editor-side-surfaces-accepted-posture).
