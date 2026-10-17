# AGENTS.md — Godot-AI-Animation

A **Godot-MCP extension** for Godot's built-in animation system (`AnimationPlayer` / `AnimationTree`,
`AnimationLibrary`, `Animation`), shipped as an **RQL module**: it adds no MCP tools, only what the core
`rql-query` / `rql-mutate` / `rql-schema` tools need to build animations (library / animation maps, creation,
tracks and keys, validation, an agent guide rendered into the core `godot-rql` skill). A **source-only NuGet
package** (`com.IvanMurzak.Godot.MCP.Animation`) compiled inside the consumer's Godot project against the
consumer's own GodotSharp; the packaging recipe is in `docs/source-only-nuget-recipe.md`.

## Layout

- `src/Godot-AI-Animation/Editor/Rql/` — the module:
  - `AnimationModule.cs` — the `IRqlModule` (Order 100, `#if TOOLS`): the three adapters and the guide.
  - `Godot_AnimationMixer_Adapter.cs` (AnimationPlayer + AnimationTree: `libraries`, `animations`, `AddLibrary`,
    `CreateAnimation`), `Godot_AnimationLibrary_Adapter.cs` (`animations`, `CreateAnimation`),
    `Godot_Animation_Adapter.cs` (`Length`, `tracks`, `trackCount`, `AddTrack`, `InsertKey`, `InsertTransformKey`)
    — one adapter per type (`#if TOOLS`); the Godot calls behind them live in `AnimationOps.cs`.
  - Pure-managed, NOT behind `#if TOOLS`: `AnimationRules.cs` (validation), `AnimationGuide.cs` (the guide),
    `AnimationTrackRow.cs` (the read-only `tracks` row), `AnimationTrackKind.cs` / `AnimationLoopMode.cs` (label
    parsers and Godot enum mapping).
- `src/Godot-AI-Animation/build/com.IvanMurzak.Godot.MCP.Animation.props` — source-injection props, auto-imported
  by NuGet in the consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Animation.Tests/` — xUnit (no Godot binary): rules, label parsers, guide examples parse, the
  module's member tables (GodotSharp metadata, `TOOLS` defined), and the e2e manifest runs every guide example.
- `tests/e2e/e2e-tools.json` — the live calls (`rql-*`), driven by `scripts/extension_e2e.py` from the monorepo suite.
- `testbed/Animation-Testbed.csproj` — consumer project restoring the local-packed package (injection proof).
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

## Critical invariants

- **RQL surface.** Everything is registered by `AnimationModule` and documented in `AnimationGuide` (topic
  "Animation"). Every guide example runs, in order, in `tests/e2e/e2e-tools.json` (xUnit-asserted).
- **Undo follows the call target.** The core snapshots the stored Godot properties of the object a call is made
  on. So a method lives on the object it changes: `AddLibrary` on the mixer (its `libraries`),
  `CreateAnimation` on the library, `AddTrack` / keys on the Animation. The mixer's `CreateAnimation` is undoable
  only when it adds the library (documented in the guide); keep that caveat if you change it.
- **Validate, never clamp.** Every call checks all its arguments (`AnimationRules`, the label parsers, existing
  names) before it changes anything, so Godot never logs an error (the headless suite fails on any `ERROR:`).
- The namespace ends in `.Animation`, which shadows `Godot.Animation`: use the `GodotAnimation` alias.
- The module uses only Godot, `com.IvanMurzak.Rql` and the BCL, never core-addon types (the authoring build has
  no core). Undo, dirty marking and saving come from the core's mutation hooks.

## Build / test (no Godot binary)

```bash
dotnet build src/Godot-AI-Animation/Godot-AI-Animation.csproj -c Debug   # the module (#if TOOLS included)
dotnet test  tests/Godot-AI-Animation.Tests/Godot-AI-Animation.Tests.csproj
```

The live suite (one headless editor for every module) is `python scripts/test_extensions.py` from the monorepo
root. `dotnet pack` reuses the **global NuGet cache** for an already-cached version: when re-packing the same
`Version`, clear `~/.nuget/packages/com.ivanmurzak.godot.mcp.animation/<ver>` (or pack a unique version).

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Animation`. Every `.cs` starts with the Apache-2.0 header and
  `#nullable enable`.
- The package declares only the `com.IvanMurzak.McpPlugin` / `ReflectorNet` / `Rql` pins (equal to the core's); **GodotSharp must
  never become a package dependency** (CI asserts the nuspec). Bump them with `commands/update-core.ps1`.
