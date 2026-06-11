<div align="center" width="100%">

## ![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/ai-developer-header.svg?raw=true)

[![MCP](https://badge.mcpx.dev 'MCP Server')](https://modelcontextprotocol.io/introduction)
[![npm](https://img.shields.io/npm/v/godot-cli?label=godot-cli&logo=npm&labelColor=333A41 'godot-cli on npm')](https://www.npmjs.com/package/godot-cli)
[![Godot](https://img.shields.io/badge/Godot-4.3%E2%80%934.5-478CBF?style=flat&logo=godotengine&logoColor=white&labelColor=333A41 'Godot 4.3–4.5, C#/.NET (mono)')](https://godotengine.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&logo=dotnet&logoColor=white&labelColor=333A41 '.NET 8')](https://dotnet.microsoft.com/)
[![release](https://github.com/IvanMurzak/Godot-MCP/workflows/release/badge.svg 'release')](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/release.yml)</br>
[![Discord](https://img.shields.io/badge/Discord-Join-7289da?logo=discord&logoColor=white&labelColor=333A41 'Join')](https://discord.gg/cfbdMZX99G)
[![Stars](https://img.shields.io/github/stars/IvanMurzak/Godot-MCP 'Stars')](https://github.com/IvanMurzak/Godot-MCP/stargazers)
[![License](https://img.shields.io/github/license/IvanMurzak/Godot-MCP?label=License&labelColor=333A41)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE)
[![Stand With Ukraine](https://raw.githubusercontent.com/vshymanskyy/StandWithUkraine/main/badges/StandWithUkraine.svg)](https://stand-with-ukraine.pp.ua)

  <img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/promo/ai-developer-banner.jpg" alt="AI Game Developer — Godot MCP" title="AI-driven Godot game development" width="100%">

  <p>
    <a href="https://claude.ai/download"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/claude-64.png" alt="Claude" title="Claude" height="36"></a>&nbsp;&nbsp;
    <a href="https://openai.com/index/introducing-codex/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/codex-64.png" alt="Codex" title="Codex" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.cursor.com/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/cursor-64.png" alt="Cursor" title="Cursor" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/docs/copilot/overview"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/github-copilot-64.png" alt="GitHub Copilot" title="GitHub Copilot" height="36"></a>&nbsp;&nbsp;
    <a href="https://gemini.google.com/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/gemini-64.png" alt="Gemini" title="Gemini" height="36"></a>&nbsp;&nbsp;
    <a href="https://antigravity.google/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/antigravity-64.png" alt="Antigravity" title="Antigravity" height="36"></a>&nbsp;&nbsp;
    <a href="https://code.visualstudio.com/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/vs-code-64.png" alt="VS Code" title="VS Code" height="36"></a>&nbsp;&nbsp;
    <a href="https://www.jetbrains.com/rider/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/rider-64.png" alt="Rider" title="Rider" height="36"></a>&nbsp;&nbsp;
    <a href="https://visualstudio.microsoft.com/"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/visual-studio-64.png" alt="Visual Studio" title="Visual Studio" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/anthropics/claude-code"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/open-code-64.png" alt="Open Code" title="Open Code" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/cline/cline"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/cline-64.png" alt="Cline" title="Cline" height="36"></a>&nbsp;&nbsp;
    <a href="https://github.com/Kilo-Org/kilocode"><img src="https://github.com/IvanMurzak/Godot-MCP/raw/main/docs/img/mcp-clients/kilo-code-64.png" alt="Kilo Code" title="Kilo Code" height="36"></a>
  </p>

</div>

`Godot MCP` is an AI-powered game development assistant **for the Godot Editor**. Connect **Claude**, **Cursor**, **Copilot**, or any MCP-aware agent to Godot and let it inspect and drive your project — create nodes, edit scenes, manage resources and scripts, capture screenshots, and more.

Godot-MCP is the Godot counterpart of [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP): a C# **editor addon** that exposes Godot Editor operations as **AI Tools** and connects them to an MCP server through the same hosted cloud backend ([ai-game.dev](https://ai-game.dev)) that powers Unity-MCP — or your own self-hosted server. The MCP / reflection stack is **not forked**: it is shared with Unity-MCP and consumed from [nuget.org](https://www.nuget.org/) as `PackageReference`s.

> **[💬 Join our Discord Server](https://discord.gg/cfbdMZX99G)** — Ask questions, showcase your work, and connect with other developers!

## ![Features](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-features.svg?raw=true)

- ✔️ **AI agents** — Use the best agents from **Anthropic**, **OpenAI**, **Google**, or any other provider with no vendor lock-in
- ✔️ **36 built-in Tools** — A wide range of [MCP Tools](#tools-reference) across 10 families for operating the Godot Editor
- ✔️ **C# & GDScript** — Read, create, and update both `.cs` and `.gd` scripts, and attach them to nodes
- ✔️ **Scene & Node control** — Build and edit the scene tree, open/save `.tscn` scenes, mutate `.tres`/`.res` resources
- ✔️ **Visual feedback** — Capture viewport, camera, and isolated-node screenshots the LLM can inspect
- ✔️ **Reflection escape hatch** — Find and call any C# method across loaded assemblies via [ReflectorNet](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet)
- ✔️ **Cloud or self-hosted** — Connect to `ai-game.dev` out of the box, or point at your own server
- ✔️ **Natural conversation** — Chat with AI like you would with a human

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Quick Start

Get up and running in a few steps using the [`godot-cli`](https://www.npmjs.com/package/godot-cli) (the Godot analog of `unity-mcp-cli`):

> **Prerequisite:** first add the addon files and the two NuGet packages to your project — see
> [Installation](#installation) Steps 1–2. `install-plugin` below only flips the `project.godot`
> enable flag; it does not copy the addon or add the NuGet pins, so the editor cannot load the plugin
> without them.

```bash
# 1. Install godot-cli
npm install -g godot-cli

# 2. Enable the godot_mcp addon in your Godot C# project (addon files + NuGet pins must already be present)
godot-cli install-plugin ./MyGodotProject

# 3. Pick an AI agent (Claude Code, Cursor, Copilot, …) and write its MCP config
godot-cli setup-mcp claude-code ./MyGodotProject

# 4. Open the Godot editor (auto-connects with the right GODOT_MCP_* env vars)
godot-cli open ./MyGodotProject

# 5. Wait until the plugin answers the readiness probe
godot-cli wait-for-ready ./MyGodotProject
```

That's it. Ask your AI *"Create 3 cubes in a circle with radius 2"* and watch it happen. ✨

> See the [full CLI documentation](https://github.com/IvanMurzak/Godot-MCP/blob/main/cli/README.md) for every command, editor-resolution order, and connection env vars.

# Contents

- [Quick Start](#quick-start)
- [Tools Reference](#tools-reference)
- [Requirements](#requirements)
- [Installation](#installation)
  - [Step 1: Add the addon](#step-1-add-the-addon)
    - [Option A — Godot Asset Library (recommended)](#option-a--godot-asset-library-recommended)
    - [Option B — GitHub Release zip](#option-b--github-release-zip)
    - [Option C — copy from source](#option-c--copy-from-source)
  - [Step 2: Add the NuGet packages](#step-2-add-the-nuget-packages)
  - [Step 3: Install an AI agent](#step-3-install-an-ai-agent)
- [Connect](#connect)
  - [Cloud mode (default) — ai-game.dev](#cloud-mode-default--ai-gamedev)
  - [Custom mode — your own server](#custom-mode--your-own-server)
- [Godot `MCP Server` setup](#godot-mcp-server-setup)
  - [Local server — let the addon download & run it for you](#local-server--let-the-addon-download--run-it-for-you)
  - [Build & run the server manually (advanced)](#build--run-the-server-manually-advanced)
- [Customize Tools](#customize-tools)
- [How Godot MCP Architecture Works](#how-godot-mcp-architecture-works)
- [Building & contributing](#building--contributing)
- [License](#license)

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Tools Reference

Godot-MCP ships **36 built-in tools** grouped into **10 families**. Tool names mirror Unity-MCP where
sensible (`scene-*`, `node-*`, …). Every tool returns a structured, [ReflectorNet](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet)-serialized
result (or a PNG image for screenshots). All tools are available immediately after the addon is enabled —
no extra configuration required.

| Family | Tools | What it does |
| --- | --- | --- |
| **ping** | `ping` | Lightweight readiness probe — echoes a message back, or returns `pong`. Verifies the end-to-end MCP path (editor → SignalR → tool dispatch). |
| **node** | `node-find`, `node-create`, `node-modify`, `node-set-parent`, `node-duplicate`, `node-delete` | Inspect and edit the active scene tree (the Godot analog of Unity GameObjects), driving `EditorInterface` on the main thread. |
| **scene** | `scene-open`, `scene-save`, `scene-create`, `scene-list-opened`, `scene-get-data` | Open, save, create, and inspect Godot scenes (`res://*.tscn` PackedScenes) in the editor. |
| **resource** | `resource-find`, `resource-get-data`, `resource-modify`, `resource-create`, `resource-move`, `resource-delete` | Find and mutate Godot resources (`.tres`/`.res`) through `ResourceLoader`/`ResourceSaver`/`EditorFileSystem`, keeping `.import` sidecars consistent. |
| **filesystem** | `filesystem-list`, `filesystem-reimport` | Browse and reimport the project's `res://` tree via the editor `EditorFileSystem` index (file types + uids without loading resources). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node` | CRUD on C# (`.cs`) and GDScript (`.gd`) files, plus attaching a script to a node. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Capture the editor viewport, a specific camera, or an isolated node render, returned as a PNG image the LLM can inspect. |
| **editor** | `editor-application-get-state`, `editor-application-set-state`, `editor-selection-get`, `editor-selection-set` | Read/drive the editor run-and-play lifecycle (Godot launches the game in a separate process) and the current selection. |
| **console** | `console-get-logs`, `console-clear-logs` | Read and clear the plugin's editor log collector (`GD.Print`/`GD.PushWarning`/`GD.PushError`). |
| **reflection** | `reflection-method-find`, `reflection-method-call` | Find and call C# methods (static/instance, public/private) across every loaded assembly via ReflectorNet — the engine-agnostic escape hatch. |

<details>
  <summary>Per-tool descriptions</summary>

**ping**

- `ping` — Lightweight readiness probe; echoes a message back, or returns `pong`.

**node**

- `node-find` — Find nodes in the active scene tree by path, type, or name.
- `node-create` — Create a new node under a parent (optionally instancing a `.tscn` sub-scene).
- `node-modify` — Set fields/properties on one or more nodes.
- `node-set-parent` — Reparent nodes within the scene tree.
- `node-duplicate` — Duplicate nodes together with their subtrees.
- `node-delete` — Delete nodes from the active scene.

**scene**

- `scene-open` — Open a `res://*.tscn` PackedScene in the editor.
- `scene-save` — Save an open scene back to its `.tscn` file.
- `scene-create` — Create a new scene asset in the project.
- `scene-list-opened` — List the scenes currently open in the editor.
- `scene-get-data` — Retrieve the root nodes / structure of a scene.

**resource**

- `resource-find` — Search the project for resources (`.tres`/`.res`).
- `resource-get-data` — Read a resource's serialized fields and properties.
- `resource-modify` — Modify a resource's properties.
- `resource-create` — Create a new resource asset.
- `resource-move` — Move / rename a resource, keeping `.import` sidecars consistent.
- `resource-delete` — Delete a resource from the project.

**filesystem**

- `filesystem-list` — Browse the `res://` tree (file types + uids) via the editor file index.
- `filesystem-reimport` — Reimport files in the project.

**script**

- `script-read` — Read a `.cs` / `.gd` script file.
- `script-create` — Create a new script file.
- `script-update` — Update an existing script file's contents.
- `script-delete` — Delete a script file.
- `script-attach-to-node` — Attach a script to a node.

**screenshot**

- `screenshot-viewport` — Capture the editor viewport as a PNG.
- `screenshot-camera` — Capture from a specific camera.
- `screenshot-isolated` — Render a node in isolation from a chosen angle.

**editor**

- `editor-application-get-state` — Read the editor application/run state.
- `editor-application-set-state` — Start / stop the running game.
- `editor-selection-get` — Get the current editor selection.
- `editor-selection-set` — Set the current editor selection.

**console**

- `console-get-logs` — Read the plugin's collected editor logs (with filtering).
- `console-clear-logs` — Clear the collected log cache.

**reflection**

- `reflection-method-find` — Find C# methods (including private) across every loaded assembly.
- `reflection-method-call` — Call any C# method with input parameters and get the result.

</details>

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Requirements

- **Godot 4.3+** — the C# / .NET (mono) edition. The addon csproj pins `Godot.NET.Sdk/4.3.0` as its
  minimum floor; newer 4.x editors (4.4, 4.5) work.
- **.NET 8 SDK** (`net8.0`).

> [!IMPORTANT]
> Godot-MCP requires the **mono (C#/.NET)** build of Godot — the standard (GDScript-only) build cannot
> compile the addon.

# Installation

There are two things to install: the **addon** (the plugin files) and the two **NuGet packages** the
addon's C# depends on. Godot compiles *every* `.cs` under your project into one assembly, so your
project's `.csproj` must declare the same NuGet references the addon needs — otherwise the addon's C# will
not compile.

## Step 1: Add the addon

Pick **one** of the following ways to get the `addons/godot_mcp/` folder into your Godot C# project.

### Option A — Godot Asset Library (recommended)

The easiest path: install directly from inside the editor.

1. Open the **AssetLib** tab at the top of the Godot editor.
2. Search for **Godot-MCP** and open the asset.
3. Click **Download**, then **Install** — Godot unpacks the addon into your project's
   `res://addons/godot_mcp/`.

> The Asset Library entry is published per release and always points at a tagged version, so an
> in-editor install gives you a known-good snapshot of the addon. (See note below if the entry is not
> visible yet.)

### Option B — GitHub Release zip

Grab the latest `godot-mcp-addon-<version>.zip` from the
[Releases page](https://github.com/IvanMurzak/Godot-MCP/releases/latest) and extract it into your
project's root — the archive already contains `addons/godot_mcp/...`, so the files land at
`res://addons/godot_mcp/`.

### Option C — copy from source

Copy the `addons/godot_mcp/` folder from this repository (or your clone) into your project's `addons/`
directory by hand.

---

After the files are in place, **enable** the plugin:
**Project → Project Settings → Plugins → Godot-MCP → Enable** (or run
[`godot-cli`](https://www.npmjs.com/package/godot-cli) `install-plugin ./MyGodotProject`, which flips the
same enable flag in `project.godot` — it does **not** copy the addon files, so the files from Option A/B/C
must already be present). On a successful load the editor Output panel prints:

```
[Godot-MCP] plugin loaded
```

> **Asset Library availability.** The in-editor AssetLib entry (Option A) appears after the maintainer's
> first submission is approved by the Godot Asset Library moderators. Until then, use Option B (GitHub
> Release zip) or Option C.

## Step 2: Add the NuGet packages

Add both `PackageReference`s to your project's `.csproj` (use these exact pinned versions — they must
match the addon's `Godot-MCP.csproj`):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="5.3.1" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="6.7.0" />
</ItemGroup>
```

| Package | Version | Role |
| --- | --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | `5.3.1` | Reflection / serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | `6.7.0` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`) |

Run `dotnet restore` so the packages land in your NuGet cache, then build. **No manual DLL copying is
required** — at editor runtime the addon's assembly resolver locates the DLLs in your NuGet
global-packages folder by reading the build's `*.deps.json`. (If you prefer self-contained output, set
`<CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>` so the DLLs are copied beside your
project assembly instead.)

## Step 3: Install an AI agent

Choose a single `AI agent` you prefer — you don't need to install all of them. This is your main chat
window to communicate with the LLM.

- [Claude Code](https://github.com/anthropics/claude-code) **(recommended)**
- [Claude Desktop](https://claude.ai/download)
- [GitHub Copilot in VS Code](https://code.visualstudio.com/docs/copilot/overview)
- [Antigravity](https://antigravity.google/)
- [Cursor](https://www.cursor.com/)
- Any other MCP-aware agent

Write the agent's MCP-client config with `godot-cli setup-mcp <agent> ./MyGodotProject` — it points the
client at the Godot server's `<host>/mcp` URL. See the
[CLI documentation](https://github.com/IvanMurzak/Godot-MCP/blob/main/cli/README.md) for the full list of
supported agents.

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Connect

The plugin connects to an MCP server in one of two modes. The mode and its URL / token can be set in the
serialized config or overridden at process start with environment variables (handy for CI, headless runs,
and local dev). All variable names are the Godot analog of Unity-MCP's `UNITY_MCP_*`. The active mode
always recomputes from the environment, so a process-level override wins over the serialized config
without editing any file.

## Cloud mode (default) — ai-game.dev

In **Cloud** mode the plugin connects to the hosted backend at `https://ai-game.dev` (the `/mcp` hub path
is appended automatically). This is the default `connectionMode`.

| Environment variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Force the mode: `Cloud` or `Custom` (case-insensitive). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | Override the cloud base URL. A trailing `/mcp` is stripped if present; a non-http(s) value falls back to the default. | `https://ai-game.dev` |
| `GODOT_MCP_TOKEN` | Bearer token, routed to the active mode's token. Surrounding quotes are trimmed. | (none) |

## Custom mode — your own server

In **Custom** mode the plugin connects to a server URL you supply (a local dev server, a self-hosted
instance, etc.).

| Environment variable | Purpose | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Set to `Custom` to select this mode. | `Cloud` |
| `GODOT_MCP_HOST` | The custom server URL. Must be an absolute http(s) URL or it falls back to the default. | `http://localhost:8080` |
| `GODOT_MCP_TOKEN` | Bearer token (only needed if the server requires authorization). | (none) |

Example — boot the editor pointed at a local server:

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:5300
# export GODOT_MCP_TOKEN=...   # only if the server enforces auth
```

> The [`godot-cli open`](https://github.com/IvanMurzak/Godot-MCP/blob/main/cli/README.md) command forwards
> these env vars for you via `--mode`, `--url`, `--cloud-url`, and `--token` flags.

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Godot `MCP Server` setup

In **Cloud** mode you don't run a server at all — the plugin talks to `ai-game.dev`. If you want to host
the server yourself (local dev, CI, or your own cloud), you have two options: let the addon **download and
run the matched server binary for you** (recommended), or **build and run it manually** (advanced).

## Local server — let the addon download & run it for you

In [Custom mode](#custom-mode--your-own-server) the plugin can **host its own MCP server** — you don't have
to build or launch anything by hand. Open the addon dock's **Server** card while Custom mode is selected and
use the **Local server** row:

- **Start Server** — downloads the server build that **exactly matches the addon's version** (read from
  `addons/godot_mcp/plugin.cfg`), caches it, launches it, and the plugin connects to it. **Stop Server**
  terminates it (it is also stopped automatically when you close the editor).
- The download is the per-platform release asset
  `godot-mcp-server-<rid>.zip` — pulled over **HTTPS from `github.com` only**, from this repo's GitHub
  Release for the addon's version. The release is tagged `v<version>`, so the asset URL is:
  `https://github.com/IvanMurzak/Godot-MCP/releases/download/v<version>/godot-mcp-server-<rid>.zip`.
  The `<rid>` (platform runtime identifier — e.g. `win-x64`, `osx-arm64`, `linux-x64`) is resolved
  automatically for your machine; all seven published RIDs are supported (`win-x64`/`x86`/`arm64`,
  `linux-x64`/`arm64`, `osx-x64`/`arm64`).
- The binary is cached under your project's `.godot/mcp-server/<rid>/` folder (gitignored) and re-used on
  later launches; it is only re-downloaded when the addon version changes (an **exact** plugin-version →
  server-version match, so the editor plugin and the server it talks to never drift). The server is launched
  on the port from your **Server URL** (default `http://localhost:8080`), over the `streamableHttp` transport.

> **Version pinning & security.** The download URL is derived **solely** from the addon's own version and
> your platform RID — there is no arbitrary-URL binary execution. If the matching release asset can't be
> fetched (you're offline, or no release has been published for this version yet), the addon logs a warning
> and the local server simply doesn't start — fall back to the manual build below, or use Cloud mode. The
> download is **skipped entirely under CI** (the `CI` / `GITHUB_ACTIONS` environment), where no local server
> is hosted.

This mirrors [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)'s self-hosted server flow: the editor
plugin manages the version-matched server binary for you instead of requiring a manual build.

## Build & run the server manually (advanced)

For development on the server itself, or to run it as a standalone / cloud process, this repo ships
**`Godot-MCP-Server/`**, a thin ASP.NET Core host around the shared MCP server core
(`com.IvanMurzak.McpPlugin.Server`). Both transports are supported: `streamableHttp` (HTTP) and `stdio`.

```bash
cd Godot-MCP-Server

# HTTP transport on port 8080
dotnet run --project com.IvanMurzak.Godot.MCP.Server.csproj -- --client-transport streamableHttp --port 8080

# stdio transport — for local MCP clients that launch the server directly
dotnet run --project com.IvanMurzak.Godot.MCP.Server.csproj -- --client-transport stdio
```

`build-all.sh` / `build-all.ps1` produce self-contained single-file binaries for win/linux/osx RIDs under
`./publish/`. Then point the plugin at it in [Custom mode](#custom-mode--your-own-server)
(`GODOT_MCP_HOST=http://localhost:8080`).

> **Choosing a transport:** use `stdio` when the MCP client launches the server binary directly (local
> use — the most common setup); use `streamableHttp` when running the server as a standalone process or in
> the cloud and connecting over HTTP.

See [`Godot-MCP-Server/README.md`](https://github.com/IvanMurzak/Godot-MCP/blob/main/Godot-MCP-Server/README.md)
for the full argument / environment-variable table and the cross-platform build matrix.

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Customize Tools

Godot-MCP supports custom `MCP Tool` development directly in your project code. A tool family is a
`partial class` decorated `[AiToolType]`; each tool method is decorated `[AiTool("tool-name", …)]` with a
`[Description]` on the method and on each parameter to help the LLM understand it.

> Any Godot API call (`Node`, `Resource`, `EditorInterface`, …) **must** run on the editor main thread —
> marshal it through `MainThread.Instance.Run(...)` (ReflectorNet's `MainThread` is backed by the Godot
> main-thread dispatcher on plugin boot). Never touch engine objects off-thread.

```csharp
[AiToolType]
public partial class Tool_MyFeature
{
    [AiTool("my-custom-task", Title = "Do a custom task")]
    [Description("Explain to the LLM what this does and when to call it.")]
    public string CustomTask
    (
        [Description("Explain to the LLM what this parameter is.")]
        string inputData
    )
    {
        // ... work that does not touch the Godot API can run on this background thread ...

        return MainThread.Instance.Run(() =>
        {
            // ... touch EditorInterface / Node / Resource here, on the main thread ...
            return "[Success] Operation completed.";
        });
    }
}
```

Return a structured data model (ReflectorNet-serialized) or `void` for side-effect-only ops — never ad-hoc
string formatting for parseable output. Use `string? optional = null` parameters (nullable + default) to
mark them as optional for the LLM.

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# How Godot MCP Architecture Works

Godot-MCP is a bridge between LLMs and the Godot editor. It exposes and explains Godot's tools to the LLM,
which then understands the interface and uses the tools according to your requests.

On editor load, the `[Tool]` `EditorPlugin` (`GodotMcpPlugin`) boots the plugin: it installs a main-thread
dispatcher, builds a [ReflectorNet](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) `Reflector`
with Godot type converters, and opens a SignalR connection to an MCP server over the reused
[`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) client. The AI tools
it registers are then callable by any MCP-aware AI agent.

## What is `MCP`

MCP — Model Context Protocol. In a few words, it is `USB Type-C` for AI, specifically for LLMs (Large
Language Models). It teaches the LLM how to use external features — such as the Godot Engine in this case,
or even your own custom C# method. [Official documentation](https://modelcontextprotocol.io/).

## What is an `AI agent`

It is an application with a chat window. It may have smart agents to operate better, and embedded advanced
MCP Tools. A well-built MCP client is 50% of the AI success in executing a task — which is why it is
important to choose a good one.

## What is the `MCP Server`

It is the bridge between the `MCP Client` and "something else" — in this case the Godot editor. In **Cloud**
mode this is the hosted `ai-game.dev` backend; in **Custom** mode it is the `Godot-MCP-Server/` host you
run yourself.

## What is an `MCP Tool`

An `MCP Tool` is a function the LLM can call to interact with Godot. These tools are the bridge between
natural-language requests and actual Godot operations. When you ask the AI to "create a node" or
"open a scene," it uses MCP Tools to execute the action. Tools have typed, described parameters; return
structured results; and are thread-aware (main-thread for Godot API calls, background-thread for heavy
processing).

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Building & contributing

`Godot.NET.Sdk` is a NuGet SDK, so **no Godot binary is required to compile or unit-test**:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore   # 0 errors required (CI gate)
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

A Godot 4.3+ editor is only needed for live behavioral verification of the engine-driving tools. See
[`CLAUDE.md`](https://github.com/IvanMurzak/Godot-MCP/blob/main/CLAUDE.md) for the full build/test/run
runbook, the editor-runtime assembly-load fix, conventions, and the headless testbed smoke.

Contributions are highly appreciated. **Please give this project a star 🌟 if you find it useful!**

1. 👉 [Fork the project](https://github.com/IvanMurzak/Godot-MCP/fork)
2. Clone the fork and open it in a Godot 4.3+ (mono) editor
3. Implement new things, commit, and push to GitHub
4. Create a Pull Request targeting the original [Godot-MCP](https://github.com/IvanMurzak/Godot-MCP/compare) repository, `main` branch.

# License

[Apache-2.0](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE) © Ivan Murzak
