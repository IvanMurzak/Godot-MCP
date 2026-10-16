# AGENTS.md — Godot-AI-Terrain3D

A **Godot-MCP extension** wrapping the third-party [**Terrain3D**](https://github.com/TokisanGames/Terrain3D)
addon (large-scale, editable clipmap terrain). Shipped as a **source-only NuGet package**
(`com.IvanMurzak.Godot.MCP.Terrain3D`) that compiles inside the consumer's Godot project against the
consumer's own GodotSharp. Created from
[`Godot-AI-Tools-Template`](https://github.com/IvanMurzak/Godot-MCP/tree/main/templates/extension). The packaging recipe is
the load-bearing detail — read `docs/source-only-nuget-recipe.md`.

## Addon-dependent (Class B) — the load-bearing rules

Terrain3D is a third-party precompiled C++ **GDExtension** whose classes are NOT in GodotSharp, and the
package must not depend on it:

- **No compile-time dependency.** Reference Terrain3D classes **by string name only**, resolved at runtime.
  `dotnet build -c Debug` MUST exit 0 with the addon ABSENT (the no-dependency gate).
- **Resolution:** a GDExtension registers its classes in `ClassDB` (not the GDScript global-class list):
  resolve via `ClassDB.ClassExists` / `ClassDB.Instantiate`.
- **Presence gate first.** Every editor tool's first action inside `MainThread.Instance.Run(...)` checks
  `AddonInterop.NativeClassExists(AddonGate.PresenceClass)` (`Terrain3D`) and, when missing, returns the
  structured `installed: false` result (`AddonGateResult`) with an install hint — never a raw throw (an opaque
  HTTP 500 to the LLM).
- **Names are the contract.** Drive addon objects via `GodotObject.Set/Get/Call` with GDScript **snake_case**
  member names (not C# PascalCase); enums are plain ints. Keep these name constants centralized in
  `Runtime/Terrain3D/Terrain3DNames.cs` and unit-tested — there are no compile-time types to lean on.
- The addon is **never** vendored, submoduled, or downloaded by this repo; installing it is the consumer's
  job. CI installs the pinned `TokisanGames/Terrain3D` @ `v1.0.2-stable` into `testbed/addons/terrain_3d/`
  only for the e2e leg (`ADDON_*` in `.github/workflows/test.yml`).

Terrain3D persists regions to a **writable data directory** (not just the scene): `terrain3d-create` /
`terrain3d-set-data-directory` set a `res://` data dir, and the e2e fixture points at one.

## Layout

- `src/Godot-AI-Terrain3D/` — the source-only package (`Godot.NET.Sdk`):
  - `Runtime/Tools/` — `Tool_Terrain3D.cs` (the `[AiToolType]` partial class), `Tool_Terrain3D.Ids.cs` (all
    tool-id consts, pinned by tests), and the pure-managed `Tool_Terrain3D.Defaults.cs`.
  - `Runtime/Interop/` — `AddonInterop.cs` (name resolution + instantiation; entirely behind `#if TOOLS`,
    E2E-verified, never unit-tested), `AddonGate.cs` (the `AddonGateResult` shape + `NotInstalled(...)`
    factory; pure-managed, unit-tested).
  - `Runtime/Terrain3D/` — `Terrain3DNames` (class/member snake_case names + `RegionSizes`),
    `Terrain3DConfig`, `Terrain3DDefaults`, `Terrain3DNodeInfo`; all unit-tested.
  - `Editor/Tools/` — one file per editor tool (`Tool_Terrain3D.<Name>.cs`) + shared helpers in
    `Tool_Terrain3D.Editor.cs`; all behind `#if TOOLS`.
  - `build/com.IvanMurzak.Godot.MCP.Terrain3D.props` — source-injection props, auto-imported by NuGet in the
    consumer; MUST stay named `<PackageId>.props`.
- `tests/Godot-AI-Terrain3D.Tests/` — xUnit specs for the pure-managed sources only (no Godot binary).
- `tests/e2e/` — live `run-tool` E2E: the tool list `e2e-tools.json`, driven by `run_tool_e2e.py`.
- `testbed/Terrain3D-Testbed.csproj` — consumer `Godot.NET.Sdk` project restoring the local-packed package;
  its `dotnet build` is the source-injection proof.
- `commands/` — `bump-version`, `update-core`, `init` (`.ps1` + `.py`), `get-version.ps1`.
- CI and publishing are centralized in the monorepo; see `../../docs/EXTENSION-MONOREPO.md`.

- `docs/` — `source-only-nuget-recipe.md` (the packaging recipe + consumer story); `ci.md` (workflows, version
  gate, multi-Godot matrix, NuGet publishing setup).

## Tools

Ids live in `Runtime/Tools/Tool_Terrain3D.Ids.cs` (the source of truth). Current tools: `terrain3d-defaults`
(pure-managed), `terrain3d-create`, `terrain3d-set-data-directory`, `terrain3d-set-region-size`,
`terrain3d-set-material`, `terrain3d-get`.

## Build / test (no Godot binary, addon absent)

```bash
dotnet build src/Godot-AI-Terrain3D/Godot-AI-Terrain3D.csproj  # source-only package compiles tools
dotnet test  tests/Godot-AI-Terrain3D.Tests/Godot-AI-Terrain3D.Tests.csproj
dotnet pack  src/Godot-AI-Terrain3D/Godot-AI-Terrain3D.csproj -p:Version=0.0.0-ci -o local-nuget
dotnet build testbed/Terrain3D-Testbed.csproj                  # consumes the local package (injection proof)
```

`Godot.NET.Sdk` supplies GodotSharp from NuGet, so build/test/pack and the source-injection proof need no
Godot install (the testbed build is a faithful proxy for `godot --build-solutions`). CI proves the recipe
across Godot 4.4 / 4.5 (the central monorepo SDK matrix). `dotnet pack` reuses the **global NuGet cache**
for an already-cached version: when re-packing the same `Version`, clear
`~/.nuget/packages/com.ivanmurzak.godot.mcp.terrain3d/<ver>` (or pack a unique version) before re-restoring
the testbed, or you silently build the stale cached source.

## Conventions

- Root namespace `com.IvanMurzak.Godot.MCP.Terrain3D`. Every `.cs` starts with the Apache-2.0 header.
- Pure-managed code (tool ids, the `AddonGateResult` shape, name/enum-int constants, defaults) → `Runtime/`
  (outside `#if TOOLS`, unit-testable); editor-driving tools + `AddonInterop` → `#if TOOLS` (every Godot call
  via `MainThread.Instance.Run(...)`, E2E-verified).
- **Namespace shadow:** the root namespace contains `com.IvanMurzak.Godot`, so an inline `Godot.`-qualified
  type binds to that ancestor, not the engine — write `global::Godot.Collections.Dictionary` or import via
  `using Godot.Collections;`. Plain `using Godot;` + unqualified `Node`/`ClassDB` are unaffected.
- The package declares ONLY the `com.IvanMurzak.McpPlugin` / `com.IvanMurzak.ReflectorNet` min-version deps;
  **GodotSharp and the Terrain3D addon must never become a package dependency** (CI asserts the nuspec). Keep
  the MCP pins in lockstep with the core Godot-MCP addon; bump with `commands/update-core.ps1`.
- One `[AiToolType] partial class Tool_Terrain3D`; one `[AiTool]` method per partial-class file. Add every new
  pure-managed source to the test csproj `<Compile Include>` list so it is unit-tested.
