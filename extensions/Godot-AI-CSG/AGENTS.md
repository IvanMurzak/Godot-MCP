# AGENTS.md — Godot-AI-CSG

A **Godot-MCP extension**: MCP tools for Godot's built-in **CSG** (Constructive Solid Geometry) 3D nodes — the
`CSGShape3D` family (`CSGBox3D`, `CSGSphere3D`, `CSGCylinder3D`, `CSGCombiner3D`). Shipped as a **source-only
NuGet package** (`com.IvanMurzak.Godot.MCP.CSG`) that compiles inside the consumer's Godot project against the
consumer's own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-CSG/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_CSG.cs` (the `[AiToolType]` partial class), `Tool_CSG.Ids.cs` (all tool-id
    consts, pinned by tests), and the pure-managed `Tool_CSG.Defaults.cs`.
  - `Runtime/CSG/` — pure-managed support types (`CsgOperation` parsing/mapping, `CsgPrimitiveKind`,
    `CsgDefaults` value rules, `CsgShapeInfo`), all unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_CSG.<Name>.cs`) + shared helpers in
    `Tool_CSG.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.CSG.props` — source-injection props, auto-imported by NuGet in the
    consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-CSG.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/CSG-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed package; its
  `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_CSG.Ids.cs` (the source of truth). Current tools: `csg-defaults`
(pure-managed), `csg-box-create`, `csg-sphere-create`, `csg-cylinder-create`, `csg-combiner-create`,
`csg-set-operation`, `csg-get`.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-CSG/Godot-AI-CSG.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-CSG.Tests/Godot-AI-CSG.Tests.csproj
dotnet pack  src/Godot-AI-CSG/Godot-AI-CSG.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/CSG-Testbed.csproj            # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.3 / 4.4 / 4.5 (the `test-pull-request.yml` matrix). `dotnet pack` reuses the **global NuGet
cache** for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.csg/<ver>` (or pack a unique version) before re-restoring the
testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.CSG`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (no Godot native API) → `Runtime/` (outside `#if TOOLS`, unit-testable); editor-driving
  tools → `Editor/` (behind `#if TOOLS`, every Godot call via `MainThread.Instance.Run(...)`, E2E-verified).
- `CSG` is the feature name, not an engine type (the nodes are `CSGBox3D`, …), so the root namespace shadows
  nothing and needs no per-file `Godot.*` alias (unlike Animation or GridMap).
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp must never become a package dependency** (CI asserts the nuspec). Keep the MCP pins in lockstep
  with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_CSG`; one `[AiTool]` method per partial-class file. Add every new
  pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
