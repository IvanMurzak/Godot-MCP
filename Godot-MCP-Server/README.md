# Godot-MCP-Server

A thin [ASP.NET Core](https://learn.microsoft.com/aspnet/core/) host around the
[Model Context Protocol](https://modelcontextprotocol.io/) server core
(`com.IvanMurzak.McpPlugin.Server`). It bridges MCP clients (Claude, Cursor,
Copilot, etc.) and the Godot Editor / Godot games via the
[`addons/godot_mcp`](../addons/godot_mcp) plugin over SignalR.

This is the Godot analog of `Unity-MCP-Server`. The server logic lives entirely
in the `McpPlugin.Server` NuGet package — there is no Godot-specific server code
here. `Program.cs` simply wires Kestrel / SignalR / NLog and delegates to the
`McpPlugin.Server` extension methods.

## Build / run

```bash
# Build
dotnet build com.IvanMurzak.Godot.MCP.Server.csproj

# Run (HTTP transport on port 8080)
dotnet run --project com.IvanMurzak.Godot.MCP.Server.csproj -- --client-transport streamableHttp --port 8080

# Run (STDIO transport — for local MCP clients)
dotnet run --project com.IvanMurzak.Godot.MCP.Server.csproj -- --client-transport stdio
```

## Configuration

CLI args / environment variables are parsed by the shared
`com.IvanMurzak.McpPlugin.Common` `DataArguments`:

| Argument                | Env var                       | Default          | Description                                              |
| ----------------------- | ----------------------------- | ---------------- | ------------------------------------------------------- |
| `--port`                | `MCP_PLUGIN_PORT`             | `8080`           | Client → Server ← Plugin connection port.               |
| `--plugin-timeout`      | `MCP_PLUGIN_CLIENT_TIMEOUT`   | `10000`          | Plugin → Server connection timeout (ms).                |
| `--idle-timeout-seconds`| —                             | `600`            | Evict an idle plugin connection after this many seconds.|
| `--client-transport`    | `MCP_PLUGIN_CLIENT_TRANSPORT` | `stdio`          | `stdio` or `streamableHttp`.                            |
| `--token`               | —                             | —                | Bearer token (when authorization is `required`).        |
| `--authorization`       | —                             | `none`           | `none` or `required`.                                   |

## Cross-platform binaries

`build-all.sh` / `build-all.ps1` produce self-contained single-file binaries for
`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, and
`osx-arm64`, zipped as `godot-mcp-server-<rid>.zip` under `./publish/`.

```bash
./build-all.sh            # all runtimes
./build-all.sh win-x64    # a single runtime
```

```powershell
./build-all.ps1                       # all runtimes
./build-all.ps1 -Platforms win-x64    # a single runtime
```

## License

Licensed under the [Apache License, Version 2.0](./LICENSE).
