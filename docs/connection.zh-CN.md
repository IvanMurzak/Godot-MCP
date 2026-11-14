[English](connection.md) · **中文** · [日本語](connection.ja.md) · [Español](connection.es.md) · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

# 连接与服务器

编辑器插件以两种模式之一连接到 MCP 服务器。在编辑器停靠面板中设置模式，或在进程启动时用 `GODOT_MCP_*` 环境变量覆盖（适用于 CI、无界面运行和本地开发）。环境变量始终优先于已保存的配置。`godot-cli open` 会通过 `--mode`、`--url`、`--cloud-url` 和 `--token` 替你传入这些变量。

<a id="cloud-mode-default-ai-gamedev"></a>
## Cloud 模式（默认）：ai-game.dev

插件连接到 `https://ai-game.dev`；你无需运行任何服务器。Cloud 是默认模式，因为它无需开放端口，并能避免与其他程序发生端口冲突；而且它是免费的。中继不存储请求或响应内容，只保留元数据，保存 90 天。

用 `godot-cli login` 登录一次即可。它使用 OAuth 2.1 设备授权流程（RFC 8628）：打印一个短代码，打开浏览器，你批准后将凭据保存到本机存储（`~/.ai-game-dev/credentials.json`）。编辑器插件会自动读取它，因此无需复制任何令牌。

| 变量 | 用途 | 默认值 |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` 或 `Custom`（不区分大小写）。 | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | 云端基础 URL。末尾的 `/mcp` 会被去掉。 | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | 凭据存储目录的绝对路径，用于隔离测试。也会从项目的 `.env` 中读取。 | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | 当前模式使用的 Bearer 令牌；供 CI 和无界面运行手动覆盖。 | 无 |

<a id="custom-mode-your-own-server"></a>
## Custom 模式：你自己的服务器

插件连接到你选择的服务器 URL：本地服务器或自托管服务器。适用于离线或物理隔离（air-gapped）环境。

| 变量 | 用途 | 默认值 |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | 设为 `Custom`。 | `Cloud` |
| `GODOT_MCP_HOST` | 服务器 URL，绝对的 http(s) URL。 | `http://localhost:<port>`，端口由项目路径推导得出 |
| `GODOT_MCP_TOKEN` | 发送给该服务器的 Bearer 令牌。设置后即选择 `token` 授权。 | 无 |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`；覆盖上一行的规则。 | 已保存的选择，否则为 `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # 仅当服务器要求认证时
```

`godot-cli open` 会替你设置这些变量。对于要求使用你自选令牌的本地服务器，请用该令牌打开编辑器，然后在停靠面板中点击 **Start Server**（`--auth Required` 选择 `token` 授权）：

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

- 在环境变量中（或 Custom 模式下在项目 `.env` 中）设置的令牌，也会用于编辑器启动的本地服务器以及通过 **Configure** 写入的智能体配置。它不会保存到配置文件中。
- 使用 `oauth` 时，你的 ai-game.dev 账户令牌**只会发送给 localhost 服务器**（`localhost`、`127.0.0.0/8`、`::1`）。其他主机请设置 `GODOT_MCP_TOKEN`。
- 处于 `token` 模式的服务器会拒绝不带令牌的连接；插件会在日志中说明需要设置什么。

<a id="the-server"></a>
## 服务器

服务器是共享的 [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)（`gamedev-mcp-server`），Unity-MCP、Godot-MCP 和 Unreal-MCP 共用同一个可执行文件，在其自己的仓库中发布。插件固定了它所使用的版本（`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs` 中的 `ServerVersion`）。

<a id="let-the-addon-run-it"></a>
### 由插件运行

在 Custom 模式下，打开停靠面板的 **Server** 卡片并点击 **Start Server**。插件会：

- 通过 HTTPS 从 GameDev-MCP-Server 的 GitHub 发布版下载固定版本的 `gamedev-mcp-server-<rid>.zip`（`win-x64`、`win-x86`、`win-arm64`、`linux-x64`、`linux-arm64`、`osx-x64`、`osx-arm64`）；
- 将其缓存在 `.godot/mcp-server/<rid>/` 中，仅在固定版本变化时重新下载；
- 在你的 **Server URL** 所指端口（默认是由项目路径推导出的端口）上启动它并连接。

服务器只监听回环地址，并对带有不允许的 `Origin` 头的请求返回 `403`（防御 DNS 重绑定）。它使用停靠面板中选择的授权方式：`none` 时你机器上的任何进程都可以调用它，`token` 时需要 Bearer 令牌。绑定、授权和 Origin 规则：[GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)。

**Stop Server** 会停止服务器；关闭编辑器也会停止它。下载 URL 仅由固定版本和你的平台决定。离线时，插件记录一条警告，服务器不会启动。在 CI 下（`CI` / `GITHUB_ACTIONS`）会跳过下载。

工具调用最长可运行 5 分钟。`script-execute` 会在其 `timeout` 到期时自行停止代码片段（默认 30 秒）。

开发时，`GODOT_MCP_SERVER_PATH=<absolute path>` 让编辑器启动该现有的 `gamedev-mcp-server` 可执行文件，而不是固定的发布版（不下载、不检查版本、不清理孤立进程）。它在每次插件程序集加载时从进程环境或项目 `.env` 中读取一次；指向不存在文件的路径会被忽略并给出警告。在 Linux 和 macOS 上，该文件必须已具有可执行权限。

<a id="run-it-yourself"></a>
### 自行运行

下载 [GameDev-MCP-Server 发布版](https://github.com/IvanMurzak/GameDev-MCP-Server/releases)，或使用 [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) Docker 镜像：

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # 由 MCP 客户端直接启动
```

然后在 Custom 模式下让插件指向它（`GODOT_MCP_HOST=http://localhost:8080`）。所有参数和环境变量见 [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)。

<a id="editor-side-security-notes"></a>
## 编辑器端安全说明

- **编辑器令牌以明文存储。** 插件将其连接配置（包括 `token` 和 `cloudToken`）以普通 JSON 保存在 `user://godot-mcp-config.json` 中。请把该文件当作机密：不要提交或分享。如需轮换，在停靠面板中清除令牌（或删除该文件）后重新连接。来自进程环境的令牌，或 Custom 模式下来自 `.env` 的令牌，不会写入此文件。
- **dev-control 桥接默认关闭，除非 `GODOT_MCP_DEV_CONTROL=1`。** 它是仅用于开发的 HTTP 桥接，用于在测试中操控停靠面板：仅限编辑器、绑定到 `127.0.0.1`、无认证，且未设置该变量时永不启动。

详情：[runtime-security.md](runtime-security.zh-CN.md#editor-side-surfaces-accepted-posture)。
