# AGENTS.md — Godot-AI-GridMap

A **Godot-MCP extension**: MCP tools for Godot's built-in `GridMap` node (a 3D grid of cells, each holding a
mesh from an assigned `MeshLibrary`). Shipped as a **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.GridMap`) that compiles inside the consumer's Godot project against the consumer's
own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-GridMap/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_GridMap.cs` (the `[AiToolType]` partial class), `Tool_GridMap.Ids.cs` (all
    tool-id consts, pinned by tests), and the pure-managed `Tool_GridMap.Defaults.cs`.
  - `Runtime/GridMap/` — pure-managed support types (`GridMapDefaults`: starter config + value-clamping rules;
    `GridMapCellInfo`, `GridMapNodeInfo`), all unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_GridMap.<Name>.cs`) + shared helpers in
    `Tool_GridMap.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.GridMap.props` — source-injection props, auto-imported by NuGet in the
    consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-GridMap.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/GridMap-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed package; its
  `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_GridMap.Ids.cs` (the source of truth). Current tools: `gridmap-defaults`
(pure-managed), `gridmap-create`, `gridmap-set-mesh-library`, `gridmap-set-cell`, `gridmap-clear-cell`,
`gridmap-clear`, `gridmap-get`.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-GridMap/Godot-AI-GridMap.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-GridMap.Tests/Godot-AI-GridMap.Tests.csproj
dotnet pack  src/Godot-AI-GridMap/Godot-AI-GridMap.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/GridMap-Testbed.csproj                # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.3 / 4.4 / 4.5 (the central monorepo SDK matrix). `dotnet pack` reuses the **global NuGet
cache** for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.gridmap/<ver>` (or pack a unique version) before re-restoring the
testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.GridMap`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (no Godot native API) → `Runtime/` (outside `#if TOOLS`, unit-testable); editor-driving
  tools → `Editor/` (behind `#if TOOLS`, every Godot call via `MainThread.Instance.Run(...)`, E2E-verified).
- **Namespace shadow:** the root namespace `com.IvanMurzak.Godot.MCP.GridMap` shadows `Godot.GridMap`, so an
  unqualified `GridMap` binds to the namespace (`CS0118`). Every editor file that names the engine type adds
  `using GdGridMap = Godot.GridMap;` (or fully qualifies `Godot.GridMap`).
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp must never become a package dependency** (CI asserts the nuspec). Keep the MCP pins in lockstep
  with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_GridMap`; one `[AiTool]` method per partial-class file. Add every new
  pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
