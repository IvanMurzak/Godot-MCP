# AGENTS.md — Godot-AI-CSG

A **Godot-MCP extension** for Godot's built-in CSG nodes (`CSGBox3D`, `CSGSphere3D`, `CSGCylinder3D`, `CSGTorus3D`,
`CSGCombiner3D` …), shipped as an **RQL module**: it adds no MCP tools, only what the core `rql-query` /
`rql-mutate` / `rql-schema` tools need to work with CSG shapes (validated dimensions, `rootShape`, an agent guide
rendered into the core `godot-rql` skill). A **source-only NuGet package** (`com.IvanMurzak.Godot.MCP.CSG`)
compiled inside the consumer's Godot project against the consumer's own GodotSharp; the packaging recipe is in
`docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-CSG/Editor/Rql/` — the module:
  - `CsgModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the adapters and the guide.
  - `Godot_CsgShape3D_Adapter.cs` (every CSG node: `rootShape`), `Godot_CsgBox3D_Adapter.cs`,
    `Godot_CsgSphere3D_Adapter.cs`, `Godot_CsgCylinder3D_Adapter.cs`, `Godot_CsgTorus3D_Adapter.cs` — one adapter
    per type (`#if TOOLS`).
  - `CsgRules.cs` (validation) and `CsgGuide.cs` (the guide) — pure-managed, NOT behind `#if TOOLS`.
- `src/Godot-AI-CSG/build/com.IvanMurzak.Godot.MCP.CSG.props` — source-injection props, auto-imported by NuGet in
  the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-CSG.Tests/` — xUnit (no Godot binary): rules, guide examples parse, the module's member tables
  (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `run_tool_e2e.py` from the monorepo suite.
- `testbed/CSG-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `CsgModule` and documented in `CsgGuide` (topic "CSG"). Every
  guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Validated members replace the C# properties** of the same name and type (`Size`, `Radius`, `Height`,
  `RadialSegments`, `Rings`, `Sides`, `RingSides`). Only what Godot rejects or silently changes, or what 0.x
  enforced, is validated; a rejected value fails the whole mutation — never clamp silently. `Size` re-checks in
  its setter because a write through one axis (`set Size.X = 0`) bypasses the member's validate.
- Creation and the boolean `Operation` (enum by name) are core RQL; do not add create / set-operation helpers.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-CSG/Godot-AI-CSG.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-CSG.Tests/Godot-AI-CSG.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.csg/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.CSG` (the feature name, not an engine type: it shadows nothing). Every
  `.cs` starts with the Apache-2.0 header and `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` floors; **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump floors with `commands/update-core.ps1`.
