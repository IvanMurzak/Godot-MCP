<div align="center" width="100%">

  <h1>✨ AI Game Developer — <i>Godot MCP</i></h1>

[![MCP](https://badge.mcpx.dev 'MCP Server')](https://modelcontextprotocol.io/introduction)
[![npm](https://img.shields.io/npm/v/godot-cli?label=godot-cli&logo=npm&labelColor=333A41 'godot-cli on npm')](https://www.npmjs.com/package/godot-cli)
[![Godot](https://img.shields.io/badge/Godot-4.3%2B-478CBF?style=flat&logo=godotengine&logoColor=white&labelColor=333A41 'Godot 4.3+, C#/.NET (mono)')](https://godotengine.org/)
[![Godot Editor](https://img.shields.io/badge/Editor-X?style=flat&logo=godotengine&logoColor=white&labelColor=333A41&color=2A2A2A 'Godot Editor supported')](https://godotengine.org/)
[![Godot Runtime](https://img.shields.io/badge/Runtime-X?style=flat&logo=godotengine&logoColor=white&labelColor=333A41&color=2A2A2A 'Godot Runtime supported')](https://godotengine.org/)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat&labelColor=333A41 '.NET 8')](https://dotnet.microsoft.com/)
[![release](https://github.com/IvanMurzak/Godot-MCP/workflows/release/badge.svg 'release')](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/release.yml)</br>
[![Discord](https://img.shields.io/badge/Discord-Join-7289da?logo=discord&logoColor=white&labelColor=333A41 'Join')](https://discord.gg/cfbdMZX99G)
[![Stars](https://img.shields.io/github/stars/IvanMurzak/Godot-MCP 'Stars')](https://github.com/IvanMurzak/Godot-MCP/stargazers)
[![Docker Image](https://img.shields.io/badge/Docker-Image-2496ED?style=flat&logo=docker&logoColor=white&labelColor=333A41 'Docker Image')](https://hub.docker.com/r/aigamedeveloper/mcp-server)
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
- ✔️ **39 built-in Tools** — A wide range of [MCP Tools](#tools-reference) across 11 families for operating the Godot Editor
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
- [Runtime usage (in-game)](#runtime-usage-in-game)
  - [Capturing in-game runtime errors](#capturing-in-game-runtime-errors)
  - [Sample: a live game-state tool](#sample-a-live-game-state-tool)
  - [Where the server URL and token come from](#where-the-server-url-and-token-come-from)
  - [Security: opt-in only, default OFF](#security-opt-in-only-default-off)
- [How Godot MCP Architecture Works](#how-godot-mcp-architecture-works)
- [Building & contributing](#building--contributing)
- [License](#license)

![AI Game Developer — Godot MCP](https://github.com/IvanMurzak/Godot-MCP/blob/main/docs/img/promo/hazzard-divider.svg?raw=true)

# Tools Reference

Godot-MCP ships **39 built-in tools** grouped into **11 families**. Tool names mirror Unity-MCP where
sensible (`scene-*`, `node-*`, …). Every tool returns a structured, [ReflectorNet](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet)-serialized
result (or a PNG image for screenshots). All editor tools are available immediately after the addon is
enabled — no extra configuration required. The **runtime-errors** family is the exception: it surfaces
errors from the *running game* and is **OFF by default** — opt in with `builder.WithRuntimeErrorCapture()`
(see [Capturing in-game runtime errors](#capturing-in-game-runtime-errors)).

| Family | Tools | What it does |
| --- | --- | --- |
| **ping** | `ping` | Lightweight readiness probe — echoes a message back, or returns `pong`. Verifies the end-to-end MCP path (editor → SignalR → tool dispatch). |
| **node** | `node-find`, `node-create`, `node-modify`, `node-set-parent`, `node-duplicate`, `node-delete` | Inspect and edit the active scene tree (the Godot analog of Unity GameObjects), driving `EditorInterface` on the main thread. |
| **scene** | `scene-open`, `scene-save`, `scene-create`, `scene-list-opened`, `scene-get-data` | Open, save, create, and inspect Godot scenes (`res://*.tscn` PackedScenes) in the editor. |
| **resource** | `resource-find`, `resource-get-data`, `resource-modify`, `resource-create`, `resource-move`, `resource-delete` | Find and mutate Godot resources (`.tres`/`.res`) through `ResourceLoader`/`ResourceSaver`/`EditorFileSystem`, keeping `.import` sidecars consistent. |
| **filesystem** | `filesystem-list`, `filesystem-reimport` | Browse and reimport the project's `res://` tree via the editor `EditorFileSystem` index (file types + uids without loading resources). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate` | CRUD on C# (`.cs`) and GDScript (`.gd`) files, plus attaching a script to a node and validating GDScript. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Capture the editor viewport, a specific camera, or an isolated node render, returned as a PNG image the LLM can inspect. |
| **editor** | `editor-application-get-state`, `editor-application-set-state`, `editor-selection-get`, `editor-selection-set` | Read/drive the editor run-and-play lifecycle (Godot launches the game in a separate process) and the current selection. |
| **console** | `console-get-logs`, `console-clear-logs` | Read and clear the plugin's editor log collector (`GD.Print`/`GD.PushWarning`/`GD.PushError`). |
| **reflection** | `reflection-method-find`, `reflection-method-call` | Find and call C# methods (static/instance, public/private) across every loaded assembly via ReflectorNet — the engine-agnostic escape hatch. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | Poll errors raised inside the **running game** (NOT the editor) — GDScript runtime errors, `push_error`/`push_warning`, shader errors, and C# unhandled / unobserved-`Task` exceptions, with multi-frame GDScript backtraces on Godot 4.5+. **OFF by default** — opt in with `builder.WithRuntimeErrorCapture()`. |

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
- `script-validate` — Validate GDScript (.gd) files and return structured parse/compile diagnostics.

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

**runtime-errors** (in-game; **OFF by default** — enable with `builder.WithRuntimeErrorCapture()`)

- `runtime-errors-get` — Read captured in-game runtime errors (oldest-first, newest-kept page); poll only new errors via `sinceSequence`. Returns `available:false` when capture was never enabled, so an empty list is never mistaken for health.
- `runtime-errors-clear` — Clear the captured in-game runtime-error buffer (a no-op when capture is not enabled); the monotonic sequence counter is preserved.

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
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="6.10.0" />
</ItemGroup>
```

| Package | Version | Role |
| --- | --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | `5.3.1` | Reflection / serialization core |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | `6.10.0` | MCP plugin client (transitively pulls `McpPlugin.Common` + `ReflectorNet`; carries the shared `AgentConfig` module) |

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
run the matched server binary for you** (recommended), or **run it manually** (advanced).

The server itself is the shared, engine-agnostic
**[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server)** — one server binary
(`gamedev-mcp-server`) serving Unity-MCP, Godot-MCP, and Unreal-MCP. It is released from its own repo on
its own version line; this addon **pins** the server version it consumes (the `ServerVersion` constant in
`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

## Local server — let the addon download & run it for you

In [Custom mode](#custom-mode--your-own-server) the plugin can **host its own MCP server** — you don't have
to build or launch anything by hand. Open the addon dock's **Server** card while Custom mode is selected and
use the **Local server** row:

- **Start Server** — downloads the server build for the **pinned server version**, caches it, launches it,
  and the plugin connects to it. **Stop Server** terminates it (it is also stopped automatically when you
  close the editor).
- The download is the per-platform release asset
  `gamedev-mcp-server-<rid>.zip` — pulled over **HTTPS from `github.com` only**, from the
  [GameDev-MCP-Server release](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) tagged
  `v<ServerVersion>`, so the asset URL is:
  `https://github.com/IvanMurzak/GameDev-MCP-Server/releases/download/v<ServerVersion>/gamedev-mcp-server-<rid>.zip`.
  The `<rid>` (platform runtime identifier — e.g. `win-x64`, `osx-arm64`, `linux-x64`) is resolved
  automatically for your machine; all seven published RIDs are supported (`win-x64`/`x86`/`arm64`,
  `linux-x64`/`arm64`, `osx-x64`/`arm64`).
- The binary is cached under your project's `.godot/mcp-server/<rid>/` folder (gitignored) and re-used on
  later launches; it is only re-downloaded when the pinned server version changes (an **exact**
  version match, so the editor plugin and the server it talks to never drift). The server is launched
  on the port from your **Server URL** (default `http://localhost:8080`), over the `streamableHttp` transport.

> **Version pinning & security.** The download URL is derived **solely** from the addon's pinned
> `ServerVersion` constant and your platform RID — there is no arbitrary-URL binary execution. The addon
> version and the server version are **decoupled**: bumping the consumed server is an explicit addon change
> (a new `ServerVersion`), and the pinned `v<ServerVersion>` release must already exist on
> GameDev-MCP-Server **before** an addon release that pins it. If the release asset can't be fetched
> (you're offline), the addon logs a warning and the local server simply doesn't start — fall back to the
> manual run below, or use Cloud mode. The download is **skipped entirely under CI** (the `CI` /
> `GITHUB_ACTIONS` environment), where no local server is hosted.

This mirrors [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP)'s self-hosted server flow: the editor
plugin manages the pinned server binary for you instead of requiring a manual build.

## Run the server manually (advanced)

To run the server as a standalone / cloud process, download a
[GameDev-MCP-Server release](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) binary (or use the
[`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) Docker image). Both
transports are supported: `streamableHttp` (HTTP) and `stdio`.

```bash
# HTTP transport on port 8080
./gamedev-mcp-server --client-transport streamableHttp --port 8080

# stdio transport — for local MCP clients that launch the server directly
./gamedev-mcp-server --client-transport stdio
```

Then point the plugin at it in [Custom mode](#custom-mode--your-own-server)
(`GODOT_MCP_HOST=http://localhost:8080`).

> **Choosing a transport:** use `stdio` when the MCP client launches the server binary directly (local
> use — the most common setup); use `streamableHttp` when running the server as a standalone process or in
> the cloud and connecting over HTTP.

See the [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)
for the full argument / environment-variable table, the Docker image, and the cross-platform build matrix.

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

# Runtime usage (in-game)

Everything above runs the MCP connection **inside the Godot editor** (the `[Tool]` `EditorPlugin` boots
it for you). Godot-MCP can *also* run inside a **running / exported game build** (debug **or** release) —
the Godot analog of Unity-MCP's runtime mode. This lets an LLM read and drive your **live game state**:
imagine a Chess game whose bot logic you outsource to an LLM by exposing a couple of tools.

Two things make runtime mode different from editor mode, and both are deliberate:

- **It never auto-connects.** The editor plugin connects on boot; a game build does **not**. *You* write
  the opt-in code and decide when (if ever) to call `Connect()`.
- **There are no tools, prompts, or resources by default — strictly manual.** The runtime ships **zero**
  MCP tools, prompts, **and** resources. You register every `[AiToolType]` tool, `[AiPromptType]` prompt,
  and `[AiResourceType]` resource yourself, in your own code — and each kind is **independently optional**
  (register prompts without any tools, or vice versa). (The addon's editor tool families are gated by
  `#if TOOLS` and don't even compile into a game build, so they can never leak in.)

The entry point is `GodotMcpRuntime.Initialize(...)` (namespace `com.IvanMurzak.Godot.MCP.Runtime`).
Write it once — e.g. from a Godot **autoload**'s `_Ready()` so a `SceneTree` exists:

```csharp
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;   // GodotMcpConnectionMode, GodotMcpAuthOption
using com.IvanMurzak.Godot.MCP.Runtime;      // GodotMcpRuntime
using Godot;

public partial class GameMcp : Node
{
    private GodotMcpRuntimeHandle? _mcp;

    public override async void _Ready()
    {
        // 1) Build the connection (default OFF — nothing connects yet).
        _mcp = GodotMcpRuntime.Initialize(builder =>
        {
            builder.WithConfig(config =>
            {
                config.ConnectionMode = GodotMcpConnectionMode.Custom;   // your own server
                config.Host           = "http://localhost:8080";         // prefer loopback
                config.AuthOption     = GodotMcpAuthOption.Required;      // require a bearer token
                config.Token          = "your-secret-token";
            });

            // 2) Opt YOUR tools / prompts / resources in. Zero of each by default — this is the only way
            //    they get registered, and each kind is independently optional.
            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] classes
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] classes
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] classes
            //   …or register specific families:
            //   builder.WithTools(typeof(GameMcpTools));
            //   builder.WithPrompts(typeof(GameMcpPrompts));
            //   builder.WithResources(typeof(GameMcpResources));
        }).Build();

        // 3) Connect — explicit, the security-required opt-in. Retries in the background while
        //    KeepConnected is true (the default).
        await _mcp.Connect();
    }

    public override async void _ExitTree()
    {
        // 4) Disconnect on shutdown (or whenever you want to stop exposing tools).
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

Builder surface (all fluent / chainable):

| Call | What it does |
| ---- | ------------ |
| `GodotMcpRuntime.Initialize(configure)` | Begin configuring; returns a `GodotMcpRuntimeBuilder`. `configure` may be `null` for the zero-tool, env-configured default. |
| `builder.WithConfig(Action<GodotMcpConfig>)` | Set `Host` / `Token` / `ConnectionMode` / `AuthOption` in code. Multiple calls compose in order. |
| `builder.WithToolsFromAssembly(Assembly)` | Register every `[AiToolType]` class in an assembly (usually `Assembly.GetExecutingAssembly()`). |
| `builder.WithTools(params Type[])` | Register specific `[AiToolType]` classes when a whole-assembly scan is too broad. |
| `builder.WithPromptsFromAssembly(Assembly)` | Register every `[AiPromptType]` class in an assembly. Independent of tools/resources. |
| `builder.WithPrompts(params Type[])` | Register specific `[AiPromptType]` classes. |
| `builder.WithResourcesFromAssembly(Assembly)` | Register every `[AiResourceType]` class in an assembly. Independent of tools/prompts. |
| `builder.WithResources(params Type[])` | Register specific `[AiResourceType]` classes. |
| `builder.WithoutMainThreadDispatcher()` | Skip the automatic main-thread-dispatcher bootstrap (only if you install your own autoload dispatcher). |
| `builder.WithRuntimeErrorCapture()` | Capture errors raised in the **running game** (GDScript runtime errors, `push_error`/`push_warning`, shader errors via the Godot 4.5+ engine hook; C# unhandled / unobserved-`Task` exceptions with full stack traces) and register the `runtime-errors-*` tool so an agent can poll them. **OFF by default.** See ["Capturing in-game runtime errors"](#capturing-in-game-runtime-errors) below. |
| `builder.Build()` | Finalize; returns a **default-OFF** `GodotMcpRuntimeHandle`. |
| `handle.Connect()` / `handle.Disconnect()` | Open / close the connection. `handle.Dispose()` tears it down on shutdown. |

> `Initialize().Build()` also guarantees a main-thread dispatcher `Node` in the running `SceneTree` (so
> tool handlers can marshal Godot API calls onto the engine main thread), unless you opt out with
> `WithoutMainThreadDispatcher()`. Call it once a `SceneTree` is live (e.g. from an autoload `_Ready`).

## Capturing in-game runtime errors

In editor mode, `console-get-logs` and `script-validate` surface the plugin's own logs and GDScript
*parse* errors. But errors raised inside a **running game** — a GDScript *runtime* error (a null
dereference, a bad index), a `push_error`/`push_warning`, a shader error, or a C# unhandled exception —
are not visible to an agent through those editor tools. Without this, an agent can launch the game, poll
for logs, see silence, and wrongly conclude the game is healthy. This is the gap that blocks an unattended
"keep fixing until no errors" loop for real gameplay/runtime bugs.

Opt in with **`WithRuntimeErrorCapture()`**:

```csharp
_mcp = GodotMcpRuntime.Initialize(builder =>
{
    builder.WithConfig(cfg => { /* host / token … */ });
    builder.WithRuntimeErrorCapture();   // capture in-game runtime errors + expose the runtime-errors-* tool
}).Build();

await _mcp.Connect();
```

That single call installs three best-effort capture channels and registers the `runtime-errors-*` tool:

1. **Engine error stream (Godot 4.5+)** — registers a `Godot.Logger` via `OS.AddLogger`, so GDScript
   runtime errors, `push_error`/`push_warning`, and shader errors raised in the running game are captured
   with their **origin** (`file` / `line` / `function`).
2. **C# unhandled exceptions** — `AppDomain.CurrentDomain.UnhandledException`, with the **full managed
   stack trace**.
3. **C# unobserved `Task` exceptions** — `TaskScheduler.UnobservedTaskException`, with the **full managed
   stack trace**. (It only observes for logging — it does **not** call `SetObserved()`, so your game's own
   escalation behavior is unchanged.)

An MCP client polls the captured errors with the `runtime-errors-get` tool (and clears the buffer with
`runtime-errors-clear`):

| Tool | What it returns / does |
| ---- | ---------------------- |
| `runtime-errors-get` | A bounded, newest-kept list of `{ sequence, message, type, source, file, line, function, stackTrace, frames, timestamp }`. Pass the previous result's `highestSequence` as `sinceSequence` to poll only **new** errors — the "did anything break since I last looked?" loop. Returns `available:false` when capture was never enabled (so an empty list is never mistaken for health). |
| `runtime-errors-clear` | Clears the captured buffer (the monotonic `sequence` counter is preserved, so a pre-clear `sinceSequence` poll still behaves). |

> **Stack-trace fidelity (read this).** The two error *sources* differ in depth:
> - **Engine errors** (`source: Engine`) carry the error's **origin** — `file:line` and the originating
>   `function` — plus the message and a `type` (`Error` / `Warning` / `Script` / `Shader`). On **Godot
>   4.5+**, a GDScript runtime error **also** carries the **deep multi-frame call stack**: `frames` is the
>   ordered (innermost-first) backtrace — each `{ function, file, line }` — and `stackTrace` is the engine's
>   formatted rendering of it. On **Godot < 4.5** (or a release build without call-stack tracking) `frames`
>   is `null` and `stackTrace` is `null` (origin only). The frames are materialized off the engine's
>   non-thread-safe `ScriptBacktrace` **inside the logger callback on the originating thread** — only plain
>   managed values ever cross into the collector, never a live engine object.
> - **C# faults** (`source: UnhandledException` / `UnobservedTaskException`) carry the **full managed stack
>   trace** (inner exceptions inlined) in `stackTrace`, plus the CLR exception type name in `type`. (`frames`
>   is `null` — the managed stack lives in the `stackTrace` string.)

> **Graceful degradation.** On **Godot < 4.5** there is no `OS.AddLogger` managed hook, so the engine
> channel is silently unavailable — the C# exception channels still work, and `runtime-errors-get` still
> functions (it just won't see GDScript runtime errors). And like the rest of runtime mode, capture is
> **strictly opt-in** — without `WithRuntimeErrorCapture()` nothing is hooked and there is no behavior
> change. Disposing the handle (`handle.Dispose()`) uninstalls the hooks.

> ⚠️ **Security — information disclosure.** Captured errors forward the **full** message and (for C#
> faults) the **full managed stack trace** to the connected agent through `runtime-errors-get`. Those
> strings can embed sensitive runtime data — absolute filesystem paths, machine/user names, query
> strings, or a secret/token that happened to appear in an exception message or argument. That is the
> intended diagnostic value, but it widens what is exposed over the connection. Enable
> `WithRuntimeErrorCapture()` **only on a trusted connection**: a loopback host (`http://localhost:…` /
> `127.0.0.1`) with `AuthOption = GodotMcpAuthOption.Required` and a real token — never an
> unauthenticated public interface in a release build. See [Security](#security-opt-in-only-default-off)
> and [`docs/runtime-security.md`](docs/runtime-security.md).

## Sample: a live game-state tool

A runtime tool is written exactly like an editor tool — a `partial class` decorated `[AiToolType]`, each
method decorated `[AiTool("tool-name", …)]` with a `[Description]` on the method and each parameter. Any
Godot API call (`Node`, `SceneTree`, …) **must** run on the engine main thread — marshal it through
`MainThread.Instance.Run(...)` (ReflectorNet's `MainThread`, backed by the dispatcher `Initialize()`
bootstrapped for you).

This Godot analog of Unity-MCP's "Chess bot" sample exposes the **live running `SceneTree`** to the LLM —
a pure-managed `game-ping` round-trip plus a `game-scene-tree-summary` that reads real `Node` state:

```csharp
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;              // [AiToolType], [AiTool]
using com.IvanMurzak.ReflectorNet.Utils;     // MainThread
using Godot;

[AiToolType]
public partial class GameMcpTools
{
    [AiTool("game-ping", Title = "Game Ping", ReadOnlyHint = true, IdempotentHint = true)]
    [Description("Runtime readiness probe. Echoes 'message' back, or returns 'pong-from-game' when omitted.")]
    public string GamePing(
        [Description("Optional message to echo back. When null/empty, returns 'pong-from-game'.")]
        string? message = null)
    {
        return string.IsNullOrEmpty(message) ? "pong-from-game" : message;
    }

    [AiTool("game-scene-tree-summary", Title = "Game Scene-Tree Summary", ReadOnlyHint = true)]
    [Description("Summary of the LIVE running game's SceneTree (current scene + root child node names).")]
    public SceneTreeSummary GameSceneTreeSummary()
    {
        // Touch the live SceneTree on the engine main thread — MainThread.Instance was installed by
        // GodotMcpRuntime.Initialize(...). Touching Node APIs off the main thread would fault.
        return MainThread.Instance.Run(() =>
        {
            var summary = new SceneTreeSummary();
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            {
                summary.CurrentSceneName = "<no-scene-tree>";
                return summary;
            }

            summary.CurrentSceneName = tree.CurrentScene?.Name ?? "<none>";
            summary.RootChildCount   = tree.Root.GetChildCount();
            foreach (var child in tree.Root.GetChildren())
                summary.RootChildNames.Add(child.Name);
            return summary;
        });
    }
}

// Structured result (ReflectorNet-serialized — never ad-hoc string formatting for parseable output).
public sealed class SceneTreeSummary
{
    public string CurrentSceneName { get; set; } = string.Empty;
    public int RootChildCount { get; set; }
    public System.Collections.Generic.List<string> RootChildNames { get; set; } = new();
}
```

Register it from the `Initialize(...)` block above (`WithToolsFromAssembly(Assembly.GetExecutingAssembly())`
picks it up automatically), connect, and the LLM can now call `game-ping` / `game-scene-tree-summary`
against your live game. Real example outsourcing bot logic: a `chess-do-turn` tool that calls into your
game controller on the main thread, plus a `chess-get-board` tool returning a structured board model.

> Same `[AiToolType]`/`[AiTool]`/`MainThread.Instance.Run(...)` contract as the editor [Customize
> Tools](#customize-tools) section — the only difference is that in a game build *you* register the tools
> and *you* call `Connect()`.

## Sample: a prompt and a resource

Tools are not the only thing you can expose — MCP also has **prompts** (reusable instruction templates an
LLM can request by name) and **resources** (addressable, read-only content the LLM can fetch by URI). They
register exactly like tools: a `partial class` decorated `[AiPromptType]` / `[AiResourceType]`, with each
member decorated `[AiPrompt(...)]` / `[AiResource(...)]` and a `[Description]`. Register your prompt and
resource classes the same way you register tools — `WithPromptsFromAssembly(...)` /
`WithResourcesFromAssembly(...)` (or the by-type `WithPrompts(...)` / `WithResources(...)`) from the
`Initialize(...)` block above. **Each kind is independently optional** — a game can expose prompts and/or
resources with no tools at all.

```csharp
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;               // [AiPromptType], [AiPrompt], [AiResourceType], [AiResource]
using com.IvanMurzak.McpPlugin.Common.Model;  // Role, ResponseResourceContent
using com.IvanMurzak.ReflectorNet.Utils;      // MainThread
using Godot;

// A PROMPT — a named, reusable instruction the LLM can request. Returns the prompt text; Role marks who
// the message is from. Set Enabled = false to ship a prompt registered-but-off until you flip it on.
[AiPromptType]
public partial class GameMcpPrompts
{
    [AiPrompt(Name = "explain-game-state", Role = Role.User)]
    [Description("Ask the assistant to summarize the current game state for the player.")]
    public string ExplainGameState()
    {
        return "Read the live SceneTree via the game tools and explain the current game state in one paragraph.";
    }
}

// A RESOURCE — addressable read-only content fetched by URI. Route is the URI template; the method
// returns ResponseResourceContent[]. Any Godot API access marshals onto the main thread, exactly like a tool.
[AiResourceType]
public partial class GameMcpResources
{
    [AiResource(
        Name = "Live SceneTree node names",
        Route = "game://scene-tree/nodes",
        MimeType = "application/json",
        Description = "The root child node names of the live running game's SceneTree.")]
    public ResponseResourceContent[] SceneTreeNodes(string uri)
    {
        return MainThread.Instance.Run(() =>
        {
            var names = new System.Collections.Generic.List<string>();
            if (Engine.GetMainLoop() is SceneTree tree && tree.Root != null)
            {
                foreach (var child in tree.Root.GetChildren())
                    names.Add(child.Name);
            }

            var json = System.Text.Json.JsonSerializer.Serialize(names);
            return new[] { ResponseResourceContent.CreateText(uri, json, "application/json") };
        });
    }
}
```

> Same independently-optional, manual-registration contract as tools. The `[AiPrompt]`/`[AiResource]`
> attribute names, `Role` enum, and `ResponseResourceContent` helper all come from the reused
> `com.IvanMurzak.McpPlugin` package the editor path already uses — nothing Godot-specific to learn.

## Where the server URL and token come from

A game build never auto-loads the editor's saved config (that's an editor-only convenience). You supply
host/token one of two ways — and they **compose**, with env winning over code at resolution time:

1. **In code** — `builder.WithConfig(c => { c.Host = …; c.Token = …; })`, as above.
2. **Out-of-band** — `GODOT_MCP_*` process environment variables or a project `.env` file (read **live**
   by `GodotMcpConfig`, so a build can be reconfigured without recompiling):

   | Environment variable | Values | Description |
   | -------------------- | ------ | ----------- |
   | `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Connection mode (a loopback host implies `Custom`). |
   | `GODOT_MCP_CLOUD_URL` | URL | Override the Cloud base URL (default `https://ai-game.dev`). |
   | `GODOT_MCP_HOST` | URL | Custom-mode server host (default `http://localhost:8080`). |
   | `GODOT_MCP_AUTH_OPTION` | `None` / `Required` | Whether Custom mode sends a bearer token. |
   | `GODOT_MCP_TOKEN` | string | The bearer token (routed to Cloud or Custom by the active mode). |
   | `GODOT_MCP_LOG_LEVEL` | `Trace`…`None` | Log-verbosity threshold. |

   ```bash
   export GODOT_MCP_CONNECTION_MODE=Custom
   export GODOT_MCP_HOST=http://localhost:8080
   export GODOT_MCP_AUTH_OPTION=Required
   export GODOT_MCP_TOKEN=your-secret-token
   ```

- **Cloud mode** (`GodotMcpConnectionMode.Cloud`) connects to `https://ai-game.dev` (override with
  `GODOT_MCP_CLOUD_URL`).
- **Custom mode** (`GodotMcpConnectionMode.Custom`) connects to your own server — a local dev server,
  self-hosted, or a loopback address. **This is the recommended mode for a shipped game** (see below).

## Security: opt-in only, default OFF

Exposing an MCP server inside a shipped game opens a **remote-control surface**: anything your registered
tools can do, a connected MCP client can drive. Godot-MCP's runtime is built so this can only happen
**deliberately**:

- **Opt-in only / default OFF.** Building a handle does **not** connect. Nothing happens until *your* code
  calls `Connect()`. There is no auto-connect path in a game build.
- **Zero tools by default.** A runtime with no `WithTools…` call registers **nothing**. The attack surface
  is exactly the set of tools you chose to register — no more.
- **No persisted-config auto-load.** A game build never silently reads a saved config file; host/token
  come only from your code or `GODOT_MCP_*` env / `.env`.
- **Prefer loopback + a required token.** For local tooling, bind to a loopback host
  (`http://localhost:…` / `127.0.0.1`) and set `AuthOption = GodotMcpAuthOption.Required` with a real
  `Token`. Avoid exposing the connection on a public interface in a release build unless you have
  explicitly designed and secured that surface.
- **Runtime-error capture forwards sensitive data.** `WithRuntimeErrorCapture()` is **OFF by default**.
  When enabled, captured messages and stack traces are sent verbatim to the connected agent via
  `runtime-errors-get` and may contain absolute paths, machine/user names, or a secret that appeared in
  an exception — so enable it only on a trusted loopback + token connection (full note under
  [Capturing in-game runtime errors](#capturing-in-game-runtime-errors)).

### Editor-side security notes (accepted posture)

The points above are about a **game build**. Two editor-side surfaces are documented here for completeness;
both are **by design** today:

- **Editor token storage is plaintext at rest.** When you connect the editor plugin (Cloud device-auth or
  a Custom-mode token), the plugin persists your connection config — including the bearer token (`token`)
  and Cloud token (`cloudToken`) — as **plaintext JSON** in `user://godot-mcp-config.json` (resolved per
  Godot's `user://` data directory). It is **not** encrypted or stored in an OS keystore. The trust
  assumption is the **local user account**: anyone with read access to your user data directory can read
  the token. Treat that file as a secret — don't commit it, sync it, or share it. To rotate, clear the
  saved token in the dock (or delete the file) and reconnect. (Process-env / `.env` overrides via
  `GODOT_MCP_TOKEN` always shadow the persisted value and are not written back to this file.)
- **The dev-control bridge is unauthenticated but gated OFF.** A development-only inject/control HTTP
  bridge exists for driving the editor dock in tests. It is **unauthenticated**, but its security boundary
  is threefold: it is editor-only (`#if TOOLS`, never compiled into a game), binds **`127.0.0.1`** only,
  and starts **only when `GODOT_MCP_DEV_CONTROL=1`** — a shipped addon (and any editor session without the
  env var) never listens. That env gate is load-bearing and enforced by a unit test + a boot-time
  assertion, so it can never silently ship enabled.

A short standalone copy of this contract lives in
[`docs/runtime-security.md`](docs/runtime-security.md).

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
mode this is the hosted `ai-game.dev` backend; in **Custom** mode it is the shared
[GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) host you run yourself (or let the
addon download and run for you).

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
