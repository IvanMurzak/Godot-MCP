# AGENTS.md — Godot-AI-Terrain3D

A **Godot-MCP extension** for the third-party [**Terrain3D**](https://github.com/TokisanGames/Terrain3D) GDExtension
addon, shipped as an **RQL module**: it adds no MCP tools, only what the core `rql-query` / `rql-mutate` /
`rql-schema` tools need to work with terrains (the `terrain3d` / `terrain3d.addon` sources, the
`terrain3d.newMaterial()` function, `ChangeRegionSize`, an agent guide rendered into the core `godot-rql` skill). A
**source-only NuGet package** (`com.IvanMurzak.Godot.MCP.Terrain3D`) compiled inside the consumer's Godot project
against the consumer's own GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Terrain3D/Editor/Rql/` — the module:
  - `Terrain3DModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): sources, function, guide.
  - `Terrain3DApi.cs` (the sources and the function; `Terrain3DAddon`) and `Terrain3DView.cs` (one row per terrain:
    read-only members, `node`, `ChangeRegionSize`) — `#if TOOLS`.
  - `Terrain3DNames.cs` (the addon's class / snake_case names, region sizes, install hint), `Terrain3DRules.cs`
    (validation) and `Terrain3DGuide.cs` (the guide) — pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-Terrain3D/build/com.IvanMurzak.Godot.MCP.Terrain3D.props` — source-injection props, auto-imported by
  NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Terrain3D.Tests/` — xUnit (no Godot binary, no addon): rules, names, guide examples parse, the
  module's registrations and view type, and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `run_tool_e2e.py` from the monorepo suite with
  the pinned addon installed (`extension.json` `testAddon`: `TokisanGames/Terrain3D` @ `v1.0.2-stable`).
- `testbed/Terrain3D-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants (addon-dependent)

- **No compile-time dependency on the addon.** Terrain3D classes have no C# type (a Terrain3D node is a `Node3D`
  wrapper, a Terrain3DMaterial a `Resource`), so adapters cannot target them: reach them by ClassDB name and
  snake_case property / method names from `Terrain3DNames` only. `dotnet build` must pass with the addon absent.
  The addon is never vendored or downloaded by this repo.
- **One approach:** the `terrain3d` source yields read-only `Terrain3DView` rows; writes go to the node itself
  (`get node | set props["…"]`), so they are undoable through the core hooks. Only `ChangeRegionSize` is a view
  call: it validates the size (Terrain3D logs an error for any other value), is not undoable (the size is not a
  stored scene property) and marks the scene unsaved itself.
- **Missing addon:** `terrain3d` is empty, `terrain3d.addon` reports `installed: false` plus the install hint,
  `terrain3d.newMaterial()` fails with that hint — never a crash.
- Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted). The module uses only Godot,
  `com.IvanMurzak.Rql` and the BCL, never core-addon types.

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-Terrain3D/Godot-AI-Terrain3D.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Terrain3D.Tests/Godot-AI-Terrain3D.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.terrain3d/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Terrain3D`. It contains `com.IvanMurzak.Godot`, so an inline
  `Godot.`-qualified type binds to that ancestor: write `global::Godot.…` or rely on `using Godot;`. Every `.cs`
  starts with the Apache-2.0 header and `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` floors; **GodotSharp and the
  Terrain3D addon must never become package dependencies** (CI asserts the nuspec). Bump floors with
  `commands/update-core.ps1`.
