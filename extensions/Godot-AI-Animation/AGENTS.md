# AGENTS.md — Godot-AI-Animation

A **Godot-MCP extension**: MCP tools for Godot's built-in animation system — `AnimationPlayer` nodes,
`AnimationLibrary` and `Animation` resources, their tracks and keyframes. Shipped as a **source-only NuGet
package** (`com.IvanMurzak.Godot.MCP.Animation`) that compiles inside the consumer's Godot project against the
consumer's own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Animation/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_Animation.cs` (the `[AiToolType]` partial class), `Tool_Animation.Ids.cs` (all
    tool-id consts, pinned by tests), and the pure-managed `Tool_Animation.Defaults.cs`.
  - `Runtime/Animation/` — pure-managed support types (`AnimationDefaults`: starter config + parameter
    validation; `AnimationTrackKind`, `AnimationLoopMode`, the `Animation{Player,Clip,Track}Info` shapes), all
    unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_Animation.<Name>.cs`) + shared helpers in
    `Tool_Animation.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.Animation.props` — source-injection props, auto-imported by NuGet in the
    consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Animation.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/Animation-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed package;
  its `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_Animation.Ids.cs` (the source of truth). Current tools: `animation-defaults`
(pure-managed), `animation-player-create`, `animation-library-add`, `animation-create`, `animation-add-track`,
`animation-insert-key`, `animation-get`.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-Animation/Godot-AI-Animation.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-Animation.Tests/Godot-AI-Animation.Tests.csproj
dotnet pack  src/Godot-AI-Animation/Godot-AI-Animation.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/Animation-Testbed.csproj                  # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.3 / 4.4 / 4.5 (the `test-pull-request.yml` matrix). `dotnet pack` reuses the **global NuGet
cache** for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.animation/<ver>` (or pack a unique version) before re-restoring
the testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Animation`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (no Godot native API) → `Runtime/` (outside `#if TOOLS`, unit-testable); editor-driving
  tools → `Editor/` (behind `#if TOOLS`, every Godot call via `MainThread.Instance.Run(...)`, E2E-verified).
- **Namespace shadow:** the root namespace `com.IvanMurzak.Godot.MCP.Animation` shadows `Godot.Animation`;
  files that name the engine type alias it per file (`using GodotAnimation = Godot.Animation;`).
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp must never become a package dependency** (CI asserts the nuspec). Keep the MCP pins in lockstep
  with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_Animation`; one `[AiTool]` method per partial-class file. Add every new
  pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
