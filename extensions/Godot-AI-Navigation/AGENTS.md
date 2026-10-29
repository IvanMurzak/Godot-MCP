# AGENTS.md — Godot-AI-Navigation

A **Godot-MCP extension** for Godot's built-in navigation nodes (2D and 3D: `NavigationRegion*`, `NavigationAgent*`,
`NavigationLink*`), shipped as an **RQL module**: it adds no MCP tools, only what the core `rql-query` /
`rql-mutate` / `rql-schema` tools need (validated members, region `Bake()` / `polygonCount`, NavigationPolygon
outline calls, an agent guide rendered into the core `godot-rql` skill). A **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.Navigation`) compiled inside the consumer's Godot project against the consumer's own
GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Navigation/Editor/Rql/` — the module:
  - `NavigationModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the adapters and the guide.
  - `Godot_Navigation{Region,Agent,Link}{3D,2D}_Adapter.cs`, `Godot_NavigationPolygon_Adapter.cs` — one adapter per
    type (`#if TOOLS`); the shared validated-member helpers live in `NavigationMembers.cs`.
  - `NavigationRules.cs` (validation, bake preconditions) and `NavigationGuide.cs` (the guide) — pure-managed, NOT
    behind `#if TOOLS`.
- `src/Godot-AI-Navigation/build/com.IvanMurzak.Godot.MCP.Navigation.props` — source-injection props, auto-imported
  by NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Navigation.Tests/` — xUnit (no Godot binary): rules, guide examples parse, the module's member
  tables (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`, including a real 3D and 2D bake), driven by
  `scripts/extension_e2e.py` from the monorepo suite.
- `testbed/Navigation-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- Versions and core pins are set from the monorepo root: `scripts/extension_release.py bump` / `version`,
  `scripts/update_core.py`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `NavigationModule` and documented in `NavigationGuide` (topic
  "Navigation"). Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Validated members replace the C# properties** of the same name and type (float). Only what Godot rejects
  (logs an error for) or what 0.x enforced is validated; a rejected value fails the whole mutation — never clamp.
- `Bake()` bakes synchronously on the main thread (`onThread: false`) and refuses a region without its resource
  or one already baking (Godot would log an error). Baked polygons are not undoable; say so in docs.
- Methods go on the object they change: `AddOutline` is on NavigationPolygon (undoable), not on the region.
  Do not `Expose` CLR methods whose overloads change across GodotSharp versions (`AddOutline` takes `Vector2[]` in
  4.3 and `ReadOnlySpan<Vector2>` later): add a typed `Method` instead.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-Navigation/Godot-AI-Navigation.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Navigation.Tests/Godot-AI-Navigation.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.navigation/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Navigation`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` pins (equal to the core's); **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump them with `scripts/update_core.py`.
