<!--
  ┌──────────────────────────────────────────────────────────────────┐
  │  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
  │  Repository: GitHub (https://github.com/IvanMurzak/Godot-MCP)    │
  │  Copyright (c) 2026 Ivan Murzak                                  │
  │  Licensed under the Apache License, Version 2.0.                 │
  │  See the LICENSE file in the project root for more information.  │
  └──────────────────────────────────────────────────────────────────┘
-->

# Godot Asset Library — submission package

Ready-to-paste values for the Godot Asset Library submission form at
<https://godotengine.org/asset-library/> (**Submit Asset**, or **Edit** for a version update). The
procedure (first submission vs. subsequent updates) lives in [`docs/RELEASING.md`](../RELEASING.md);
this file is just the field values. Keep it in sync whenever any field changes.

> **Gate:** submission requires the maintainer's godotengine.org account and is performed by Ivan. A
> GitHub Release for the version must already exist (the entry references the released commit).

## Form fields

| Form field | Value to paste |
| --- | --- |
| **Asset name** | `Godot-MCP` |
| **Category** | `Tools` (an Addon-side category — categories are split into Addons / Projects, and picking an Addon category is what makes this entry an Addon and surfaces it in the in-editor *AssetLib* tab; there is no separate "Type" field) |
| **Godot version** | `4.3` (lowest supported; the addon also runs on 4.4 / 4.5 — a single entry cannot carry multiple engine versions, so each version it should advertise needs its own resubmission) |
| **Version** | `0.16.1` (must match `addons/godot_mcp/plugin.cfg` `version=` and the `v0.16.1` tag) |
| **Repository host** | `GitHub` |
| **Repository URL** | `https://github.com/IvanMurzak/Godot-MCP` |
| **Issues URL** | `https://github.com/IvanMurzak/Godot-MCP/issues` |
| **Download Commit** | the commit hash the `v0.16.1` tag points at — get it with `git rev-list -n1 v0.16.1` (a hash, not the tag name) |
| **License** | `Apache-2.0` |
| **Icon URL** | `https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/main/addons/godot_mcp/icon.png` (square 512×512 PNG; AssetLib requires square PNG/JPG ≥ 128×128 — SVG is not accepted) |

## plugin.cfg description parity (one line — NOT a separate form field)

The form has a single **Description** field (below); there is no separate short-description input. Keep
this one-liner in sync with `addons/godot_mcp/plugin.cfg` `description`, and use it as the opening line of
the Description below so the listing leads with the same summary:

```
Model Context Protocol (MCP) integration for the Godot Editor. AI tools in C#, cloud-connected to ai-game.dev.
```

## Description (PLAIN TEXT — the single form field; the form does not render Markdown)

```
Model Context Protocol (MCP) integration for the Godot Editor. AI tools in C#, cloud-connected to ai-game.dev.

Godot-MCP connects AI agents (Claude, Cursor, GitHub Copilot, Gemini, or any MCP-aware client) to the
Godot Editor so they can inspect and drive your project — create nodes, edit scenes, manage resources
and scripts, capture screenshots, and more.

It is the Godot counterpart of Unity-MCP: a C# editor addon that exposes Godot Editor operations as AI
Tools and connects them to an MCP server through the hosted cloud backend at ai-game.dev, or your own
self-hosted server. The MCP / reflection stack is shared with Unity-MCP and consumed from nuget.org as
NuGet package references (not forked).

39 built-in tools across 11 families: ping, node, scene, resource, filesystem, script, screenshot,
editor, console, reflection, and runtime-errors. Tool names mirror Unity-MCP where sensible (scene-*, node-*, ...).

Requirements:
- Godot 4.3+ — the C#/.NET (mono) edition. The standard GDScript-only build cannot compile the addon.
- .NET 8 SDK.

Important install note: Godot compiles every .cs file under your project into one assembly, so your
project's .csproj must declare the two NuGet package references the addon depends on:
  com.IvanMurzak.ReflectorNet  version 5.3.1
  com.IvanMurzak.McpPlugin     version 6.11.0
Without them the addon's C# will not compile. Run dotnet restore after adding them. No manual DLL
copying is required — at editor runtime the addon's assembly resolver locates the DLLs in your NuGet
global-packages folder.

Full documentation, the complete tool list, and connection setup:
https://github.com/IvanMurzak/Godot-MCP

License: Apache-2.0.
```

## Preview images (optional — up to 3 images / YouTube videos)

The Asset Library accepts image or video previews. Each preview has three form fields — **Type**,
**Image/YouTube URL**, and **Thumbnail URL**. For an image preview the same raw GitHub URL serves as
both the image and its thumbnail (paste it into both fields). Use the promo art via raw GitHub URLs:

| # | Type | Image/YouTube URL | Thumbnail URL |
| --- | --- | --- | --- |
| 1 | Image | `https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/main/docs/img/promo/ai-developer-banner.jpg` | (same as Image URL) |

(One strong banner preview is enough; add more from `docs/img/promo/` if desired. Prefer PNG/JPG over
SVG for preview thumbnails — SVG may not render reliably in the Asset Library gallery.)

## Per-release update checklist

On every later release, edit the existing entry (do NOT create a new one):

1. Bump **Version** here and in the form to the new `plugin.cfg` semver.
2. Update **Download Commit** to `git rev-list -n1 v<newversion>`.
3. Update the long description / previews only if they changed (e.g. tool count, NuGet pins).
